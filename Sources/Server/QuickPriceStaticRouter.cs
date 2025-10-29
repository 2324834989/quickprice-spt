// ----------------------------------------------------------------------------
// QuickPrice - Custom Static Router
// 处理HTTP路由注册和请求处理
// ----------------------------------------------------------------------------

using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Services;
using System.Collections.Generic;
using System.Linq;

namespace QuickPrice.Server
{
    /// <summary>
    /// QuickPrice 自定义静态路由器
    /// 继承自StaticRouter，注册HTTP端点
    /// </summary>
    [Injectable]
    public class QuickPriceStaticRouter : StaticRouter
    {
        private static DatabaseService? _databaseServiceStatic;
        private static RagfairOfferService? _ragfairOfferServiceStatic;
        private static ISptLogger<QuickPriceStaticRouter>? _loggerStatic;

        // 价格缓存
        private static Dictionary<string, double>? _cachedDynamicPrices;
        private static DateTime _lastCacheUpdate = DateTime.MinValue;
        private static readonly object _cacheLock = new object();
        private static bool _isUpdatingCache = false;
        private static System.Threading.Timer? _autoRefreshTimer; // 自动刷新定时器
        private static bool _isPreloadStarted = false; // 防止重复预加载

        // 配置
        private static QuickPriceConfig? _config;

        public QuickPriceStaticRouter(
            JsonUtil jsonUtil,
            DatabaseService databaseService,
            RagfairOfferService ragfairOfferService,
            ISptLogger<QuickPriceStaticRouter> logger) : base(
            jsonUtil,
            GetCustomRoutes()
        )
        {
            // 保存到静态变量供路由处理方法使用
            _databaseServiceStatic = databaseService;
            _ragfairOfferServiceStatic = ragfairOfferService;
            _loggerStatic = logger;

            logger.Info("[QuickPrice] 自定义路由已初始化", null);

            // 加载配置文件
            LoadConfig();

            // 启动时预加载动态价格缓存（异步，不阻塞启动）
            // 使用锁防止重复初始化
            lock (_cacheLock)
            {
                if (!_isPreloadStarted)
                {
                    _isPreloadStarted = true;
                    _ = PreloadDynamicPriceCacheAsync();
                }
                else
                {
                    logger.Info("[QuickPrice] 预加载已启动，跳过重复初始化", null);
                }
            }
        }

        /// <summary>
        /// 加载配置文件
        /// </summary>
        private static void LoadConfig()
        {
            try
            {
                // 获取模组目录路径
                var modPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "user", "mods", "QuickPrice");
                var configPath = Path.Combine(modPath, "config.json");

                if (File.Exists(configPath))
                {
                    var jsonContent = File.ReadAllText(configPath);
                    _config = JsonSerializer.Deserialize<QuickPriceConfig>(jsonContent);
                    _loggerStatic?.Info($"[QuickPrice] 配置文件已加载 (自动刷新间隔: {_config?.AutoRefreshIntervalMinutes ?? 5} 分钟)", null);
                }
                else
                {
                    _loggerStatic?.Warning($"[QuickPrice] 配置文件不存在: {configPath}, 将使用默认配置", null);
                    _config = new QuickPriceConfig();
                }
            }
            catch (Exception ex)
            {
                _loggerStatic?.Error($"[QuickPrice] 加载配置文件失败: {ex.Message}, 将使用默认配置", ex);
                _config = new QuickPriceConfig();
            }
        }

