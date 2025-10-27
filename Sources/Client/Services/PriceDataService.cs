using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SPT.Common.Http;
using QuickPrice.Config;

namespace QuickPrice.Services
{
    /// <summary>
    /// 价格数据服务 - 负责从服务端获取和缓存价格数据
    /// v2.0: 新增异步支持和缓存过期机制
    /// </summary>
    public class PriceDataService
    {
        private static PriceDataService _instance;
        public static PriceDataService Instance => _instance ??= new PriceDataService();

        private Dictionary<string, double> _priceCache;
        private DateTime _lastUpdate = DateTime.MinValue;
        private readonly object _lockObject = new object(); // 新增：线程安全锁
        private Task<bool> _updateTask; // 新增：异步任务追踪

        // 跳蚤市场禁售物品列表缓存
        private HashSet<string> _ragfairBannedItems;
        private DateTime _bannedItemsLastUpdate = DateTime.MinValue;
        private Task<bool> _bannedItemsUpdateTask; // 禁售物品异步任务追踪

        private PriceDataService() { }

        /// <summary>
        /// 更新价格数据
        /// </summary>
        public bool UpdatePrices(bool force = false)
        {
            // 检查缓存是否过期
            if (!force && _priceCache != null && !ShouldRefresh())
            {
                return true;
            }

            try
            {
                // 根据配置选择动态或静态价格
                string endpoint = Settings.UseDynamicPrices.Value
                    ? "/showMeTheMoney/getDynamicPriceTable"
                    : "/showMeTheMoney/getStaticPriceTable";

                // Plugin.Log.LogDebug($"正在从服务端获取价格数据: {endpoint}");

                string json = RequestHandler.GetJson(endpoint);

                if (!string.IsNullOrEmpty(json))
                {
                    _priceCache = JsonConvert.DeserializeObject<Dictionary<string, double>>(json);
                    _lastUpdate = DateTime.Now;

                    Plugin.Log.LogInfo($"✅ 价格数据更新成功: {_priceCache.Count} 个物品");
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
                Plugin.Log.LogError($"❌ 获取价格数据失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取指定物品的价格
        /// </summary>
        public double? GetPrice(string templateId)
        {
            // 只在首次调用且缓存为空时加载
            // 避免频繁HTTP请求导致游戏卡顿
            if (_priceCache == null)
            {
                Plugin.Log.LogWarning("价格缓存未初始化，尝试加载...");
                UpdatePrices();
            }

            // 查询价格
            if (_priceCache?.TryGetValue(templateId, out var price) == true)
            {
                return price;
            }

            return null;
        }

        /// <summary>
        /// 检查是否需要刷新缓存
        /// v2.0: 根据配置的缓存模式决定
        /// </summary>
        private bool ShouldRefresh()
        {
            // 检查缓存模式配置
            var cacheMode = Settings.PriceCacheMode?.Value ?? Settings.CacheMode.Permanent;

            // 永久缓存或仅手动刷新模式：不自动刷新
            if (cacheMode == Settings.CacheMode.Permanent || cacheMode == Settings.CacheMode.Manual)
            {
                return false;
            }

            // 首次加载检查
            if (_lastUpdate == DateTime.MinValue)
                return true; // 从未更新，需要刷新

            if (_priceCache == null || _priceCache.Count == 0)
                return true; // 缓存为空，需要刷新

            // 根据配置的缓存模式检查过期时间
            var cacheAge = (DateTime.Now - _lastUpdate).TotalMinutes;
            int expireMinutes = cacheMode switch
            {
                Settings.CacheMode.FiveMinutes => 5,
                Settings.CacheMode.TenMinutes => 10,
                _ => int.MaxValue // 其他模式不自动过期
            };

            return cacheAge > expireMinutes;
        }

        /// <summary>
        /// 获取缓存的价格数量
        /// </summary>
        public int GetCachedPriceCount() => _priceCache?.Count ?? 0;

        /// <summary>
        /// 手动刷新价格数据
        /// </summary>
        public void ForceRefresh()
        {
            Plugin.Log.LogInfo("手动刷新价格数据...");
            UpdatePrices(true);
        }

        /// <summary>
        /// 获取缓存年龄（秒）
        /// </summary>
        public double GetCacheAge()
        {
            return (DateTime.Now - _lastUpdate).TotalSeconds;
        }

        // ========== v2.0 新增功能 ==========

        /// <summary>
        /// 异步更新价格数据（新增）
        /// 不阻塞主线程，适合在后台刷新价格
        /// </summary>
        public async Task<bool> UpdatePricesAsync(bool force = false)
        {
            // 检查缓存是否过期
            if (!force && _priceCache != null && !ShouldRefresh())
            {
                // Plugin.Log.LogDebug("价格缓存仍然有效，跳过异步更新");
                return true;
            }

            // 如果已有更新任务在运行，等待其完成
            if (_updateTask != null && !_updateTask.IsCompleted)
            {
                // Plugin.Log.LogDebug("价格更新任务已在运行，等待完成...");
                await _updateTask;
                return _priceCache != null && _priceCache.Count > 0;
            }

            // 创建新的异步更新任务
            _updateTask = Task.Run(() =>
            {
                try
                {
                    // 根据配置选择动态或静态价格
                    string endpoint = Settings.UseDynamicPrices.Value
                        ? "/showMeTheMoney/getDynamicPriceTable"
                        : "/showMeTheMoney/getStaticPriceTable";

                    // Plugin.Log.LogDebug($"正在异步获取价格数据: {endpoint}");

                    string json = RequestHandler.GetJson(endpoint);

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
                    return false;
                }
            });

            return await _updateTask;
        }

        /// <summary>
        /// 获取价格（线程安全版本）
        /// v2.0: 添加线程安全保护
        /// </summary>
        public double? GetPriceThreadSafe(string templateId)
        {
            lock (_lockObject)
            {
                return GetPrice(templateId);
            }
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

        /// <summary>
        /// 检查缓存是否已过期
        /// </summary>
        public bool IsCacheExpired()
        {
            return ShouldRefresh();
        }

        // ========== 跳蚤市场禁售物品功能 ==========

        /// <summary>
        /// 异步获取跳蚤市场禁售物品列表
        /// 不阻塞主线程，仅在后台获取一次
        /// </summary>
        public async Task<bool> UpdateRagfairBannedItemsAsync()
        {
            // 如果已有缓存且最近24小时内更新过，直接返回
            if (_ragfairBannedItems != null &&
                (DateTime.Now - _bannedItemsLastUpdate).TotalHours < 24)
            {
                return true;
            }

            // 如果已有更新任务在运行，等待其完成
            if (_bannedItemsUpdateTask != null && !_bannedItemsUpdateTask.IsCompleted)
            {
                await _bannedItemsUpdateTask;
                return _ragfairBannedItems != null;
            }

            // 创建新的异步更新任务
            _bannedItemsUpdateTask = Task.Run(() =>
            {
                try
                {
                    Plugin.Log.LogInfo("[跳蚤禁售] 开始异步获取禁售物品列表...");

                    string json = RequestHandler.GetJson("/showMeTheMoney/getRagfairBannedItems");

                    if (!string.IsNullOrEmpty(json))
                    {
                        var bannedList = JsonConvert.DeserializeObject<List<string>>(json);

                        // 使用锁保护缓存更新
                        lock (_lockObject)
                        {
                            _ragfairBannedItems = new HashSet<string>(bannedList);
                            _bannedItemsLastUpdate = DateTime.Now;
                        }

                        Plugin.Log.LogInfo($"[跳蚤禁售] 成功获取 {_ragfairBannedItems.Count} 个禁售物品");
                        return true;
                    }
                    else
                    {
                        Plugin.Log.LogWarning("[跳蚤禁售] 服务端返回空数据，默认所有物品可售");
                        lock (_lockObject)
                        {
                            _ragfairBannedItems = new HashSet<string>();
                            _bannedItemsLastUpdate = DateTime.Now;
                        }
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"[跳蚤禁售] 获取禁售物品列表失败: {ex.Message}");
                    // 失败时创建空集合，避免重复请求
                    lock (_lockObject)
                    {
                        _ragfairBannedItems = new HashSet<string>();
                        _bannedItemsLastUpdate = DateTime.Now;
                    }
                    return false;
                }
            });

            return await _bannedItemsUpdateTask;
        }

        /// <summary>
        /// 检查物品是否在跳蚤市场禁售（线程安全）
        /// </summary>
        /// <param name="templateId">物品模板ID</param>
        /// <returns>true = 可售, false = 禁售, null = 数据未加载</returns>
        public bool? IsRagfairBanned(string templateId)
        {
            if (string.IsNullOrEmpty(templateId))
                return null;

            lock (_lockObject)
            {
                // 如果数据还未加载，返回 null
                if (_ragfairBannedItems == null)
                    return null;

                // 检查是否在禁售列表中
                return _ragfairBannedItems.Contains(templateId);
            }
        }

        /// <summary>
        /// 启动异步加载禁售物品列表（非阻塞）
        /// 仅在后台启动任务，不等待完成
        /// </summary>
        public void StartLoadRagfairBannedItems()
        {
            // 使用 Fire-and-Forget 方式启动异步任务
            _ = UpdateRagfairBannedItemsAsync();
        }

        /// <summary>
        /// 获取禁售物品缓存状态
        /// </summary>
        public string GetRagfairBannedCacheStatus()
        {
            lock (_lockObject)
            {
                if (_ragfairBannedItems == null)
                    return "禁售列表: 未加载";

                var age = (DateTime.Now - _bannedItemsLastUpdate).TotalHours;
                return $"禁售列表: {_ragfairBannedItems.Count} 项, 年龄: {age:F1}小时";
            }
        }
    }
}
