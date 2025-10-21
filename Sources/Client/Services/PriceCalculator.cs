using EFT.InventoryLogic;
using QuickPrice.Config;
using QuickPrice.Models;
using System.Linq;

namespace QuickPrice.Services
{
    /// <summary>
    /// 配件信息 - 用于层级显示
    /// </summary>
    public class ModInfo
    {
        public string Name;      // 配件名称
        public double Price;     // 配件价格
        public int Depth;        // 层级深度（0=根配件）
    }

    /// <summary>
    /// 价格计算器 - 计算物品价格信息
    /// </summary>
    public static class PriceCalculator
    {
        /// <summary>
        /// 获取物品的价格信息
        /// </summary>
        public static PriceInfo GetPriceInfo(Item item)
        {
            if (item == null)
                return null;

            // 获取跳蚤市场价格
            var fleaPrice = PriceDataService.Instance.GetPrice(item.TemplateId);
            if (!fleaPrice.HasValue)
                return null;

            // 计算单格价格
            int slots = GetItemSlots(item);
            double pricePerSlot = slots > 0 ? fleaPrice.Value / slots : fleaPrice.Value;

            return new PriceInfo
            {
                SourceName = "跳蚤市场",
                TotalPrice = fleaPrice.Value,
                PricePerSlot = pricePerSlot,
                IsBestPrice = true
            };
        }

        /// <summary>
        /// 计算武器所有配件的总价（递归）
        /// </summary>
        public static double CalculateWeaponModsPrice(Weapon weapon)
        {
            if (weapon == null)
                return 0;

            double totalModsPrice = 0;

            try
            {
                // 创建循环检测集合
                var visitedMods = new System.Collections.Generic.HashSet<string>();

                // 递归计算所有配件价格（带保护）
                totalModsPrice = CalculateModsPrice(weapon.Mods, visitedMods, 0);

                Plugin.Log.LogDebug($"武器 {weapon.LocalizedName()} 配件总价: {totalModsPrice:N0}₽");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"计算配件价格失败: {ex.Message}");
            }

            return totalModsPrice;
        }

        /// <summary>
        /// 收集武器所有配件的详细信息（用于层级显示）
        /// </summary>
        public static System.Collections.Generic.List<ModInfo> CollectWeaponModsInfo(Weapon weapon)
        {
            var modInfoList = new System.Collections.Generic.List<ModInfo>();

            if (weapon == null)
                return modInfoList;

            try
            {
                // 创建循环检测集合
                var visitedMods = new System.Collections.Generic.HashSet<string>();

                // 递归收集所有配件信息（带保护）
                CollectModsInfo(weapon.Mods, visitedMods, 0, modInfoList);

                Plugin.Log.LogDebug($"武器 {weapon.LocalizedName()} 收集到 {modInfoList.Count} 个配件");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"收集配件信息失败: {ex.Message}");
            }

            return modInfoList;
        }

        /// <summary>
        /// 递归计算配件价格（带循环检测和深度限制）
        /// </summary>
        /// <param name="mods">配件列表</param>
        /// <param name="visitedMods">已访问的配件ID集合（防止循环引用）</param>
        /// <param name="depth">当前递归深度（防止栈溢出）</param>
        /// <returns>配件总价</returns>
        public static double CalculateModsPrice(
            System.Collections.Generic.IEnumerable<Mod> mods,
            System.Collections.Generic.HashSet<string> visitedMods,
            int depth)
        {
            if (mods == null)
                return 0;

            // 防止栈溢出：最大递归深度 100 层
            if (depth >= 100)
            {
                Plugin.Log.LogWarning($"⚠️ 配件递归深度达到限制 (100层)，停止计算以防止栈溢出");
                return 0;
            }

            double total = 0;

            foreach (var mod in mods)
            {
                if (mod == null)
                    continue;

                // 防止循环引用：检查是否已访问过此配件
                if (visitedMods.Contains(mod.Id))
                {
                    Plugin.Log.LogWarning($"⚠️ 检测到循环引用: {mod.LocalizedName()} (ID: {mod.Id})");
                    continue;
                }

                // 标记为已访问
                visitedMods.Add(mod.Id);

                // 获取配件本身的价格
                var modPrice = PriceDataService.Instance.GetPrice(mod.TemplateId);
                if (modPrice.HasValue)
                {
                    total += modPrice.Value;
                    Plugin.Log.LogDebug($"  {new string(' ', depth * 2)}配件: {mod.LocalizedName()} = {modPrice.Value:N0}₽ (深度:{depth})");
                }

                // 递归计算配件上的配件
                if (mod.Slots != null && mod.Slots.Any())
                {
                    foreach (var slot in mod.Slots)
                    {
                        if (slot.ContainedItem != null && slot.ContainedItem is Mod childMod)
                        {
                            // 递归调用（深度 +1）
                            var childMods = new[] { childMod };
                            total += CalculateModsPrice(childMods, visitedMods, depth + 1);
                        }
                    }
                }
            }

            return total;
        }

        /// <summary>
        /// 递归收集配件信息（带循环检测和深度限制）
        /// </summary>
        /// <param name="mods">配件列表</param>
        /// <param name="visitedMods">已访问的配件ID集合（防止循环引用）</param>
        /// <param name="depth">当前递归深度（防止栈溢出）</param>
        /// <param name="modInfoList">配件信息列表（输出）</param>
        public static void CollectModsInfo(
            System.Collections.Generic.IEnumerable<Mod> mods,
            System.Collections.Generic.HashSet<string> visitedMods,
            int depth,
            System.Collections.Generic.List<ModInfo> modInfoList)
        {
            if (mods == null)
                return;

            // 防止栈溢出：最大递归深度 100 层
            if (depth >= 100)
            {
                Plugin.Log.LogWarning($"⚠️ 配件递归深度达到限制 (100层)，停止收集以防止栈溢出");
                return;
            }

            foreach (var mod in mods)
            {
                if (mod == null)
                    continue;

                // 防止循环引用：检查是否已访问过此配件
                if (visitedMods.Contains(mod.Id))
                {
                    Plugin.Log.LogWarning($"⚠️ 检测到循环引用: {mod.LocalizedName()} (ID: {mod.Id})");
                    continue;
                }

                // 标记为已访问
                visitedMods.Add(mod.Id);

                // 获取配件本身的价格
                var modPrice = PriceDataService.Instance.GetPrice(mod.TemplateId);
                if (modPrice.HasValue)
                {
                    // 添加到列表
                    modInfoList.Add(new ModInfo
                    {
                        Name = mod.LocalizedName(),
                        Price = modPrice.Value,
                        Depth = depth
                    });

                    Plugin.Log.LogDebug($"  {new string(' ', depth * 2)}收集配件: {mod.LocalizedName()} = {modPrice.Value:N0}₽ (深度:{depth})");
                }

                // 递归收集配件上的配件
                if (mod.Slots != null && mod.Slots.Any())
                {
                    foreach (var slot in mod.Slots)
                    {
                        if (slot.ContainedItem != null && slot.ContainedItem is Mod childMod)
                        {
                            // 递归调用（深度 +1）
                            var childMods = new[] { childMod };
                            CollectModsInfo(childMods, visitedMods, depth + 1, modInfoList);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 获取物品占用的格子数
        /// </summary>
        private static int GetItemSlots(Item item)
        {
            try
            {
                return item.Width * item.Height;
            }
            catch
            {
                return 1;
            }
        }
    }
}
