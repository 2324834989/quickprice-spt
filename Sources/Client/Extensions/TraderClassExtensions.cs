using System.Reflection;
using EFT;

namespace QuickPrice.Extensions
{
    /// <summary>
    /// TraderClass 扩展方法
    /// </summary>
    public static class TraderClassExtensions
    {
        private static readonly FieldInfo SupplyDataField =
            typeof(TraderClass).GetField("SupplyData_0", BindingFlags.Public | BindingFlags.Instance);

        /// <summary>
        /// 获取商人的 SupplyData（包含货币汇率）
        /// </summary>
        public static SupplyData GetSupplyData(this TraderClass trader)
        {
            return SupplyDataField?.GetValue(trader) as SupplyData;
        }
    }
}
