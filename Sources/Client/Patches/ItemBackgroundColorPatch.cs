using System;
using System.Reflection;
using System.Linq;
using EFT.InventoryLogic;
using JsonType;
using HarmonyLib;
using SPT.Reflection.Patching;
using QuickPrice.Config;
using QuickPrice.Services;
using QuickPrice.Utils;

namespace QuickPrice.Patches
{
    /// <summary>
    /// 物品背景色自动着色补丁
    /// 拦截 Item.BackgroundColor 属性的 getter，根据物品价格自动返回对应颜色
    /// 无需鼠标悬停，打开物品栏即可看到所有物品已着色
    ///
    /// 【着色规则】
    /// - 护甲：按防弹等级着色（1-6级：灰→绿→蓝→紫→橙→红）
    /// - 武器：不着色（保持默认）
    /// - 背包/容器：不着色（保持默认）
    /// - 弹匣：不着色（保持默认）
    /// - 子弹：按穿甲等级着色（白→绿→蓝→紫→橙→红）
    /// - 配件：按单格价值着色
    /// - 其他物品：按单格价值着色
    ///
    /// 【重要】内置自定义颜色支持：
    /// - 本补丁配合 CustomColorConverterPatch 使用，支持任意 RGB 颜色
    /// - 可以返回任意 RGB 颜色（如 #FF0000 亮红色），无需依赖外部插件
    /// - 如果禁用 CustomColorConverterPatch，则只能使用游戏预定义的 9 种颜色
    /// </summary>
    public class ItemBackgroundColorPatch : ModulePatch
    {
        // ===== 自定义颜色编码系统（原理来自 ColorConverterAPI）=====
        // 通过偏移量来编码自定义颜色，扩展 TaxonomyColor 枚举：
        // 自定义颜色值 = RGB颜色代码 + TaxonomyColor枚举数量
        // 例如：亮红色 #FF0000 = 0xFF0000 (16711680) + 9 = 16711689
        // CustomColorConverterPatch 会在运行时拦截并转换这些自定义颜色值
        private static readonly int ColorEnumCount = System.Enum.GetValues(typeof(TaxonomyColor)).Length;

        /// <summary>
        /// 将 RGB 颜色代码转换为 TaxonomyColor（配合 CustomColorConverterPatch 使用）
        /// </summary>
        /// <param name="hexColorCode">十六进制颜色代码（不带#号），例如 "FF0000" 表示红色</param>
        /// <returns>TaxonomyColor 枚举值</returns>
        private static TaxonomyColor CreateCustomColor(string hexColorCode)
        {
            // 将十六进制颜色代码转换为整数
            // 例如：" FF0000" → 16711680
            int colorValue = Convert.ToInt32(hexColorCode, 16);

            // 添加偏移量以避免与原始枚举值冲突
            // TaxonomyColor 有 9 个预定义值 (default, blue, green, orange, violet, red, yellow, black, grey)
            // 所以自定义颜色从 9 开始编号
            int customColorValue = colorValue + ColorEnumCount;

            // 将整数强制转换为 TaxonomyColor 枚举
            // CustomColorConverterPatch 会在运行时将这个值转换回实际的 Unity Color
            return (TaxonomyColor)customColorValue;
        }

        /// <summary>
        /// 根据单格价值获取对应的背景颜色（使用配置阈值）
        /// </summary>
        /// <param name="pricePerSlot">单格价值</param>
        /// <returns>对应的 TaxonomyColor</returns>
        private static TaxonomyColor GetBackgroundColorByPricePerSlot(double pricePerSlot)
        {
            if (pricePerSlot <= Settings.PriceThreshold1.Value)
                return CustomColors.LightGray;      // 浅灰色 - 最低价值

            if (pricePerSlot <= Settings.PriceThreshold2.Value)
                return CustomColors.LightGreen;     // 浅绿色 - 低价值

            if (pricePerSlot <= Settings.PriceThreshold3.Value)
                return CustomColors.LightBlue;      // 天蓝色 - 中等价值

            if (pricePerSlot <= Settings.PriceThreshold4.Value)
                return CustomColors.LightPurple;    // 兰花紫 - 较高价值

            if (pricePerSlot <= Settings.PriceThreshold5.Value)
                return CustomColors.LightOrange;    // 橙色 - 高价值

            return CustomColors.LightRed;           // 番茄红 - 最高价值
        }

