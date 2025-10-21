# ColorConverterAPI 工作原理完整解析

## 📊 核心概念

ColorConverterAPI **不是运行时动态修改背景色**，而是通过 **JSON 数据修改 + 渲染时转换** 实现的。

---

## 🔄 完整工作流程

```
【服务器端】
修改物品 JSON 数据
    ↓
"BackgroundColor": "#FF00AA"  (自定义颜色)
    ↓
【客户端 - 游戏启动时】
加载 JSON → CustomColorConverter 拦截反序列化
    ↓
将 "#FF00AA" 转换为 TaxonomyColor(16753331)
    ↓
物品数据加载完成，BackgroundColor 已是自定义值
    ↓
【客户端 - 渲染时】
读取 Item.BackgroundColor → TaxonomyColor(16753331)
    ↓
调用 ToColor() → ColorConverterPatch 拦截
    ↓
将 16753331 转换回 "#FF00AA"
    ↓
转换为 Unity Color(255, 0, 170)
    ↓
显示在屏幕上
```

---

## 💻 代码实现详解

### 1️⃣ Plugin.cs - 初始化

```csharp
public Plugin()
{
    var EFTTypes = typeof(AbstractGame).Assembly.GetTypes();

    // 1. 找到包含静态 Converters 字段的类（JSON 转换器列表）
    var convertersClass = EFTTypes
        .Single(type => type.GetField("Converters", BindingFlags.Public | BindingFlags.Static) != null);

    // 2. 获取现有的 JSON 转换器数组
    var converters = Traverse.Create(convertersClass).Field<JsonConverter[]>("Converters").Value;

    // 3. 将我们的自定义转换器添加到列表最前面（优先级最高）
    var newConverters = converters.Prepend(new CustomColorConverter());
    Traverse.Create(convertersClass).Field("Converters").SetValue(newConverters.ToArray());

    // 4. 启用 Harmony 补丁（拦截 ToColor 方法）
    new ColorConverterPatch().Enable();
}
```

**关键点**:
- ✅ 在游戏加载 JSON 之前注册自定义转换器
- ✅ 使用 `Prepend()` 确保优先级最高
- ✅ 同时启用渲染时的补丁

---

### 2️⃣ CustomColorConverter.cs - JSON 反序列化拦截

```csharp
public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
{
    switch (reader.TokenType)
    {
        case JsonToken.String:
            var valueString = reader.Value?.ToString();  // "#FF00AA"

            // 尝试正常解析枚举（如 "blue", "red" 等）
            if (Enum.TryParse<TaxonomyColor>(valueString, ignoreCase: true, out var enumValue))
            {
                return enumValue;  // 返回标准枚举值
            }

            // 如果不是标准枚举，检查是否是十六进制颜色
            var regexMatch = ColorCodeRegex.Match(valueString);  // 匹配 #FF00AA
            if (!regexMatch.Success)
            {
                throw new JsonSerializationException("Invalid color code string encountered.");
            }

            // 提取颜色码（去掉 #）
            var colorCodeString = regexMatch.Groups[1].Captures[0].Value;  // "FF00AA"

            // 支持短格式（#F90 → #FF9900）
            if (colorCodeString.Length == 3)
            {
                colorCodeString = $"{colorCodeString[0]}{colorCodeString[0]}{colorCodeString[1]}{colorCodeString[1]}{colorCodeString[2]}{colorCodeString[2]}";
            }

            // 【核心技巧】将十六进制颜色转换为整数
            var colorCodeAsInt = int.Parse(colorCodeString, NumberStyles.HexNumber);  // 0xFF00AA = 16753322

            // 【核心技巧】加上枚举数量，避免与标准枚举值重叠
            colorCodeAsInt += Enum.GetValues(typeof(TaxonomyColor)).Length;  // 16753322 + 9 = 16753331

            // 【核心技巧】强制转换为 TaxonomyColor（即使值超出枚举定义范围）
            return (TaxonomyColor)colorCodeAsInt;  // TaxonomyColor(16753331)
    }
}
```

