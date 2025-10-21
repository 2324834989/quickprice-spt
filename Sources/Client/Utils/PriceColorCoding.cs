using QuickPrice.Config;
using JsonType;

namespace QuickPrice.Utils
{
    /// <summary>
    /// 价格颜色编码工具
    /// </summary>
    public static class PriceColorCoding
    {
        // 固定颜色方案（6级）
        private const string COLOR_WHITE = "FFFFFF";   // ≤ 3,000 - 垃圾
        private const string COLOR_GREEN = "4CAF50";   // ≤ 10,000 - 普通
        private const string COLOR_BLUE = "2196F3";    // ≤ 20,000 - 良好
        private const string COLOR_PURPLE = "9C27B0";  // ≤ 50,000 - 稀有
        private const string COLOR_ORANGE = "FF9800";  // ≤ 100,000 - 史诗
        private const string COLOR_RED = "F44336";     // ≥ 100,000 - 传说

        /// <summary>
        /// 根据价格获取对应颜色代码（使用配置的阈值）
        /// </summary>
        public static string GetColorForPrice(double price)
        {
            if (price <= Settings.PriceThreshold1.Value) return COLOR_WHITE;
            if (price <= Settings.PriceThreshold2.Value) return COLOR_GREEN;
            if (price <= Settings.PriceThreshold3.Value) return COLOR_BLUE;
            if (price <= Settings.PriceThreshold4.Value) return COLOR_PURPLE;
            if (price <= Settings.PriceThreshold5.Value) return COLOR_ORANGE;
            return COLOR_RED;
        }

        /// <summary>
        /// 为文本应用颜色标签
        /// </summary>
        public static string ApplyColor(string text, double price)
        {
            if (!Settings.EnableColorCoding.Value)
                return text;

            string color = GetColorForPrice(price);
            return $"<color=#{color}>{text}</color>";
        }

        /// <summary>
        /// 获取价格等级名称（用于调试）
        /// </summary>
        public static string GetPriceTier(double price)
        {
            if (price <= 3000) return "垃圾";
            if (price <= 10000) return "普通";
            if (price <= 20000) return "良好";
            if (price <= 50000) return "稀有";
            if (price <= 100000) return "史诗";
            return "传说";
        }

        /// <summary>
        /// 根据价格获取对应的 TaxonomyColor 枚举值（用于物品背景色）
        /// 使用配置文件中的价格阈值
        /// </summary>
        public static TaxonomyColor GetBackgroundColorForPrice(double price)
        {
            // 使用配置的价格阈值来决定颜色等级
            if (price <= Settings.PriceThreshold1.Value)
                return TaxonomyColor.@default;      // 白色 (≤ 3,000)

            if (price <= Settings.PriceThreshold2.Value)
                return TaxonomyColor.green;         // 绿色 (≤ 10,000)

            if (price <= Settings.PriceThreshold3.Value)
                return TaxonomyColor.blue;          // 蓝色 (≤ 20,000)

            if (price <= Settings.PriceThreshold4.Value)
                return TaxonomyColor.violet;        // 紫色 (≤ 50,000)

            if (price <= Settings.PriceThreshold5.Value)
                return TaxonomyColor.orange;        // 橙色 (≤ 100,000)

            return TaxonomyColor.red;               // 红色 (> 100,000)
        }
    }
}
