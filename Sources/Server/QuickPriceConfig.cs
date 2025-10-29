// ----------------------------------------------------------------------------
// QuickPrice - Server Configuration Model
// 服务端配置模型
// ----------------------------------------------------------------------------

using System.Text.Json.Serialization;

namespace QuickPrice.Server
{
    /// <summary>
    /// QuickPrice 服务端配置模型
    /// 对应 config.json 文件结构
    /// </summary>
    public class QuickPriceConfig
    {
        /// <summary>
        /// 是否启用模组
        /// </summary>
        [JsonPropertyName("Enabled")]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 日志级别
        /// </summary>
        [JsonPropertyName("LogLevel")]
        public string LogLevel { get; set; } = "Info";

        /// <summary>
        /// 缓存超时时间（秒）
        /// </summary>
        [JsonPropertyName("CacheTimeoutSeconds")]
        public int CacheTimeoutSeconds { get; set; } = 300;

        /// <summary>
        /// 自动刷新间隔（分钟）
        /// 默认5分钟，设置为0则禁用自动刷新
        /// </summary>
        [JsonPropertyName("AutoRefreshIntervalMinutes")]
        public int AutoRefreshIntervalMinutes { get; set; } = 5;

        /// <summary>
        /// 配置说明
        /// </summary>
        [JsonPropertyName("Notes")]
        public string Notes { get; set; } = "QuickPrice 服务端配置文件";
    }
}
