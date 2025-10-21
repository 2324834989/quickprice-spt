# Show Me The Money - Server Mod (SPT 4.0.0)

## 当前状态

✅ **编译成功！** - 框架已完成，等待HTTP路由实现

## 快速开始

### 编译

```bash
cd "D:\C#\spt-show-me-the-money-main\ServerMod"
dotnet build SwiftXP.SPT.ShowMeTheMoney.Server.csproj -c Release
```

### 安装（等待完成后）

1. 将以下文件复制到 SPT服务器的 `user/mods/showmethemoney/` 目录：
   - `bin/Release/swiftxp-showmethemoney.dll`
   - `bin/Release/mod.json`

2. 启动SPT服务器，查看日志确认加载成功

## 文件结构

```
ServerMod/
├── ShowMeTheMoneyServerMod.cs       - 主代码（已完成）
├── ShowMeTheMoneyModMetadata.cs     - 元数据（已完成）
├── mod.json                          - 模组配置（已完成）
├── SwiftXP.SPT.ShowMeTheMoney.Server.csproj  - 项目文件（已完成）
├── UPGRADE_SUCCESS_REPORT.md        - 详细升级报告
├── UPGRADE_TO_4.0.0.md              - 升级文档
└── README_SERVER_MOD.md             - 本文件
```

## 待完成

- [ ] HTTP路由注册（关键）
- [ ] 业务逻辑实现

详见 `UPGRADE_SUCCESS_REPORT.md`