        /// <summary>
        /// 根据穿甲值获取对应的背景颜色（使用配置阈值）
        /// </summary>
        /// <param name="penetration">穿甲值</param>
        /// <returns>对应的 TaxonomyColor</returns>
        private static TaxonomyColor GetBackgroundColorByPenetration(int penetration)
        {
            if (penetration < Settings.PenetrationThreshold1.Value)
                return CustomColors.Ammo_Level1;    // 浅灰色 - 1-2级穿甲

            if (penetration < Settings.PenetrationThreshold2.Value)
                return CustomColors.Ammo_Level3;    // 浅绿色 - 3级穿甲

            if (penetration < Settings.PenetrationThreshold3.Value)
                return CustomColors.Ammo_Level4;    // 天蓝色 - 4级穿甲

            if (penetration < Settings.PenetrationThreshold4.Value)
                return CustomColors.Ammo_Level5;    // 兰花紫 - 5级穿甲

            if (penetration < Settings.PenetrationThreshold5.Value)
                return CustomColors.Ammo_Level6;    // 橙色 - 6级穿甲

            return CustomColors.Ammo_Level7;        // 番茄红 - 7级+穿甲
        }

        /// <summary>
        /// 根据护甲等级获取对应的背景颜色
        /// </summary>
        /// <param name="armorClass">护甲等级（1-6）</param>
        /// <returns>对应的 TaxonomyColor</returns>
        private static TaxonomyColor GetBackgroundColorByArmorClass(int armorClass)
        {
            switch (armorClass)
            {
                case 1:
                    return CustomColors.Armor_Class1;    // 中灰色 - 1级护甲
                case 2:
                    return CustomColors.Armor_Class2;    // 森林绿 - 2级护甲
                case 3:
                    return CustomColors.Armor_Class3;    // 道奇蓝 - 3级护甲
                case 4:
                    return CustomColors.Armor_Class4;    // 兰花紫 - 4级护甲
                case 5:
                    return CustomColors.Armor_Class5;    // 鲜橙色 - 5级护甲
                case 6:
                    return CustomColors.Armor_Class6;    // 鲜红色 - 6级护甲
                default:
                    return CustomColors.LightGray;       // 未知等级，使用灰色
            }
        }

        /// <summary>
        /// 常用颜色定义（使用自定义颜色编码格式）
        /// 调整后的颜色方案：绿/蓝/灰更饱和更暗，红/橙更鲜艳
        /// </summary>
        private static class CustomColors
        {
            // ===== 价值等级颜色（6级，用于配件等普通物品）=====
            // 调整策略：
            // - 绿色：高饱和度，低明度 (#90EE90 → #32CD32 → #228B22 森林绿)
            // - 蓝色：提高饱和度，降低明度 (#87CEEB → #1E90FF 道奇蓝)
            // - 灰色：降低明度 (#D3D3D3 → #999999 中灰)
            // - 紫色：保持不变
            // - 橙色：更鲜艳 (#FFA500 → #FF9900)
            // - 红色：更鲜艳 (#FF6347 → #FF3333)

            public static TaxonomyColor LightGray => CreateCustomColor("999999");   // 中灰色 - 最低价值（降低明度）
            public static TaxonomyColor LightGreen => CreateCustomColor("228B22");  // 森林绿 - 低价值（高饱和度，低明度）
            public static TaxonomyColor LightBlue => CreateCustomColor("1E90FF");   // 道奇蓝 - 中等价值（高饱和度，中等明度）
            public static TaxonomyColor LightPurple => CreateCustomColor("DA70D6"); // 兰花紫 - 较高价值（保持不变）
            public static TaxonomyColor LightOrange => CreateCustomColor("FF9900"); // 鲜橙色 - 高价值（更鲜艳）
            public static TaxonomyColor LightRed => CreateCustomColor("FF3333");    // 鲜红色 - 最高价值（更鲜艳）

