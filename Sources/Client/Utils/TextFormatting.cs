using System;

namespace QuickPrice.Utils
{
    /// <summary>
    /// 文本格式化工具
    /// </summary>
    public static class TextFormatting
    {
        /// <summary>
        /// 格式化价格为卢布格式: ₽45,000
        /// </summary>
        public static string FormatPrice(double price)
        {
            return $"₽{price:#,0}";
        }

        /// <summary>
        /// 格式化价格为紧凑格式: ₽45k, ₽1.5M
        /// </summary>
        public static string FormatPriceCompact(double price)
        {
            if (price >= 1000000)
                return $"₽{price / 1000000:0.#}M";
            if (price >= 1000)
                return $"₽{price / 1000:0.#}k";
            return $"₽{price:0}";
        }

        /// <summary>
        /// 为文本添加粗体标签
        /// </summary>
        public static string Bold(string text)
        {
            return $"<b>{text}</b>";
        }

        /// <summary>
        /// 为文本添加斜体标签
        /// </summary>
        public static string Italic(string text)
        {
            return $"<i>{text}</i>";
        }
    }
}
