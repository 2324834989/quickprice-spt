using QuickPrice.Config;
using JsonType;

namespace QuickPrice.Utils
{
    /// <summary>
    /// 护甲等级颜色编码
    /// </summary>
    public static class ArmorColorCoding
    {
        // 护甲等级颜色（与价格颜色相同的色系）
        private const string COLOR_GRAY = "999999";    // 1级
        private const string COLOR_GREEN = "4CAF50";   // 2级
        private const string COLOR_BLUE = "2196F3";    // 3级
        private const string COLOR_PURPLE = "9C27B0";  // 4级
        private const string COLOR_ORANGE = "FF9800";  // 5级
        private const string COLOR_RED = "F44336";     // 6级

        /// <summary>
        /// 根据护甲等级获取颜色
        /// </summary>
        public static string GetColorForArmorClass(int armorClass)
        {
            switch (armorClass)
            {
                case 1: return COLOR_GRAY;
                case 2: return COLOR_GREEN;
                case 3: return COLOR_BLUE;
                case 4: return COLOR_PURPLE;
                case 5: return COLOR_ORANGE;
                case 6: return COLOR_RED;
                default: return COLOR_GRAY; // 未知等级使用灰色
            }
        }

        /// <summary>
        /// 应用护甲等级颜色
        /// </summary>
        public static string ApplyArmorClassColor(string text, int armorClass)
        {
            if (!Settings.EnableColorCoding.Value)
                return text;

            if (!Settings.EnableArmorClassColoring.Value)
                return text;

            string color = GetColorForArmorClass(armorClass);
            return $"<color=#{color}>{text}</color>";
        }
    }
}
