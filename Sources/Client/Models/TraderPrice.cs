using EFT;

namespace QuickPrice.Models
{
    /// <summary>
    /// 商人价格信息
    /// </summary>
    public class TraderPrice
    {
        public TraderPrice(
            string traderId,
            string traderName,
            int amount,
            MongoID currencyId,
            double currencyCourse,
            double priceInRoubles)
        {
            TraderId = traderId;
            TraderName = traderName;
            Amount = amount;
            CurrencyId = currencyId;
            CurrencyCourse = currencyCourse;
            PriceInRoubles = priceInRoubles;
        }

        /// <summary>
        /// 商人ID
        /// </summary>
        public string TraderId { get; }

        /// <summary>
        /// 商人名称（本地化）
        /// </summary>
        public string TraderName { get; }

        /// <summary>
        /// 价格数额
        /// </summary>
        public int Amount { get; }

        /// <summary>
        /// 货币ID
        /// </summary>
        public MongoID CurrencyId { get; }

        /// <summary>
        /// 货币汇率
        /// </summary>
        public double CurrencyCourse { get; }

        /// <summary>
        /// 卢布价格（用于对比）
        /// </summary>
        public double PriceInRoubles { get; }

        /// <summary>
        /// 货币符号
        /// </summary>
        public string CurrencySymbol
        {
            get
            {
                try
                {
                    return GClass3130.GetCurrencyCharById(CurrencyId);
                }
                catch
                {
                    return "₽";
                }
            }
        }
    }
}