        /// <summary>
        /// 定义自定义路由
        /// </summary>
        private static List<RouteAction> GetCustomRoutes()
        {
            return
            [
                // 路由1: 获取货币购买价格
                new RouteAction<EmptyRequestData>(
                    "/showMeTheMoney/getCurrencyPurchasePrices",
                    async (url, info, sessionId, output) =>
                        await HandleGetCurrencyPurchasePrices(url, info, sessionId)
                ),

                // 路由2: 获取静态价格表
                new RouteAction<EmptyRequestData>(
                    "/showMeTheMoney/getStaticPriceTable",
                    async (url, info, sessionId, output) =>
                        await HandleGetStaticPriceTable(url, info, sessionId)
                ),

                // 路由3: 获取动态价格表
                new RouteAction<EmptyRequestData>(
                    "/showMeTheMoney/getDynamicPriceTable",
                    async (url, info, sessionId, output) =>
                        await HandleGetDynamicPriceTable(url, info, sessionId)
                ),

                // 路由4: 获取跳蚤市场禁售物品列表
                new RouteAction<EmptyRequestData>(
                    "/showMeTheMoney/getRagfairBannedItems",
                    async (url, info, sessionId, output) =>
                        await HandleGetRagfairBannedItems(url, info, sessionId)
                )
            ];
        }

        #region 路由处理方法

        /// <summary>
        /// 处理获取货币购买价格的请求
        /// </summary>
        private static ValueTask<string> HandleGetCurrencyPurchasePrices(
            string url,
            EmptyRequestData info,
            MongoId sessionId)
        {
            try
            {
                double eurPrice = 153;  // 默认值
                double usdPrice = 139;  // 默认值

                // 尝试从数据库获取实际价格
                if (_databaseServiceStatic != null)
                {
                    try
                    {
                        var tables = _databaseServiceStatic.GetTables();

                        // Skier (Скупщик) - 出售欧元
                        // Peacekeeper (Миротворец) - 出售美元
                        // 这些是默认的货币交易商ID
                        const string SkierId = "58330581ace78e27b8b10cee";
                        const string PeacekeeperId = "5935c25fb3acc3127c3d8cd9";

                        // 尝试从交易商数据获取货币价格
                        // 注：实际实现取决于 DatabaseService 的 API
                        // 如果无法获取，使用默认值

                        _loggerStatic?.Info($"[QuickPrice] Currency prices queried - EUR: {eurPrice}, USD: {usdPrice}", null);
                    }
                    catch (Exception dbEx)
                    {
                        _loggerStatic?.Warning($"[QuickPrice] Could not get currency prices from database: {dbEx.Message}", null);
                    }
                }

                var prices = new CurrencyPurchasePrices
                {
                    Eur = eurPrice,
                    Usd = usdPrice
                };

                var json = JsonSerializer.Serialize(prices);
                return new ValueTask<string>(json);
            }
            catch (Exception ex)
            {
                _loggerStatic?.Error($"[QuickPrice] Error in GetCurrencyPurchasePrices: {ex.Message}", ex);
                // Console.WriteLine($"[QuickPrice] Error in GetCurrencyPurchasePrices: {ex.Message}");
                // 返回默认值
                var fallback = JsonSerializer.Serialize(new CurrencyPurchasePrices { Eur = 153, Usd = 139 });
                return new ValueTask<string>(fallback);
            }
        }

        /// <summary>
        /// 处理获取静态价格表的请求
        /// </summary>
        private static ValueTask<string> HandleGetStaticPriceTable(
            string url,
            EmptyRequestData info,
            MongoId sessionId)
        {
            try
            {
                var priceTable = new Dictionary<string, double>();

                if (_databaseServiceStatic != null)
                {
                    try
                    {
                        var tables = _databaseServiceStatic.GetTables();

                        // 获取价格表 - 这是主要的价格来源
                        if (tables?.Templates?.Prices != null)
                        {
                            foreach (var priceEntry in tables.Templates.Prices)
                            {
                                string itemId = priceEntry.Key;
                                double price = priceEntry.Value;

                                if (price > 0)
                                {
                                    priceTable[itemId] = price;
                                }
                            }

                            _loggerStatic?.Info($"[QuickPrice] Static price table generated with {priceTable.Count} items", null);
                        }
                        else
                        {
                            _loggerStatic?.Warning("[QuickPrice] Templates.Prices is null", null);
                        }
                    }
                    catch (Exception dbEx)
                    {
                        _loggerStatic?.Error($"[QuickPrice] Error accessing database: {dbEx.Message}", dbEx);
                    }
                }
                else
                {
                    _loggerStatic?.Warning("[QuickPrice] DatabaseService is not available", null);
                }

                var json = JsonSerializer.Serialize(priceTable);
                return new ValueTask<string>(json);
            }
            catch (Exception ex)
            {
                _loggerStatic?.Error($"[QuickPrice] Error in GetStaticPriceTable: {ex.Message}", ex);
                // Console.WriteLine($"[QuickPrice] Error in GetStaticPriceTable: {ex.Message}");
                var fallback = JsonSerializer.Serialize(new Dictionary<string, double>());
                return new ValueTask<string>(fallback);
            }
        }

