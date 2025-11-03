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
                // 防止递归
                if (IsProcessing)
                    return true;

                try
                {
                    IsProcessing = true;

                    // 获取物品价格等级
                    int priceLevel = SearchSoundPatch.GetItemPriceLevel(__instance.Item);

                    // 根据价格等级计算搜索时间
                    float searchTime = CalculateSearchTime(priceLevel);

                    // 播放搜索音效
                    SearchSoundPatch.PlaySound(__instance.Item);

                    // 执行自定义搜索逻辑
                    __result = ExecuteCustomSearch(__instance, searchTime);
                    return false; // 跳过原始方法
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"搜索执行失败: {ex.Message}");
                    IsProcessing = false;
                    return true; // 出错时回退到原始方法
                }
            }

            /// <summary>
            /// 执行自定义搜索逻辑
            /// </summary>
            private static async Task ExecuteCustomSearch(GClass3515 __instance, float searchTime)
            {
                try
                {
                    // 等待自定义搜索时间
                    await Task.Delay(TimeSpan.FromSeconds(searchTime));

                    // 调用原始搜索逻辑
                    await CallOriginalMethod6(__instance);

                    // 确保物品被正确标记
                    if (__instance.IplayerSearchController_0.ContainsUnknownItems(__instance.Item))
                    {
                        __instance.IplayerSearchController_0.SetItemAsKnown(__instance.Item, true);
                    }
                }
                finally
                {
                    IsProcessing = false;
                }
            }

            /// <summary>
            /// 调用原始的逻辑
            /// </summary>
            private static async Task CallOriginalMethod6(GClass3515 __instance)
            {
                try
                {
                    // 使用反射调用原始方法
                    var originalMethod = AccessTools.Method(typeof(GClass3515), "method_6");
                    if (originalMethod != null)
                    {
                        var task = (Task)originalMethod.Invoke(__instance, null);
                        if (task != null)
                        {
                            await task;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"调用原始搜索方法失败: {ex.Message}");
                }
            }

            private static float CalculateSearchTime(int priceLevel)
            {
                return priceLevel switch
                {
                    1 => 0f,
                    2 => 1.5f,
                    3 => 2f,
                    4 => 3f,
                    5 => 4f,
                    6 => 5.0f,
                    _ => 0f
                };
            }
        }
    }
}