            // ===== 子弹穿甲等级颜色（6级）=====
            // 穿甲等级：1-2级（灰）、3级（绿）、4级（蓝）、5级（紫）、6级（橙）、7级+（红）

            public static TaxonomyColor Ammo_Level1 => LightGray;    // 1-2级穿甲 - 中灰
            public static TaxonomyColor Ammo_Level3 => LightGreen;   // 3级穿甲 - 柠檬绿
            public static TaxonomyColor Ammo_Level4 => LightBlue;    // 4级穿甲 - 道奇蓝
            public static TaxonomyColor Ammo_Level5 => LightPurple;  // 5级穿甲 - 兰花紫
            public static TaxonomyColor Ammo_Level6 => LightOrange;  // 6级穿甲 - 鲜橙色
            public static TaxonomyColor Ammo_Level7 => LightRed;     // 7级+穿甲 - 鲜红色

            // ===== 护甲等级颜色（6级）=====
            // 护甲等级：1级（灰）、2级（绿）、3级（蓝）、4级（紫）、5级（橙）、6级（红）

            public static TaxonomyColor Armor_Class1 => LightGray;    // 1级护甲 - 中灰色
            public static TaxonomyColor Armor_Class2 => LightGreen;   // 2级护甲 - 森林绿
            public static TaxonomyColor Armor_Class3 => LightBlue;    // 3级护甲 - 道奇蓝
            public static TaxonomyColor Armor_Class4 => LightPurple;  // 4级护甲 - 兰花紫
            public static TaxonomyColor Armor_Class5 => LightOrange;  // 5级护甲 - 鲜橙色
            public static TaxonomyColor Armor_Class6 => LightRed;     // 6级护甲 - 鲜红色
        }

        protected override MethodBase GetTargetMethod()
        {
            // 尝试获取 BackgroundColor 属性的 getter 方法
            var property = AccessTools.Property(typeof(Item), nameof(Item.BackgroundColor));

            if (property != null)
            {
                var getter = property.GetGetMethod();
                if (getter != null)
                {
                    Plugin.Log.LogInfo($"✅ 找到 Item.BackgroundColor 属性 getter 方法");
                    return getter;
                }
            }

            // 如果找不到属性，记录错误
            Plugin.Log.LogError($"❌ 无法找到 Item.BackgroundColor 属性！");
            Plugin.Log.LogError($"   这可能意味着 BackgroundColor 是字段而非属性");
            Plugin.Log.LogError($"   或者游戏版本不兼容");

            // 返回 null 会导致补丁不生效，但不会导致游戏崩溃
            return null;
        }