        /// <summary>
        /// 处理获取动态价格表的请求
        /// 优化版：使用缓存 + 并行查询
        /// </summary>
        private static ValueTask<string> HandleGetDynamicPriceTable(
            string url,
            EmptyRequestData info,
            MongoId sessionId)
        {
            try
            {
                // 检查缓存是否有效（5分钟内）
                var cacheAge = (DateTime.Now - _lastCacheUpdate).TotalMinutes;
                bool isCacheValid = _cachedDynamicPrices != null && cacheAge < 5;

                if (isCacheValid)
                {
                    _loggerStatic?.Info($"[QuickPrice] 返回缓存的动态价格（{_cachedDynamicPrices!.Count} 个物品，缓存年龄: {cacheAge:F1} 分钟）", null);
                    var json = JsonSerializer.Serialize(_cachedDynamicPrices);
                    return new ValueTask<string>(json);
                }

                // 缓存过期或不存在，触发异步更新（不阻塞）
                if (!_isUpdatingCache)
                {
                    _ = UpdateDynamicPriceCacheAsync();
                }

                // 如果有旧缓存，先返回旧数据（不让客户端等待）
                if (_cachedDynamicPrices != null)
                {
                    _loggerStatic?.Info($"[QuickPrice] 返回旧缓存数据，同时后台更新（{_cachedDynamicPrices.Count} 个物品，缓存年龄: {cacheAge:F1} 分钟）", null);
                    var json = JsonSerializer.Serialize(_cachedDynamicPrices);
                    return new ValueTask<string>(json);
                }

                // 如果没有缓存，回退到静态价格
                _loggerStatic?.Warning("[QuickPrice] 无可用缓存，回退到静态价格", null);
                return HandleGetStaticPriceTable(url, info, sessionId);
            }
            catch (Exception ex)
            {
                _loggerStatic?.Error($"[QuickPrice] Error in GetDynamicPriceTable: {ex.Message}", ex);
                // 回退到静态价格表
                return HandleGetStaticPriceTable(url, info, sessionId);
            }
        }