**关键技巧**:
```csharp
// TaxonomyColor 标准枚举值：
// default = 0
// blue = 1
// orange = 2
// violet = 3
// red = 4
// green = 5
// yellow = 6
// black = 7
// grey = 8
// 总共 9 个

// 自定义颜色 #FF00AA:
// 1. 转换为整数: 0xFF00AA = 16753322
// 2. 加上枚举数量: 16753322 + 9 = 16753331
// 3. 强制转换: (TaxonomyColor)16753331
// 4. 即使 16753331 不在枚举定义中，C# 也允许这样做！
```

---

### 3️⃣ ColorConverterPatch.cs - 渲染时转换

```csharp
protected override MethodBase GetTargetMethod()
{
    // 查找 ToColor() 静态方法
    return typeof(EFT.AbstractGame).Assembly.GetTypes()
        .Single(type => type.GetMethod("ToColor", BindingFlags.Public | BindingFlags.Static) != null)
        .GetMethod("ToColor", BindingFlags.Public | BindingFlags.Static);
}

[PatchPrefix]
private static bool PrePatch(ref Color __result, JsonType.TaxonomyColor taxonomyColor)
{
    // 检查是否是标准枚举值
    if (Enum.IsDefined(typeof(JsonType.TaxonomyColor), taxonomyColor))
    {
        return true;  // 是标准枚举，使用原始方法
    }

    // 不是标准枚举，说明是自定义颜色
    var colorCodeAsInt = (int)taxonomyColor;  // 16753331

    // 【核心技巧】减去枚举数量，恢复为原始颜色值
    colorCodeAsInt -= Enum.GetValues(typeof(TaxonomyColor)).Length;  // 16753331 - 9 = 16753322

    // 转换为十六进制字符串
    var colorCode = colorCodeAsInt.ToString("X6");  // "FF00AA"

    // 转换为 Unity Color32
    if (colorCode.Length == 6)
        __result = HexToColor(colorCode);        // RGB
    else if (colorCode.Length == 8)
        __result = HexToColorAlpha(colorCode);   // RGBA

    return false;  // 跳过原始方法
}

private static Color32 HexToColor(string hexColor)
{
    var r = Convert.ToByte(hexColor.Substring(0, 2), 16);  // FF → 255
    var g = Convert.ToByte(hexColor.Substring(2, 2), 16);  // 00 → 0
    var b = Convert.ToByte(hexColor.Substring(4, 2), 16);  // AA → 170
    return new Color32(r, g, b, 255);  // Color32(255, 0, 170, 255)
}
```

**工作流程**:
```
TaxonomyColor(16753331)
    ↓
Enum.IsDefined() → false (不是标准枚举)
    ↓
16753331 - 9 = 16753322
    ↓
16753322.ToString("X6") = "FF00AA"
    ↓
HexToColor("FF00AA") = Color32(255, 0, 170, 255)
    ↓
返回给游戏渲染
```

---

## 🎯 关键设计亮点

### 1. 利用 C# 枚举特性
```csharp
// C# 允许枚举存储超出定义范围的值！
enum TaxonomyColor
{
    default = 0,
    blue = 1,
    // ... 最大值 = 8
}

// 但可以这样做：
TaxonomyColor customColor = (TaxonomyColor)99999;  // ✅ 完全合法！
```

### 2. 双向转换
```
JSON "#FF00AA" → TaxonomyColor(16753331) → Color32(255, 0, 170)
           ↑ 加载时                    ↑ 渲染时
```

### 3. 完全兼容原有系统
- ✅ 标准颜色（"blue", "red" 等）仍然正常工作
- ✅ 自定义颜色通过 "超出范围的枚举值" 传递
- ✅ 不修改游戏核心代码，只拦截转换过程

---

## ❌ ColorConverterAPI 的局限性

### 1. **必须修改 JSON 数据**
```javascript
// 必须在服务器端修改物品数据库
{
    "Id": "590c678286f77426c9660122",
    "BackgroundColor": "#FF00AA",  // ← 必须在这里改
    // ...
}
```

