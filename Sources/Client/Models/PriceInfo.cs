namespace QuickPrice.Models
{
    /// <summary>
    /// 价格信息模型
    /// </summary>
    public class PriceInfo
    {
        /// <summary>
        /// 价格来源名称（如"跳蚤市场"）
        /// </summary>
        public string SourceName { get; set; }

        /// <summary>
        /// 总价格
        /// </summary>
        public double TotalPrice { get; set; }

        /// <summary>
        /// 单格价格
        /// </summary>
        public double PricePerSlot { get; set; }

        /// <summary>
        /// 是否是最佳价格
        /// </summary>
        public bool IsBestPrice { get; set; }
    }
}
