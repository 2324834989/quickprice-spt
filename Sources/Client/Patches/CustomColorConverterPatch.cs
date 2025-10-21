using System;
using System.Linq;
using System.Reflection;
using SPT.Reflection.Patching;
using JsonType;
using UnityEngine;

namespace QuickPrice.Patches
{
    /// <summary>
    /// 自定义颜色转换补丁
    ///
    /// 功能：将自定义的 TaxonomyColor 枚举值转换为 Unity Color
    ///
    /// 原理说明：
    /// 1. TaxonomyColor 是 EFT 游戏中预定义的 9 种颜色枚举（白、绿、蓝、紫、橙、红、黄、黑、灰）
    /// 2. 我们通过"偏移编码"技术来扩展这个枚举，支持任意 RGB 颜色：
    ///    自定义颜色值 = RGB颜色代码 + TaxonomyColor枚举数量(9)
    ///    例如：亮红色 #FF0000 = 0xFF0000 (16711680) + 9 = 16711689
    ///
    /// 3. 游戏在渲染物品颜色时，会调用 ToColor(TaxonomyColor) 方法
    /// 4. 本补丁拦截这个方法，检测到非标准枚举值时：
    ///    - 减去偏移量 9，得到原始 RGB 值
    ///    - 将 RGB 值转换为十六进制字符串
    ///    - 使用 HexToColor 方法转换为 Unity Color32
    ///
    /// 示例流程：
    /// ItemBackgroundColorPatch 返回 TaxonomyColor(16711689)
    ///     ↓
    /// 游戏调用 ToColor(16711689)
    ///     ↓
    /// 本补丁拦截：16711689 - 9 = 16711680
    ///     ↓
    /// 转换为十六进制：16711680.ToString("X6") = "FF0000"
    ///     ↓
    /// 解析 RGB：R=255, G=0, B=0
    ///     ↓
    /// 返回 Unity Color32(255, 0, 0, 255) - 亮红色
    ///
    /// 注意：
    /// - 这个补丁复制自 ColorConverterAPI 项目，无需依赖外部插件
    /// - 支持 6 位 RGB 格式（#RRGGBB）和 8 位 RGBA 格式（#RRGGBBAA）
    /// - 如果枚举值是标准的 TaxonomyColor 值，则跳过补丁，使用游戏原始逻辑
    /// </summary>
    public class CustomColorConverterPatch : ModulePatch
    {
        /// <summary>
        /// 获取目标方法：ToColor(TaxonomyColor)
        /// 这是 EFT 游戏中负责将 TaxonomyColor 枚举转换为 Unity Color 的静态方法
        /// </summary>
        protected override MethodBase GetTargetMethod()
        {
            // 在 EFT.AbstractGame 程序集中查找包含 ToColor 静态方法的类型
            return typeof(EFT.AbstractGame).Assembly.GetTypes()
                .Single(type => type.GetMethod("ToColor", BindingFlags.Public | BindingFlags.Static) != null)
                .GetMethod("ToColor", BindingFlags.Public | BindingFlags.Static);
        }

        /// <summary>
        /// 将 6 位十六进制颜色字符串转换为 Unity Color32（不带透明度）
        /// </summary>
        /// <param name="hexColor">6 位十六进制字符串，例如 "FF0000" 表示红色</param>
        /// <returns>Unity Color32，透明度固定为 255（完全不透明）</returns>
        /// <example>
        /// HexToColor("FF0000") → Color32(255, 0, 0, 255) - 红色
        /// HexToColor("00FF00") → Color32(0, 255, 0, 255) - 绿色
        /// HexToColor("0000FF") → Color32(0, 0, 255, 255) - 蓝色
        /// </example>
        private static Color32 HexToColor(string hexColor)
        {
            // 解析红色通道（前2位）
            var r = Convert.ToByte(hexColor.Substring(0, 2), 16);
            // 解析绿色通道（中间2位）
            var g = Convert.ToByte(hexColor.Substring(2, 2), 16);
            // 解析蓝色通道（后2位）
            var b = Convert.ToByte(hexColor.Substring(4, 2), 16);

            // 返回 Color32，透明度默认为 255（完全不透明）
            return new Color32(r, g, b, 255);
        }

