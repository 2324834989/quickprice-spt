// ----------------------------------------------------------------------------
// QuickPrice - SPT 4.0.0 Server Mod
// 为客户端BepInEx插件提供价格数据的服务端模组
// ----------------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using SPTarkov.Server.Core.Models.External;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.DI.Annotations;
using System.Collections.Generic;
using System.Linq;

namespace QuickPrice.Server
{
    /// <summary>
    /// QuickPrice 服务端模组主类
    /// 实现IPreSptLoadModAsync接口，在服务器启动早期阶段加载
    /// </summary>
    [Injectable(InjectionType.Singleton, null, 2147483647)]
    public class QuickPriceServerMod : IPreSptLoadModAsync
    {
        private readonly ISptLogger<QuickPriceServerMod> _logger;
        private readonly QuickPriceStaticRouter _staticRouter;

        /// <summary>
        /// 构造函数，通过依赖注入获取日志记录器和自定义路由器
        /// </summary>
        public QuickPriceServerMod(
            ISptLogger<QuickPriceServerMod> logger,
            QuickPriceStaticRouter staticRouter)
        {
            _logger = logger;
            _staticRouter = staticRouter;
        }

        /// <summary>
        /// 实现IPreSptLoadModAsync接口的核心方法
        /// 在SPT服务器启动的早期阶段被自动调用
        /// </summary>
        public async Task PreSptLoadAsync()
        {
            try
            {
                _logger.Info("[QuickPrice v1.0.0] Server mod loading...", null);

                // 路由已通过依赖注入自动注册
                // QuickPriceStaticRouter 在构造时自动注册以下端点：
                // 1. /showMeTheMoney/getCurrencyPurchasePrices
                // 2. /showMeTheMoney/getStaticPriceTable
                // 3. /showMeTheMoney/getDynamicPriceTable

                _logger.Success("[QuickPrice v1.0.0] Server mod loaded successfully. Ready to make some money...", null);
                _logger.Info("[QuickPrice] HTTP routes registered successfully", null);
            }
            catch (Exception ex)
            {
                try
                {
                    _logger.Error("[QuickPrice] PreSptLoadAsync方法发生异常", ex);
                }
                catch
                {
                    // 如果logger也不可用，使用备用方式
                    try
                    {
                        Console.WriteLine("[QuickPrice] PreSptLoadAsync方法发生异常");
                        Console.WriteLine(ex.Message);
                        Console.WriteLine(ex.StackTrace);
                    }
                    catch { /* 忽略无法输出的情况 */ }
                }
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// 保留原有的静态Load方法，确保兼容性
        /// </summary>
        public static void Load()
        {
            // 空实现，保留以确保兼容性
        }

        #region 业务逻辑方法 (暂时注释，等待路由注册完成后启用)

        /*
        /// <summary>
        /// 获取货币购买价格（美元和欧元）
        /// </summary>
        public CurrencyPurchasePrices GetCurrencyPurchasePrices()
        {
            try
            {
                // TODO: 需要确认4.0.0中DatabaseService的正确用法
                // var peacekeeper = _databaseService.GetTrader("5935c25fb3acc3127c3d8cd9");
                // var skier = _databaseService.GetTrader("58330581ace78e27b8b10cee");

                // 暂时返回默认值
                _logger.Debug("[QuickPrice] Currency prices - EUR: 153, USD: 139", null);

                return new CurrencyPurchasePrices
                {
                    Eur = 153,
                    Usd = 139
                };
            }
            catch (Exception ex)
            {
                _logger.Error($"[QuickPrice] Error getting currency purchase prices: {ex.Message}", ex);
                return new CurrencyPurchasePrices { Eur = 153, Usd = 139 };
            }
        }

        /// <summary>
        /// 获取静态价格表
        /// </summary>
        public Dictionary<string, double> GetStaticPriceTable()
        {
            try
            {
                // TODO: 需要确认4.0.0中数据库访问的正确API
                var clonedPriceTable = new Dictionary<string, double>();

                _logger.Info($"[QuickPrice] Generated static price table with {clonedPriceTable.Count} items", null);

                return clonedPriceTable;
            }
            catch (Exception ex)
            {
                _logger.Error($"[QuickPrice] Error getting static price table: {ex.Message}", ex);
                return new Dictionary<string, double>();
            }
        }

        /// <summary>
        /// 获取动态价格表（结合实际跳蚤市场报价）
        /// </summary>
        public Dictionary<string, double> GetDynamicPriceTable()
        {
            try
            {
                var priceTable = GetStaticPriceTable();

                _logger.Info($"[QuickPrice] Generated dynamic price table with {priceTable.Count} items", null);

                return priceTable;
            }
            catch (Exception ex)
            {
                _logger.Error($"[QuickPrice] Error getting dynamic price table: {ex.Message}", ex);
                return GetStaticPriceTable();
            }
        }
        */

        #endregion
    }

    #region 数据模型

    /// <summary>
    /// 货币购买价格模型
    /// </summary>
    public class CurrencyPurchasePrices
    {
        [System.Text.Json.Serialization.JsonPropertyName("eur")]
        public double Eur { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("usd")]
        public double Usd { get; set; }
    }

    #endregion
}