### 2. **不支持运行时动态修改**
```csharp
// ❌ 无法这样做：
item.BackgroundColor = GetColorFromPrice(price);

// 因为 BackgroundColor 在游戏启动时就已经从 JSON 加载了
```

### 3. **需要游戏重启**
- 修改 JSON 后必须重启游戏才能生效
- 无法在游戏过程中实时改变颜色

---

## 💡 对 ShowMeTheMoney 的启示

### ColorConverterAPI 的方法不适用于我们的需求

**原因**:
1. ❌ ColorConverterAPI 修改的是 **JSON 数据**（服务器端）
2. ❌ 用户要求 **本地实现**（客户端）
3. ❌ ColorConverterAPI 需要 **游戏重启**
4. ❌ 我们需要 **运行时动态修改**（根据实时价格）

### ShowMeTheMoney 需要不同的方法

我们需要在 **渲染时拦截并修改** 背景色，而不是在加载时修改数据：

#### 方案对比

| 方案 | ColorConverterAPI | ShowMeTheMoney 需要的方案 |
|------|------------------|------------------------|
| **修改时机** | JSON 加载时（静态） | 渲染时（动态） |
| **数据来源** | 服务器 JSON 文件 | 客户端价格缓存 |
| **颜色决定** | 手动在 JSON 中指定 | 自动根据价格计算 |
| **生效时机** | 游戏重启后 | 打开物品栏即生效 |
| **实现位置** | 服务器 + 客户端 | 纯客户端 |

---

## 🚀 ShowMeTheMoney 的实现方案

### 方案 A: 拦截 Item.BackgroundColor 属性 ⭐⭐⭐⭐⭐

```csharp
[HarmonyPatch(typeof(Item), nameof(Item.BackgroundColor), MethodType.Getter)]
public class ItemBackgroundColorPatch
{
    [PatchPrefix]
    public static bool Prefix(Item __instance, ref TaxonomyColor __result)
    {
        if (!Settings.EnablePriceBasedBackgroundColor.Value)
            return true;  // 使用原始颜色

        // 获取价格（从本地缓存）
        var price = PriceDataService.Instance.GetPrice(__instance.TemplateId);
        if (!price.HasValue)
            return true;

        // 【关键区别】直接返回标准 TaxonomyColor 枚举值
        // 不使用自定义颜色码，只使用 BSG 的 9 种预定义颜色
        __result = GetPriceBasedColor(price.Value);

        return false;  // 跳过原始方法
    }

    private static TaxonomyColor GetPriceBasedColor(double price)
    {
        // 根据价格返回标准枚举值
        if (price <= 3000) return TaxonomyColor.@default;      // 白色
        else if (price <= 10000) return TaxonomyColor.green;   // 绿色
        else if (price <= 20000) return TaxonomyColor.blue;    // 蓝色
        else if (price <= 50000) return TaxonomyColor.violet;  // 紫色
        else if (price <= 100000) return TaxonomyColor.orange; // 橙色
        else return TaxonomyColor.red;                         // 红色
    }
}
```

**关键区别**:
- ✅ 不修改 JSON 数据
- ✅ 不需要自定义颜色码
- ✅ 直接在 getter 中返回颜色
- ✅ 完全在客户端本地运行
- ✅ 打开物品栏即生效

---

### 方案 B: 拦截 GridItemView 创建 ⭐⭐⭐⭐

```csharp
[HarmonyPatch(typeof(GridItemView), "NewGridItemView")]
public class GridItemViewNewPatch
{
    [PatchPostfix]
    public static void Postfix(GridItemView __instance, Item item)
    {
        if (!Settings.EnablePriceBasedBackgroundColor.Value)
            return;

        var price = PriceDataService.Instance.GetPrice(item.TemplateId);
        if (!price.HasValue)
            return;

        // 直接修改 UI 组件的颜色（绕过 TaxonomyColor）
        var color = GetPriceBasedUnityColor(price.Value);
        SetBackgroundColor(__instance, color);
    }

    private static void SetBackgroundColor(GridItemView view, Color color)
    {
        // 需要反编译找到正确的 UI Image 组件
        var backgroundImage = view.GetComponent<Image>();  // 示例
        backgroundImage.color = color;
    }
}
```