        /// <summary>
        /// 将 8 位十六进制颜色字符串转换为 Unity Color32（带透明度）
        /// </summary>
        /// <param name="hexColor">8 位十六进制字符串，例如 "FF000080" 表示半透明红色</param>
        /// <returns>Unity Color32，包含自定义透明度</returns>
        /// <example>
        /// HexToColorAlpha("FF0000FF") → Color32(255, 0, 0, 255) - 不透明红色
        /// HexToColorAlpha("FF000080") → Color32(255, 0, 0, 128) - 半透明红色
        /// HexToColorAlpha("00FF0000") → Color32(0, 255, 0, 0) - 完全透明绿色
        /// </example>
        private static Color32 HexToColorAlpha(string hexColor)
        {
            // 解析红色通道（第1-2位）
            var r = Convert.ToByte(hexColor.Substring(0, 2), 16);
            // 解析绿色通道（第3-4位）
            var g = Convert.ToByte(hexColor.Substring(2, 2), 16);
            // 解析蓝色通道（第5-6位）
            var b = Convert.ToByte(hexColor.Substring(4, 2), 16);
            // 解析透明度通道（第7-8位）
            var a = Convert.ToByte(hexColor.Substring(6, 2), 16);

            // 返回 Color32，包含自定义透明度
            return new Color32(r, g, b, a);
        }

        /// <summary>
        /// Harmony 前置补丁：拦截 ToColor 方法
        /// 检测自定义颜色值并转换为 Unity Color
        /// </summary>
        /// <param name="__result">方法返回值（通过 ref 修改）</param>
        /// <param name="taxonomyColor">输入的 TaxonomyColor 枚举值</param>
        /// <returns>true = 执行原始方法，false = 跳过原始方法</returns>
        [PatchPrefix]
        private static bool PrePatch(ref Color __result, JsonType.TaxonomyColor taxonomyColor)
        {
            // ===== 步骤1：检查是否为标准枚举值 =====
            // 如果是游戏预定义的 9 种颜色之一，则使用游戏原始逻辑
            if (Enum.IsDefined(typeof(JsonType.TaxonomyColor), taxonomyColor))
            {
                Plugin.Log.LogDebug($"🎨 标准颜色: {taxonomyColor}");
                return true; // 执行原始方法
            }

            // ===== 步骤2：解码自定义颜色值 =====
            // 获取枚举的整数值
            var colorCodeAsInt = (int)taxonomyColor;

            // 减去偏移量，得到原始 RGB 值
            // 偏移量 = TaxonomyColor 枚举的数量（9）
            colorCodeAsInt -= Enum.GetValues(typeof(TaxonomyColor)).Length;

            Plugin.Log.LogDebug($"🎨 自定义颜色检测: 枚举值={(int)taxonomyColor}, RGB值={colorCodeAsInt}");

            // ===== 步骤3：转换为十六进制字符串 =====
            // "X6" 格式：转换为 6 位十六进制（不足6位前面补0）
            // 例如：16711680 → "FF0000"
            var colorCode = colorCodeAsInt.ToString("X6");

            Plugin.Log.LogDebug($"🎨 颜色代码: #{colorCode} (长度: {colorCode.Length})");

            // ===== 步骤4：根据长度选择转换方法 =====
            if (colorCode.Length == 6)
            {
                // 6 位：RGB 格式（#RRGGBB）
                __result = HexToColor(colorCode);
                Plugin.Log.LogDebug($"🎨 RGB颜色: #{colorCode} → R={__result.r}, G={__result.g}, B={__result.b}");
            }
            else if (colorCode.Length == 8)
            {
                // 8 位：RGBA 格式（#RRGGBBAA）
                __result = HexToColorAlpha(colorCode);
                Plugin.Log.LogDebug($"🎨 RGBA颜色: #{colorCode} → R={__result.r}, G={__result.g}, B={__result.b}, A={__result.a}");
            }
            else
            {
                // 异常长度：输出警告并使用白色
                Plugin.Log.LogWarning($"⚠️ 颜色代码长度异常: #{colorCode} (长度: {colorCode.Length})");
                __result = new Color32(255, 255, 255, 255); // 白色
            }

            // 跳过原始方法，使用我们计算的颜色
            return false;
        }
    }
}
