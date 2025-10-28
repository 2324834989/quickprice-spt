using System.Reflection;
using System.Threading.Tasks;
using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using QuickPrice.Config;
using QuickPrice.Services;

namespace QuickPrice.Patches
{
    /// <summary>
    /// v2.0: 打开物品栏时自动刷新价格补丁
    /// 拦截 InventoryScreen.Show() 方法，在打开物品栏时异步更新价格数据（如果缓存过期）
    /// </summary>
    public class InventoryScreenShowPatch : ModulePatch
    {
        private static bool _isUpdating = false;

        protected override MethodBase GetTargetMethod()
        {
            // 查找 InventoryScreen.Show() 方法
            return AccessTools.FirstMethod(
                typeof(InventoryScreen),
                x => x.Name == nameof(InventoryScreen.Show)
            );
        }

        [PatchPrefix]
        public static void Prefix()
        {
            try
            {
                // 检查配置是否启用自动刷新
                if (!Settings.AutoRefreshOnOpenInventory.Value)
                {
                    return;
                }

                // 防止重复更新
                if (_isUpdating)
                {
                    Plugin.Log.LogDebug("价格更新已在进行中，跳过");
                    return;
                }

                // 检查缓存是否需要刷新
                if (!PriceDataService.Instance.IsCacheExpired())
                {
                    var cacheAge = PriceDataService.Instance.GetCacheAge();
                    Plugin.Log.LogDebug($"价格缓存仍然新鲜 ({cacheAge:F0}秒)，跳过刷新");
                    return;
                }

                var age = PriceDataService.Instance.GetCacheAge();
                // Plugin.Log.LogInfo($"📦 打开物品栏，缓存已过期 ({age:F0}秒)，开始刷新价格...");

                // 异步更新价格（Fire-and-Forget）
                _ = UpdatePricesAsync();
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"❌ 物品栏刷新补丁失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 异步更新价格数据
        /// </summary>
        private static async Task UpdatePricesAsync()
        {
            _isUpdating = true;

            try
            {
                var success = await PriceDataService.Instance.UpdatePricesAsync();

                if (success)
                {
                    var count = PriceDataService.Instance.GetCachedPriceCount();
                    // Plugin.Log.LogInfo($"✅ 价格数据刷新成功: {count} 个物品");
                }
                else
                {
                    Plugin.Log.LogWarning("⚠️ 价格数据刷新失败");
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"❌ 异步刷新失败: {ex.Message}");
            }
            finally
            {
                _isUpdating = false;
            }
        }
    }
}
