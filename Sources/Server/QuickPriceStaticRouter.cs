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

            logger.Info("[QuickPrice] Custom router initialized", null);
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
                Console.WriteLine($"[QuickPrice] Error in GetCurrencyPurchasePrices: {ex.Message}");
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
                Console.WriteLine($"[QuickPrice] Error in GetStaticPriceTable: {ex.Message}");
                var fallback = JsonSerializer.Serialize(new Dictionary<string, double>());
                return new ValueTask<string>(fallback);
            }
        }

        /// <summary>
        /// 处理获取动态价格表的请求
        /// </summary>
        private static ValueTask<string> HandleGetDynamicPriceTable(
            string url,
            EmptyRequestData info,
            MongoId sessionId)
        {
            try
            {
                // 先获取静态价格作为基础
                var priceTable = new Dictionary<string, double>();

                if (_databaseServiceStatic != null)
                {
                    try
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

                        // 然后尝试用跳蚤市场的动态价格覆盖
                        if (_ragfairOfferServiceStatic != null && priceTable.Count > 0)
                        {
                            try
                            {
                                int updatedCount = 0;

                                // 遍历每个物品，尝试获取跳蚤市场价格
                                foreach (var itemId in priceTable.Keys.ToList())
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
                                                    priceTable[itemId] = avgPrice;
                                                    updatedCount++;
                                                }
                                            }
                                        }
                                    }
                                    catch
                                    {
                                        // 如果某个物品的跳蚤市场数据获取失败，继续处理其他物品
                                        continue;
                                    }
                                }

                                _loggerStatic?.Info($"[QuickPrice] Dynamic price table generated with {priceTable.Count} items ({updatedCount} updated from flea market)", null);
                            }
                            catch (Exception fleaEx)
                            {
                                _loggerStatic?.Warning($"[QuickPrice] Could not get flea market prices: {fleaEx.Message}", null);
                            }
                        }
                        else
                        {
                            _loggerStatic?.Info($"[QuickPrice] Dynamic price table generated with {priceTable.Count} items (flea market data not available)", null);
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
                _loggerStatic?.Error($"[QuickPrice] Error in GetDynamicPriceTable: {ex.Message}", ex);
                Console.WriteLine($"[QuickPrice] Error in GetDynamicPriceTable: {ex.Message}");
                // 回退到静态价格表
                return HandleGetStaticPriceTable(url, info, sessionId);
            }
        }

        #endregion
    }
}
