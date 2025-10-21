using System;
using System.Linq;
using System.Reflection;
using EFT.InventoryLogic;

namespace QuickPrice.Utils
{
    /// <summary>
    /// 护甲辅助工具 - 获取护甲的实际防弹等级
    /// 支持检测内部的防弹插板和内置防弹内衬
    /// </summary>
    public static class ArmorHelper
    {
        /// <summary>
        /// 获取护甲的实际防弹等级
        /// </summary>
        /// <param name="item">护甲物品</param>
        /// <returns>防弹等级，如果无法获取则返回 null</returns>
        public static int? GetArmorClass(Item item)
        {
            if (item == null)
                return null;

            try
            {
                // 1. 先尝试获取护甲本身的防弹等级
                var armorClass = GetArmorClassFromTemplate(item);

                // 2. 如果护甲本身等级为 0，检查内部的防弹插板
                if (!armorClass.HasValue || armorClass.Value == 0)
                {
                    var plateArmorClass = GetArmorClassFromPlates(item);
                    if (plateArmorClass.HasValue && plateArmorClass.Value > 0)
                    {
                        return plateArmorClass.Value;
                    }
                }

                return armorClass;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 从 Template 获取 ArmorClass
        /// </summary>
        private static int? GetArmorClassFromTemplate(Item item)
        {
            try
            {
                var templateProp = item.GetType().GetProperty("Template");
                if (templateProp != null)
                {
                    var template = templateProp.GetValue(item);
                    if (template != null)
                    {
                        var templateType = template.GetType();

                        // 尝试直接获取 ArmorClass 属性
                        var armorClassProp = templateType.GetProperty("ArmorClass");
                        if (armorClassProp != null)
                        {
                            var ac = armorClassProp.GetValue(template);
                            if (ac is int armorClass)
                                return armorClass;
                        }

                        // 如果直接获取失败，尝试通过接口获取（GInterface383）
                        var interfaces = templateType.GetInterfaces();
                        foreach (var iface in interfaces)
                        {
                            armorClassProp = iface.GetProperty("ArmorClass");
                            if (armorClassProp != null)
                            {
                                var ac = armorClassProp.GetValue(template);
                                if (ac is int armorClass)
                                    return armorClass;
                            }
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// 从防弹插板获取最高防弹等级
        /// 支持检测 ArmorPlateItemClass（可拆卸插板）和 BuiltInInsertsItemClass（内置防弹内衬）
        /// </summary>
        private static int? GetArmorClassFromPlates(Item item)
        {
            try
            {
                var itemType = item.GetType();

                // 获取 AllSlots 属性
                var allSlotsProp = itemType.GetProperty("AllSlots");
                if (allSlotsProp == null)
                    return null;

                var allSlots = allSlotsProp.GetValue(item) as System.Collections.IEnumerable;
                if (allSlots == null)
                    return null;

                int maxArmorClass = 0;

                foreach (var slot in allSlots)
                {
                    if (slot == null)
                        continue;

                    // 获取插槽中的物品
                    var containedItemProp = slot.GetType().GetProperty("ContainedItem");
                    if (containedItemProp == null)
                        continue;

                    var containedItem = containedItemProp.GetValue(slot);
                    if (containedItem == null)
                        continue;

                    // 检查是否是防弹插板（ArmorPlateItemClass）或内置防弹内衬（BuiltInInsertsItemClass）
                    if (containedItem is ArmorPlateItemClass ||
                        containedItem.GetType().Name == "BuiltInInsertsItemClass")
                    {
                        var plateArmorClass = GetArmorClassFromTemplate(containedItem as Item);
                        if (plateArmorClass.HasValue && plateArmorClass.Value > maxArmorClass)
                        {
                            maxArmorClass = plateArmorClass.Value;
                        }
                    }
                }

                return maxArmorClass > 0 ? maxArmorClass : (int?)null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 检查物品是否是护甲类型
        /// </summary>
        public static bool IsArmor(Item item)
        {
            if (item == null)
                return false;

            var typeName = item.GetType().Name;

            // 检查是否是护甲相关类型
            // 包括：护甲本体、战术背心、头盔（HeadwearItemClass）及各种变体
            return typeName == "ArmorItemClass" ||
                   typeName == "VestItemClass" ||
                   typeName == "ArmorVestItemClass" ||
                   typeName == "HeadwearItemClass" ||
                   typeName.Contains("Helmet") ||
                   typeName.Contains("helmet") ||
                   typeName.Contains("Headwear") ||
                   typeName.Contains("headwear") ||
                   typeName.Contains("Armor") ||
                   typeName.Contains("armor") ||
                   typeName.Contains("Vest") ||
                   typeName.Contains("vest");
        }
    }
}