        [PatchPrefix]
        public static bool Prefix(Item __instance, ref TaxonomyColor __result)
        {
            try
            {
                // 检查是否启用背景着色功能
                if (!Settings.EnablePriceBasedBackgroundColor.Value)
                    return true; // 使用原始颜色

                // 检查物品是否为 null
                if (__instance == null)
                    return true;

                // ===== 按类型判断（注意顺序！）=====

                // 1. 武器 - 排除，不着色
                if (__instance is Weapon)
                    return true; // 使用原始颜色

                // 2. 护甲 - 按防弹等级着色（如果启用）
                // ⚠️ 必须在容器检测之前！因为防弹胸挂既是护甲又是容器
                if (Settings.EnableArmorClassColoring.Value && ArmorHelper.IsArmor(__instance))
                {
                    var armorClass = ArmorHelper.GetArmorClass(__instance);
                    if (armorClass.HasValue && armorClass.Value > 0)
                    {
                        __result = GetBackgroundColorByArmorClass(armorClass.Value);
                        return false;
                    }
                    // 如果护甲等级为0或无法获取，继续执行后续逻辑
                }

                // 2.5 防弹插板 - 按防弹等级着色（如果启用）
                if (Settings.EnableArmorClassColoring.Value && IsArmorPlate(__instance))
                {
                    var plateClass = ArmorHelper.GetArmorClass(__instance);
                    if (plateClass.HasValue && plateClass.Value > 0)
                    {
                        __result = GetBackgroundColorByArmorClass(plateClass.Value);
                        return false;
                    }
                }

                // 3. 背包/容器 - 排除，不着色
                // 检查是否为容器类型（背包、箱子等）
                // ⚠️ 必须在护甲检测之后！
                var itemType = __instance.GetType();
                var isContainerProperty = itemType.GetProperty("IsContainer");
                if (isContainerProperty != null)
                {
                    var isContainer = isContainerProperty.GetValue(__instance);
                    if (isContainer is bool boolValue && boolValue)
                        return true; // 使用原始颜色
                }

                // 4. 弹匣 - 排除，不着色（保持默认颜色）
                // 注意：MagazineItemClass 继承自 Mod，必须在检查 Mod 之前判断
                if (__instance is MagazineItemClass)
                    return true; // 使用原始颜色

                // 5. 子弹 - 按穿甲等级着色
                if (__instance is AmmoItemClass ammoItem)
                {
                    if (ammoItem.PenetrationPower > 0)
                    {
                        __result = GetBackgroundColorByPenetration(ammoItem.PenetrationPower);
                        return false;
                    }
                    return true; // 穿甲值为0，使用原始颜色
                }

                // 5.5 弹药包 - 按内部子弹的穿甲等级着色
                if (__instance is AmmoBox ammoBox)
                {
                    // 尝试获取第一颗子弹的穿甲值
                    if (Settings.UseCaliberPenetrationPower.Value && ammoBox.Cartridges?.Items != null)
                    {
                        var firstAmmo = ammoBox.Cartridges.Items.FirstOrDefault() as AmmoItemClass;
                        if (firstAmmo != null && firstAmmo.PenetrationPower > 0)
                        {
                            __result = GetBackgroundColorByPenetration(firstAmmo.PenetrationPower);
                            return false;
                        }
                    }

                    // 如果无法获取子弹信息，按价格着色
                    var ammoBoxPrice = PriceDataService.Instance.GetPrice(__instance.TemplateId);
                    if (ammoBoxPrice.HasValue)
                    {
                        int slots = __instance.Width * __instance.Height;
                        double pricePerSlot = slots > 0 ? ammoBoxPrice.Value / slots : ammoBoxPrice.Value;
                        __result = GetBackgroundColorByPricePerSlot(pricePerSlot);
                        return false;
                    }

                    return true; // 无价格数据，使用原始颜色
                }

                // 6. 配件 - 按单格价值着色
                // 注意：弹匣已在上面被排除
                if (__instance is Mod)
                {
                    var price = PriceDataService.Instance.GetPrice(__instance.TemplateId);
                    if (!price.HasValue)
                        return true; // 无价格数据，使用原始颜色

                    // 计算单格价值
                    int slots = __instance.Width * __instance.Height;
                    double pricePerSlot = slots > 0 ? price.Value / slots : price.Value;

                    __result = GetBackgroundColorByPricePerSlot(pricePerSlot);
                    return false;
                }

                // 7. 其他物品 - 按单格价值着色
                var itemPrice = PriceDataService.Instance.GetPrice(__instance.TemplateId);
                if (!itemPrice.HasValue)
                    return true; // 无价格数据，使用原始颜色

                // 计算单格价值
                int itemSlots = __instance.Width * __instance.Height;
                double itemPricePerSlot = itemSlots > 0 ? itemPrice.Value / itemSlots : itemPrice.Value;

                __result = GetBackgroundColorByPricePerSlot(itemPricePerSlot);
                return false;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"❌ 背景色计算失败: {ex.Message}");
                Plugin.Log.LogError($"   堆栈跟踪: {ex.StackTrace}");

                // 出错时使用原始颜色，避免影响游戏
                return true;
            }
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
    }
}
