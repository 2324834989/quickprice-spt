using System;
using System.Threading.Tasks;
using Comfort.Common;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using QuickPrice.Services;
using UnityEngine;
using QuickPrice.Config;
using QuickPrice.Utils;

namespace QuickPrice.Patches
{
    public static class SearchPatch
    {
        /// <summary>
        /// 搜索音效补丁 - 根据物品价格等级播放不同音效
        /// </summary>
        [HarmonyPatch(typeof(GClass3517), "PlayDiscoverSound")]
        public static class SearchSoundPatch
        {
            [HarmonyPrefix]
            public static bool Prefix(GClass3517 __instance, Item item)
            {
                try
                {
                    PlaySound(item);
                    return false; // 跳过原始方法
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"搜索音效播放失败: {ex.Message}");
                    return true; // 出错时使用原始方法
                }
            }

            /// <summary>
            /// 第二个搜索音效补丁 - 确保所有搜索路径都被覆盖
            /// </summary>
            [HarmonyPatch(typeof(SearchContentOperationResultClass), "smethod_5")]
            public static class SearchSoundPatch2
            {
                [HarmonyPrefix]
                public static bool Prefix(SearchContentOperationResultClass __instance, Item item)
                {
                    try
                    {
                        SearchSoundPatch.PlaySound(item);
                        return false; // 跳过原始方法
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.LogError($"搜索音效播放失败: {ex.Message}");
                        return true; // 出错时使用原始方法
                    }
                }
            }

            /// <summary>
            /// 根据价格等级获取对应的音效类型
            /// </summary>
            public static EUISoundType GetSoundTypeByPriceLevel(int priceLevel)
            {
                return priceLevel switch
                {
                    6 => EUISoundType.AchievementCompleted,  // 最高价值 - 成就完成音效
                    5 => EUISoundType.InsuranceInsured,      // 高价值 - 保险音效
                    4 => EUISoundType.MenuInspectorWindowClose, // 较高价值 - 菜单关闭音效
                    3 => EUISoundType.ButtonClick,           // 中等价值 - 按钮点击音效
                    2 => EUISoundType.ButtonOver,            // 低价值 - 按钮悬停音效
                    _ => EUISoundType.ButtonOver             // 最低价值 - 按钮悬停音效
                };
            }

            /// <summary>
            /// 播放搜索音效
            /// </summary>
            public static void PlaySound(Item item)
            {
                if (item == null) return;

                // 获取物品价格等级
                int priceLevel = GetItemPriceLevel(item);

                // 播放对应音效
                var soundType = GetSoundTypeByPriceLevel(priceLevel);
                Singleton<GUISounds>.Instance.PlayUISound(soundType);
            }

            /// <summary>
            /// 根据物品价格计算等级（1-6）
            /// </summary>
            public static int GetItemPriceLevel(Item item)
            {
                var price = PriceDataService.Instance.GetPrice(item.TemplateId);
                if (!price.HasValue) return 1;

                // 计算单格价值
                int slots = item.Width * item.Height;
                double pricePerSlot = slots > 0 ? price.Value / slots : price.Value;

                // 使用与背景色相同的等级划分逻辑
                if (pricePerSlot <= Settings.PriceThreshold1.Value) return 1;
                if (pricePerSlot <= Settings.PriceThreshold2.Value) return 2;
                if (pricePerSlot <= Settings.PriceThreshold3.Value) return 3;
                if (pricePerSlot <= Settings.PriceThreshold4.Value) return 4;
                if (pricePerSlot <= Settings.PriceThreshold5.Value) return 5;
                return 6;
            }
        }

        /// <summary>
        /// 主搜索执行补丁 - 在 method_6 中引入自定义延迟
        /// </summary>
        [HarmonyPatch(typeof(GClass3515), "method_6")]
        public static class MainSearchExecutionPatch
        {
            private static bool IsProcessing = false;

