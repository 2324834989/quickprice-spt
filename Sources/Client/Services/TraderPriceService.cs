using System;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using QuickPrice.Models;
using QuickPrice.Extensions;

namespace QuickPrice.Services
{
    /// <summary>
    /// 商人价格服务
    /// 负责获取商人收购价格
    /// </summary>
    public class TraderPriceService
    {
        private static TraderPriceService _instance;
        public static TraderPriceService Instance => _instance ??= new TraderPriceService();

        private bool _hasShownInitTip = false;  // 是否已显示初始化提示

        private TraderPriceService() { }

        /// <summary>
        /// 获取最佳商人收购价格
        /// </summary>
        /// <param name="item">物品</param>
        /// <returns>最高收购价格，如果没有商人收购则返回 null</returns>
        public TraderPrice GetBestTraderPrice(Item item)
        {
            try
            {
                TraderPrice highestPrice = null;

                // 获取所有商人
                var traders = GetAllTraders();
                if (traders == null || !traders.Any())
                {
                    if (!_hasShownInitTip)
                    {
                        Plugin.Log.LogInfo("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                        Plugin.Log.LogInfo("💡 首次使用商人价格功能");
                        Plugin.Log.LogInfo("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                        Plugin.Log.LogInfo("   请打开任意商人界面（如 Prapor、Therapist）");
                        Plugin.Log.LogInfo("   然后关闭界面，商人价格功能即可正常使用");
                        Plugin.Log.LogInfo("   💡 此步骤每次游戏启动只需执行一次");
                        Plugin.Log.LogInfo("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                        _hasShownInitTip = true;
                    }
                    return null;
                }

                // 遍历所有商人
                foreach (TraderClass trader in traders)
                {
                    // 检查商人是否可用
                    if (!IsTraderAvailable(trader))
                        continue;

                    try
                    {
                        // 克隆物品并设置数量为1（获取单价）
                        Item singleItem = item.CloneItem();
                        singleItem.StackObjectsCount = 1;

                        // 获取商人收购价格
                        var priceStruct = trader.GetUserItemPrice(singleItem);
                        if (!priceStruct.HasValue)
                            continue;

                        // 获取价格金额和货币ID
                        int amount = priceStruct.Value.Amount;
                        MongoID? currencyIdNullable = priceStruct.Value.CurrencyId;

                        // 如果货币ID为null，跳过此商人
                        if (!currencyIdNullable.HasValue)
                            continue;

                        MongoID currencyId = currencyIdNullable.Value;

                        // 获取货币汇率
                        double currencyCourse = GetCurrencyCourse(trader, currencyId);

                        // 计算卢布价格（用于对比）
                        double priceInRoubles = amount * currencyCourse;

                        // 保存最高价格
                        if (highestPrice == null || priceInRoubles > highestPrice.PriceInRoubles)
                        {
                            highestPrice = new TraderPrice(
                                trader.Id,
                                trader.LocalizedName,
                                amount,
                                currencyId,
                                currencyCourse,
                                priceInRoubles
                            );
                        }
                    }
                    catch (Exception)
                    {
                        // 静默跳过失败的商人
                        continue;
                    }
                }

                // 如果第一次使用时没有找到价格，给出友好提示
                if (highestPrice == null && !_hasShownInitTip)
                {
                    Plugin.Log.LogInfo("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                    Plugin.Log.LogInfo("💡 首次使用商人价格功能");
                    Plugin.Log.LogInfo("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                    Plugin.Log.LogInfo("   请打开任意商人界面（如 Prapor、Therapist）");
                    Plugin.Log.LogInfo("   然后关闭界面，商人价格功能即可正常使用");
                    Plugin.Log.LogInfo("   此步骤每次游戏启动只需执行一次");
                    Plugin.Log.LogInfo("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                    _hasShownInitTip = true;
                }

                return highestPrice;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"❌ 获取商人价格失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取所有商人
        /// </summary>
        private System.Collections.Generic.IEnumerable<TraderClass> GetAllTraders()
        {
            try
            {
                // 使用 Singleton 获取 ClientApplication
                var clientApp = Singleton<ClientApplication<ISession>>.Instance;
                if (clientApp == null)
                    return null;

                var session = clientApp.GetClientBackEndSession();
                if (session != null && session.Traders != null)
                    return session.Traders;

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// 检查商人是否可用
        /// </summary>
        private bool IsTraderAvailable(TraderClass trader)
        {
            try
            {
                return trader != null
                    && trader.Info != null
                    && trader.Info.Available
                    && !trader.Info.Disabled
                    && trader.Info.Unlocked;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取货币汇率
        /// </summary>
        private double GetCurrencyCourse(TraderClass trader, MongoID currencyId)
        {
            try
            {
                var supplyData = trader.GetSupplyData();
                if (supplyData?.CurrencyCourses != null
                    && supplyData.CurrencyCourses.ContainsKey(currencyId))
                {
                    return supplyData.CurrencyCourses[currencyId];
                }

                // 默认汇率为 1（卢布）
                return 1.0;
            }
            catch
            {
                return 1.0;
            }
        }
    }
}
