using System;
using EFT.InventoryLogic;
using QuickPrice.Services;

namespace QuickPrice.Utils
{
    /// <summary>
    /// 跳蚤市场辅助类 - 用于检测物品是否可以在跳蚤市场上出售
    /// v2.0: 使用服务端数据，游戏启动时异步加载，无卡顿
    /// </summary>
    public static class RagfairHelper
    {
        /// <summary>
        /// 检查物品是否可以在跳蚤市场上出售
        /// </summary>
        /// <param name="item">要检查的物品</param>
        /// <returns>true = 可以出售, false = 禁止出售, null = 数据加载中</returns>
        public static bool? CanSellOnRagfair(Item item)
        {
            if (item == null)
                return null;

            // 从服务端数据检查（数据在游戏启动时已异步加载）
            var isBanned = PriceDataService.Instance.IsRagfairBanned(item.TemplateId);

            if (isBanned.HasValue)
            {
                // 返回反转值：isBanned=true 表示禁售，返回 false(不可售)
                return !isBanned.Value;
            }

            // 数据尚未加载完成，默认返回可售
            return true;
        }

        /// <summary>
        /// 获取跳蚤市场禁售标签文本
        /// </summary>
        /// <param name="item">物品</param>
        /// <returns>如果禁售返回标签文本，否则返回空字符串</returns>
        public static string GetRagfairBanLabel(Item item)
        {
            var canSell = CanSellOnRagfair(item);

            if (canSell.HasValue && !canSell.Value)
            {
                // 物品不能在跳蚤市场出售
                return "\n<color=#FF6B6B>[跳蚤禁售]</color>";
            }

            return "";
        }

        /// <summary>
        /// 检查物品是否可以在跳蚤市场上出售（用于显示价格判断）
        /// </summary>
        /// <param name="item">要检查的物品</param>
        /// <returns>true = 可以显示跳蚤价格, false = 禁售不显示跳蚤价格</returns>
        public static bool ShouldShowRagfairPrice(Item item)
        {
            var canSell = CanSellOnRagfair(item);

            // 如果无法确定（null），默认显示价格
            // 如果明确禁售（false），则不显示
            return !canSell.HasValue || canSell.Value;
        }
    }
}
