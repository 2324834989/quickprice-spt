using QuickPrice.Config;
using JsonType;

namespace QuickPrice.Utils
{
    /// <summary>
    /// 子弹穿甲等级颜色编码
    /// </summary>
    public static class AmmoColorCoding
    {
        // 穿甲等级颜色（与价格颜色相同的色系）
        private const string COLOR_WHITE = "FFFFFF";   // < 15
        private const string COLOR_GREEN = "4CAF50";   // < 25
        private const string COLOR_BLUE = "2196F3";    // < 35
        private const string COLOR_PURPLE = "9C27B0";  // < 45
        private const string COLOR_ORANGE = "FF9800";  // < 55
        private const string COLOR_RED = "F44336";     // >= 55

        /// <summary>
        /// 根据穿甲值获取颜色（使用配置的阈值）
        /// </summary>
        public static string GetColorForPenetration(int penetration)
        {
            if (penetration < Settings.PenetrationThreshold1.Value) return COLOR_WHITE;
            if (penetration < Settings.PenetrationThreshold2.Value) return COLOR_GREEN;
            if (penetration < Settings.PenetrationThreshold3.Value) return COLOR_BLUE;
            if (penetration < Settings.PenetrationThreshold4.Value) return COLOR_PURPLE;
            if (penetration < Settings.PenetrationThreshold5.Value) return COLOR_ORANGE;
            return COLOR_RED;
        }

        /// <summary>
        /// 应用穿甲等级颜色
        /// </summary>
        public static string ApplyPenetrationColor(string text, int penetration)
        {
            if (!Settings.EnableColorCoding.Value)
                return text;

            if (!Settings.UseCaliberPenetrationPower.Value)
                return text;

            string color = GetColorForPenetration(penetration);
            return $"<color=#{color}>{text}</color>";
        }
    }
}
