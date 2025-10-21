# 快速完成异步升级 - 批量替换脚本

## 方法1: PowerShell 批量替换（推荐）

在项目根目录运行以下 PowerShell 脚本：

```powershell
# 批量替换所有文件中的 PriceDataService 为 PriceDataServiceAsync

# 要替换的文件列表
$files = @(
    "Patches\TestTooltipPatch.cs",
    "Patches\ItemBackgroundColorPatch.cs",
    "Services\PriceCalculator.cs"
)

foreach ($file in $files) {
    $path = Join-Path $PSScriptRoot $file

    if (Test-Path $path) {
        Write-Host "正在处理: $file" -ForegroundColor Yellow

        # 读取文件内容
        $content = Get-Content $path -Raw -Encoding UTF8

        # 执行替换
        $newContent = $content -replace 'PriceDataService\.Instance', 'PriceDataServiceAsync.Instance'

        # 写回文件
        Set-Content $path -Value $newContent -Encoding UTF8 -NoNewline

        Write-Host "✅ 完成: $file" -ForegroundColor Green
    } else {
        Write-Host "❌ 文件不存在: $file" -ForegroundColor Red
    }
}

Write-Host "`n🎉 所有文件替换完成！" -ForegroundColor Cyan
Write-Host "请编译项目并测试功能。" -ForegroundColor Cyan
```

**使用方法**：
1. 将上述代码保存为 `D:\C#\ShowMeTheMoney-SPT4\QuickUpgrade.ps1`
2. 在 PowerShell 中运行：
   ```powershell
   cd D:\C#\ShowMeTheMoney-SPT4
   .\QuickUpgrade.ps1
   ```

---

## 方法2: Visual Studio 批量替换

如果你使用 Visual Studio：

1. 打开解决方案
2. 按 `Ctrl+Shift+H`（替换在文件中）
3. **查找内容**: `PriceDataService\.Instance`
4. **替换为**: `PriceDataServiceAsync.Instance`
5. **查找范围**: 选择 "当前项目"
6. **使用正则表达式**: 取消勾选
7. 点击"全部替换"
8. 确认替换（应该有约15-20处）

---

## 方法3: VS Code 批量替换

如果你使用 VS Code：

1. 打开项目文件夹
2. 按 `Ctrl+Shift+F`（在文件中查找）
3. 在搜索框输入: `PriceDataService\.Instance`
4. 在替换框输入: `PriceDataServiceAsync.Instance`
5. 点击 "包含的文件" 过滤器，输入：
   ```
   Patches/**/*.cs, Services/**/*.cs
   ```
6. 点击"全部替换"图标（或 `Ctrl+Shift+1`）

---

## 方法4: 手动逐个替换（最安全）

如果你想更谨慎地替换，按以下顺序手动修改：

### 1. TestTooltipPatch.cs（约12处）

查找所有：
```csharp
PriceDataService.Instance.GetPrice(
```

替换为：
```csharp
PriceDataServiceAsync.Instance.GetPrice(
```

**关键位置**：
- 第175行：`var weaponPrice = PriceDataService.Instance.GetPrice(...)`
- 第213行：`var boxPrice = PriceDataService.Instance.GetPrice(...)`
- 第246行：`var magPrice = PriceDataService.Instance.GetPrice(...)`
- 第263行：`var ammoPrice = PriceDataService.Instance.GetPrice(...)`
- 第273行：`var price = PriceDataService.Instance.GetPrice(...)`
- 其他类似位置...

### 2. ItemBackgroundColorPatch.cs（约2处）

查找：
```csharp
var price = PriceDataService.Instance.GetPrice(__instance.TemplateId);
```

替换为：
```csharp
var price = PriceDataServiceAsync.Instance.GetPrice(__instance.TemplateId);
```

**位置**：
- 第217行
- 第231行

### 3. PriceCalculator.cs（约3-5处）

查找并替换所有 `PriceDataService.Instance` 为 `PriceDataServiceAsync.Instance`

---

## 验证替换是否完整

运行以下 PowerShell 命令检查是否还有遗漏：

```powershell
# 搜索项目中所有使用旧服务的地方
Get-ChildItem -Path "D:\C#\ShowMeTheMoney-SPT4" -Filter "*.cs" -Recurse |
    Select-String -Pattern "PriceDataService\.Instance" |
    Select-Object Path, LineNumber, Line

# 如果输出为空，说明替换完成
# 如果有输出，说明还有未替换的地方
```

---

## 编译和测试

### 1. 编译项目

