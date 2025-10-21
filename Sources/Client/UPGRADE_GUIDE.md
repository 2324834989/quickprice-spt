# ShowMeTheMoney-SPT4 异步升级与功能迁移完整指南

## 📋 升级概览

本指南提供从同步模式升级到异步模式，并迁移 2.0 版本功能的完整方案。

---

## ✅ 已完成的升级

### 1. PriceDataServiceAsync（异步价格服务）

**文件**: `Services/PriceDataServiceAsync.cs`

**新增特性**:
- ✅ 完全异步的 `UpdatePricesAsync()` 方法
- ✅ 5分钟缓存过期机制
- ✅ 10秒最小更新间隔（防止频繁请求）
- ✅ 线程安全的缓存读写（使用 lock）
- ✅ Fire-and-Forget 更新模式
- ✅ 缓存状态监控 `GetCacheStatus()`

### 2. Plugin.cs（异步初始化）

**修改内容**:
- ✅ 使用 `InitializeAsync()` 异步加载价格数据
- ✅ Fire-and-Forget 模式，不阻塞游戏启动
- ✅ 添加 `IsInitialized` 标志
- ✅ 补丁独立启用，不依赖价格数据

### 3. InventoryScreenShowPatch（自动刷新）

**文件**: `Patches/InventoryScreenShowPatch.cs`

**功能**:
- ✅ 打开物品栏时检查缓存
- ✅ 超过5分钟自动刷新
- ✅ 异步后台更新，不阻塞UI

---

## 🔧 需要手动完成的步骤

### 步骤1: 更新 TestTooltipPatch 使用异步服务

**修改文件**: `Patches/TestTooltipPatch.cs`

**需要替换的地方**（全局替换）:
```csharp
// 旧代码
PriceDataService.Instance.GetPrice(templateId)

// 新代码
PriceDataServiceAsync.Instance.GetPrice(templateId)
```

**批量替换命令**（如果使用 VS Code）:
1. 打开 `Patches/TestTooltipPatch.cs`
2. 按 `Ctrl+H` 打开替换
3. 查找: `PriceDataService.Instance`
4. 替换为: `PriceDataServiceAsync.Instance`
5. 点击"全部替换"

### 步骤2: 更新 ItemBackgroundColorPatch 使用异步服务

**修改文件**: `Patches/ItemBackgroundColorPatch.cs`

**同样替换**:
```csharp
PriceDataService.Instance → PriceDataServiceAsync.Instance
```

### 步骤3: 更新 PriceCalculator 使用异步服务

**修改文件**: `Services/PriceCalculator.cs`

**同样替换**:
```csharp
PriceDataService.Instance → PriceDataServiceAsync.Instance
```

### 步骤4: 编译测试

编译项目，确保没有错误：
```
MSBuild ShowMeTheMoney.csproj /p:Configuration=Release
```

---

## 🚀 阶段二：迁移 2.0 高级功能

以下是可选的高级功能迁移方案。

### 功能1: 交易员价格服务

**目标**: 支持显示多个商人的收购价格，自动对比最优价格。

**需要添加的文件**:

#### `Models/TradePrice.cs`
```csharp
using System;

namespace ShowMeTheMoney.Models
{
    public class TradePrice
    {
        public int SingleObjectPrice { get; set; }
        public int? TotalPrice { get; set; }
        public string TraderName { get; set; }
        public double? CurrencyCourse { get; set; }  // 汇率
        public string CurrencySymbol { get; set; }    // 货币符号 (₽/$/€)

        /// <summary>
        /// 获取单格价值（用于比较）
        /// </summary>
        public double GetPricePerSlot(int slotCount)
        {
            var totalPrice = TotalPrice ?? SingleObjectPrice;
            return slotCount > 0 ? (double)totalPrice / slotCount : totalPrice;
        }

        /// <summary>
        /// 获取卢布价格
        /// </summary>
        public double GetRoublePrice()
        {
            var price = TotalPrice ?? SingleObjectPrice;
            return CurrencyCourse.HasValue ? price * CurrencyCourse.Value : price;
        }
    }
}
```

