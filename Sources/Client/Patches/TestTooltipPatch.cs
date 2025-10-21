using System.Reflection;
using System.Text;
using System.Linq;
using EFT.UI;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;
using QuickPrice.Config;
using QuickPrice.Services;
using QuickPrice.Utils;

namespace QuickPrice.Patches
{
    /// <summary>
    /// 价格显示补丁 - 拦截 SimpleTooltip.Show() 方法
    /// 在物品 tooltip 后面添加价格信息
    /// </summary>
    public class PriceTooltipPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            // 查找 SimpleTooltip.Show() 方法
            // 参数: string text, Vector2? offset, float delay, float? maxWidth
            return AccessTools.FirstMethod(
                typeof(SimpleTooltip),
                x => x.Name == "Show" && x.GetParameters()[0].Name == "text"
            );
        }

        [PatchPrefix]
        public static void Prefix(SimpleTooltip __instance, ref string text, ref float delay)
        {
            // 检查插件是否启用
            if (!Settings.PluginEnabled.Value)
                return;

            // 检查是否启用跳蚤价格
            if (!Settings.ShowFleaPrices.Value)
                return;

            // 检查是否需要按住Ctrl键
            if (Settings.RequireCtrlKey.Value && !IsCtrlPressed())
                return;

            try
            {
                // 从 Plugin.HoveredItem 获取当前悬停的物品
                var item = Plugin.HoveredItem;
                if (item == null)
                {
                    // Plugin.Log.LogDebug("HoveredItem 为 null");
                    return;
                }

                // 获取物品占用格数
                int slots = item.Width * item.Height;

                // 给物品名称着色（在添加价格信息之前）
                if (Settings.ColorItemName.Value && Settings.EnableColorCoding.Value)
                {
                    text = ColorizeItemName(text, item);
                }

                // 按类型处理（注意顺序：子类判断必须在父类之前）
                // Plugin.Log.LogInfo($"🔍 开始类型判断: {item.LocalizedName()} (类型: {item.GetType().Name})");

                // 武器
                if (item is Weapon weapon)
                {
                    // Plugin.Log.LogInfo($"   ✅ 识别为武器");
                    text += FormatWeaponPriceText(weapon, slots);
                }
                // 弹匣（必须在 Mod 之前，因为 MagazineItemClass 继承自 Mod）
                else if (item is MagazineItemClass magazine)
                {
                    // Plugin.Log.LogInfo($"   ✅ 识别为弹匣");
                    text += FormatMagazinePriceText(magazine, slots);
                }
                // 弹药盒
                else if (item is AmmoBox ammoBox)
                {
                    // Plugin.Log.LogInfo($"   ✅ 识别为弹药盒");
                    text += FormatNormalItemPriceText(ammoBox, slots);
                }
                // 护甲（在子弹之前检查，避免被其他分类覆盖）
                else if (ArmorHelper.IsArmor(item))
                {
                    // Plugin.Log.LogInfo($"   ✅ 识别为护甲");
                    text += FormatArmorPriceText(item, slots);
                }
                // 防弹插板（在配件之前检查）
                else if (IsArmorPlate(item))
                {
                    // Plugin.Log.LogInfo($"   ✅ 识别为防弹插板");
                    text += FormatArmorPlatePriceText(item, slots);
                }
                // 单发子弹
                else if (item is AmmoItemClass ammoItem)
                {
                    // Plugin.Log.LogInfo($"   ✅ 识别为单发子弹");
                    text += FormatAmmoPriceText(ammoItem, slots);
                }
                // 配件（必须在 MagazineItemClass 之后）
                else if (item is Mod mod)
                {
                    // Plugin.Log.LogInfo($"   ✅ 识别为配件");
                    text += FormatModPriceText(mod, slots);
                }
                // 容器（背包、箱子等）
                else if (HasContainer(item))
                {
                    // Plugin.Log.LogInfo($"   ✅ 识别为容器");
                    text += FormatContainerPriceText(item, slots);
                }
                else if (item.StackObjectsCount > 1)
                {
                    // Plugin.Log.LogInfo($"   ✅ 识别为堆叠物品 (x{item.StackObjectsCount})");
                    text += FormatStackedItemPriceText(item, slots);
                }
                else
                {
                    // Plugin.Log.LogInfo($"   ✅ 识别为普通物品");
                    text += FormatNormalItemPriceText(item, slots);
                }

                // 设置延迟
                delay = Settings.TooltipDelay.Value;
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"❌ 价格显示错误: {ex.Message}");
                Plugin.Log.LogError(ex.StackTrace);
            }
        }

        /// <summary>
        /// 检查Ctrl键是否按下
        /// </summary>
        private static bool IsCtrlPressed()
        {
            return UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftControl)
                || UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightControl);
        }

        /// <summary>
        /// 给物品名称着色
        /// </summary>
        private static string ColorizeItemName(string text, Item item)
        {
            try
            {
                // 提取第一行（物品名称）
                int firstLineEnd = text.IndexOf('\n');
                string itemName;
                string restOfText;

                if (firstLineEnd > 0)
                {
                    itemName = text.Substring(0, firstLineEnd);
                    restOfText = text.Substring(firstLineEnd);
                }
                else
                {
                    itemName = text;
                    restOfText = "";
                }

                // 如果名称已经包含颜色标签，先移除
                if (itemName.Contains("<color="))
                {
                    // 移除现有颜色标签
                    int colorStart = itemName.IndexOf("<color=");
                    int colorEnd = itemName.IndexOf(">", colorStart);
                    int colorCloseStart = itemName.IndexOf("</color>");
                    if (colorStart >= 0 && colorEnd >= 0 && colorCloseStart >= 0)
                    {
                        itemName = itemName.Substring(colorEnd + 1, colorCloseStart - colorEnd - 1);
                    }
                }

                string coloredName = itemName;

                // 根据物品类型决定着色方式
                if (item is Weapon weapon)
                {
                    // 武器：按单格价值着色（武器 + 配件）
                    var weaponPrice = PriceDataService.Instance.GetPrice(weapon.TemplateId);
                    if (weaponPrice.HasValue)
                    {
                        double modsPrice = 0;
                        if (Settings.ShowWeaponModsPrice.Value)
                        {
                            modsPrice = PriceCalculator.CalculateWeaponModsPrice(weapon);
                        }
                        double totalPrice = weaponPrice.Value + modsPrice;

                        // 计算单格价值
                        int weaponSlots = weapon.Width * weapon.Height;
                        double pricePerSlot = weaponSlots > 0 ? totalPrice / weaponSlots : totalPrice;

                        coloredName = PriceColorCoding.ApplyColor(itemName, pricePerSlot);
                    }
                }
                else if (item is AmmoBox ammoBox)
                {
                    // 弹药盒：按平均穿甲等级着色（如果启用）
                    if (Settings.UseCaliberPenetrationPower.Value && ammoBox.Cartridges?.Items != null)
                    {
                        int totalPenetration = 0;
                        int penetrationCount = 0;

                        foreach (var cartridge in ammoBox.Cartridges.Items)
                        {
                            if (cartridge is AmmoItemClass ammoItem && ammoItem.PenetrationPower > 0)
                            {
                                totalPenetration += ammoItem.PenetrationPower;
                                penetrationCount++;
                            }
                        }

                        if (penetrationCount > 0)
                        {
                            int avgPenetration = totalPenetration / penetrationCount;
                            coloredName = AmmoColorCoding.ApplyPenetrationColor(itemName, avgPenetration);
                        }
                    }
                    else
                    {
                        // 否则按单格价值着色
                        var boxPrice = PriceDataService.Instance.GetPrice(ammoBox.TemplateId);
                        if (boxPrice.HasValue)
                        {
                            int ammoBoxSlots = ammoBox.Width * ammoBox.Height;
                            double pricePerSlot = ammoBoxSlots > 0 ? boxPrice.Value / ammoBoxSlots : boxPrice.Value;
                            coloredName = PriceColorCoding.ApplyColor(itemName, pricePerSlot);
                        }
                    }
                }
                else if (item is MagazineItemClass magazine)
                {
                    // 弹匣：按平均穿甲等级着色（如果启用）
                    if (Settings.UseCaliberPenetrationPower.Value && magazine.Cartridges?.Items != null)
                    {
                        int totalPenetration = 0;
                        int penetrationCount = 0;

                        foreach (var cartridge in magazine.Cartridges.Items)
                        {
                            if (cartridge is AmmoItemClass ammoItem && ammoItem.PenetrationPower > 0)
                            {
                                totalPenetration += ammoItem.PenetrationPower;
                                penetrationCount++;
                            }
                        }

                        if (penetrationCount > 0)
                        {
                            int avgPenetration = totalPenetration / penetrationCount;
                            coloredName = AmmoColorCoding.ApplyPenetrationColor(itemName, avgPenetration);
                        }
                    }
                    else
                    {
                        // 否则按单格价值着色
                        var magPrice = PriceDataService.Instance.GetPrice(magazine.TemplateId);
                        if (magPrice.HasValue)
                        {
                            int magSlots = magazine.Width * magazine.Height;
                            double pricePerSlot = magSlots > 0 ? magPrice.Value / magSlots : magPrice.Value;
                            coloredName = PriceColorCoding.ApplyColor(itemName, pricePerSlot);
                        }
                    }
                }
                else if (item is AmmoItemClass ammo)
                {
                    // 子弹：按穿甲等级着色（如果启用）
                    if (Settings.UseCaliberPenetrationPower.Value && ammo.PenetrationPower > 0)
                    {
                        coloredName = AmmoColorCoding.ApplyPenetrationColor(itemName, ammo.PenetrationPower);
                    }
                    else
                    {
                        // 否则按单格价值着色
                        var ammoPrice = PriceDataService.Instance.GetPrice(ammo.TemplateId);
                        if (ammoPrice.HasValue)
                        {
                            double totalPrice = ammoPrice.Value * ammo.StackObjectsCount;
                            int ammoSlots = ammo.Width * ammo.Height;
                            double pricePerSlot = ammoSlots > 0 ? totalPrice / ammoSlots : totalPrice;
                            coloredName = PriceColorCoding.ApplyColor(itemName, pricePerSlot);
                        }
                    }
                }
                // 护甲：按防弹等级着色（如果启用）
                else if (ArmorHelper.IsArmor(item))
                {
                    if (Settings.EnableArmorClassColoring.Value)
                    {
                        var armorClass = ArmorHelper.GetArmorClass(item);
                        if (armorClass.HasValue && armorClass.Value > 0)
                        {
                            coloredName = ArmorColorCoding.ApplyArmorClassColor(itemName, armorClass.Value);
                        }
                    }
                    else
                    {
                        // 否则按单格价值着色
                        var armorPrice = PriceDataService.Instance.GetPrice(item.TemplateId);
                        if (armorPrice.HasValue)
                        {
                            int armorSlots = item.Width * item.Height;
                            double pricePerSlot = armorSlots > 0 ? armorPrice.Value / armorSlots : armorPrice.Value;
                            coloredName = PriceColorCoding.ApplyColor(itemName, pricePerSlot);
                        }
                    }
                }
                // 防弹插板：按防弹等级着色（如果启用）
                else if (IsArmorPlate(item))
                {
                    if (Settings.EnableArmorClassColoring.Value)
                    {
                        var plateClass = ArmorHelper.GetArmorClass(item);
                        if (plateClass.HasValue && plateClass.Value > 0)
                        {
                            coloredName = ArmorColorCoding.ApplyArmorClassColor(itemName, plateClass.Value);
                        }
                    }
                    else
                    {
                        // 否则按单格价值着色
                        var platePrice = PriceDataService.Instance.GetPrice(item.TemplateId);
                        if (platePrice.HasValue)
                        {
                            int plateSlots = item.Width * item.Height;
                            double pricePerSlot = plateSlots > 0 ? platePrice.Value / plateSlots : platePrice.Value;
                            coloredName = PriceColorCoding.ApplyColor(itemName, pricePerSlot);
                        }
                    }
                }
                else
                {
                    // 普通物品：按单格价值着色
                    var price = PriceDataService.Instance.GetPrice(item.TemplateId);
                    if (price.HasValue)
                    {
                        double totalPrice = price.Value * item.StackObjectsCount;
                        int itemSlots = item.Width * item.Height;
                        double pricePerSlot = itemSlots > 0 ? totalPrice / itemSlots : totalPrice;
                        coloredName = PriceColorCoding.ApplyColor(itemName, pricePerSlot);
                    }
                }

                return coloredName + restOfText;
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogWarning($"⚠️ 物品名称着色失败: {ex.Message}");
                return text; // 出错时返回原始文本
            }
        }

        /// <summary>
        /// 格式化武器价格文本
        /// </summary>
        private static string FormatWeaponPriceText(Weapon weapon, int slots)
        {
            var sb = new StringBuilder();
            sb.Append("\n");

            // 获取武器本体价格
            var weaponPrice = PriceDataService.Instance.GetPrice(weapon.TemplateId);
            if (!weaponPrice.HasValue)
                return "";

            // 计算配件总价
            double modsPrice = 0;
            if (Settings.ShowWeaponModsPrice.Value)
            {
                modsPrice = PriceCalculator.CalculateWeaponModsPrice(weapon);
            }

            // 总价 = 武器 + 配件
            double totalPrice = weaponPrice.Value + modsPrice;

            // 计算单格价值（用于颜色编码）
            double pricePerSlotForColor = slots > 0 ? totalPrice / slots : totalPrice;

            // 显示总价
            string totalPriceText = $"跳蚤市场: {TextFormatting.FormatPrice(totalPrice)}";
            if (Settings.EnableColorCoding.Value)
            {
                totalPriceText = PriceColorCoding.ApplyColor(totalPriceText, pricePerSlotForColor);
            }
            if (Settings.ShowBestPriceInBold.Value)
            {
                totalPriceText = TextFormatting.Bold(totalPriceText);
            }
            sb.Append($"\n{totalPriceText}");

            // 显示商人回收价格（单独一行）
            AppendTraderPriceIfEnabled(sb, weapon);

            // 显示单格价值（如果启用）
            if (Settings.ShowPricePerSlot.Value && slots > 1)
            {
                double pricePerSlot = totalPrice / slots;
                sb.Append($"\n单格: {TextFormatting.FormatPrice(pricePerSlot)}");
            }

            // 显示枪械本体价格
            sb.Append($"\n枪械价格: {TextFormatting.FormatPrice(weaponPrice.Value)}");

            // 显示配件总价（如果有配件且启用）
            if (Settings.ShowWeaponModsPrice.Value && modsPrice > 0)
            {
                sb.Append($"\n配件总价: {TextFormatting.FormatPrice(modsPrice)}");
            }

            // 显示详细配件列表（如果启用）
            if (Settings.ShowDetailedWeaponMods.Value && Settings.ShowWeaponModsPrice.Value)
            {
                var modsList = PriceCalculator.CollectWeaponModsInfo(weapon);
                if (modsList != null && modsList.Count > 0)
                {
                    sb.Append("\n━━━━━━━━━━━━━━━━");
                    sb.Append("\n配件详情:");

                    foreach (var mod in modsList)
                    {
                        // 根据深度添加缩进（每层4个空格，更明显）
                        string indent = new string(' ', mod.Depth * 4);

                        // 使用更明显的层级前缀
                        string prefix = "";
                        if (mod.Depth > 0)
                        {
                            prefix = "└─ ";
                        }

                        // 格式化配件名称和价格
                        string priceStr = TextFormatting.FormatPrice(mod.Price);

                        // 应用颜色编码（只给名称和价格着色，不给缩进和前缀着色）
                        if (Settings.EnableColorCoding.Value)
                        {
                            string coloredNameAndPrice = PriceColorCoding.ApplyColor($"{mod.Name} {priceStr}", mod.Price);
                            sb.Append($"\n | {indent}{prefix}{coloredNameAndPrice}");
                        }
                        else
                        {
                            sb.Append($"\n | {indent}{prefix}{mod.Name} {priceStr}");
                        }
                    }
                    sb.Append("\n━━━━━━━━━━━━━━━━");
                }
            }

            // Plugin.Log.LogDebug($"✅ 武器价格: {weapon.LocalizedName()} = 总价{totalPrice:N0}₽ (枪{weaponPrice.Value:N0}₽ + 配件{modsPrice:N0}₽)");

            return sb.ToString();
        }

        /// <summary>
        /// 添加商人价格信息（如果启用）
        /// </summary>
        private static void AppendTraderPriceIfEnabled(StringBuilder sb, Item item)
        {
            // 检查是否启用商人价格显示
            if (!Settings.ShowTraderPrices.Value)
                return;

            try
            {
                // 获取最佳商人收购价格
                var traderPrice = TraderPriceService.Instance.GetBestTraderPrice(item);
                if (traderPrice != null)
                {
                    // 显示商人回收信息
                    string traderText = $"商人回收: {traderPrice.TraderName} {traderPrice.CurrencySymbol}{traderPrice.Amount:N0}";

                    // 如果不是卢布，显示转换后的价格
                    if (traderPrice.CurrencySymbol != "₽")
                    {
                        traderText += $" (₽{traderPrice.PriceInRoubles:N0})";
                    }

                    // 应用颜色编码
                    if (Settings.EnableColorCoding.Value)
                    {
                        traderText = PriceColorCoding.ApplyColor(traderText, traderPrice.PriceInRoubles);
                    }

                    sb.Append($"<br>{traderText}");
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"❌ 获取商人价格失败: {ex.Message}");
                Plugin.Log.LogError(ex.StackTrace);
            }
        }

        /// <summary>
        /// 格式化普通物品价格文本
        /// </summary>
        private static string FormatNormalItemPriceText(Item item, int slots)
        {
            var sb = new StringBuilder();
            sb.Append("\n");

            // 获取价格
            var price = PriceDataService.Instance.GetPrice(item.TemplateId);
            if (!price.HasValue)
                return "";

            // 计算单格价值（用于颜色编码）
            double pricePerSlotForColor = slots > 0 ? price.Value / slots : price.Value;

            // 显示跳蚤市场价格
            string priceText = $"跳蚤市场: {TextFormatting.FormatPrice(price.Value)}";
            if (Settings.EnableColorCoding.Value)
            {
                priceText = PriceColorCoding.ApplyColor(priceText, pricePerSlotForColor);
            }
            if (Settings.ShowBestPriceInBold.Value)
            {
                priceText = TextFormatting.Bold(priceText);
            }
            sb.Append($"\n{priceText}");

            // 显示商人回收价格（单独一行）
            AppendTraderPriceIfEnabled(sb, item);

            // 只有多格物品才显示单格价值
            if (Settings.ShowPricePerSlot.Value && slots > 1)
            {
                double pricePerSlot = price.Value / slots;
                sb.Append($"\n单格: {TextFormatting.FormatPrice(pricePerSlot)}");
            }

            // Plugin.Log.LogDebug($"✅ 物品价格: {item.LocalizedName()} = {price.Value:N0}₽");

            return sb.ToString();
        }

        /// <summary>
        /// 格式化堆叠物品价格文本
        /// </summary>
        private static string FormatStackedItemPriceText(Item item, int slots)
        {
            var sb = new StringBuilder();
            sb.Append("\n");

            // 获取单价
            var unitPrice = PriceDataService.Instance.GetPrice(item.TemplateId);
            if (!unitPrice.HasValue)
                return "";

            int stackCount = item.StackObjectsCount;
            double totalPrice = unitPrice.Value * stackCount;

            // 计算单格价值（用于颜色编码）
            double pricePerSlotForColor = slots > 0 ? totalPrice / slots : totalPrice;

            // 显示堆叠总价
            string totalPriceText = $"跳蚤市场: {TextFormatting.FormatPrice(totalPrice)} (x{stackCount})";
            if (Settings.EnableColorCoding.Value)
            {
                totalPriceText = PriceColorCoding.ApplyColor(totalPriceText, pricePerSlotForColor);
            }
            if (Settings.ShowBestPriceInBold.Value)
            {
                totalPriceText = TextFormatting.Bold(totalPriceText);
            }
            sb.Append($"\n{totalPriceText}");

            // 显示商人回收价格（单独一行）
            AppendTraderPriceIfEnabled(sb, item);

            // 显示单价
            sb.Append($"\n单价: {TextFormatting.FormatPrice(unitPrice.Value)}");

            // 如果多格，显示单格价值
            if (Settings.ShowPricePerSlot.Value && slots > 1)
            {
                double pricePerSlot = totalPrice / slots;
                sb.Append($"\n单格: {TextFormatting.FormatPrice(pricePerSlot)}");
            }

            // Plugin.Log.LogDebug($"✅ 堆叠物品: {item.LocalizedName()} = 总价{totalPrice:N0}₽ (单价{unitPrice.Value:N0}₽ x{stackCount})");

            return sb.ToString();
        }

        /// <summary>
        /// 格式化子弹盒/弹匣价格文本
        /// </summary>
        private static string FormatAmmoBoxPriceText(AmmoBox ammoBox, int slots)
        {
            var sb = new StringBuilder();
            sb.Append("\n");

            // Plugin.Log.LogDebug($"🔍 弹匣分析: {ammoBox.LocalizedName()}");
            // Plugin.Log.LogDebug($"  - Cartridges 是否为 null: {ammoBox.Cartridges == null}");
            // Plugin.Log.LogDebug($"  - Cartridges.Items 是否为 null: {ammoBox.Cartridges?.Items == null}");
            // if (ammoBox.Cartridges?.Items != null)
            // {
            //     Plugin.Log.LogDebug($"  - Cartridges.Items.Count: {ammoBox.Cartridges.Items.Count()}");
            // }

            // 获取弹匣本体价格
            var boxPrice = PriceDataService.Instance.GetPrice(ammoBox.TemplateId);
            if (!boxPrice.HasValue)
            {
                Plugin.Log.LogWarning($"⚠️ 弹匣 {ammoBox.LocalizedName()} 没有价格数据");
                return "";
            }

            // 计算子弹总价
            double ammosPrice = 0;
            int ammoCount = 0;
            int? avgPenetration = null;

            if (ammoBox.Cartridges?.Items != null)
            {
                int totalPenetration = 0;
                int penetrationCount = 0;

                foreach (var cartridge in ammoBox.Cartridges.Items)
                {
                    // Plugin.Log.LogDebug($"  - 子弹项类型: {cartridge?.GetType().Name}");

                    if (cartridge is AmmoItemClass ammoItem)
                    {
                        ammoCount++;
                        // Plugin.Log.LogDebug($"    - 子弹 #{ammoCount}: {ammoItem.LocalizedName()}");

                        var ammoPrice = PriceDataService.Instance.GetPrice(ammoItem.TemplateId);
                        if (ammoPrice.HasValue)
                        {
                            ammosPrice += ammoPrice.Value;
                            // Plugin.Log.LogDebug($"      价格: {ammoPrice.Value:N0}₽");
                        }
                        else
                        {
                            Plugin.Log.LogWarning($"      ⚠️ 子弹 {ammoItem.LocalizedName()} 没有价格数据");
                        }

                        // 计算平均穿甲值
                        if (ammoItem.PenetrationPower > 0)
                        {
                            totalPenetration += ammoItem.PenetrationPower;
                            penetrationCount++;
                            // Plugin.Log.LogDebug($"      穿甲: {ammoItem.PenetrationPower}");
                        }
                    }
                }

                if (penetrationCount > 0)
                {
                    avgPenetration = totalPenetration / penetrationCount;
                }

                // Plugin.Log.LogDebug($"  - 统计: 子弹数量={ammoCount}, 子弹总价={ammosPrice:N0}₽");
            }
            else
            {
                Plugin.Log.LogWarning($"⚠️ 弹匣 {ammoBox.LocalizedName()} 的 Cartridges.Items 为 null 或空");
            }

            // 总价 = 弹匣 + 子弹
            double totalPrice = boxPrice.Value + ammosPrice;

            // 计算单格价值（用于颜色编码）
            double pricePerSlotForColor = slots > 0 ? totalPrice / slots : totalPrice;

            // 显示总价（按穿甲等级着色）
            string totalPriceText = $"跳蚤市场: {TextFormatting.FormatPrice(totalPrice)}";
            if (avgPenetration.HasValue && Settings.UseCaliberPenetrationPower.Value)
            {
                totalPriceText = AmmoColorCoding.ApplyPenetrationColor(totalPriceText, avgPenetration.Value);
            }
            else if (Settings.EnableColorCoding.Value)
            {
                totalPriceText = PriceColorCoding.ApplyColor(totalPriceText, pricePerSlotForColor);
            }
            if (Settings.ShowBestPriceInBold.Value)
            {
                totalPriceText = TextFormatting.Bold(totalPriceText);
            }
            sb.Append($"\n{totalPriceText}");

            // 显示弹匣价值
            sb.Append($"\n弹匣价值: {TextFormatting.FormatPrice(boxPrice.Value)}");

            // 显示子弹价值（含数量）
            if (ammoCount > 0)
            {
                sb.Append($"\n子弹价值: {TextFormatting.FormatPrice(ammosPrice)} (x{ammoCount})");
            }

            // 显示单格价值
            if (Settings.ShowPricePerSlot.Value && slots > 1)
            {
                double pricePerSlot = totalPrice / slots;
                sb.Append($"\n单格: {TextFormatting.FormatPrice(pricePerSlot)}");
            }

            // Plugin.Log.LogDebug($"✅ 弹匣: {ammoBox.LocalizedName()} = 总价{totalPrice:N0}₽ (弹匣{boxPrice.Value:N0}₽ + 子弹{ammosPrice:N0}₽ x{ammoCount})");

            return sb.ToString();
        }

        /// <summary>
        /// 格式化弹匣价格文本 (MagazineItemClass)
        /// </summary>
        private static string FormatMagazinePriceText(MagazineItemClass magazine, int slots)
        {
            var sb = new StringBuilder();
            sb.Append("\n");

            // Plugin.Log.LogInfo($"🔍 弹匣详细分析: {magazine.LocalizedName()}");
            // Plugin.Log.LogInfo($"  - 弹匣类型: {magazine.GetType().FullName}");
            // Plugin.Log.LogInfo($"  - Cartridges 是否为 null: {magazine.Cartridges == null}");

            // if (magazine.Cartridges != null)
            // {
            //     Plugin.Log.LogInfo($"  - Cartridges 类型: {magazine.Cartridges.GetType().FullName}");
            //     Plugin.Log.LogInfo($"  - Cartridges.Items 是否为 null: {magazine.Cartridges.Items == null}");

            //     if (magazine.Cartridges.Items != null)
            //     {
            //         var itemsList = magazine.Cartridges.Items.ToList();
            //         Plugin.Log.LogInfo($"  - Cartridges.Items.Count: {itemsList.Count}");

            //         for (int i = 0; i < itemsList.Count && i < 3; i++)
            //         {
            //             var item = itemsList[i];
            //             Plugin.Log.LogInfo($"    - Item[{i}]: {item?.GetType().Name} - {item?.LocalizedName()}");
            //         }
            //     }
            // }

            // // 尝试其他可能的属性
            // Plugin.Log.LogInfo($"  - 尝试 Count 属性: {magazine.Count}");
            // Plugin.Log.LogInfo($"  - 尝试 MaxCount 属性: {magazine.MaxCount}");

            // 获取弹匣本体价格
            var magPrice = PriceDataService.Instance.GetPrice(magazine.TemplateId);
            if (!magPrice.HasValue)
            {
                Plugin.Log.LogWarning($"⚠️ 弹匣 {magazine.LocalizedName()} 没有价格数据");
                return "";
            }

            // 计算子弹总价和数量（支持混装弹匣）
            double ammosPrice = 0;
            int ammoCount = magazine.Count; // 弹匣内子弹总数
            int totalPenetration = 0;
            int totalAmmoForPenetration = 0;

            // 保存每种子弹的详细信息（用于显示）
            var ammoDetails = new System.Collections.Generic.List<(string name, int penetration, int count, double price)>();

            // Plugin.Log.LogInfo($"  - 弹匣内子弹总数: {ammoCount}");

            // 遍历所有子弹类型（混装弹匣可能有多种子弹）
            if (magazine.Cartridges?.Items != null && magazine.Cartridges.Items.Any())
            {
                // Plugin.Log.LogInfo($"  - 子弹类型数量: {magazine.Cartridges.Items.Count()}");

                int itemIndex = 0;
                foreach (var cartridge in magazine.Cartridges.Items)
                {
                    if (cartridge is AmmoItemClass ammoItem)
                    {
                        int stackCount = ammoItem.StackObjectsCount;
                        // Plugin.Log.LogInfo($"    - Item[{itemIndex}]: {ammoItem.LocalizedName()}");
                        // Plugin.Log.LogInfo($"      堆叠数量: {stackCount}");

                        // 获取子弹单价
                        var ammoUnitPrice = PriceDataService.Instance.GetPrice(ammoItem.TemplateId);
                        if (ammoUnitPrice.HasValue)
                        {
                            double itemTotalPrice = ammoUnitPrice.Value * stackCount;
                            ammosPrice += itemTotalPrice;

                            // 保存子弹详细信息
                            ammoDetails.Add((
                                ammoItem.LocalizedName(),
                                ammoItem.PenetrationPower,
                                stackCount,
                                itemTotalPrice
                            ));

                            // Plugin.Log.LogInfo($"      单价: {ammoUnitPrice.Value:N0}₽");
                            // Plugin.Log.LogInfo($"      小计: {itemTotalPrice:N0}₽ ({ammoUnitPrice.Value:N0}₽ × {stackCount})");
                        }
                        else
                        {
                            // Plugin.Log.LogWarning($"      ⚠️ 子弹 {ammoItem.LocalizedName()} 没有价格数据");
                        }

                        // 计算加权平均穿甲值
                        if (ammoItem.PenetrationPower > 0)
                        {
                            totalPenetration += ammoItem.PenetrationPower * stackCount;
                            totalAmmoForPenetration += stackCount;
                            // Plugin.Log.LogInfo($"      穿甲值: {ammoItem.PenetrationPower}");
                        }

                        itemIndex++;
                    }
                }

                // Plugin.Log.LogInfo($"  - 子弹总价: {ammosPrice:N0}₽");
            }
            else
            {
                // Plugin.Log.LogWarning($"⚠️ 弹匣 {magazine.LocalizedName()} 的 Cartridges.Items 为空");
            }

            // 计算加权平均穿甲值
            int? avgPenetration = null;
            if (totalAmmoForPenetration > 0)
            {
                avgPenetration = totalPenetration / totalAmmoForPenetration;
                // Plugin.Log.LogInfo($"  - 平均穿甲值: {avgPenetration.Value} (加权平均)");
            }

            // 总价 = 弹匣 + 子弹
            double totalPrice = magPrice.Value + ammosPrice;

            // 计算单格价值（用于颜色编码）
            double pricePerSlotForColor = slots > 0 ? totalPrice / slots : totalPrice;

            // 显示总价（按穿甲等级着色）
            string totalPriceText = $"跳蚤市场: {TextFormatting.FormatPrice(totalPrice)}";
            if (avgPenetration.HasValue && Settings.UseCaliberPenetrationPower.Value)
            {
                totalPriceText = AmmoColorCoding.ApplyPenetrationColor(totalPriceText, avgPenetration.Value);
            }
            else if (Settings.EnableColorCoding.Value)
            {
                totalPriceText = PriceColorCoding.ApplyColor(totalPriceText, pricePerSlotForColor);
            }
            if (Settings.ShowBestPriceInBold.Value)
            {
                totalPriceText = TextFormatting.Bold(totalPriceText);
            }
            sb.Append($"\n{totalPriceText}");

            // 显示商人回收价格（单独一行）
            AppendTraderPriceIfEnabled(sb, magazine);

            // 显示弹匣价值
            sb.Append($"\n弹匣价值: {TextFormatting.FormatPrice(magPrice.Value)}");

            // 显示子弹价值（详细列出每种子弹）
            if (ammoCount > 0)
            {
                sb.Append($"\n子弹价值: {TextFormatting.FormatPrice(ammosPrice)}");

                // 显示子弹详情（参考配件展示格式）
                sb.Append("\n━━━━━━━━━━━━━━━━");
                sb.Append("\n子弹详情:");

                // 逐个显示每种子弹的详细信息
                foreach (var detail in ammoDetails)
                {
                    string ammoLine = $"{detail.name} 穿甲{detail.penetration} x{detail.count} {TextFormatting.FormatPrice(detail.price)}";

                    // 按穿甲等级着色（如果启用）
                    if (Settings.UseCaliberPenetrationPower.Value && Settings.EnableColorCoding.Value)
                    {
                        ammoLine = AmmoColorCoding.ApplyPenetrationColor(ammoLine, detail.penetration);
                    }

                    sb.Append($"\n | {ammoLine}");  // 使用 | 前缀统一格式
                }
                sb.Append("\n━━━━━━━━━━━━━━━━");
            }

            // 显示单格价值
            if (Settings.ShowPricePerSlot.Value && slots > 1)
            {
                double pricePerSlot = totalPrice / slots;
                sb.Append($"\n单格: {TextFormatting.FormatPrice(pricePerSlot)}");
            }

            // Plugin.Log.LogDebug($"✅ 弹匣: {magazine.LocalizedName()} = 总价{totalPrice:N0}₽ (弹匣{magPrice.Value:N0}₽ + 子弹{ammosPrice:N0}₽ x{ammoCount})");

            return sb.ToString();
        }

        /// <summary>
        /// 格式化单发子弹价格文本
        /// </summary>
        private static string FormatAmmoPriceText(AmmoItemClass ammoItem, int slots)
        {
            var sb = new StringBuilder();
            sb.Append("\n");

            // 获取价格
            var price = PriceDataService.Instance.GetPrice(ammoItem.TemplateId);
            if (!price.HasValue)
                return "";

            int stackCount = ammoItem.StackObjectsCount;
            double totalPrice = price.Value * stackCount;

            // 计算单格价值（用于颜色编码）
            double pricePerSlotForColor = slots > 0 ? totalPrice / slots : totalPrice;

            // 显示价格（按穿甲等级着色）
            string priceText = $"跳蚤市场: {TextFormatting.FormatPrice(totalPrice)}";
            if (stackCount > 1)
            {
                priceText += $" (x{stackCount})";
            }

            if (ammoItem.PenetrationPower > 0 && Settings.UseCaliberPenetrationPower.Value)
            {
                priceText = AmmoColorCoding.ApplyPenetrationColor(priceText, ammoItem.PenetrationPower);
            }
            else if (Settings.EnableColorCoding.Value)
            {
                priceText = PriceColorCoding.ApplyColor(priceText, pricePerSlotForColor);
            }

            if (Settings.ShowBestPriceInBold.Value)
            {
                priceText = TextFormatting.Bold(priceText);
            }
            sb.Append($"\n{priceText}");

            // 显示商人回收价格（单独一行）
            AppendTraderPriceIfEnabled(sb, ammoItem);

            // 如果是堆叠，显示单价
            if (stackCount > 1)
            {
                sb.Append($"\n单价: {TextFormatting.FormatPrice(price.Value)}");
            }

            // 显示穿甲值
            if (ammoItem.PenetrationPower > 0)
            {
                sb.Append($"\n穿甲: {ammoItem.PenetrationPower}");
            }

            // Plugin.Log.LogDebug($"✅ 子弹: {ammoItem.LocalizedName()} = {totalPrice:N0}₽ (穿甲{ammoItem.PenetrationPower})");

            return sb.ToString();
        }

        /// <summary>
        /// 格式化护甲价格文本
        /// </summary>
        private static string FormatArmorPriceText(Item armor, int slots)
        {
            var sb = new StringBuilder();
            sb.Append("\n");

            // 获取护甲价格
            var armorPrice = PriceDataService.Instance.GetPrice(armor.TemplateId);
            if (!armorPrice.HasValue)
                return "";

            // 计算单格价值（用于颜色编码）
            double pricePerSlotForColor = slots > 0 ? armorPrice.Value / slots : armorPrice.Value;

            // 显示跳蚤市场价格
            string priceText = $"跳蚤市场: {TextFormatting.FormatPrice(armorPrice.Value)}";
            if (Settings.EnableColorCoding.Value)
            {
                priceText = PriceColorCoding.ApplyColor(priceText, pricePerSlotForColor);
            }
            if (Settings.ShowBestPriceInBold.Value)
            {
                priceText = TextFormatting.Bold(priceText);
            }
            sb.Append($"\n{priceText}");

            // 显示商人回收价格（单独一行）
            AppendTraderPriceIfEnabled(sb, armor);

            // 显示单格价值（如果启用）
            if (Settings.ShowPricePerSlot.Value && slots > 1)
            {
                double pricePerSlot = armorPrice.Value / slots;
                sb.Append($"\n单格: {TextFormatting.FormatPrice(pricePerSlot)}");
            }

            // 显示防弹等级（如果启用）
            if (Settings.ShowArmorClass.Value)
            {
                var armorClass = ArmorHelper.GetArmorClass(armor);
                if (armorClass.HasValue && armorClass.Value > 0)
                {
                    string armorClassText = $"防弹等级: {armorClass.Value}级";

                    // 根据防弹等级着色（如果启用）
                    if (Settings.EnableArmorClassColoring.Value && Settings.EnableColorCoding.Value)
                    {
                        // 使用护甲等级颜色（与背景色相同的颜色方案）
                        armorClassText = ArmorColorCoding.ApplyArmorClassColor(armorClassText, armorClass.Value);
                    }

                    sb.Append($"\n{armorClassText}");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// 检查物品是否是防弹插板
        /// </summary>
        private static bool IsArmorPlate(Item item)
        {
            if (item == null)
                return false;

            var typeName = item.GetType().Name;
            return typeName == "ArmorPlateItemClass" ||
                   typeName == "BuiltInInsertsItemClass" ||
                   typeName.Contains("Plate") ||
                   typeName.Contains("plate") ||
                   typeName.Contains("Insert") ||
                   typeName.Contains("insert");
        }

        /// <summary>
        /// 格式化防弹插板价格文本
        /// </summary>
        private static string FormatArmorPlatePriceText(Item plate, int slots)
        {
            var sb = new StringBuilder();
            sb.Append("\n");

            // 获取插板价格
            var platePrice = PriceDataService.Instance.GetPrice(plate.TemplateId);
            if (!platePrice.HasValue)
                return "";

            // 计算单格价值（用于颜色编码）
            double pricePerSlotForColor = slots > 0 ? platePrice.Value / slots : platePrice.Value;

            // 显示跳蚤市场价格
            string priceText = $"跳蚤市场: {TextFormatting.FormatPrice(platePrice.Value)}";
            if (Settings.EnableColorCoding.Value)
            {
                priceText = PriceColorCoding.ApplyColor(priceText, pricePerSlotForColor);
            }
            if (Settings.ShowBestPriceInBold.Value)
            {
                priceText = TextFormatting.Bold(priceText);
            }
            sb.Append($"\n{priceText}");

            // 显示商人回收价格（单独一行）
            AppendTraderPriceIfEnabled(sb, plate);

            // 显示单格价值（如果启用）
            if (Settings.ShowPricePerSlot.Value && slots > 1)
            {
                double pricePerSlot = platePrice.Value / slots;
                sb.Append($"\n单格: {TextFormatting.FormatPrice(pricePerSlot)}");
            }

            // 显示防弹等级（如果启用）
            if (Settings.ShowArmorClass.Value)
            {
                var plateClass = ArmorHelper.GetArmorClass(plate);
                if (plateClass.HasValue && plateClass.Value > 0)
                {
                    string plateClassText = $"防弹等级: {plateClass.Value}级";

                    // 根据防弹等级着色（如果启用）
                    if (Settings.EnableArmorClassColoring.Value && Settings.EnableColorCoding.Value)
                    {
                        plateClassText = ArmorColorCoding.ApplyArmorClassColor(plateClassText, plateClass.Value);
                    }

                    sb.Append($"\n{plateClassText}");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// 格式化配件价格文本（显示配件和子配件）
        /// </summary>
        private static string FormatModPriceText(Mod mod, int slots)
        {
            var sb = new StringBuilder();
            sb.Append("\n");

            // 获取配件本体价格
            var modPrice = PriceDataService.Instance.GetPrice(mod.TemplateId);
            if (!modPrice.HasValue)
                return "";

            // 计算子配件总价
            double childModsPrice = 0;
            if (Settings.ShowWeaponModsPrice.Value && mod.Slots != null && mod.Slots.Any())
            {
                var visitedMods = new System.Collections.Generic.HashSet<string>();
                visitedMods.Add(mod.Id); // 标记当前配件为已访问

                // 收集所有子配件
                var childMods = new System.Collections.Generic.List<Mod>();
                foreach (var slot in mod.Slots)
                {
                    if (slot.ContainedItem is Mod childMod)
                    {
                        childMods.Add(childMod);
                    }
                }

                if (childMods.Count > 0)
                {
                    childModsPrice = PriceCalculator.CalculateModsPrice(childMods, visitedMods, 1);
                }
            }

            // 总价 = 配件本体 + 子配件
            double totalPrice = modPrice.Value + childModsPrice;

            // 计算单格价值（用于颜色编码）
            double pricePerSlotForColor = slots > 0 ? totalPrice / slots : totalPrice;

            // 显示总价
            string totalPriceText = $"跳蚤市场: {TextFormatting.FormatPrice(totalPrice)}";
            if (Settings.EnableColorCoding.Value)
            {
                totalPriceText = PriceColorCoding.ApplyColor(totalPriceText, pricePerSlotForColor);
            }
            if (Settings.ShowBestPriceInBold.Value)
            {
                totalPriceText = TextFormatting.Bold(totalPriceText);
            }
            sb.Append($"\n{totalPriceText}");

            // 显示商人回收价格（单独一行）
            AppendTraderPriceIfEnabled(sb, mod);

            // 显示单格价值（如果启用）
            if (Settings.ShowPricePerSlot.Value && slots > 1)
            {
                double pricePerSlot = totalPrice / slots;
                sb.Append($"\n单格: {TextFormatting.FormatPrice(pricePerSlot)}");
            }

            // 显示配件本体价格
            sb.Append($"\n配件价格: {TextFormatting.FormatPrice(modPrice.Value)}");

            // 显示子配件总价（如果有子配件且启用）
            if (Settings.ShowWeaponModsPrice.Value && childModsPrice > 0)
            {
                sb.Append($"\n子配件总价: {TextFormatting.FormatPrice(childModsPrice)}");
            }

            // 显示详细子配件列表（如果启用）
            if (Settings.ShowDetailedWeaponMods.Value && Settings.ShowWeaponModsPrice.Value)
            {
                var visitedMods = new System.Collections.Generic.HashSet<string>();
                visitedMods.Add(mod.Id);

                var childMods = new System.Collections.Generic.List<Mod>();
                if (mod.Slots != null)
                {
                    foreach (var slot in mod.Slots)
                    {
                        if (slot.ContainedItem is Mod childMod)
                        {
                            childMods.Add(childMod);
                        }
                    }
                }

                if (childMods.Count > 0)
                {
                    var modsList = new System.Collections.Generic.List<ModInfo>();
                    PriceCalculator.CollectModsInfo(childMods, visitedMods, 1, modsList);

                    if (modsList.Count > 0)
                    {
                        sb.Append("\n━━━━━━━━━━━━━━━━");
                        sb.Append("\n子配件详情:");

                        foreach (var modInfo in modsList)
                        {
                            // 调整深度显示（从1开始，所以减1）
                            string indent = new string(' ', (modInfo.Depth - 1) * 4);
                            string prefix = modInfo.Depth > 1 ? "└─ " : "";
                            string priceStr = TextFormatting.FormatPrice(modInfo.Price);

                            if (Settings.EnableColorCoding.Value)
                            {
                                string coloredNameAndPrice = PriceColorCoding.ApplyColor($"{modInfo.Name} {priceStr}", modInfo.Price);
                                sb.Append($"\n | {indent}{prefix}{coloredNameAndPrice}");
                            }
                            else
                            {
                                sb.Append($"\n | {indent}{prefix}{modInfo.Name} {priceStr}");
                            }
                        }
                        sb.Append("\n━━━━━━━━━━━━━━━━");
                    }
                }
            }

            // Plugin.Log.LogDebug($"✅ 配件价格: {mod.LocalizedName()} = 总价{totalPrice:N0}₽ (配件{modPrice.Value:N0}₽ + 子配件{childModsPrice:N0}₽)");

            return sb.ToString();
        }

        /// <summary>
        /// 快速估算容器内物品数量（不递归，仅统计第一层）
        /// 用于判断是否跳过详细计算
        /// </summary>
        /// <param name="container">容器物品</param>
        /// <returns>估算的物品数量</returns>
        private static int EstimateContainerItemCount(Item container)
        {
            try
            {
                int count = 0;
                var containerType = container.GetType();

                // 尝试获取 Grids 属性
                var gridsProperty = containerType.GetProperty("Grids");
                if (gridsProperty == null)
                    return 0;

                var grids = gridsProperty.GetValue(container) as System.Collections.IEnumerable;
                if (grids == null)
                    return 0;

                // 遍历所有网格，统计物品数量
                foreach (var grid in grids)
                {
                    if (grid == null)
                        continue;

                    var itemsProperty = grid.GetType().GetProperty("Items");
                    if (itemsProperty == null)
                        continue;

                    var items = itemsProperty.GetValue(grid) as System.Collections.IEnumerable;
                    if (items == null)
                        continue;

                    // 仅统计数量，不进行任何计算
                    count += items.Cast<object>().Count();
                }

                return count;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 检查物品是否有容器（背包、箱子等）
        /// </summary>
        private static bool HasContainer(Item item)
        {
            try
            {
                var itemType = item.GetType();
                // Plugin.Log.LogInfo($"   🔍 HasContainer检查: {item.LocalizedName()} (类型: {itemType.Name})");

                // 方法1: 检查 IsContainer 属性（最简单）
                var isContainerProperty = itemType.GetProperty("IsContainer");
                if (isContainerProperty != null)
                {
                    var isContainer = isContainerProperty.GetValue(item);
                    // Plugin.Log.LogInfo($"      - IsContainer 属性值: {isContainer}");

                    if (isContainer is bool boolValue && boolValue)
                    {
                        // Plugin.Log.LogInfo($"      ✅ HasContainer 结果: true (通过 IsContainer 属性)");
                        return true;
                    }
                }

                // 方法2: 检查 Grids 属性（备用）
                var gridsProperty = itemType.GetProperty("Grids");
                if (gridsProperty != null)
                {
                    var grids = gridsProperty.GetValue(item) as System.Collections.IEnumerable;
                    if (grids != null && grids.Cast<object>().Any())
                    {
                        // Plugin.Log.LogInfo($"      ✅ HasContainer 结果: true (通过 Grids 属性)");
                        return true;
                    }
                }

                // Plugin.Log.LogInfo($"      ❌ HasContainer 结果: false");
                return false;
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"⚠️ 检查容器失败: {item.LocalizedName()} - {ex.Message}");
                Plugin.Log.LogError($"   堆栈: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// 格式化容器价格文本（背包、箱子等）
        /// </summary>
        private static string FormatContainerPriceText(Item container, int slots)
        {
            var sb = new StringBuilder();
            sb.Append("\n");

            // 获取容器本身价格
            var containerPrice = PriceDataService.Instance.GetPrice(container.TemplateId);
            if (!containerPrice.HasValue)
            {
                return "";
            }

            // 快速估算容器内物品数量
            int estimatedItemCount = EstimateContainerItemCount(container);

            // 检查是否跳过大容器计算
            if (Settings.SkipLargeContainers.Value &&
                estimatedItemCount > Settings.LargeContainerThreshold.Value)
            {
                // 大容器：仅显示容器本身价格 + 警告
                double pricePerSlotForColor = slots > 0 ? containerPrice.Value / slots : containerPrice.Value;

                // 显示容器本身价格
                string containerPriceText = $"跳蚤市场: {TextFormatting.FormatPrice(containerPrice.Value)}";
                if (Settings.EnableColorCoding.Value)
                {
                    containerPriceText = PriceColorCoding.ApplyColor(containerPriceText, pricePerSlotForColor);
                }
                if (Settings.ShowBestPriceInBold.Value)
                {
                    containerPriceText = TextFormatting.Bold(containerPriceText);
                }
                sb.Append($"\n{containerPriceText}");

                // 显示商人回收价格（单独一行）
                AppendTraderPriceIfEnabled(sb, container);

                // 显示单格价值（如果启用）
                if (Settings.ShowPricePerSlot.Value && slots > 1)
                {
                    double pricePerSlot = containerPrice.Value / slots;
                    sb.Append($"\n单格: {TextFormatting.FormatPrice(pricePerSlot)}");
                }

                // 显示容器本身价格
                sb.Append($"\n容器价值: {TextFormatting.FormatPrice(containerPrice.Value)}");

                // 显示警告信息
                sb.Append($"\n⚠️ 物品过多（约{estimatedItemCount}个）");
                sb.Append($"\n跳过详细计算以避免卡顿");
                sb.Append($"\n可在配置中调整此限制");

                return sb.ToString();
            }

            // 小容器：正常计算所有物品价值
            double itemsPrice = CalculateContainerItemsPrice(container, 0);

            // 总价 = 容器 + 内部物品
            double totalPrice = containerPrice.Value + itemsPrice;

            // 计算单格价值（用于颜色编码）
            double totalPricePerSlot = slots > 0 ? totalPrice / slots : totalPrice;

            // 显示总价
            string totalPriceText = $"跳蚤市场: {TextFormatting.FormatPrice(totalPrice)}";
            if (Settings.EnableColorCoding.Value)
            {
                totalPriceText = PriceColorCoding.ApplyColor(totalPriceText, totalPricePerSlot);
            }
            if (Settings.ShowBestPriceInBold.Value)
            {
                totalPriceText = TextFormatting.Bold(totalPriceText);
            }
            sb.Append($"\n{totalPriceText}");

            // 显示商人回收价格（单独一行）
            AppendTraderPriceIfEnabled(sb, container);

            // 显示单格价值（如果启用）
            if (Settings.ShowPricePerSlot.Value && slots > 1)
            {
                double pricePerSlot = totalPrice / slots;
                sb.Append($"\n单格: {TextFormatting.FormatPrice(pricePerSlot)}");
            }

            // 显示容器本身价格
            sb.Append($"\n容器价值: {TextFormatting.FormatPrice(containerPrice.Value)}");

            // 显示容器内物品价值（即使为0也显示）
            sb.Append($"\n内部物品: {TextFormatting.FormatPrice(itemsPrice)}");
            if (itemsPrice == 0)
            {
                sb.Append(" (空)");
            }

            return sb.ToString();
        }

        /// <summary>
        /// 递归计算容器内所有物品的价值
        /// </summary>
        /// <param name="container">容器物品</param>
        /// <param name="depth">递归深度（防止栈溢出）</param>
        /// <returns>容器内所有物品的总价值</returns>
        private static double CalculateContainerItemsPrice(Item container, int depth)
        {
            // 防止栈溢出：使用配置的最大递归深度（默认5层，原版50层）
            if (depth >= Settings.MaxContainerDepth.Value)
            {
                return 0;
            }

            double total = 0;

            try
            {
                var containerType = container.GetType();
                // Plugin.Log.LogInfo($"  {new string(' ', depth * 2)}🔍 尝试查找容器网格属性...");

                // 尝试多种可能的属性名
                string[] possibleGridNames = { "Grids", "Grid", "Containers", "Container" };
                System.Reflection.PropertyInfo gridsProperty = null;

                foreach (var name in possibleGridNames)
                {
                    gridsProperty = containerType.GetProperty(name);
                    if (gridsProperty != null)
                    {
                        // Plugin.Log.LogInfo($"  {new string(' ', depth * 2)}✅ 找到属性: {name} (类型: {gridsProperty.PropertyType.Name})");
                        break;
                    }
                    else
                    {
                        // Plugin.Log.LogInfo($"  {new string(' ', depth * 2)}❌ 未找到属性: {name}");
                    }
                }

                if (gridsProperty == null)
                {
                    // Plugin.Log.LogInfo($"  {new string(' ', depth * 2)}⚠️ 容器没有任何已知的网格属性");

                    // 列出所有属性帮助调试
                    // var allProps = containerType.GetProperties().Take(20).Select(p => p.Name);
                    // Plugin.Log.LogInfo($"  {new string(' ', depth * 2)}📋 容器的前20个属性: {string.Join(", ", allProps)}");
                    return 0;
                }

                var grids = gridsProperty.GetValue(container) as System.Collections.IEnumerable;
                if (grids == null)
                {
                    // Plugin.Log.LogInfo($"  {new string(' ', depth * 2)}⚠️ {gridsProperty.Name} 为 null");
                    return 0;
                }

                var gridsList = grids.Cast<object>().ToList();
                if (gridsList.Count == 0)
                {
                    // Plugin.Log.LogInfo($"  {new string(' ', depth * 2)}ℹ️  容器为空 (无网格)");
                    return 0;
                }

                // Plugin.Log.LogInfo($"  {new string(' ', depth * 2)}📦 容器有 {gridsList.Count} 个网格");

                int itemCount = 0;

                foreach (var grid in gridsList)
                {
                    if (grid == null)
                        continue;

                    // 使用反射访问 Items 属性
                    var itemsProperty = grid.GetType().GetProperty("Items");
                    if (itemsProperty == null)
                    {
                        // Plugin.Log.LogInfo($"  {new string(' ', depth * 2)}  ⚠️ Grid 没有 Items 属性");
                        continue;
                    }

                    var items = itemsProperty.GetValue(grid) as System.Collections.IEnumerable;
                    if (items == null)
                    {
                        // Plugin.Log.LogInfo($"  {new string(' ', depth * 2)}  ⚠️ Grid.Items 为 null");
                        continue;
                    }

                    var itemsList = items.Cast<object>().ToList();
                    // Plugin.Log.LogInfo($"  {new string(' ', depth * 2)}  📦 网格有 {itemsList.Count} 个物品");

                    foreach (var gridItemObj in itemsList)
                    {
                        if (gridItemObj == null)
                            continue;

                        Item gridItem = gridItemObj as Item;
                        if (gridItem == null)
                        {
                            // Plugin.Log.LogInfo($"  {new string(' ', depth * 2)}    ⚠️ 物品转换失败 (类型: {gridItemObj.GetType().Name})");
                            continue;
                        }

                        itemCount++;

                        // 检查是否超过物品数量限制（0表示无限制）
                        int maxItems = Settings.MaxContainerItems.Value;
                        if (maxItems > 0 && itemCount > maxItems)
                        {
                            // 超过限制，停止计算
                            return total;
                        }

                        // 获取物品价格
                        var itemPrice = PriceDataService.Instance.GetPrice(gridItem.TemplateId);
                        if (itemPrice.HasValue)
                        {
                            double itemValue = itemPrice.Value * gridItem.StackObjectsCount;
                            total += itemValue;

                            // Plugin.Log.LogInfo($"  {new string(' ', depth * 2)}    ✅ 物品 #{itemCount}: {gridItem.LocalizedName()} = {itemValue:N0}₽");
                        }
                        else
                        {
                            // Plugin.Log.LogInfo($"  {new string(' ', depth * 2)}    ⚠️ 物品 #{itemCount}: {gridItem.LocalizedName()} 无价格数据");
                        }

                        // 如果是武器，计算配件价值
                        if (gridItem is Weapon weaponInContainer)
                        {
                            double weaponModsPrice = PriceCalculator.CalculateWeaponModsPrice(weaponInContainer);
                            total += weaponModsPrice;
                            // Plugin.Log.LogInfo($"  {new string(' ', depth * 2)}      └─ 武器配件: {weaponModsPrice:N0}₽");
                        }
                        // 如果是弹匣，计算子弹价值
                        else if (gridItem is MagazineItemClass magazineInContainer)
                        {
                            double ammoPrice = 0;
                            if (magazineInContainer.Cartridges?.Items != null)
                            {
                                foreach (var cartridge in magazineInContainer.Cartridges.Items)
                                {
                                    if (cartridge is AmmoItemClass ammoItem)
                                    {
                                        var ammoUnitPrice = PriceDataService.Instance.GetPrice(ammoItem.TemplateId);
                                        if (ammoUnitPrice.HasValue)
                                        {
                                            ammoPrice += ammoUnitPrice.Value * ammoItem.StackObjectsCount;
                                        }
                                    }
                                }
                            }
                            total += ammoPrice;
                            if (ammoPrice > 0)
                            {
                                // Plugin.Log.LogInfo($"  {new string(' ', depth * 2)}      └─ 弹匣子弹: {ammoPrice:N0}₽");
                            }
                        }

                        // 递归计算子容器（如背包里的背包）
                        if (HasContainer(gridItem))
                        {
                            // Plugin.Log.LogInfo($"  {new string(' ', depth * 2)}      🔍 检测到子容器: {gridItem.LocalizedName()}");
                            double subContainerPrice = CalculateContainerItemsPrice(gridItem, depth + 1);
                            total += subContainerPrice;
                            // Plugin.Log.LogInfo($"  {new string(' ', depth * 2)}      └─ 子容器内容: {subContainerPrice:N0}₽");
                        }
                    }
                }

                // Plugin.Log.LogInfo($"  {new string(' ', depth * 2)}📊 总计发现 {itemCount} 个物品，总价值 {total:N0}₽");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"❌ 计算容器内物品价格失败 (深度:{depth}): {ex.Message}");
                Plugin.Log.LogError($"   堆栈跟踪: {ex.StackTrace}");
            }

            return total;
        }
    }
}