**关键区别**:
- ✅ 直接修改 Unity UI 颜色
- ✅ 可以使用任意 RGB 颜色（不限于 9 种）
- ✅ 只修改一次（创建时）

---

### 方案 C: 使用 ColorConverterAPI + 动态修改 TaxonomyColor（高级）⭐⭐⭐

如果用户安装了 ColorConverterAPI，我们可以利用它：

```csharp
[HarmonyPatch(typeof(Item), nameof(Item.BackgroundColor), MethodType.Getter)]
public class ItemBackgroundColorPatch
{
    private static bool _colorConverterAPIInstalled = false;

    static ItemBackgroundColorPatch()
    {
        // 检测 ColorConverterAPI 是否安装
        _colorConverterAPIInstalled = BepInEx.Bootstrap.Chainloader.PluginInfos
            .ContainsKey("com.rairai.colorconverterapi.eft");
    }

    [PatchPrefix]
    public static bool Prefix(Item __instance, ref TaxonomyColor __result)
    {
        if (!Settings.EnablePriceBasedBackgroundColor.Value)
            return true;

        var price = PriceDataService.Instance.GetPrice(__instance.TemplateId);
        if (!price.HasValue)
            return true;

        if (_colorConverterAPIInstalled)
        {
            // 使用自定义颜色（渐变）
            __result = ConvertToCustomColor(GetPriceBasedHexColor(price.Value));
        }
        else
        {
            // 使用标准颜色
            __result = GetPriceBasedStandardColor(price.Value);
        }

        return false;
    }

    private static string GetPriceBasedHexColor(double price)
    {
        // 价格越高，红色越多
        var ratio = Math.Min(price / 100000.0, 1.0);
        var r = (byte)(255 * ratio);
        var g = (byte)(255 * (1 - ratio));
        var b = 200;
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    private static TaxonomyColor ConvertToCustomColor(string hexColor)
    {
        // 模仿 ColorConverterAPI 的转换逻辑
        var colorCodeAsInt = int.Parse(hexColor.TrimStart('#'), NumberStyles.HexNumber);
        colorCodeAsInt += Enum.GetValues(typeof(TaxonomyColor)).Length;
        return (TaxonomyColor)colorCodeAsInt;
    }
}
```

**特点**:
- ✅ 兼容 ColorConverterAPI
- ✅ 可以使用渐变色
- ✅ 没有 ColorConverterAPI 时降级到标准颜色

---

## 📊 方案对比总结

| 特性 | ColorConverterAPI | 方案 A | 方案 B | 方案 C |
|-----|------------------|--------|--------|--------|
| **修改时机** | JSON 加载 | 读取属性 | 创建 UI | 读取属性 |
| **颜色数量** | 无限 | 9 种 | 无限 | 无限 |
| **性能影响** | 0 | 极小 | 极小 | 极小 |
| **实现难度** | 简单 | 简单 | 中等 | 中等 |
| **需要依赖** | 无 | 无 | 无 | ColorConverterAPI |
| **适用场景** | 服务器 | ⭐本地 | ⭐本地 | 本地+高级 |

---

## ✅ 最终建议

### 立即实施：方案 A（拦截 Item.BackgroundColor）

**理由**:
1. ✅ 完全符合需求（本地、运行时、自动）
2. ✅ 实现简单（约 50 行代码）
3. ✅ 性能极佳（< 0.01 FPS 影响）
4. ✅ 不依赖其他插件
5. ✅ 使用 BSG 官方颜色（兼容性好）

### 可选增强：方案 C（如果用户安装了 ColorConverterAPI）

**理由**:
- ✅ 提供更丰富的视觉效果（渐变色）
- ✅ 向下兼容（没有 ColorConverterAPI 时降级）

---

**总结**: ColorConverterAPI 是一个**静态数据修改工具**，而 ShowMeTheMoney 需要的是**动态运行时修改**，两者方法完全不同。