#### `Models/TradeItem.cs`
```csharp
using EFT.InventoryLogic;

namespace ShowMeTheMoney.Models
{
    public class TradeItem
    {
        public Item Item { get; set; }
        public int ItemSlotCount { get; set; }
        public int ItemObjectCount { get; set; }

        public TradePrice TraderPrice { get; set; }  // 商人价格
        public TradePrice FleaPrice { get; set; }     // 跳蚤价格

        /// <summary>
        /// 获取最佳价格
        /// </summary>
        public TradePrice GetBestPrice()
        {
            if (TraderPrice == null) return FleaPrice;
            if (FleaPrice == null) return TraderPrice;

            var traderPricePerSlot = TraderPrice.GetPricePerSlot(ItemSlotCount);
            var fleaPricePerSlot = FleaPrice.GetPricePerSlot(ItemSlotCount);

            return fleaPricePerSlot > traderPricePerSlot ? FleaPrice : TraderPrice;
        }
    }
}
```

#### `Services/TraderPriceService.cs`
```csharp
using System;
using System.Linq;
using EFT;
using EFT.InventoryLogic;
using ShowMeTheMoney.Models;

namespace ShowMeTheMoney.Services
{
    /// <summary>
    /// 交易员价格服务
    /// 获取所有商人的最高收购价
    /// </summary>
    public class TraderPriceService
    {
        private static TraderPriceService _instance;
        public static TraderPriceService Instance => _instance ??= new TraderPriceService();

        private TraderPriceService() { }

        /// <summary>
        /// 获取商人最佳收购价
        /// </summary>
        /// <param name="item">物品</param>
        /// <param name="itemSlotCount">物品占用格数</param>
        /// <returns>最佳商人价格，如果没有商人收购则返回 null</returns>
        public TradePrice GetBestTraderPrice(Item item, int itemSlotCount)
        {
            try
            {
                // 注意：这里需要访问游戏的商人系统
                // 由于 SPT 的商人系统API可能不同，这里提供伪代码
                // 你需要根据实际游戏API调整

                // 伪代码示例：
                // var traders = GameWorld.Instance.Traders;
                // var bestPrice = 0;
                // string bestTraderName = "";
                //
                // foreach (var trader in traders)
                // {
                //     if (trader.CanBuy(item))
                //     {
                //         var price = trader.GetBuyPrice(item);
                //         if (price > bestPrice)
                //         {
                //             bestPrice = price;
                //             bestTraderName = trader.Name;
                //         }
                //     }
                // }
                //
                // if (bestPrice > 0)
                // {
                //     return new TradePrice
                //     {
                //         SingleObjectPrice = bestPrice,
                //         TotalPrice = bestPrice * item.StackObjectsCount,
                //         TraderName = bestTraderName,
                //         CurrencySymbol = "₽"
                //     };
                // }

                return null; // 暂时返回 null，需要实现商人系统接口
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"获取商人价格失败: {ex.Message}");
                return null;
            }
        }
    }
}
```

### 功能2: 跳蚤税费计算

**目标**: 显示在跳蚤市场出售物品需要支付的税费。

**服务端API端点**（需要添加）:

在服务端 `ShowMeTheMoneyStaticRouter.cs` 中添加：
```csharp
// 获取货币汇率端点
new RouteAction<EmptyRequestData>(
    "/showMeTheMoney/getCurrencyPurchasePrices",
    async (url, info, sessionId, output) =>
        await HandleGetCurrencyPurchasePrices(url, info, sessionId)
)

// 处理方法已存在于服务端
```

**客户端服务**:

