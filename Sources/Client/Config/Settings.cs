using BepInEx.Configuration;
using UnityEngine;

namespace QuickPrice.Config
{
    public static class Settings
    {
        // ===== 1. 主要设置 =====
        public static ConfigEntry<bool> PluginEnabled;
        public static ConfigEntry<bool> ShowFleaPrices;
        public static ConfigEntry<bool> ShowPricePerSlot;
        public static ConfigEntry<bool> ShowWeaponModsPrice;
        public static ConfigEntry<bool> ShowDetailedWeaponMods;
        public static ConfigEntry<bool> RequireCtrlKey;
        public static ConfigEntry<float> TooltipDelay;

        // ===== 2. 显示设置 =====
        public static ConfigEntry<bool> EnableColorCoding;
        public static ConfigEntry<bool> ShowBestPriceInBold;
        public static ConfigEntry<bool> UseCaliberPenetrationPower;
        public static ConfigEntry<bool> ColorItemName;
        public static ConfigEntry<bool> EnablePriceBasedBackgroundColor;
        public static ConfigEntry<bool> ShowGroundItemPrice;  // 显示地面物品价格（跟随物品名称）

        // ===== 2.1 价格颜色阈值 =====
        public static ConfigEntry<int> PriceThreshold1; // 白色→绿色
        public static ConfigEntry<int> PriceThreshold2; // 绿色→蓝色
        public static ConfigEntry<int> PriceThreshold3; // 蓝色→紫色
        public static ConfigEntry<int> PriceThreshold4; // 紫色→橙色
        public static ConfigEntry<int> PriceThreshold5; // 橙色→红色

        // ===== 2.2 穿甲颜色阈值 =====
        public static ConfigEntry<int> PenetrationThreshold1; // 白色→绿色
        public static ConfigEntry<int> PenetrationThreshold2; // 绿色→蓝色
        public static ConfigEntry<int> PenetrationThreshold3; // 蓝色→紫色
        public static ConfigEntry<int> PenetrationThreshold4; // 紫色→橙色
        public static ConfigEntry<int> PenetrationThreshold5; // 橙色→红色

        // ===== 2.3 护甲等级设置 =====
        public static ConfigEntry<bool> EnableArmorClassColoring; // 启用护甲等级着色
        public static ConfigEntry<bool> ShowArmorClass; // 显示护甲等级文字

        // ===== 2.4 重置功能 =====
        public static ConfigEntry<string> ResetThresholdsButton; // 重置阈值按钮

        // ===== 3. 性能设置 =====
        public static ConfigEntry<bool> UseDynamicPrices;
        public static ConfigEntry<CacheMode> PriceCacheMode;    // v2.0: 缓存模式

        // ===== 3.1 容器性能优化 =====
        public static ConfigEntry<bool> EnableContainerPriceCalculation; // 启用容器内物品价格计算
        public static ConfigEntry<int> MaxContainerDepth;        // 最大递归深度
        public static ConfigEntry<int> MaxContainerItems;        // 最大计算物品数
        public static ConfigEntry<bool> SkipLargeContainers;     // 跳过大容器
        public static ConfigEntry<int> LargeContainerThreshold;  // 大容器阈值

        // ===== 4. v2.0 新增功能 =====
        public static ConfigEntry<bool> ShowTraderPrices;      // 显示商人价格
        public static ConfigEntry<bool> ShowFleaTax;            // 显示跳蚤税费
        public static ConfigEntry<bool> AutoRefreshOnOpenInventory; // 打开物品栏自动刷新
        public static ConfigEntry<KeyCode> RefreshPricesKey;   // 刷新价格快捷键

        // 默认阈值常量
        private const int DEFAULT_PRICE_THRESHOLD_1 = 5000;
        private const int DEFAULT_PRICE_THRESHOLD_2 = 25000;
        private const int DEFAULT_PRICE_THRESHOLD_3 = 60000;
        private const int DEFAULT_PRICE_THRESHOLD_4 = 100000;
        private const int DEFAULT_PRICE_THRESHOLD_5 = 200000;

        private const int DEFAULT_PENETRATION_THRESHOLD_1 = 20;
        private const int DEFAULT_PENETRATION_THRESHOLD_2 = 30;
        private const int DEFAULT_PENETRATION_THRESHOLD_3 = 40;
        private const int DEFAULT_PENETRATION_THRESHOLD_4 = 50;
        private const int DEFAULT_PENETRATION_THRESHOLD_5 = 60;