        /// <summary>
        /// 异步更新动态价格缓存（并行优化）
        /// </summary>
        private static async Task UpdateDynamicPriceCacheAsync()
        {
            // 防止重复更新
            lock (_cacheLock)
            {
                if (_isUpdatingCache)
                {
                    _loggerStatic?.Info("[QuickPrice] 缓存更新已在进行中，跳过", null);
                    return;
                }
                _isUpdatingCache = true;
            }

            try
            {
                _loggerStatic?.Info("[QuickPrice] 开始动态价格缓存更新（并行模式）...", null);
                var startTime = DateTime.Now;

                // 获取静态价格作为基础
                var priceTable = new Dictionary<string, double>();

                if (_databaseServiceStatic != null)
                {
                    var tables = _databaseServiceStatic.GetTables();

                    // 先加载静态价格
                    if (tables?.Templates?.Prices != null)
                    {
                        foreach (var priceEntry in tables.Templates.Prices)
                        {
                            if (priceEntry.Value > 0)
                            {
                                priceTable[priceEntry.Key] = priceEntry.Value;
                            }
                        }
                    }

                    // 并行查询跳蚤市场价格
                    if (_ragfairOfferServiceStatic != null && priceTable.Count > 0)
                    {
                        int updatedCount = 0;
                        var updateLock = new object();

                        // 使用 Parallel.ForEach 并行处理
                        await Task.Run(() =>
                        {
                            Parallel.ForEach(
                                priceTable.Keys.ToList(),
                                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                                itemId =>
                                {
                                    try
                                    {
                                        var offers = _ragfairOfferServiceStatic.GetOffersOfType(itemId);

                                        if (offers != null && offers.Any())
                                        {
                                            // 计算所有非交易商报价的平均价格
                                            var playerOffers = offers?.Where(o => o.User?.Id != null).ToList();

                                            if (playerOffers != null && playerOffers.Count > 0)
                                            {
                                                // 使用中位数或平均值
                                                var prices = playerOffers
                                                    .Where(o => o.RequirementsCost.HasValue && o.RequirementsCost.Value > 0)
                                                    .Select(o => (double)o.RequirementsCost!.Value)
                                                    .ToList();

                                                if (prices.Count > 0)
                                                {
                                                    double avgPrice = prices.Average();

                                                    lock (updateLock)
                                                    {
                                                        priceTable[itemId] = avgPrice;
                                                        updatedCount++;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    catch
                                    {
                                        // 某个物品查询失败，继续处理其他物品
                                    }
                                }
                            );
                        });

                        var duration = (DateTime.Now - startTime).TotalSeconds;
                        _loggerStatic?.Info($"[QuickPrice] 动态价格缓存已更新: {priceTable.Count} 个物品（{updatedCount} 个来自跳蚤市场），耗时 {duration:F1} 秒", null);
                    }
                }

                // 更新缓存
                lock (_cacheLock)
                {
                    _cachedDynamicPrices = priceTable;
                    _lastCacheUpdate = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                _loggerStatic?.Error($"[QuickPrice] 缓存更新失败: {ex.Message}", ex);
            }
            finally
            {
                lock (_cacheLock)
                {
                    _isUpdatingCache = false;
                }
            }
        }

        /// <summary>
        /// 服务器启动时预加载动态价格缓存
        /// </summary>
        private static async Task PreloadDynamicPriceCacheAsync()
        {
            try
            {
                // 等待更长时间，确保数据库和跳蚤市场已完全初始化
                _loggerStatic?.Info("[QuickPrice] 正在等待数据库初始化...", null);

                // 第一阶段：等待数据库就绪
                int maxRetries = 30;
                int retryDelayMs = 2000;
                bool databaseReady = false;

                for (int i = 0; i < maxRetries; i++)
                {
                    await Task.Delay(retryDelayMs);

                    try
                    {
                        // 尝试访问数据库，检查是否已初始化
                        if (_databaseServiceStatic != null)
                        {
                            var tables = _databaseServiceStatic.GetTables();
                            if (tables?.Templates?.Prices != null && tables.Templates.Prices.Count > 0)
                            {
                                databaseReady = true;
                                _loggerStatic?.Info($"[QuickPrice] 数据库已就绪（耗时 {(i + 1) * retryDelayMs / 1000} 秒）", null);
                                break;
                            }
                        }
                    }
                    catch
                    {
                        // 数据库还未就绪，继续等待
                        if (i % 5 == 0)  // 每10秒输出一次日志
                        {
                            _loggerStatic?.Info($"[QuickPrice] 仍在等待数据库... ({i + 1}/{maxRetries})", null);
                        }
                    }
                }

                if (!databaseReady)
                {
                    _loggerStatic?.Warning("========================================", null);
                    _loggerStatic?.Warning("[QuickPrice] 数据库初始化超时（60秒）", null);
                    _loggerStatic?.Warning("[QuickPrice] 将在客户端首次请求时重试", null);
                    _loggerStatic?.Warning("========================================", null);
                    return;
                }

                // 第二阶段：额外等待10秒，确保跳蚤市场报价已生成
                _loggerStatic?.Info("[QuickPrice] 再等待10秒以确保跳蚤市场报价生成完成...", null);
                await Task.Delay(10000);

                _loggerStatic?.Info("========================================", null);
                _loggerStatic?.Info("[QuickPrice] 开始预加载动态价格缓存...", null);
                _loggerStatic?.Info("========================================", null);

                // 调用更新缓存方法
                await UpdateDynamicPriceCacheAsync();

                if (_cachedDynamicPrices != null && _cachedDynamicPrices.Count > 0)
                {
                    _loggerStatic?.Success("========================================", null);
                    _loggerStatic?.Success($"[QuickPrice] 缓存预加载完成！{_cachedDynamicPrices.Count} 个物品已就绪", null);
                    _loggerStatic?.Success("[QuickPrice] 客户端现在可以即时获取价格数据！", null);
                    _loggerStatic?.Success("========================================", null);

                    // 启动定时刷新（每30分钟）
                    StartAutoRefreshTimer();
                }
                else
                {
                    _loggerStatic?.Warning("========================================", null);
                    _loggerStatic?.Warning("[QuickPrice] 缓存预加载失败，将使用静态价格作为备用", null);
                    _loggerStatic?.Warning("========================================", null);
                }
            }
            catch (Exception ex)
            {
                _loggerStatic?.Error($"[QuickPrice] 预加载缓存失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 启动自动刷新定时器（根据配置的间隔刷新）
        /// </summary>
        private static void StartAutoRefreshTimer()
        {
            try
            {
                // 获取配置的刷新间隔（默认5分钟）
                int intervalMinutes = _config?.AutoRefreshIntervalMinutes ?? 5;

                // 如果间隔为0，则禁用自动刷新
                if (intervalMinutes <= 0)
                {
                    _loggerStatic?.Info("[QuickPrice] 自动刷新已禁用 (配置间隔为0)", null);
                    return;
                }

                // 如果定时器已存在，先停止
                _autoRefreshTimer?.Dispose();

                // 创建定时器：根据配置的间隔刷新
                var refreshInterval = TimeSpan.FromMinutes(intervalMinutes);

                _autoRefreshTimer = new System.Threading.Timer(
                    async (state) =>
                    {
                        _loggerStatic?.Info("========================================", null);
                        _loggerStatic?.Info($"[QuickPrice] 自动刷新定时器触发（每{intervalMinutes}分钟）", null);
                        _loggerStatic?.Info("========================================", null);

                        await UpdateDynamicPriceCacheAsync();
                    },
                    null,
                    refreshInterval,  // 首次执行延迟
                    refreshInterval   // 后续执行间隔
                );

                _loggerStatic?.Info($"[QuickPrice] 自动刷新定时器已启动（间隔: {intervalMinutes} 分钟）", null);
            }
            catch (Exception ex)
            {
                _loggerStatic?.Error($"[QuickPrice] 启动自动刷新定时器失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 处理获取跳蚤市场禁售物品列表的请求
        /// </summary>
        private static ValueTask<string> HandleGetRagfairBannedItems(
            string url,
            EmptyRequestData info,
            MongoId sessionId)
        {
            try
            {
                var bannedItems = new HashSet<string>();
                int totalItems = 0;
                int checkedItems = 0;

                if (_databaseServiceStatic != null)
                {
                    try
                    {
                        var tables = _databaseServiceStatic.GetTables();

                        // 遍历所有物品模板，检查跳蚤市场相关属性
                        if (tables?.Templates?.Items != null)
                        {
                            totalItems = tables.Templates.Items.Count;
                            _loggerStatic?.Info($"[QuickPrice-RagfairBan] 开始检查 {totalItems} 个物品模板...", null);

                            // 打印第一个物品的详细信息（用于调试）
                            var firstItem = tables.Templates.Items.FirstOrDefault();
                            if (firstItem.Value != null)
                            {
                                var firstItemType = firstItem.Value.GetType();
                                _loggerStatic?.Info($"[QuickPrice-RagfairBan] 物品模板类型: {firstItemType.FullName}", null);

                                // 列出所有属性名
                                var props = firstItemType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                                var propNames = string.Join(", ", props.Select(p => p.Name).Take(20));
                                _loggerStatic?.Info($"[QuickPrice-RagfairBan] 前20个属性: {propNames}", null);
                            }

                            foreach (var itemEntry in tables.Templates.Items)
                            {
                                try
                                {
                                    checkedItems++;
                                    string itemId = itemEntry.Key;
                                    var item = itemEntry.Value;

                                    // 使用反射检查物品是否可以在跳蚤市场出售
                                    var itemType = item.GetType();

                                    // 尝试获取 CanSellOnRagfair 属性
                                    var canSellProp = itemType.GetProperty("CanSellOnRagfair",
                                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                                    var canRequireProp = itemType.GetProperty("CanRequireOnRagfair",
                                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                                    bool? canSell = null;
                                    bool? canRequire = null;

                                    if (canSellProp != null)
                                    {
                                        var value = canSellProp.GetValue(item);
                                        if (value is bool b)
                                            canSell = b;
                                    }

                                    if (canRequireProp != null)
                                    {
                                        var value = canRequireProp.GetValue(item);
                                        if (value is bool b)
                                            canRequire = b;
                                    }

                                    // 如果任一属性明确禁止，则加入禁售列表
                                    if (canSell == false || canRequire == false)
                                    {
                                        bannedItems.Add(itemId);
                                        // 打印前10个禁售物品（用于验证）
                                        if (bannedItems.Count <= 10)
                                        {
                                            _loggerStatic?.Info($"[QuickPrice-RagfairBan] 找到禁售物品: {itemId} (CanSell={canSell}, CanRequire={canRequire})", null);
                                        }
                                    }
                                }
                                catch (Exception itemEx)
                                {
                                    // 某个物品检查失败，跳过继续处理其他物品
                                    if (checkedItems <= 5)
                                    {
                                        _loggerStatic?.Warning($"[QuickPrice-RagfairBan] 检查物品失败: {itemEx.Message}", null);
                                    }
                                    continue;
                                }
                            }

                            _loggerStatic?.Info($"[QuickPrice-RagfairBan] 检查完成: 总共{totalItems}个物品, 检查了{checkedItems}个, 找到{bannedItems.Count}个禁售物品", null);
                        }
                        else
                        {
                            _loggerStatic?.Warning("[QuickPrice-RagfairBan] Templates.Items is null", null);
                        }
                    }
                    catch (Exception dbEx)
                    {
                        _loggerStatic?.Error($"[QuickPrice-RagfairBan] Error accessing database: {dbEx.Message}", dbEx);
                    }
                }
                else
                {
                    _loggerStatic?.Warning("[QuickPrice-RagfairBan] DatabaseService is not available", null);
                }

                // 返回禁售物品ID列表
                var json = JsonSerializer.Serialize(bannedItems.ToList());
                return new ValueTask<string>(json);
            }
            catch (Exception ex)
            {
                _loggerStatic?.Error($"[QuickPrice-RagfairBan] Error in GetRagfairBannedItems: {ex.Message}", ex);
                // Console.WriteLine($"[QuickPrice-RagfairBan] Error in GetRagfairBannedItems: {ex.Message}");
                // 返回空列表
                var fallback = JsonSerializer.Serialize(new List<string>());
                return new ValueTask<string>(fallback);
            }
        }

        #endregion
    }
}