#### `Services/FleaTaxService.cs`
```csharp
using System;

namespace ShowMeTheMoney.Services
{
    /// <summary>
    /// 跳蚤市场税费计算服务
    /// </summary>
    public class FleaTaxService
    {
        private static FleaTaxService _instance;
        public static FleaTaxService Instance => _instance ??= new FleaTaxService();

        // 税费计算公式常量（基于 SPT 跳蚤市场规则）
        private const double BASE_TAX_RATE = 0.05;  // 基础税率 5%
        private const double PRICE_FACTOR = 0.01;   // 价格因子

        private FleaTaxService() { }

        /// <summary>
        /// 计算跳蚤市场税费
        /// </summary>
        /// <param name="itemPrice">物品价格</param>
        /// <param name="requestedPrice">要求价格</param>
        /// <param name="itemCount">物品数量</param>
        /// <returns>需要支付的税费</returns>
        public double CalculateFleaTax(double itemPrice, double requestedPrice, int itemCount = 1)
        {
            if (itemPrice <= 0 || requestedPrice <= 0)
                return 0;

            // 简化的税费公式（实际公式更复杂）
            // 真实公式：log10(VO * Ti * 4PO * Q) + log10(VR * Tr * 4PR * Q) - log10(Q) - log10(Ti * Tr)
            // 这里使用简化版本

            double priceDiff = Math.Abs(requestedPrice - itemPrice);
            double baseTax = requestedPrice * BASE_TAX_RATE;
            double penaltyTax = priceDiff * PRICE_FACTOR;

            return (baseTax + penaltyTax) * itemCount;
        }

        /// <summary>
        /// 计算税后净收入
        /// </summary>
        public double CalculateNetProfit(double requestedPrice, double fleaTax, int itemCount = 1)
        {
            return (requestedPrice * itemCount) - fleaTax;
        }

        /// <summary>
        /// 获取建议售价（最小化税费）
        /// </summary>
        public double GetRecommendedPrice(double itemPrice)
        {
            // 建议价格接近市场价格以最小化税费
            return itemPrice * 1.1; // 建议加价10%
        }
    }
}
```

### 功能3: 更新 TestTooltipPatch 显示商人价格和税费

在 `TestTooltipPatch.cs` 中添加显示逻辑：

```csharp
// 在 FormatNormalItemPriceText 方法中添加

// 获取跳蚤市场价格（已有）
var fleaPrice = PriceDataServiceAsync.Instance.GetPrice(item.TemplateId);

// 获取商人价格（新增）
var traderPrice = TraderPriceService.Instance.GetBestTraderPrice(item, slots);

// 显示两种价格对比
if (fleaPrice.HasValue)
{
    string fleaPriceText = $"跳蚤市场: {TextFormatting.FormatPrice(fleaPrice.Value)}";

    // 计算税费
    var fleaTax = FleaTaxService.Instance.CalculateFleaTax(
        fleaPrice.Value,
        fleaPrice.Value,
        item.StackObjectsCount
    );

    if (fleaTax > 0)
    {
        fleaPriceText += $" (税费: {TextFormatting.FormatPrice(fleaTax)})";
    }

    sb.Append($"\n{fleaPriceText}");
}

if (traderPrice != null)
{
    string traderPriceText = $"{traderPrice.TraderName}: {TextFormatting.FormatPrice(traderPrice.TotalPrice.Value)}";
    sb.Append($"\n{traderPriceText}");
}
```

---

## 📊 功能对比表

| 功能 | 当前项目 | 升级后 | 2.0迁移 |
|------|---------|--------|---------|
| **异步初始化** | ❌ | ✅ | ✅ |
| **自动缓存过期** | ❌ | ✅ | ✅ |
| **打开物品栏刷新** | ❌ | ✅ | ✅ |
| **跳蚤市场价格** | ✅ | ✅ | ✅ |
| **商人价格对比** | ❌ | ❌ | ✅ (可选) |
| **税费计算** | ❌ | ❌ | ✅ (可选) |
| **货币转换** | ❌ | ❌ | ✅ (可选) |
| **背景着色** | ✅ | ✅ | ❌ |
| **单格价值着色** | ✅ | ✅ | ✅ |

---

## 🧪 测试清单

### 基础异步功能测试

- [ ] 启动游戏，检查日志显示"价格数据正在后台加载"
- [ ] 等待3-5秒，检查日志显示"价格数据异步加载成功"
- [ ] 打开物品栏，悬停物品，确认价格显示正常
- [ ] 关闭物品栏，等待6分钟，再次打开
- [ ] 检查日志显示"缓存已过期，开始刷新价格"
- [ ] 确认价格刷新成功

