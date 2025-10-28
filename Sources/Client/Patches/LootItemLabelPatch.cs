using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using QuickPrice.Config;
using QuickPrice.Services;
using QuickPrice.Utils;
using TMPro;

namespace QuickPrice.Patches
{
    /// <summary>
    /// 地面物品名称着色补丁 - 简化优化版本
    /// 拦截 ActionPanel 的物品名称设置，应用价格颜色编码
    /// </summary>
    public class LootItemLabelPatch : ModulePatch
    {
        // ===== 反射字段缓存（性能优化）=====
        private static FieldInfo _itemNameField;              // ActionPanel._itemName 字段
        private static FieldInfo _gamePlayerOwnerField;       // ActionPanel.gamePlayerOwner_0 字段

        // ===== 颜色缓存（性能优化）=====
        private static readonly Dictionary<string, string> _colorCache = new Dictionary<string, string>();

        // ===== 状态标志 =====
        private static bool _isInitialized = false;           // 是否已初始化反射字段
        private static bool _isReflectionFailed = false;      // 反射是否失败（快速失败）

        protected override MethodBase GetTargetMethod()
        {
            // 查找 ActionPanel 的 method_0 方法
            // 签名: void method_0(ActionsReturnClass interactionState)
            var method = AccessTools.Method(typeof(ActionPanel), "method_0");
            if (method == null)
            {
                Plugin.Log.LogError("❌ 无法找到 ActionPanel.method_0 方法");
            }
            else
            {
                Plugin.Log.LogInfo($"✅ 找到目标方法: {method.DeclaringType.FullName}.{method.Name}");
                Plugin.Log.LogInfo($"   参数: {string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name))}");
            }
            return method;
        }

