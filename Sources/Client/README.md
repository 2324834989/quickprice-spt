# Show Me The Money Reborn

**专为 SPT 4.0.0 / EFT 0.16.9.0.40087 重写的价格显示插件**

---

## 🎯 版本信息

| 项目 | 信息 |
|------|------|
| **插件名称** | Show Me The Money Reborn |
| **DLL 名称** | `ShowMeTheMoney.Reborn.dll` |
| **版本号** | `4.0.0` |
| **插件 ID** | `com.swiftxp.showmethemoney.reborn` |
| **依赖** | SPT 4.0.0 |

---

## 📦 安装方法

1. 解压 `ShowMeTheMoney.Reborn-4.0.0-xxxxxxxx.zip`
2. 复制 `BepInEx` 文件夹到游戏根目录
3. 确认文件位置：
   ```
   EscapeFromTarkov/
   └── BepInEx/
       └── plugins/
           └── ShowMeTheMoney.Reborn/
               └── ShowMeTheMoney.Reborn.dll
   ```
4. 启动游戏

---

## 🧪 当前版本（测试骨架）

### 已实现功能

✅ **Tooltip 拦截测试**
- 所有物品悬浮窗后面会显示 "这是测试" 文字
- 三种颜色显示（绿色/黄色/红色）
- 验证 Harmony 补丁工作正常

### 测试方法

1. 启动游戏
2. 进入库存界面（Tab 键）
3. 将鼠标悬停在任意物品上
4. 应该看到：
   ```
   [原始物品描述]

   ✅ 这是测试
   Show Me The Money Reborn 4.0.0
   Tooltip 拦截成功！
   ```

### 日志验证

查看 `BepInEx/LogOutput.log`，应看到：

```
===========================================
  Show Me The Money Reborn - SPT 4.0.0
  版本: 4.0.0
===========================================
✅ 测试补丁已启用
===========================================
  插件加载完成！
  请进入游戏测试物品悬浮窗
===========================================
```

---

## 🚀 开发计划

### 待实现功能（按优先级）

#### **阶段 1：中文配置系统** ⏳
- [ ] F12 配置管理器显示中文
- [ ] 8 个配置选项（3个分组）
- [ ] 主要设置、显示设置、性能设置

#### **阶段 2：价格数据服务** ⏳
- [ ] HTTP 连接服务端
- [ ] 获取 4054 个物品价格
- [ ] 5 分钟缓存机制

#### **阶段 3：六级颜色编码** ⏳
- [ ] 白色（≤ 3,000₽）
- [ ] 绿色（≤ 10,000₽）
- [ ] 蓝色（≤ 20,000₽）
- [ ] 紫色（≤ 50,000₽）
- [ ] 橙色（≤ 100,000₽）
- [ ] 红色（≥ 100,000₽）

#### **阶段 4：价格显示** ⏳
- [ ] 跳蚤市场价格
- [ ] 单格价格计算
- [ ] 最佳价格加粗
- [ ] 价格格式化（₽45,000）

---

## 🔄 与旧版本的区别

| 项目 | 旧版本 | Reborn 版本 |
|------|--------|-------------|
| **插件 ID** | `com.swiftxp.showmethemoney` | `com.swiftxp.showmethemoney.reborn` |
| **DLL 名称** | `ShowMeTheMoney.dll` | `ShowMeTheMoney.Reborn.dll` |
| **版本号** | `2.0.0` | `4.0.0` |
| **SPT 兼容** | 3.x | 4.0.0 |
| **配置系统** | spt-common | BepInEx 原生 |
| **交易商价格** | ✅ 支持 | ❌ 暂不支持 |
| **跳蚤市场价格** | ✅ 支持 | ✅ 支持 |

---

## ❗ 重要说明

### 不兼容旧版本配置

**Reborn 版本使用全新的配置系统，不会读取旧版本的配置文件。**

- 旧配置：`BepInEx/config/com.swiftxp.showmethemoney.cfg`
- 新配置：`BepInEx/config/com.swiftxp.showmethemoney.reborn.cfg`

### 可以与旧版共存

**插件 ID 不同，理论上可以同时安装，但建议删除旧版本以避免冲突。**

删除旧版本方法：
```
删除 BepInEx/plugins/ShowMeTheMoney/ 目录
```

---

## 📝 开发日志

### 2025-10-16
- ✅ 创建项目骨架
- ✅ 实现 Tooltip 拦截测试补丁
- ✅ 成功编译并生成 DLL
- ⏳ 等待游戏内测试验证

---

## 🤝 致谢

- **DynamicMaps** - 项目结构参考
- **SPT 团队** - 框架支持
- **Show Me The Money 原版** - 核心逻辑参考

---

## 📄 许可证

MIT License