            [HarmonyPrefix]
            public static bool Prefix(ref Task __result, GClass3515 __instance)
            {
                if (IsProcessing)
                    return true;

                try
                {
                    IsProcessing = true;
                    __result = ExecuteCustomSearch(__instance);
                    return false;
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"搜索执行失败: {ex.Message}");
                    IsProcessing = false;
                    return true;
                }
            }

            /// <summary>
            /// 执行自定义搜索逻辑 - 为每个物品单独计算搜索时间
            /// </summary>
            private static async Task ExecuteCustomSearch(GClass3515 __instance)
            {
                try
                {
                    if (!__instance.IplayerSearchController_0.ContainsUnknownItems(__instance.Item))
                        return;

                    // 获取技能系数（与原代码相同的计算方式）
                    bool isEquipment = __instance.Item.Parent.GetOwner().RootItem is InventoryEquipment;
                    IInventoryProfileSkillInfo skillsInfo = __instance.Profile_0.SkillsInfo;
                    float skillFactor = isEquipment ?
                        (1f + skillsInfo.AttentionLootSpeedValue + skillsInfo.SearchBuffSpeedValue) :
                        (1f + skillsInfo.AttentionLootSpeedValue);

                    Item unknownItem;
                    while (GetUnknownItem(__instance, out unknownItem))
                    {
                        // 为当前未知物品计算价格等级和搜索时间
                        int priceLevel = GetItemPriceLevel(unknownItem); // 注意：这里使用unknownItem而不是__instance.Item
                        float baseSearchTime = CalculateSearchTime(priceLevel);
                        float actualSearchTime = baseSearchTime / skillFactor;

                        // 使用固定延迟，移除随机因素
                        try
                        {
                            await Task.Delay((int)(actualSearchTime * 1000f), __instance.CancellationTokenSource_0.Token);
                        }
                        catch (TaskCanceledException)
                        {
                            return;
                        }

                        if (__instance.Boolean_0)
                            return;

                        // 再次检查并发现物品
                        Item currentItem;
                        if (GetUnknownItem(__instance, out currentItem))
                        {
                            var discoverMethod = AccessTools.Method(typeof(GClass3515), "DiscoverItem");
                            discoverMethod?.Invoke(__instance, new object[] { currentItem });
                        }
                    }

                    __instance.IplayerSearchController_0.OnItemFullySearched();
                }
                finally
                {
                    IsProcessing = false;
                }
            }


            /// <summary>
            /// 获取未知物品（替换原method_7）
            /// </summary>
            private static bool GetUnknownItem(GClass3515 __instance, out Item unknownItem)
            {
                foreach (Item item in __instance.Item.GetFirstLevelItems())
                {
                    if (!__instance.IplayerSearchController_0.IsItemKnown(item, null))
                    {
                        unknownItem = item;
                        return true;
                    }
                }
                unknownItem = null;
                return false;
            }

            private static float CalculateSearchTime(int priceLevel)
            {
                return priceLevel switch
                {
                    1 => 1f,
                    2 => 2f,
                    3 => 3f,
                    4 => 4f,
                    5 => 5f,
                    6 => 6f,
                    _ => 1f
                };
            }
            /// <summary>
            /// 根据物品价格计算等级（1-6）- 复用你已有的逻辑
            /// </summary>
            public static int GetItemPriceLevel(Item item)
            {
                var price = PriceDataService.Instance.GetPrice(item.TemplateId);
                if (!price.HasValue) return 1;

                // 计算单格价值
                int slots = item.Width * item.Height;
                double pricePerSlot = slots > 0 ? price.Value / slots : price.Value;

                // 使用与背景色相同的等级划分逻辑
                if (pricePerSlot <= Settings.PriceThreshold1.Value) return 1;
                if (pricePerSlot <= Settings.PriceThreshold2.Value) return 2;
                if (pricePerSlot <= Settings.PriceThreshold3.Value) return 3;
                if (pricePerSlot <= Settings.PriceThreshold4.Value) return 4;
                if (pricePerSlot <= Settings.PriceThreshold5.Value) return 5;
                return 6;
            }
        }
    }
}
