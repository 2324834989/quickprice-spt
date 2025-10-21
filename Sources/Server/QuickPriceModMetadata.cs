// ----------------------------------------------------------------------------
// QuickPrice - Mod Metadata
// 模组元数据定义
// ----------------------------------------------------------------------------

using SPTarkov.Server.Core.Models.Spt.Mod;
using SemanticVersioning;
using System.Collections.Generic;

namespace QuickPrice.Server
{
    /// <summary>
    /// QuickPrice 模组元数据类
    /// 提供模组的基本信息给SPT服务器
    /// </summary>
    public record QuickPriceModMetadata : AbstractModMetadata
    {
        /// <summary>
        /// Mod的唯一标识符
        /// </summary>
        public override string ModGuid { get; init; } = "com.quickprice.spt";

        /// <summary>
        /// Mod的名称
        /// </summary>
        public override string Name { get; init; } = "QuickPrice";

        /// <summary>
        /// Mod的作者
        /// </summary>
        public override string Author { get; init; } = "QuickPrice Team";

        /// <summary>
        /// Mod的版本号
        /// </summary>
        public override SemanticVersioning.Version Version { get; init; } = SemanticVersioning.Version.Parse("1.0.0");

        /// <summary>
        /// 支持的SPT版本范围
        /// </summary>
        public override SemanticVersioning.Range SptVersion { get; init; } = SemanticVersioning.Range.Parse("4.0.0");

        /// <summary>
        /// 贡献者列表
        /// </summary>
        public override List<string>? Contributors { get; init; }

        /// <summary>
        /// Mod的URL
        /// </summary>
        public override string? Url { get; init; } = "https://github.com/quickprice/quickprice-spt";

        /// <summary>
        /// Mod的许可证
        /// </summary>
        public override string? License { get; init; } = "MIT";

        /// <summary>
        /// 是否为捆绑包mod
        /// </summary>
        public override bool? IsBundleMod { get; init; } = false;

        /// <summary>
        /// Mod依赖关系
        /// </summary>
        public override Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }

        /// <summary>
        /// 不兼容的mod列表
        /// </summary>
        public override List<string>? Incompatibilities { get; init; } = new List<string>();
    }
}