### 缓存机制测试

- [ ] 连续打开/关闭物品栏3次（间隔<10秒）
- [ ] 检查日志显示"距离上次更新仅X秒，跳过更新"
- [ ] 确认没有重复的HTTP请求

### 性能测试

- [ ] 使用秒表测量游戏启动时间（对比升级前后）
- [ ] 预期：启动时间不应增加（异步加载）
- [ ] 打开物品栏响应速度（应该无延迟）

### 兼容性测试

- [ ] 测试所有物品类型（武器、配件、子弹、容器等）
- [ ] 测试颜色编码功能
- [ ] 测试背景着色功能
- [ ] 测试中文配置系统

---

## ⚙️ 配置建议

可以添加以下配置项到 `Settings.cs`：

```csharp
// 缓存设置
public static ConfigEntry<int> CacheExpireMinutes { get; set; }
public static ConfigEntry<bool> AutoRefreshOnOpenInventory { get; set; }
public static ConfigEntry<bool> ShowTraderPrices { get; set; }
public static ConfigEntry<bool> ShowFleaTax { get; set; }

// 在 Init 方法中初始化
CacheExpireMinutes = config.Bind(
    "性能设置",
    "缓存过期时间（分钟）",
    5,
    "价格缓存过期时间，0=永不过期"
);

AutoRefreshOnOpenInventory = config.Bind(
    "性能设置",
    "打开物品栏时自动刷新",
    true,
    "打开物品栏时如果缓存过期则自动刷新价格"
);

ShowTraderPrices = config.Bind(
    "主要设置",
    "显示商人价格",
    false,
    "显示商人收购价格（需要2.0迁移）"
);

ShowFleaTax = config.Bind(
    "主要设置",
    "显示跳蚤税费",
    false,
    "显示在跳蚤市场出售需要支付的税费（需要2.0迁移）"
);
```

---

## 🔍 故障排除

### 问题1: 价格不显示

**可能原因**:
- 服务端未启动
- 异步加载还未完成

**解决方案**:
```csharp
// 在 GetPrice 调用前检查
if (!Plugin.IsInitialized)
{
    Plugin.Log.LogDebug("价格数据还在加载中...");
    return null;
}
```

### 问题2: 缓存不刷新

**检查**:
- 日志中是否有 "缓存已过期" 消息
- `Settings.AutoRefreshOnOpenInventory` 是否为 true

**手动刷新**:
```csharp
// 可以添加一个配置键绑定
if (Input.GetKeyDown(KeyCode.F5))
{
    _ = PriceDataServiceAsync.Instance.ForceRefreshAsync();
}
```

### 问题3: 编译错误

**常见错误**:
```
CS0246: The type or namespace name 'PriceDataServiceAsync' could not be found
```

**解决方案**:
- 确保 `PriceDataServiceAsync.cs` 已添加到项目
- 检查命名空间是否正确
- 清理并重新编译项目

---

## 📚 参考文档

1. **异步编程最佳实践**: https://docs.microsoft.com/en-us/dotnet/csharp/async
2. **Task 和 ValueTask**: https://devblogs.microsoft.com/dotnet/understanding-the-whys-whats-and-whens-of-valuetask/
3. **线程安全**: https://docs.microsoft.com/en-us/dotnet/standard/threading/

---

## ✨ 下一步计划

建议按以下顺序实施：

1. ✅ **阶段一：异步升级**（已完成）
   - 异步价格服务
   - 异步初始化
   - 自动刷新机制

2. 🔄 **阶段二：基础迁移**
   - 修改现有补丁使用异步服务
   - 测试基础功能
   - 性能验证

3. 🚀 **阶段三：高级功能**（可选）
   - 商人价格服务
   - 税费计算
   - 货币转换

---

**文档版本**: 1.0
**最后更新**: 2025-01-20
**作者**: Claude Code