        [PatchPostfix]
        public static void Postfix(ActionPanel __instance, object interactionState)
        {
            try
            {
                // ===== 快速退出检查 =====
                if (_isReflectionFailed)
                    return;

                if (!Settings.PluginEnabled.Value || !Settings.ColorItemName.Value || !Settings.EnableColorCoding.Value)
                    return;

                if (__instance == null || interactionState == null)
                    return;

                // ===== 延迟初始化反射字段（只执行一次）=====
                if (!_isInitialized)
                {
                    InitializeReflectionFields();
                }

                // 如果初始化失败，退出
                if (_isReflectionFailed)
                    return;

                // ===== 获取 TextMeshProUGUI 组件 =====
                var itemNameText = (TextMeshProUGUI)_itemNameField.GetValue(__instance);
                if (itemNameText == null || string.IsNullOrEmpty(itemNameText.text))
                    return;

                // ===== 获取当前物品（从 Player.InteractableObject）=====
                var lootItem = GetCurrentLootItem(__instance);
                if (lootItem == null)
                    return;

                // ===== 打印详细的物品信息（用于调试）=====
                LogDetailedItemInfo(lootItem);

                // ===== 应用颜色编码（使用缓存）=====
                string originalText = itemNameText.text;
                string coloredText = GetColoredItemName(lootItem, originalText);

                if (coloredText != originalText)
                {
                    itemNameText.text = coloredText;
                    Plugin.Log.LogInfo($"✅ 地面物品着色成功: {lootItem.Name} (TemplateId: {lootItem.TemplateId})");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"❌ 地面物品名称着色失败: {ex.Message}");
                Plugin.Log.LogError($"   堆栈: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// 初始化反射字段（只执行一次，性能优化）
        /// </summary>
        private static void InitializeReflectionFields()
        {
            try
            {
                Plugin.Log.LogInfo("🔍 正在初始化地面物品着色反射字段...");

                // 1. 获取 ActionPanel._itemName 字段
                _itemNameField = AccessTools.Field(typeof(ActionPanel), "_itemName");
                if (_itemNameField == null)
                {
                    Plugin.Log.LogError("❌ 无法找到 ActionPanel._itemName 字段");
                    _isReflectionFailed = true;
                    return;
                }
                Plugin.Log.LogInfo("  ✅ _itemName 字段已缓存");

                // 2. 查找 ActionPanel.gamePlayerOwner_0 字段（混淆字段名）
                _gamePlayerOwnerField = FindFieldByType(typeof(ActionPanel), typeof(GamePlayerOwner));
                if (_gamePlayerOwnerField == null)
                {
                    Plugin.Log.LogError("❌ 无法找到 ActionPanel 的 GamePlayerOwner 字段");
                    _isReflectionFailed = true;
                    return;
                }
                Plugin.Log.LogInfo($"  ✅ GamePlayerOwner 字段已缓存: {_gamePlayerOwnerField.Name}");

                _isInitialized = true;
                Plugin.Log.LogInfo("✅ 地面物品着色反射字段初始化完成");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"❌ 反射字段初始化失败: {ex.Message}");
                _isReflectionFailed = true;
            }
        }

        /// <summary>
        /// 通过字段类型查找字段（处理混淆字段名）
        /// </summary>
        private static FieldInfo FindFieldByType(Type declaringType, Type fieldType)
        {
            var fields = declaringType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            foreach (var field in fields)
            {
                if (field.FieldType == fieldType)
                {
                    Plugin.Log.LogDebug($"  🔍 找到字段: {field.Name} ({fieldType.Name})");
                    return field;
                }
            }
            return null;
        }

        /// <summary>
        /// 从 Player.InteractableObject 获取当前 LootItem
        /// </summary>
        private static LootItem GetCurrentLootItem(ActionPanel actionPanel)
        {
            try
            {
                // 1. 获取 GamePlayerOwner
                var gamePlayerOwner = _gamePlayerOwnerField.GetValue(actionPanel) as GamePlayerOwner;
                if (gamePlayerOwner == null)
                {
                    Plugin.Log.LogDebug("🔍 GamePlayerOwner 为 null");
                    return null;
                }

                // 2. 获取 Player.InteractableObject
                var interactableObject = gamePlayerOwner.Player.InteractableObject;
                if (interactableObject == null)
                {
                    Plugin.Log.LogDebug("🔍 InteractableObject 为 null");
                    return null;
                }

                // 3. 检查是否是 LootItem
                if (interactableObject is LootItem lootItem)
                {
                    return lootItem;
                }
                else
                {
                    Plugin.Log.LogDebug($"🔍 InteractableObject 不是 LootItem，类型: {interactableObject.GetType().Name}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"⚠️ 获取地面物品失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 打印详细的物品信息（用于调试识别物品类型）
        /// </summary>
        private static void LogDetailedItemInfo(LootItem lootItem)
        {
            try
            {
                var item = lootItem.Item;
                if (item == null)
                {
                    Plugin.Log.LogWarning("⚠️ LootItem.Item 为 null");
                    return;
                }

                Plugin.Log.LogWarning("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                Plugin.Log.LogWarning($"🔍 [物品详细信息]");
                Plugin.Log.LogWarning($"   名称: {lootItem.Name}");
                Plugin.Log.LogWarning($"   TemplateId: {lootItem.TemplateId}");
                Plugin.Log.LogWarning($"   Item.TemplateId: {item.TemplateId}");

                // 打印类型信息
                var itemType = item.GetType();
                Plugin.Log.LogWarning($"   Item 类型: {itemType.FullName}");
                Plugin.Log.LogWarning($"   Item 简单类型名: {itemType.Name}");

                // 打印继承链
                Plugin.Log.LogWarning($"   继承链:");
                var baseType = itemType.BaseType;
                int depth = 1;
                while (baseType != null && depth < 5)
                {
                    Plugin.Log.LogWarning($"     {new string(' ', depth * 2)}↑ {baseType.Name}");
                    baseType = baseType.BaseType;
                    depth++;
                }

                // 打印实现的接口
                var interfaces = itemType.GetInterfaces();
                if (interfaces.Length > 0)
                {
                    Plugin.Log.LogWarning($"   实现的接口: {string.Join(", ", interfaces.Select(i => i.Name).Take(5))}");
                }

                // 检查是否是 AmmoItemClass
                if (item is AmmoItemClass ammo)
                {
                    Plugin.Log.LogWarning($"   ✅ 这是一个 AmmoItemClass (子弹)");
                    Plugin.Log.LogWarning($"      穿甲值: {ammo.PenetrationPower}");
                    Plugin.Log.LogWarning($"      口径: {ammo.Caliber}");
                    Plugin.Log.LogWarning($"      伤害: {ammo.Damage}");
                }

                // 检查是否是 AmmoBox
                if (item is AmmoBox ammoBox)
                {
                    Plugin.Log.LogWarning($"   ✅ 这是一个 AmmoBox (子弹盒/弹匣)");
                    Plugin.Log.LogWarning($"      Cartridges 是否为 null: {ammoBox.Cartridges == null}");

                    if (ammoBox.Cartridges != null)
                    {
                        Plugin.Log.LogWarning($"      Cartridges.Items 是否为 null: {ammoBox.Cartridges.Items == null}");

                        if (ammoBox.Cartridges.Items != null)
                        {
                            var cartridges = ammoBox.Cartridges.Items.ToList();
                            Plugin.Log.LogWarning($"      子弹数量: {cartridges.Count}");

                            if (cartridges.Count > 0)
                            {
                                var firstCartridge = cartridges[0];
                                Plugin.Log.LogWarning($"      第一颗子弹类型: {firstCartridge?.GetType().Name}");

                                if (firstCartridge is AmmoItemClass firstAmmo)
                                {
                                    Plugin.Log.LogWarning($"      第一颗子弹穿甲: {firstAmmo.PenetrationPower}");
                                    Plugin.Log.LogWarning($"      第一颗子弹口径: {firstAmmo.Caliber}");
                                    Plugin.Log.LogWarning($"      第一颗子弹伤害: {firstAmmo.Damage}");
                                }
                            }
                        }
                    }
                }

                // 打印所有公共属性（前20个）
                Plugin.Log.LogWarning($"   公共属性 (前20个):");
                var properties = itemType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                foreach (var prop in properties.Take(20))
                {
                    try
                    {
                        var value = prop.GetValue(item);
                        string valueStr = value?.ToString() ?? "null";
                        if (valueStr.Length > 50) valueStr = valueStr.Substring(0, 50) + "...";
                        Plugin.Log.LogWarning($"      {prop.Name} ({prop.PropertyType.Name}): {valueStr}");
                    }
                    catch
                    {
                        Plugin.Log.LogWarning($"      {prop.Name} ({prop.PropertyType.Name}): [无法获取]");
                    }
                }

                // 打印所有公共字段（前20个）
                Plugin.Log.LogWarning($"   公共字段 (前20个):");
                var fields = itemType.GetFields(BindingFlags.Public | BindingFlags.Instance);
                foreach (var field in fields.Take(20))
                {
                    try
                    {
                        var value = field.GetValue(item);
                        string valueStr = value?.ToString() ?? "null";
                        if (valueStr.Length > 50) valueStr = valueStr.Substring(0, 50) + "...";
                        Plugin.Log.LogWarning($"      {field.Name} ({field.FieldType.Name}): {valueStr}");
                    }
                    catch
                    {
                        Plugin.Log.LogWarning($"      {field.Name} ({field.FieldType.Name}): [无法获取]");
                    }
                }

                Plugin.Log.LogWarning("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"❌ 打印物品详细信息失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取着色后的物品名称（使用与背包一致的颜色逻辑和价格缓存）
        /// </summary>
        private static string GetColoredItemName(LootItem lootItem, string originalText)
        {
            try
            {
                // ===== 获取物品对象 =====
                var item = lootItem.Item;
                if (item == null)
                {
                    Plugin.Log.LogDebug("🔍 LootItem.Item 为 null");
                    return originalText;
                }

                // ===== 检查颜色缓存 =====
                string cacheKey = $"{lootItem.TemplateId}|{originalText}|{Settings.ShowGroundItemPrice.Value}";
                if (_colorCache.TryGetValue(cacheKey, out string cachedColoredText))
                {
                    return cachedColoredText;
                }

                // ===== 从价格缓存获取价格（不触发网络请求）=====
                var price = PriceDataService.Instance.GetPrice(lootItem.TemplateId);
                if (!price.HasValue)
                {
                    // 无价格数据，缓存原始文本
                    _colorCache[cacheKey] = originalText;
                    return originalText;
                }

                string coloredText = originalText;
                string priceInfo = "";

                // ===== 根据物品类型应用不同的颜色逻辑 =====

                // 1. 子弹：按穿甲等级着色
                if (Settings.UseCaliberPenetrationPower.Value && item is AmmoItemClass ammo && ammo.PenetrationPower > 0)
                {
                    coloredText = AmmoColorCoding.ApplyPenetrationColor(originalText, ammo.PenetrationPower);

                    // 价格信息（如果启用）
                    if (Settings.ShowGroundItemPrice.Value)
                    {
                        priceInfo = $" <color=#B0B0B0>({price.Value:N0}₽ | 穿甲{ammo.PenetrationPower})</color>";
                    }
                }
                // 2. 子弹盒/弹匣：显示详细信息
                else if (item is AmmoBox ammoBox)
                {
                    coloredText = GetAmmoBoxColoredText(ammoBox, originalText, price.Value, out priceInfo);
                }
                // 3. 护甲：按防弹等级着色
                else if (Settings.EnableArmorClassColoring.Value && ArmorHelper.IsArmor(item))
                {
                    var armorClass = ArmorHelper.GetArmorClass(item);
                    if (armorClass.HasValue && armorClass.Value > 0)
                    {
                        coloredText = ArmorColorCoding.ApplyArmorClassColor(originalText, armorClass.Value);

                        // 价格信息（如果启用）
                        if (Settings.ShowGroundItemPrice.Value)
                        {
                            priceInfo = $" <color=#B0B0B0>({price.Value:N0}₽ | {armorClass.Value}级)</color>";
                        }
                    }
                    else
                    {
                        // 无法获取护甲等级，按价格着色
                        int slots = item.Width * item.Height;
                        double pricePerSlot = slots > 0 ? price.Value / slots : price.Value;
                        coloredText = PriceColorCoding.ApplyColor(originalText, pricePerSlot);

                        if (Settings.ShowGroundItemPrice.Value)
                        {
                            priceInfo = $" <color=#B0B0B0>({price.Value:N0}₽)</color>";
                        }
                    }
                }
                // 4. 普通物品：按单格价值着色
                else
                {
                    int slots = item.Width * item.Height;
                    double pricePerSlot = slots > 0 ? price.Value / slots : price.Value;
                    coloredText = PriceColorCoding.ApplyColor(originalText, pricePerSlot);

                    // 价格信息（如果启用）
                    if (Settings.ShowGroundItemPrice.Value)
                    {
                        priceInfo = $" <color=#B0B0B0>({price.Value:N0}₽)</color>";
                    }
                }

                // ===== 组合最终文本 =====
                string finalText = coloredText + priceInfo;

                // ===== 缓存结果 =====
                _colorCache[cacheKey] = finalText;

                // ===== 定期清理缓存（避免内存泄漏）=====
                if (_colorCache.Count > 500)
                {
                    Plugin.Log.LogDebug($"⚠️ 颜色缓存过大 ({_colorCache.Count} 项)，清理一半");
                    ClearOldestCacheEntries(250);
                }

                return finalText;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"⚠️ 物品名称着色失败: {ex.Message}");
                return originalText;
            }
        }

        /// <summary>
        /// 获取子弹盒的着色文本和详细信息
        /// </summary>
        private static string GetAmmoBoxColoredText(AmmoBox ammoBox, string originalText, double totalPrice, out string priceInfo)
        {
            priceInfo = "";

            try
            {
                // 获取子弹盒内的子弹信息
                if (ammoBox.Cartridges?.Items != null && ammoBox.Cartridges.Items.Any())
                {
                    int ammoCount = ammoBox.Cartridges.Items.Count();
                    var firstAmmo = ammoBox.Cartridges.Items.FirstOrDefault() as AmmoItemClass;

                    if (firstAmmo != null && Settings.UseCaliberPenetrationPower.Value && firstAmmo.PenetrationPower > 0)
                    {
                        // 按穿甲等级着色
                        string coloredText = AmmoColorCoding.ApplyPenetrationColor(originalText, firstAmmo.PenetrationPower);

                        // 详细信息：穿甲、口径
                        if (Settings.ShowGroundItemPrice.Value)
                        {
                            string caliber = firstAmmo.Caliber ?? "未知口径";
                            priceInfo = $" <color=#B0B0B0>({totalPrice:N0}₽ | 穿甲{firstAmmo.PenetrationPower} | {caliber})</color>";
                        }

                        return coloredText;
                    }
                }

                // 无法获取子弹信息，按价格着色
                var boxItem = ammoBox as Item;
                int slots = boxItem.Width * boxItem.Height;
                double pricePerSlot = slots > 0 ? totalPrice / slots : totalPrice;

                if (Settings.ShowGroundItemPrice.Value)
                {
                    priceInfo = $" <color=#B0B0B0>({totalPrice:N0}₽)</color>";
                }

                return PriceColorCoding.ApplyColor(originalText, pricePerSlot);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"⚠️ 子弹盒信息获取失败: {ex.Message}");

                // 降级处理：按价格着色
                var boxItem = ammoBox as Item;
                int slots = boxItem.Width * boxItem.Height;
                double pricePerSlot = slots > 0 ? totalPrice / slots : totalPrice;

                if (Settings.ShowGroundItemPrice.Value)
                {
                    priceInfo = $" <color=#B0B0B0>({totalPrice:N0}₽)</color>";
                }

                return PriceColorCoding.ApplyColor(originalText, pricePerSlot);
            }
        }

        /// <summary>
        /// 清理最早的缓存条目（简单 FIFO 策略）
        /// </summary>
        private static void ClearOldestCacheEntries(int countToRemove)
        {
            try
            {
                var keys = new List<string>(_colorCache.Keys);
                for (int i = 0; i < countToRemove && i < keys.Count; i++)
                {
                    _colorCache.Remove(keys[i]);
                }
                Plugin.Log.LogDebug($"✅ 已清理 {countToRemove} 个颜色缓存条目");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"⚠️ 清理缓存失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 清空颜色缓存（当价格数据更新时调用）
        /// </summary>
        public static void ClearColorCache()
        {
            _colorCache.Clear();
            Plugin.Log.LogInfo("✅ 地面物品颜色缓存已清空");
        }
    }
}