```powershell
# 进入项目目录
cd D:\C#\ShowMeTheMoney-SPT4

# 编译 Release 版本
dotnet build ShowMeTheMoney.csproj -c Release

# 或使用 MSBuild
msbuild ShowMeTheMoney.csproj /p:Configuration=Release
```

### 2. 检查编译输出

应该看到：
```
生成成功。
    0 个警告
    0 个错误
```

### 3. 复制到游戏目录

DLL 位置：
```
D:\C#\ShowMeTheMoney-SPT4\bin\Release\net471\ShowMeTheMoney.Reborn.dll
```

复制到：
```
[你的游戏目录]\BepInEx\plugins\ShowMeTheMoney.Reborn\
```

### 4. 启动游戏测试

查看日志文件（位于 `BepInEx\LogOutput.log`）：

**预期日志输出**：
```
[Info   : Show Me The Money Reborn] ===========================================
[Info   : Show Me The Money Reborn]   Show Me The Money Reborn - SPT 4.0.0
[Info   : Show Me The Money Reborn]   版本: 4.0.0
[Info   : Show Me The Money Reborn] ===========================================
[Info   : Show Me The Money Reborn] ✅ 中文配置系统初始化成功
[Info   : Show Me The Money Reborn] 🔄 开始异步加载价格数据...
[Info   : Show Me The Money Reborn] ✅ 物品捕获补丁已启用
[Info   : Show Me The Money Reborn] ✅ 价格显示补丁已启用
[Info   : Show Me The Money Reborn] ✅ 自定义颜色转换补丁已启用
[Info   : Show Me The Money Reborn] ✅ 物品背景色自动着色已启用
[Info   : Show Me The Money Reborn] ✅ 自动刷新补丁已启用（打开物品栏时刷新）
[Info   : Show Me The Money Reborn] ===========================================
[Info   : Show Me The Money Reborn]   🎉 插件启动完成！
[Info   : Show Me The Money Reborn]   ⏳ 价格数据正在后台加载...
[Info   : Show Me The Money Reborn]   🎮 进入游戏查看物品价格
[Info   : Show Me The Money Reborn]   ⚙️  按 F12 打开配置管理器
[Info   : Show Me The Money Reborn] ===========================================
[Info   : Show Me The Money Reborn] ✅ 价格数据异步加载成功: 4054 个物品
[Info   : Show Me The Money Reborn] 📊 缓存: 4054 项, 年龄: 0.5秒, 过期: 否
```

---

## 常见问题解决

### 问题1: 编译错误 - "找不到 PriceDataServiceAsync"

**原因**: 新文件未包含在项目中

**解决方案**:
1. 打开 `ShowMeTheMoney.csproj`
2. 确保包含：
   ```xml
   <Compile Include="Services\PriceDataServiceAsync.cs" />
   <Compile Include="Patches\InventoryScreenShowPatch.cs" />
   ```
3. 或者在 VS 中右键项目 → 添加 → 现有项

### 问题2: 游戏启动后崩溃

**检查步骤**:
1. 查看 `BepInEx\LogOutput.log` 最后的错误信息
2. 确认所有依赖已正确引用
3. 检查 .NET Framework 版本（应为 4.7.1）

### 问题3: 价格不显示

**可能原因**:
- 服务端未启动
- 异步加载失败

**解决方案**:
1. 检查服务端是否运行
2. 查看日志中是否有"价格数据异步加载成功"
3. 手动触发刷新（打开/关闭物品栏）

---

## 回滚方案

如果升级后出现问题，可以临时回滚：

```csharp
// 在 Plugin.cs 的 InitializeAsync() 中
// 临时改回同步模式

// 旧代码（同步）
PriceDataService.Instance.UpdatePrices();
Log.LogInfo($"✅ 价格数据初始化成功 ({PriceDataService.Instance.GetCachedPriceCount()} 个物品)");

// 新代码（异步）- 如果有问题可以注释掉
// var success = await PriceDataServiceAsync.Instance.UpdatePricesAsync();
```

---

## 完成检查清单

升级完成后，请检查以下项目：

- [ ] 所有 `.cs` 文件已替换完成
- [ ] 项目编译无错误、无警告
- [ ] DLL 复制到游戏目录
- [ ] 游戏启动正常
- [ ] 日志显示"价格数据异步加载成功"
- [ ] 打开物品栏，价格显示正常
- [ ] 颜色编码功能正常
- [ ] 背景着色功能正常（如果启用）
- [ ] 等待6分钟后打开物品栏，日志显示"缓存已过期"
- [ ] 价格自动刷新成功

---

**脚本版本**: 1.0
**兼容项目**: ShowMeTheMoney-SPT4 v4.0.0+
**最后更新**: 2025-01-20
