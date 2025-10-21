using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SPT.Common.Http;
using QuickPrice.Config;

namespace QuickPrice.Services
{
    /// <summary>
    /// 异步价格数据服务 - 负责从服务端获取和缓存价格数据
    /// 支持自动缓存过期和后台更新
    /// </summary>
    public class PriceDataServiceAsync
    {
        private static PriceDataServiceAsync _instance;
        public static PriceDataServiceAsync Instance => _instance ??= new PriceDataServiceAsync();

        private Dictionary<string, double> _priceCache;
        private DateTime _lastUpdate = DateTime.MinValue;
        private readonly object _lockObject = new object();
        private Task<bool> _updateTask;

        // 缓存配置
        private const double CACHE_EXPIRE_SECONDS = 300; // 5分钟缓存过期
        private const double MIN_UPDATE_INTERVAL = 10;   // 最小更新间隔10秒（防止频繁请求）

        private PriceDataServiceAsync() { }

        /// <summary>
        /// 异步更新价格数据
        /// </summary>
        /// <param name="force">是否强制更新（忽略缓存）</param>
        /// <returns>是否更新成功</returns>
        public async Task<bool> UpdatePricesAsync(bool force = false)
        {
            // 检查是否需要更新
            if (!force && !ShouldRefresh())
            {
                Plugin.Log.LogDebug("价格缓存仍然有效，跳过更新");
                return true;
            }

            // 检查最小更新间隔（防止过于频繁的请求）
            var timeSinceLastUpdate = (DateTime.Now - _lastUpdate).TotalSeconds;
            if (!force && timeSinceLastUpdate < MIN_UPDATE_INTERVAL)
            {
                Plugin.Log.LogDebug($"距离上次更新仅 {timeSinceLastUpdate:F1}秒，跳过更新");
                return true;
            }

            // 如果已有更新任务在运行，等待其完成
            if (_updateTask != null && !_updateTask.IsCompleted)
            {
                Plugin.Log.LogDebug("价格更新任务已在运行，等待完成...");
                await _updateTask;
                return true;
            }

            // 创建新的更新任务
            _updateTask = Task.Run(async () =>
            {
                try
                {
                    // 根据配置选择动态或静态价格
                    string endpoint = Settings.UseDynamicPrices.Value
                        ? "/showMeTheMoney/getDynamicPriceTable"
                        : "/showMeTheMoney/getStaticPriceTable";

                    Plugin.Log.LogDebug($"正在异步获取价格数据: {endpoint}");

                    // 使用 Task.Run 包装同步的 RequestHandler
                    // 注意：SPT 的 RequestHandler.GetJson 是同步的，我们用 Task.Run 将其移到后台线程
                    string json = await Task.Run(() => RequestHandler.GetJson(endpoint));

                    if (!string.IsNullOrEmpty(json))
                    {
                        var newCache = JsonConvert.DeserializeObject<Dictionary<string, double>>(json);

                        // 使用锁保护缓存更新
                        lock (_lockObject)
                        {
                            _priceCache = newCache;
                            _lastUpdate = DateTime.Now;
                        }

                        Plugin.Log.LogInfo($"✅ 价格数据异步更新成功: {_priceCache.Count} 个物品");
                        return true;
                    }
                    else
                    {
                        Plugin.Log.LogWarning("⚠️ 服务端返回空数据");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"❌ 异步获取价格数据失败: {ex.Message}");
                    Plugin.Log.LogDebug($"堆栈跟踪: {ex.StackTrace}");
                    return false;
                }
            });

            return await _updateTask;
        }

        /// <summary>
        /// 同步包装方法（向后兼容）
        /// </summary>
        public bool UpdatePrices(bool force = false)
        {
            // 启动异步任务但不等待（Fire and Forget）
            // 这样不会阻塞主线程
            _ = UpdatePricesAsync(force);

            // 如果缓存已存在，立即返回成功
            return _priceCache != null && _priceCache.Count > 0;
        }

        /// <summary>
        /// 等待价格数据加载完成（带超时）
        /// </summary>
        /// <param name="timeoutSeconds">超时时间（秒）</param>
        /// <returns>是否加载成功</returns>
        public async Task<bool> WaitForPricesAsync(int timeoutSeconds = 10)
        {
            var startTime = DateTime.Now;

            while (_priceCache == null || _priceCache.Count == 0)
            {
                if ((DateTime.Now - startTime).TotalSeconds > timeoutSeconds)
                {
                    Plugin.Log.LogWarning($"⚠️ 等待价格数据超时 ({timeoutSeconds}秒)");
                    return false;
                }

                await Task.Delay(100); // 等待 100ms 后重试
            }

            return true;
        }

        /// <summary>
        /// 获取指定物品的价格（线程安全）
        /// </summary>
        public double? GetPrice(string templateId)
        {
            // 首次调用时异步加载
            if (_priceCache == null)
            {
                Plugin.Log.LogWarning("价格缓存未初始化，启动异步加载...");
                _ = UpdatePricesAsync(); // Fire and Forget
                return null;
            }

            // 使用锁保护读取
            lock (_lockObject)
            {
                if (_priceCache?.TryGetValue(templateId, out var price) == true)
                {
                    return price;
                }
            }

            return null;
        }

        /// <summary>
        /// 检查缓存是否需要刷新
        /// </summary>
        private bool ShouldRefresh()
        {
            // 如果从未更新过，需要刷新
            if (_lastUpdate == DateTime.MinValue)
                return true;

            // 如果缓存为空，需要刷新
            if (_priceCache == null || _priceCache.Count == 0)
                return true;

            // 检查缓存是否过期（5分钟）
            var cacheAge = (DateTime.Now - _lastUpdate).TotalSeconds;
            return cacheAge > CACHE_EXPIRE_SECONDS;
        }

        /// <summary>
        /// 获取缓存的价格数量
        /// </summary>
        public int GetCachedPriceCount()
        {
            lock (_lockObject)
            {
                return _priceCache?.Count ?? 0;
            }
        }

        /// <summary>
        /// 手动强制刷新价格数据（异步）
        /// </summary>
        public async Task ForceRefreshAsync()
        {
            Plugin.Log.LogInfo("手动强制刷新价格数据...");
            await UpdatePricesAsync(true);
        }

        /// <summary>
        /// 获取缓存年龄（秒）
        /// </summary>
        public double GetCacheAge()
        {
            return (DateTime.Now - _lastUpdate).TotalSeconds;
        }

        /// <summary>
        /// 获取缓存状态信息（用于调试）
        /// </summary>
        public string GetCacheStatus()
        {
            var age = GetCacheAge();
            var count = GetCachedPriceCount();
            var isExpired = ShouldRefresh();

            return $"缓存: {count} 项, 年龄: {age:F1}秒, 过期: {(isExpired ? "是" : "否")}";
        }
    }
}
