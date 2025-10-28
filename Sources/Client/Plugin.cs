using System.Linq;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using QuickPrice.Config;
using QuickPrice.Patches;
using QuickPrice.Services;
using QuickPrice.Extensions;

namespace QuickPrice
{
    [BepInPlugin("com.quickprice.spt", "QuickPrice", BuildInfo.Version)]
    [BepInDependency("com.SPT.custom", "4.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; }
        public static ManualLogSource Log => Instance.Logger;
        public static EFT.InventoryLogic.Item HoveredItem { get; set; }

        // v2.0: 异步初始化标志
        private static bool _isInitializing = false;
        public static bool IsInitializing => _isInitializing;

        private void Awake()
        {
            Instance = this;

            try
            {
                // Log.LogInfo("===========================================");
                // Log.LogInfo("  QuickPrice - Tarkov SPT 4.0.0");
                // Log.LogInfo($"  版本: {BuildInfo.Version}");
                // Log.LogInfo("===========================================");

                // 初始化配置
                Settings.Init(Config);
                // Log.LogInfo("✅ 中文配置系统初始化成功");

                // v2.0: 启动异步价格数据加载（不阻塞游戏启动）
                _ = InitializePricesAsync();

                // v2.0: 启动异步商人数据检查（不阻塞游戏启动）
                _ = InitializeTradersAsync();

                // v2.0: 启动异步跳蚤禁售物品列表加载（不阻塞游戏启动）
                _ = InitializeRagfairBannedItemsAsync();

                // 注册所有补丁（立即启用）
                EnableAllPatches();

                // Log.LogInfo("===========================================");
                // Log.LogInfo("  🎉 插件启动完成！");
                // Log.LogInfo("  ⏳ 价格数据正在后台加载...");
                // Log.LogInfo("  ⏳ 跳蚤禁售列表正在后台加载...");
                // Log.LogInfo("  🎮 进入游戏查看物品价格");
                // Log.LogInfo("  ⚙️  按 F12 打开配置管理器");
                // Log.LogInfo("===========================================");
            }
            catch (System.Exception ex)
            {
                Log.LogError($"❌ 插件启动失败: {ex.Message}");
                Log.LogError(ex.StackTrace);
            }
        }

        /// <summary>
        /// v2.0: 异步初始化价格数据
        /// 使用 Fire-and-Forget 模式，不阻塞主线程
        /// </summary>
        private async Task InitializePricesAsync()
        {
            if (_isInitializing)
            {
                // Log.LogDebug("价格数据已在初始化中，跳过");
                return;
            }

            _isInitializing = true;

            try
            {
                // Log.LogInfo("🔄 开始异步加载价格数据...");

                // 使用新增的异步方法
                var success = await PriceDataService.Instance.UpdatePricesAsync();

                if (success)
                {
                    var count = PriceDataService.Instance.GetCachedPriceCount();
                    // Log.LogInfo($"✅ 价格数据异步加载成功: {count} 个物品");
                    // Log.LogDebug($"📊 {PriceDataService.Instance.GetCacheStatus()}");
                }
                else
                {
                    Log.LogWarning("⚠️ 价格数据加载失败，将在下次使用时重试");
                }
            }
            catch (System.Exception ex)
            {
                Log.LogError($"❌ 异步初始化失败: {ex.Message}");
                // Log.LogDebug(ex.StackTrace);
            }
            finally
            {
                _isInitializing = false;
            }
        }

        /// <summary>
        /// v2.0: 异步检查和等待商人数据加载
        /// 尝试在后台等待商人数据被游戏加载
        /// </summary>
        private async Task InitializeTradersAsync()
        {
            await Task.Delay(3000); // 等待 3 秒让游戏完成初始化

            try
            {
                // Log.LogInfo("🔄 开始检查商人数据...");

                // 使用反射调用 TraderPriceService.GetAllTraders()
                var getAllTradersMethod = TraderPriceService.Instance.GetType()
                    .GetMethod("GetAllTraders", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (getAllTradersMethod == null)
                {
                    // Log.LogDebug("⚠️ 无法找到 GetAllTraders 方法");
                    return;
                }

                var tradersObj = getAllTradersMethod.Invoke(TraderPriceService.Instance, null);
                if (tradersObj == null)
                {
                    // Log.LogDebug("⚠️ 商人列表为空或未加载");
                    return;
                }

                var tradersList = new System.Collections.Generic.List<object>();
                foreach (var trader in (System.Collections.IEnumerable)tradersObj)
                {
                    tradersList.Add(trader);
                }

                if (tradersList.Count == 0)
                {
                    // Log.LogDebug("⚠️ 商人列表为空");
                    return;
                }

                int traderCount = tradersList.Count;
                int readyCount = 0;

                // 获取 TraderClass 类型
                var traderType = tradersList[0].GetType();
                var getSupplyDataMethod = traderType.GetMethod("GetSupplyData",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                var localizedNameProperty = traderType.GetProperty("LocalizedName");

                foreach (var traderObj in tradersList)
                {
                    if (traderObj == null)
                        continue;

                    // 使用扩展方法 GetSupplyData (通过反射)
                    var supplyDataType = typeof(Extensions.TraderClassExtensions);
                    var getSupplyDataExtension = supplyDataType.GetMethod("GetSupplyData",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

                    object supplyData = null;
                    if (getSupplyDataExtension != null)
                    {
                        supplyData = getSupplyDataExtension.Invoke(null, new[] { traderObj });
                    }

                    if (supplyData != null)
                    {
                        readyCount++;
                    }
                    else
                    {
                        // string traderName = "Unknown";
                        // if (localizedNameProperty != null)
                        // {
                        //     traderName = localizedNameProperty.GetValue(traderObj)?.ToString() ?? "Unknown";
                        // }
                        // Log.LogDebug("   商人 " + traderName + " 的 SupplyData 未加载");
                    }
                }

                // Log.LogInfo($"✅ 找到 {traderCount} 个商人，{readyCount} 个商人数据已就绪");

                if (readyCount < traderCount)
                {
                    // Log.LogInfo("💡 部分商人数据未加载");
                    // Log.LogInfo("   首次使用商人价格功能时，请先打开任意商人界面");
                }
            }
            catch (System.Exception ex)
            {
                Log.LogError($"❌ 检查商人数据失败: {ex.Message}");
            }
        }

        /// <summary>
        /// v2.0: 异步初始化跳蚤禁售物品列表
        /// 在游戏启动时后台加载，不阻塞主线程
        /// </summary>
        private async Task InitializeRagfairBannedItemsAsync()
        {
            try
            {
                // Log.LogInfo("🔄 开始异步加载跳蚤禁售物品列表...");

                // 使用 PriceDataService 的异步方法
                var success = await PriceDataService.Instance.UpdateRagfairBannedItemsAsync();

                if (success)
                {
                    var status = PriceDataService.Instance.GetRagfairBannedCacheStatus();
                    // Log.LogInfo($"✅ 跳蚤禁售列表加载成功: {status}");
                }
                else
                {
                    Log.LogWarning("⚠️ 跳蚤禁售列表加载失败或为空，默认所有物品可售");
                }
            }
            catch (System.Exception ex)
            {
                Log.LogError($"❌ 加载跳蚤禁售列表失败: {ex.Message}");
                Log.LogWarning("   所有物品将默认显示跳蚤价格");
            }
        }

        /// <summary>
        /// 启用所有补丁
        /// </summary>
        private void EnableAllPatches()
        {
            try
            {
                // 注册物品捕获补丁
                new GridItemOnPointerEnterPatch().Enable();
                new GridItemOnPointerExitPatch().Enable();
                // Log.LogInfo("✅ 物品捕获补丁已启用");

                // 注册价格显示补丁
                new PriceTooltipPatch().Enable();
                // Log.LogInfo("✅ 价格显示补丁已启用");

                // 注册自定义颜色转换补丁（必须在背景色补丁之前启用）
                new CustomColorConverterPatch().Enable();
                // Log.LogInfo("✅ 自定义颜色转换补丁已启用");

                // 注册物品背景色自动着色补丁（如果启用）
                if (Settings.EnablePriceBasedBackgroundColor.Value)
                {
                    new ItemBackgroundColorPatch().Enable();
                    // Log.LogInfo("✅ 物品背景色自动着色已启用");
                }
                else
                {
                    // Log.LogInfo("ℹ️  物品背景色自动着色已禁用（可在配置中启用）");
                }

                // v2.0: 注册打开物品栏自动刷新补丁（如果启用）
                if (Settings.AutoRefreshOnOpenInventory.Value)
                {
                    new InventoryScreenShowPatch().Enable();
                    // Log.LogInfo("✅ 自动刷新补丁已启用（打开物品栏时刷新）");
                }
                else
                {
                    // Log.LogInfo("ℹ️  打开物品栏自动刷新已禁用（可在配置中启用）");
                }

                // 注册地面物品名称着色补丁（如果启用物品名称着色）
                if (Settings.ColorItemName.Value && Settings.EnableColorCoding.Value)
                {
                    new LootItemLabelPatch().Enable();
                    // Log.LogInfo("✅ 地面物品名称着色补丁已启用");
                }
                else
                {
                    // Log.LogInfo("ℹ️  地面物品名称着色已禁用（可在配置中启用）");
                }
            }
            catch (System.Exception ex)
            {
                Log.LogError($"❌ 启用补丁失败: {ex.Message}");
                Log.LogError(ex.StackTrace);
            }
        }

        /// <summary>
        /// Unity Update 方法 - 每帧调用
        /// 用于检测快捷键输入
        /// </summary>
        private void Update()
        {
            try
            {
                // 检测重置阈值快捷键
                if (Input.GetKeyDown(Settings.ResetThresholdsKey.Value))
                {
                    // Log.LogInfo("===========================================");
                    // Log.LogInfo("  🔄 检测到重置快捷键，开始重置阈值...");
                    // Log.LogInfo("===========================================");

                    // 调用重置方法
                    Settings.ResetPriceThresholds();

                    // 显示重置后的值
                    // Log.LogInfo("📊 价格阈值已重置为默认值:");
                    // Log.LogInfo($"  白色→绿色: {Settings.PriceThreshold1.Value:N0}");
                    // Log.LogInfo($"  绿色→蓝色: {Settings.PriceThreshold2.Value:N0}");
                    // Log.LogInfo($"  蓝色→紫色: {Settings.PriceThreshold3.Value:N0}");
                    // Log.LogInfo($"  紫色→橙色: {Settings.PriceThreshold4.Value:N0}");
                    // Log.LogInfo($"  橙色→红色: {Settings.PriceThreshold5.Value:N0}");
                    // Log.LogInfo("");
                    // Log.LogInfo("🎯 穿甲阈值已重置为默认值:");
                    // Log.LogInfo($"  白色→绿色: {Settings.PenetrationThreshold1.Value}");
                    // Log.LogInfo($"  绿色→蓝色: {Settings.PenetrationThreshold2.Value}");
                    // Log.LogInfo($"  蓝色→紫色: {Settings.PenetrationThreshold3.Value}");
                    // Log.LogInfo($"  紫色→橙色: {Settings.PenetrationThreshold4.Value}");
                    // Log.LogInfo($"  橙色→红色: {Settings.PenetrationThreshold5.Value}");
                    // Log.LogInfo("===========================================");
                    // Log.LogInfo("  ✅ 重置完成！配置已保存");
                    // Log.LogInfo("  💾 配置文件: BepInEx/config/QuickPrice.cfg");
                    // Log.LogInfo("  ℹ️  立即生效，无需重启游戏");
                    // Log.LogInfo("===========================================");
                }
            }
            catch (System.Exception ex)
            {
                Log.LogError($"❌ 快捷键检测失败: {ex.Message}");
            }
        }

        private void OnDestroy()
        {
            // Log.LogInfo("QuickPrice 卸载");
        }
    }
}
