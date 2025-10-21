# Show Me The Money - SPT 4.0.0 升级说明

## 升级状态总览

### ✅ 已完成的部分 (约70%)

1. **项目配置文件** (`SwiftXP.SPT.ShowMeTheMoney.Server.csproj`)
   - ✅ 目标框架：.NET 9.0
   - ✅ DLL引用：使用本地SPT安装路径
   - ✅ 输出配置：正确设置输出目录

2. **模组元数据** (`ModMetadata` record)
   - ✅ ModGuid: `com.swiftxp.showmethemoney`
   - ✅ 版本: 2.0.0
   - ✅ SPT版本要求: ~4.0.0
   - ✅ 作者和许可证信息

3. **依赖注入框架**
   - ✅ 使用 `[Injectable]` 特性
   - ✅ 构造函数注入所需服务
   - ✅ 实现 `IOnLoad` 接口

4. **业务逻辑方法** (从TypeScript迁移到C#)
   - ✅ `GetCurrencyPurchasePrices()` - 获取美元和欧元购买价格
   - ✅ `GetStaticPriceTable()` - 生成静态价格表
   - ✅ `GetDynamicPriceTable()` - 生成动态价格表（结合实际跳蚤报价）

5. **数据模型**
   - ✅ `CurrencyPurchasePrices` 类

---

## ⚠️ 待完成的部分 (约30%)

### 🔴 关键缺失：HTTP路由注册

**问题描述**：
目前不清楚SPT 4.0.0如何注册HTTP路由端点。需要实现以下三个路由：

1. `/showMeTheMoney/getCurrencyPurchasePrices`
2. `/showMeTheMoney/getStaticPriceTable`
3. `/showMeTheMoney/getDynamicPriceTable`

**3.x (TypeScript) 的实现方式**：
```typescript
staticRouterModService.registerStaticRouter(
    "ShowMeTheMoneyRoutes-GetCurrencyPurchasePrices",
    [
        {
            url: "/showMeTheMoney/getCurrencyPurchasePrices",
            action: (url, info, sessionId, output) => {
                return new Promise((resolve) => {
                    const result = JSON.stringify(this.getCurrencyPurchasePrices());
                    resolve(result);
                });
            }
        }
    ],
    "Static-ShowMeTheMoneyRoutes"
);
```

**4.0.0 (C#) 可能的实现方式** (需要验证)：
```csharp
// 选项1: 使用某种路由服务
staticRouterService.RegisterRoute(
    "/showMeTheMoney/getCurrencyPurchasePrices",
    HandleGetCurrencyPurchasePrices
);

// 选项2: 使用特性路由
[HttpGet("/showMeTheMoney/getCurrencyPurchasePrices")]
public string HandleGetCurrencyPurchasePrices() { ... }

// 选项3: 其他方式（需要查阅文档或示例）
```

---

## 🔍 需要的额外信息

### 1. SPT 4.0.0 API 文档
需要以下类的API文档：
- `DatabaseService` - 数据库访问方法
- `RagfairOfferService` - 跳蚤市场报价服务
- `StaticRouterModService` (如果存在) - 路由注册服务

### 2. 更多4.0.0模组示例
特别是包含以下功能的模组：
- HTTP端点注册
- JSON数据返回
- 客户端-服务端通信

### 3. 数据结构验证
需要验证以下内容在4.0.0中是否仍然有效：
- 商人ID: `5935c25fb3acc3127c3d8cd9` (Peacekeeper)
- 商人ID: `58330581ace78e27b8b10cee` (Skier)
- 货币物品ID: `677536ee7949f87882036fb0` (EUR)
- 货币物品ID: `676d24a5798491c5260f4b01` (USD)

---

## 📁 文件清单

### 新创建的文件
```
ServerMod/
├── SwiftXP.SPT.ShowMeTheMoney.Server.csproj  ← 项目文件
├── ShowMeTheMoneyServerMod.cs                ← 主代码文件
├── config.json                                ← 配置文件（预留）
└── UPGRADE_TO_4.0.0.md                       ← 本文档
```

### 需要删除的旧文件（待完成路由注册后）
```
ServerMod/
├── src/mod.ts                    ❌ 删除
├── models/currencyPurchasePrices.ts  ❌ 删除
├── package.json                  ❌ 删除
├── tsconfig.json                 ❌ 删除
├── build.mjs                     ❌ 删除
└── types/                        ❌ 删除整个目录
```

---

## 🛠️ 下一步操作

### 立即可做
1. ✅ 编译项目检查语法错误
   ```bash
   cd D:\C#\spt-show-me-the-money-main\ServerMod
   dotnet build
   ```

2. ⚠️ 调整DLL引用路径（如果需要）
   - 当前路径：`..\..\..\..\Apps\TKFBao\TKFClient.0.16.9.0.40087\SPT\`
   - 根据实际情况修改

### 需要进一步信息
3. 🔴 **完成路由注册** - 需要SPT 4.0.0文档或示例
4. 🟡 验证数据结构的正确性
5. 🟡 测试与客户端的通信

---

## 📞 寻求帮助的途径

1. **SPT官方Discord** - 询问4.0.0模组开发相关问题
2. **SPT Hub论坛** - 查找其他开发者的经验
3. **反编译工具** - 使用ILSpy等工具查看SPTarkov.Server.Core.dll的API
4. **GitHub** - 搜索其他4.0.0服务端模组的源码

---

## 📊 预计完成度

| 类别 | 完成度 | 说明 |
|------|--------|------|
| 项目配置 | 100% | ✅ 完成 |
| 模组元数据 | 100% | ✅ 完成 |
| 依赖注入 | 100% | ✅ 完成 |
| 业务逻辑 | 95% | ✅ 几乎完成（可能需微调） |
| 路由注册 | 0% | ⚠️ 待完成 |
| 测试验证 | 0% | ⚠️ 待完成 |
| **总体** | **约70%** | 核心逻辑完成，路由待实现 |

---

## 🎯 预计剩余工作量

- **如果有明确的路由注册示例**：1-2小时
- **如果需要自行摸索**：4-6小时
- **包含完整测试**：6-8小时

---

## 作者注释

此升级基于对SPT 4.0.0有限的了解完成。核心业务逻辑已从TypeScript成功迁移到C#，但HTTP路由注册部分需要进一步的SPT 4.0.0 API知识。

建议在有更多官方文档或示例后再完成最后的30%工作。

最后更新：2025年（具体日期由用户填写）
