using System;
using System.Reflection;
using System.Linq;
using EFT.InventoryLogic;

namespace QuickPrice.Patches
{
    /// <summary>
    /// 护甲类型探索工具 - 用于查找护甲类型和防弹级别属性
    /// 编译后在游戏中运行，查看日志输出
    /// </summary>
    public static class ArmorExplorer
    {
        private static System.Collections.Generic.HashSet<string> _exploredTypes =
            new System.Collections.Generic.HashSet<string>();

        /// <summary>
        /// 从物品获取 ArmorClass（辅助方法）
        /// </summary>
        private static int? GetArmorClassFromItem(Item item)
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

                        // 尝试直接获取
                        var armorClassProp = templateType.GetProperty("ArmorClass");
                        if (armorClassProp != null)
                        {
                            var ac = armorClassProp.GetValue(template);
                            if (ac is int armorClass)
                                return armorClass;
                        }

                        // 尝试通过接口获取
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
        /// 探索物品类型，找出护甲相关的类型和属性
        /// </summary>
        public static void ExploreArmorTypes(Item item)
        {
            if (item == null)
                return;

            var itemType = item.GetType();
            var typeName = itemType.Name;

            // 避免重复输出相同类型
            if (_exploredTypes.Contains(typeName))
                return;

            // 查找所有可能的护甲类型（排除子弹和手榴弹）
            bool isArmor = (typeName.Contains("Armor") ||
                          typeName.Contains("armor") ||
                          typeName.Contains("Vest") ||
                          typeName.Contains("Helmet") ||
                          typeName.Contains("helmet") ||
                          typeName.Contains("Rig")) &&
                          !typeName.Contains("Ammo") &&
                          !typeName.Contains("Throw");

            // 或者直接检查 Template 是否有 ArmorClass 属性（不管值是多少）
            bool hasArmorClass = false;
            try
            {
                var tempProp = itemType.GetProperty("Template");
                if (tempProp != null)
                {
                    var temp = tempProp.GetValue(item);
                    if (temp != null)
                    {
                        var tempType = temp.GetType();
                        var armorClassProp = tempType.GetProperty("ArmorClass");
                        if (armorClassProp != null)
                        {
                            hasArmorClass = true; // 只要有这个属性就算，不管值是多少
                        }
                    }
                }
            }
            catch { }

            if (isArmor || hasArmorClass)
            {
                _exploredTypes.Add(typeName);

                // 获取所有属性
                var properties = itemType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

                // 🎯 首先尝试获取 ArmorClass 值
                int? armorClassValue = null;
                try
                {
                    var templateProp = itemType.GetProperty("Template");
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
                                var acValue = armorClassProp.GetValue(template);
                                if (acValue is int ac)
                                {
                                    armorClassValue = ac;
                                }
                            }

                            // 如果直接获取失败，尝试通过接口获取
                            if (!armorClassValue.HasValue)
                            {
                                var interfaces = templateType.GetInterfaces();
                                foreach (var iface in interfaces)
                                {
                                    // 查找包含 ArmorClass 的接口（通常是 GInterface383）
                                    var ifaceArmorClassProp = iface.GetProperty("ArmorClass");
                                    if (ifaceArmorClassProp != null)
                                    {
                                        var ifaceAcValue = ifaceArmorClassProp.GetValue(template);
                                        if (ifaceAcValue is int ifaceAc)
                                        {
                                            armorClassValue = ifaceAc;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // 🔍 检查护甲内部的防弹插板
                    // 如果护甲本身防弹等级为0，检查是否有插板
                    if (!armorClassValue.HasValue || armorClassValue.Value == 0)
                    {
                        Plugin.Log.LogInfo($"   🔍 护甲本身等级为0，开始检查插板...");

                        // 尝试多个可能的属性名
                        string[] possibleSlotNames = { "AllSlots", "AllSlotsWithoutArrayInside", "Slots", "Mods", "Grids", "Container", "PlateSlots" };
                        System.Reflection.PropertyInfo slotsProp = null;
                        string foundPropName = null;

                        foreach (var propName in possibleSlotNames)
                        {
                            slotsProp = itemType.GetProperty(propName);
                            if (slotsProp != null)
                            {
                                foundPropName = propName;
                                Plugin.Log.LogInfo($"   ✅ 找到 {propName} 属性");
                                break;
                            }
                        }

                        if (slotsProp != null)
                        {
                            var slots = slotsProp.GetValue(item) as System.Collections.IEnumerable;
                            if (slots != null)
                            {
                                var slotsList = slots.Cast<object>().ToList();
                                Plugin.Log.LogInfo($"   📦 {foundPropName} 数量: {slotsList.Count}");

                                int maxPlateArmorClass = 0;
                                var platesList = new System.Collections.Generic.List<string>();

                                foreach (var slot in slotsList)
                                {
                                    if (slot == null)
                                        continue;

                                    Plugin.Log.LogInfo($"   🔍 检查项: {slot.GetType().Name}");

                                    // 尝试直接检查是否是插板
                                    if (slot is ArmorPlateItemClass directPlate)
                                    {
                                        Plugin.Log.LogInfo($"      ✅ 直接是防弹插板！");
                                        var plateAc = GetArmorClassFromItem(directPlate);
                                        if (plateAc.HasValue)
                                        {
                                            Plugin.Log.LogInfo($"      🎯 插板防弹等级: {plateAc.Value}");
                                            if (plateAc.Value > maxPlateArmorClass)
                                            {
                                                maxPlateArmorClass = plateAc.Value;
                                            }
                                            platesList.Add($"{directPlate.LocalizedName()} (级别{plateAc.Value})");
                                        }
                                        continue;
                                    }

                                    // 获取 ContainedItem
                                    var containedItemProp = slot.GetType().GetProperty("ContainedItem");
                                    if (containedItemProp != null)
                                    {
                                        var containedItem = containedItemProp.GetValue(slot);
                                        if (containedItem != null)
                                        {
                                            Plugin.Log.LogInfo($"      📦 插槽内物品: {containedItem.GetType().Name}");

                                            if (containedItem is ArmorPlateItemClass plate)
                                            {
                                                Plugin.Log.LogInfo($"      ✅ 是防弹插板！");
                                                var plateAc = GetArmorClassFromItem(plate);
                                                if (plateAc.HasValue)
                                                {
                                                    Plugin.Log.LogInfo($"      🎯 插板防弹等级: {plateAc.Value}");
                                                    if (plateAc.Value > maxPlateArmorClass)
                                                    {
                                                        maxPlateArmorClass = plateAc.Value;
                                                    }
                                                    platesList.Add($"{plate.LocalizedName()} (级别{plateAc.Value})");
                                                }
                                            }
                                            else
                                            {
                                                Plugin.Log.LogInfo($"      ⚠️ 不是防弹插板，是: {containedItem.GetType().Name}");
                                            }
                                        }
                                        else
                                        {
                                            Plugin.Log.LogInfo($"      ⚠️ 插槽为空");
                                        }
                                    }
                                    else
                                    {
                                        Plugin.Log.LogInfo($"      ⚠️ 没有 ContainedItem 属性");
                                    }
                                }

                                if (maxPlateArmorClass > 0)
                                {
                                    armorClassValue = maxPlateArmorClass;
                                    Plugin.Log.LogInfo($"   ✅ 检测到防弹插板:");
                                    foreach (var plateInfo in platesList)
                                    {
                                        Plugin.Log.LogInfo($"      - {plateInfo}");
                                    }
                                    Plugin.Log.LogInfo($"   📊 实际防弹等级 = 最高插板等级: {maxPlateArmorClass}");
                                }
                                else
                                {
                                    Plugin.Log.LogInfo($"   ⚠️ 未找到任何防弹插板");
                                }
                            }
                            else
                            {
                                Plugin.Log.LogInfo($"   ⚠️ {foundPropName} 为 null");
                            }
                        }
                        else
                        {
                            Plugin.Log.LogInfo($"   ⚠️ 没有找到插槽属性（尝试了: {string.Join(", ", possibleSlotNames)}）");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"   ⚠️ 获取防弹等级时出错: {ex.Message}");
                }

                Plugin.Log.LogInfo($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                Plugin.Log.LogInfo($"🛡️  发现护甲类型: {typeName}");
                if (armorClassValue.HasValue)
                {
                    Plugin.Log.LogInfo($"   🎯🎯🎯 防弹等级: {armorClassValue.Value} 级");
                }
                else
                {
                    Plugin.Log.LogInfo($"   ⚠️ 无防弹等级（战术背心？）");
                }
                Plugin.Log.LogInfo($"   完整类型: {itemType.FullName}");
                Plugin.Log.LogInfo($"   物品名称: {item.LocalizedName()}");
                Plugin.Log.LogInfo($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

                // 列出所有属性
                Plugin.Log.LogInfo($"📋 属性列表 ({properties.Length} 个):");

                // 先列出所有属性名称，帮助我们找到插板相关的属性
                var allPropNames = new System.Collections.Generic.List<string>();
                foreach (var prop in properties)
                {
                    allPropNames.Add($"{prop.Name} ({prop.PropertyType.Name})");
                }
                Plugin.Log.LogInfo($"   所有属性: {string.Join(", ", allPropNames)}");

                foreach (var prop in properties)
                {
                    try
                    {
                        var value = prop.GetValue(item);
                        var valueStr = value?.ToString() ?? "null";

                        // 重点关注可能是防弹等级的属性
                        if (prop.Name.Contains("Class") ||
                            prop.Name.Contains("Level") ||
                            prop.Name.Contains("Armor") ||
                            prop.Name.Contains("Protection") ||
                            prop.Name.Contains("Durability") ||
                            prop.Name.Contains("Material"))
                        {
                            Plugin.Log.LogInfo($"   ⭐ {prop.Name} ({prop.PropertyType.Name}): {valueStr}");
                        }
                    }
                    catch
                    {
                        // 跳过无法读取的属性
                    }
                }

                // 尝试查找 Template 属性
                var templateProp2 = itemType.GetProperty("Template");
                if (templateProp2 != null)
                {
                    try
                    {
                        var template2 = templateProp2.GetValue(item);
                        if (template2 != null)
                        {
                            Plugin.Log.LogInfo($"📄 Template 类型: {template2.GetType().FullName}");

                            var templateProps = template2.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                            Plugin.Log.LogInfo($"📋 Template 属性:");

                            foreach (var tprop in templateProps)
                            {
                                try
                                {
                                    var tvalue = tprop.GetValue(template2);
                                    var tvalueStr = tvalue?.ToString() ?? "null";

                                    // 🎯 重点查找 ArmorClass
                                    if (tprop.Name == "ArmorClass")
                                    {
                                        Plugin.Log.LogInfo($"   🎯🎯🎯 找到了！ Template.{tprop.Name} ({tprop.PropertyType.Name}): {tvalueStr}");
                                    }
                                    else if (tprop.Name.Contains("Class") ||
                                        tprop.Name.Contains("Level") ||
                                        tprop.Name.Contains("Armor") ||
                                        tprop.Name.Contains("Protection") ||
                                        tprop.Name.Contains("Durability"))
                                    {
                                        Plugin.Log.LogInfo($"   ⭐⭐ Template.{tprop.Name} ({tprop.PropertyType.Name}): {tvalueStr}");
                                    }
                                }
                                catch { }
                            }

                            // 🔍 深度探索：查找 Template 实现的接口
                            var templateType2 = template2.GetType();
                            var interfaces = templateType2.GetInterfaces();

                            Plugin.Log.LogInfo($"🔍 Template 实现的接口 ({interfaces.Length} 个):");
                            foreach (var iface in interfaces)
                            {
                                var ifaceName = iface.Name;

                                // 查找护甲相关的接口
                                if (ifaceName.Contains("Armor") || ifaceName.Contains("Protection"))
                                {
                                    Plugin.Log.LogInfo($"   🛡️  接口: {ifaceName}");

                                    // 获取接口的属性
                                    var ifaceProps = iface.GetProperties();
                                    foreach (var iprop in ifaceProps)
                                    {
                                        try
                                        {
                                            var ivalue = iprop.GetValue(template2);
                                            var ivalueStr = ivalue?.ToString() ?? "null";

                                            if (iprop.Name == "ArmorClass")
                                            {
                                                Plugin.Log.LogInfo($"      🎯🎯🎯 找到了！ {ifaceName}.{iprop.Name} ({iprop.PropertyType.Name}): {ivalueStr}");
                                            }
                                            else
                                            {
                                                Plugin.Log.LogInfo($"      ⭐ {ifaceName}.{iprop.Name} ({iprop.PropertyType.Name}): {ivalueStr}");
                                            }
                                        }
                                        catch { }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.LogInfo($"   ⚠️ 无法读取 Template: {ex.Message}");
                    }
                }

                Plugin.Log.LogInfo($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            }
        }

        /// <summary>
        /// 列出物品的所有基类
        /// </summary>
        public static void ListBaseTypes(Item item)
        {
            if (item == null)
                return;

            var itemTypeName = item.GetType().Name;
            if (_exploredTypes.Contains(itemTypeName + "_base"))
                return;

            _exploredTypes.Add(itemTypeName + "_base");

            Plugin.Log.LogInfo($"📦 物品继承链: {item.LocalizedName()}");

            var currentType = item.GetType();
            int depth = 0;

            while (currentType != null && depth < 10)
            {
                string indent = new string(' ', depth * 2);
                Plugin.Log.LogInfo($"{indent}└─ {currentType.Name}");
                currentType = currentType.BaseType;
                depth++;
            }
        }
    }
}