        private static ConfigFile _configFile; // 保存 ConfigFile 引用用于重置

        // 缓存模式枚举
        public enum CacheMode
        {
            Permanent,      // 永久缓存（启动时加载，不再刷新）
            FiveMinutes,    // 5分钟自动过期
            TenMinutes,     // 10分钟自动过期
            Manual          // 仅手动刷新
        }

        public static void Init(ConfigFile config)
        {
            // 保存 ConfigFile 引用用于重置功能
            _configFile = config;

            // ===== 1. 主要设置 =====
            PluginEnabled = config.Bind(
                "1. 主要设置",
                "启用插件",
                true,
                "是否启用 QuickPrice 插件"
            );

            ShowFleaPrices = config.Bind(
                "1. 主要设置",
                "显示跳蚤市场价格",
                true,
                "在物品提示框中显示跳蚤市场价格"
            );

            ShowPricePerSlot = config.Bind(
                "1. 主要设置",
                "显示每格价格",
                true,
                "显示物品的单格位价格（价格/格数）"
            );

            ShowWeaponModsPrice = config.Bind(
                "1. 主要设置",
                "显示武器配件价格",
                true,
                "显示武器所有配件的总价值（递归计算所有层级配件）"
            );

            ShowDetailedWeaponMods = config.Bind(
                "1. 主要设置",
                "显示配件详细列表",
                false,
                "显示所有配件的层级结构和价格\n" +
                "以缩进树状结构显示配件及其子配件"
            );

            RequireCtrlKey = config.Bind(
                "1. 主要设置",
                "按住Ctrl键才显示",
                false,
                "需要按住Ctrl键（左Ctrl或右Ctrl）才显示价格\n" +
                "关闭此选项则鼠标悬停即显示价格"
            );

            TooltipDelay = config.Bind(
                "1. 主要设置",
                "提示框延迟（秒）",
                0.0f,
                new ConfigDescription(
                    "鼠标悬停后多久显示价格提示框（0 = 立即显示）",
                    new AcceptableValueRange<float>(0f, 2f)
                )
            );

            // ===== 2. 显示设置 =====
            EnableColorCoding = config.Bind(
                "2. 显示设置",
                "启用颜色编码",
                true,
                "根据价格自动着色物品名称\n" +
                "白色≤3千 | 绿色≤1万 | 蓝色≤2万 | 紫色≤5万 | 橙色≤10万 | 红色>10万"
            );

            ShowBestPriceInBold = config.Bind(
                "2. 显示设置",
                "最佳价格加粗",
                true,
                "用粗体突出显示最高价格"
            );

            UseCaliberPenetrationPower = config.Bind(
                "2. 显示设置",
                "子弹按穿甲等级着色",
                true,
                "子弹和弹药盒使用穿甲等级着色，而不是价格着色\n" +
                "颜色等级：白色<15 | 绿色<25 | 蓝色<35 | 紫色<45 | 橙色<55 | 红色≥55"
            );

            ColorItemName = config.Bind(
                "2. 显示设置",
                "物品名称着色",
                true,
                "根据物品价值或穿甲等级给物品名称着色\n" +
                "✅ 适用范围：物品栏提示框 + 战局内地面散落物品\n" +
                "普通物品/武器按价格着色 | 子弹/弹匣按穿甲等级着色 | 护甲按防弹等级着色"
            );

            EnablePriceBasedBackgroundColor = config.Bind(
                "2. 显示设置",
                "自动着色物品背景",
                true,
                "根据物品价格自动修改物品单元格背景颜色\n" +
                "无需鼠标悬停，打开物品栏即可看到所有物品已着色\n" +
                "使用与物品名称相同的价格阈值配置\n" +
                "注意：可能与 ColorConverterAPI 等其他颜色插件冲突"
            );

            ShowGroundItemPrice = config.Bind(
                "2. 显示设置",
                "地面物品显示价格",
                true,
                "在战局内地面散落物品名称后显示价格\n" +
                "格式：物品名称 (单格价值₽) 或 物品名称 (总价₽)\n" +
                "颜色会根据单格价值自动调整\n" +
                "注意：需要启用\"物品名称着色\"选项才会生效"
            );

            // ===== 2.1 价格颜色阈值 =====
            PriceThreshold1 = config.Bind(
                "2.1 价格颜色阈值",
                "阈值1 - 白色→绿色",
                DEFAULT_PRICE_THRESHOLD_1,
                new ConfigDescription(
                    "价格 ≤ 此值显示白色，> 此值显示绿色或更高等级",
                    new AcceptableValueRange<int>(0, 1000000)
                )
            );

            PriceThreshold2 = config.Bind(
                "2.1 价格颜色阈值",
                "阈值2 - 绿色→蓝色",
                DEFAULT_PRICE_THRESHOLD_2,
                new ConfigDescription(
                    "价格 ≤ 此值显示绿色，> 此值显示蓝色或更高等级",
                    new AcceptableValueRange<int>(0, 1000000)
                )
            );

            PriceThreshold3 = config.Bind(
                "2.1 价格颜色阈值",
                "阈值3 - 蓝色→紫色",
                DEFAULT_PRICE_THRESHOLD_3,
                new ConfigDescription(
                    "价格 ≤ 此值显示蓝色，> 此值显示紫色或更高等级",
                    new AcceptableValueRange<int>(0, 1000000)
                )
            );

            PriceThreshold4 = config.Bind(
                "2.1 价格颜色阈值",
                "阈值4 - 紫色→橙色",
                DEFAULT_PRICE_THRESHOLD_4,
                new ConfigDescription(
                    "价格 ≤ 此值显示紫色，> 此值显示橙色或更高等级",
                    new AcceptableValueRange<int>(0, 1000000)
                )
            );

            PriceThreshold5 = config.Bind(
                "2.1 价格颜色阈值",
                "阈值5 - 橙色→红色",
                DEFAULT_PRICE_THRESHOLD_5,
                new ConfigDescription(
                    "价格 ≤ 此值显示橙色，> 此值显示红色",
                    new AcceptableValueRange<int>(0, 1000000)
                )
            );

            // ===== 2.2 穿甲颜色阈值 =====
            PenetrationThreshold1 = config.Bind(
                "2.2 穿甲颜色阈值",
                "阈值1 - 白色→绿色",
                DEFAULT_PENETRATION_THRESHOLD_1,
                new ConfigDescription(
                    "穿甲值 < 此值显示白色，≥ 此值显示绿色或更高等级",
                    new AcceptableValueRange<int>(0, 100)
                )
            );

            PenetrationThreshold2 = config.Bind(
                "2.2 穿甲颜色阈值",
                "阈值2 - 绿色→蓝色",
                DEFAULT_PENETRATION_THRESHOLD_2,
                new ConfigDescription(
                    "穿甲值 < 此值显示绿色，≥ 此值显示蓝色或更高等级",
                    new AcceptableValueRange<int>(0, 100)
                )
            );

            PenetrationThreshold3 = config.Bind(
                "2.2 穿甲颜色阈值",
                "阈值3 - 蓝色→紫色",
                DEFAULT_PENETRATION_THRESHOLD_3,
                new ConfigDescription(
                    "穿甲值 < 此值显示蓝色，≥ 此值显示紫色或更高等级",
                    new AcceptableValueRange<int>(0, 100)
                )
            );

            PenetrationThreshold4 = config.Bind(
                "2.2 穿甲颜色阈值",
                "阈值4 - 紫色→橙色",
                DEFAULT_PENETRATION_THRESHOLD_4,
                new ConfigDescription(
                    "穿甲值 < 此值显示紫色，≥ 此值显示橙色或更高等级",
                    new AcceptableValueRange<int>(0, 100)
                )
            );

            PenetrationThreshold5 = config.Bind(
                "2.2 穿甲颜色阈值",
                "阈值5 - 橙色→红色",
                DEFAULT_PENETRATION_THRESHOLD_5,
                new ConfigDescription(
                    "穿甲值 < 此值显示橙色，≥ 此值显示红色",
                    new AcceptableValueRange<int>(0, 100)
                )
            );

            // ===== 2.3 护甲等级设置 =====
            EnableArmorClassColoring = config.Bind(
                "2.3 护甲等级设置",
                "启用护甲等级着色",
                true,
                "根据护甲防弹等级（1-6级）自动着色护甲背景和名称\n" +
                "1级=灰色 | 2级=绿色 | 3级=蓝色 | 4级=紫色 | 5级=橙色 | 6级=红色\n" +
                "自动检测护甲本体和内部防弹插板，取最高等级"
            );

            ShowArmorClass = config.Bind(
                "2.3 护甲等级设置",
                "显示护甲等级文字",
                true,
                "在护甲提示框中显示防弹等级（例如：防弹等级: 4级）\n" +
                "自动检测可拆卸防弹插板和内置防弹内衬"
            );

            // ===== 2.4 重置功能 =====
            ResetThresholdsButton = config.Bind(
                "2.4 重置功能",
                "点击重置所有阈值",
                "点击按钮重置",
                "点击下方按钮将所有价格和穿甲阈值重置为默认值\n" +
                "⚠️ 重置后立即生效，会覆盖您的自定义配置\n" +
                "💡 提示：在配置管理器(F12)中，修改此项的值即可触发重置"
            );

            // 监听重置按钮的值变化
            ResetThresholdsButton.SettingChanged += (sender, args) =>
            {
                // 当配置值改变时，触发重置
                ResetPriceThresholds();
                // Plugin.Log.LogInfo("===========================================");
                // Plugin.Log.LogInfo("  ✅ 阈值已重置为默认值！");
                // Plugin.Log.LogInfo($"  📊 价格阈值: {DEFAULT_PRICE_THRESHOLD_1 / 1000}K / {DEFAULT_PRICE_THRESHOLD_2 / 1000}K / {DEFAULT_PRICE_THRESHOLD_3 / 1000}K / {DEFAULT_PRICE_THRESHOLD_4 / 1000}K / {DEFAULT_PRICE_THRESHOLD_5 / 1000}K");
                // Plugin.Log.LogInfo($"  🎯 穿甲阈值: {DEFAULT_PENETRATION_THRESHOLD_1} / {DEFAULT_PENETRATION_THRESHOLD_2} / {DEFAULT_PENETRATION_THRESHOLD_3} / {DEFAULT_PENETRATION_THRESHOLD_4} / {DEFAULT_PENETRATION_THRESHOLD_5}");
                // Plugin.Log.LogInfo("  💾 配置已保存，立即生效");
                // Plugin.Log.LogInfo("===========================================");
            };

            // ===== 3. 性能设置 =====
            UseDynamicPrices = config.Bind(
                "3. 性能设置",
                "使用动态价格",
                true,  // 默认开启动态价格
                "✅ 推荐：开启（使用跳蚤市场实时价格，更准确）\n" +
                "动态价格：从跳蚤市场实时获取（更准确但加载慢）\n" +
                "静态价格：使用游戏基础价格（快速但可能略有偏差）\n" +
                "⚠️ 动态价格需要查询数千个物品，首次加载可能需要30-60秒"
            );

            PriceCacheMode = config.Bind(
                "3. 性能设置",
                "价格缓存模式",
                CacheMode.Permanent,
                "永久缓存：启动时加载一次，不再刷新（推荐）\n" +
                "5分钟刷新：缓存5分钟后自动过期\n" +
                "10分钟刷新：缓存10分钟后自动过期\n" +
                "仅手动刷新：只在手动触发时刷新\n" +
                "⚠️ 自动刷新可能导致短暂卡顿"
            );

            // ===== 3.1 容器性能优化 =====
            EnableContainerPriceCalculation = config.Bind(
                "3.1 容器性能优化",
                "启用容器内物品价格计算",
                true,
                "是否计算容器（背包、箱子等）内部物品的价格\n" +
                "✅ 启用：显示「容器价值 + 内部物品价值」的总价\n" +
                "❌ 禁用：仅显示容器本身的价格，完全跳过内部物品计算\n" +
                "⚠️ 如果您的容器物品很多导致卡顿，建议禁用此选项\n" +
                "推荐：如果经常卡顿则禁用，否则启用"
            );

            MaxContainerDepth = config.Bind(
                "3.1 容器性能优化",
                "最大递归深度",
                10,
                new ConfigDescription(
                    "容器嵌套计算的最大深度（背包套娃层数限制）\n" +
                    "默认值 10 层，原版为 50 层\n" +
                    "降低此值可显著提升性能，但可能影响深层嵌套容器的准确性\n" +
                    "推荐值：10-20\n" +
                    "⚠️ 仅在「启用容器内物品价格计算」为 true 时有效",
                    new AcceptableValueRange<int>(1, 50)
                )
            );

            MaxContainerItems = config.Bind(
                "3.1 容器性能优化",
                "最大计算物品数",
                100,
                new ConfigDescription(
                    "单个容器最多计算多少个物品的价格\n" +
                    "超过此数量将停止计算并显示警告\n" +
                    "默认值 100，设置为 0 表示无限制\n" +
                    "推荐值：50-100\n" +
                    "⚠️ 仅在「启用容器内物品价格计算」为 true 时有效",
                    new AcceptableValueRange<int>(0, 500)
                )
            );

            SkipLargeContainers = config.Bind(
                "3.1 容器性能优化",
                "跳过大容器计算",
                true,
                "当容器内物品数量超过阈值时，跳过详细价格计算\n" +
                "仅显示容器本身价格，避免卡顿\n" +
                "推荐：启用（可避免大型物品箱卡顿）\n" +
                "⚠️ 仅在「启用容器内物品价格计算」为 true 时有效"
            );

            LargeContainerThreshold = config.Bind(
                "3.1 容器性能优化",
                "大容器物品数阈值",
                50,
                new ConfigDescription(
                    "当容器内物品数量超过此值时，视为「大容器」\n" +
                    "如果启用了「跳过大容器计算」，将跳过详细计算\n" +
                    "⚠️ 修复：原默认值 150 太高，改为 50 更合理\n" +
                    "推荐值：30-100\n" +
                    "⚠️ 仅在「启用容器内物品价格计算」为 true 时有效",
                    new AcceptableValueRange<int>(10, 500)
                )
            );

            // ===== 4. v2.0 新增功能 =====
            ShowTraderPrices = config.Bind(
                "4. v2.0 新增功能",
                "显示商人价格",
                true,
                "显示所有商人的收购价格，并与跳蚤价格对比\n" +
                "自动显示最佳价格（商人 vs 跳蚤市场）\n" +
                "⚠️ 注意：首次启动游戏后，需要先打开一次商人界面（如 Prapor）来初始化商人数据"
            );

            ShowFleaTax = config.Bind(
                "4. v2.0 新增功能",
                "显示跳蚤税费",
                true,
                "显示在跳蚤市场出售物品需要支付的税费\n" +
                "包含税后净利润计算"
            );

            AutoRefreshOnOpenInventory = config.Bind(
                "4. v2.0 新增功能",
                "打开物品栏自动刷新",
                true,
                "打开物品栏时如果缓存过期则自动异步刷新价格数据\n" +
                "不阻塞界面，后台更新\n" +
                "⚠️ 仅在缓存模式为「5分钟刷新」或「10分钟刷新」时有效\n" +
                "永久缓存模式下此选项无效"
            );

            RefreshPricesKey = config.Bind(
                "4. v2.0 新增功能",
                "刷新价格快捷键",
                KeyCode.F10,
                "按此键立即强制刷新跳蚤市场价格缓存\n" +
                "默认快捷键: F10\n" +
                "刷新过程异步进行，不会阻塞游戏\n" +
                "适用于动态价格模式，可随时获取最新跳蚤市场价格\n" +
                "💡 配合永久缓存模式使用，需要更新价格时手动刷新"
            );
        }

        /// <summary>
        /// 重置所有价格和穿甲阈值为默认值
        /// </summary>
        public static void ResetPriceThresholds()
        {
            // 重置价格阈值
            PriceThreshold1.Value = DEFAULT_PRICE_THRESHOLD_1;
            PriceThreshold2.Value = DEFAULT_PRICE_THRESHOLD_2;
            PriceThreshold3.Value = DEFAULT_PRICE_THRESHOLD_3;
            PriceThreshold4.Value = DEFAULT_PRICE_THRESHOLD_4;
            PriceThreshold5.Value = DEFAULT_PRICE_THRESHOLD_5;

            // 重置穿甲阈值
            PenetrationThreshold1.Value = DEFAULT_PENETRATION_THRESHOLD_1;
            PenetrationThreshold2.Value = DEFAULT_PENETRATION_THRESHOLD_2;
            PenetrationThreshold3.Value = DEFAULT_PENETRATION_THRESHOLD_3;
            PenetrationThreshold4.Value = DEFAULT_PENETRATION_THRESHOLD_4;
            PenetrationThreshold5.Value = DEFAULT_PENETRATION_THRESHOLD_5;

            // 保存配置文件
            _configFile?.Save();
        }
    }
}
