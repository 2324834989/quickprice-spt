# Changelog

All notable changes to QuickPrice will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [1.0.0] - 2025-10-21

### 🎉 Initial Release

#### Added
- **Core Features**
  - Real-time item price display in tooltips
  - Six-tier color-coding system based on item value
  - Weapon mod pricing with recursive calculation
  - Armor class detection and coloring (1-6 levels)
  - Ammo penetration power color coding
  - Price-per-slot calculation for efficient looting
  - Trader price comparison (best trader vs. flea market)
  - Flea market tax calculation with net profit display

- **Display Options**
  - Hold Ctrl to show prices (configurable)
  - Bold highlighting for best prices
  - Item name coloring based on value/penetration
  - Optional background color for inventory grid
  - Detailed weapon mod breakdown with tree structure
  - Magazine ammo details with penetration values

- **Performance Optimizations**
  - Smart container calculation with depth limiting (default: 10 layers)
  - Item count limiting per container (default: 100 items)
  - Large container skipping (threshold: 150 items)
  - Async price data loading (non-blocking)
  - Configurable cache modes:
    - Permanent cache (recommended)
    - 5-minute auto-refresh
    - 10-minute auto-refresh
    - Manual refresh only

- **Configuration**
  - 30+ configurable options via BepInEx F12 menu
  - Customizable price color thresholds (6 tiers)
  - Customizable penetration color thresholds (6 tiers)
  - Armor class coloring toggle
  - Performance tuning options
  - Cache mode selection
  - Tooltip delay adjustment (0-2 seconds)

- **Localization**
  - Complete Chinese localization for all config options
  - Chinese tooltips and UI elements
  - Bilingual README documentation

- **Technical**
  - SPT 4.0.0 compatibility
  - BepInEx 5.4.22+ support
  - .NET Framework 4.7.1 (client)
  - .NET 9.0 (server module)
  - Harmony patching for game hooks
  - Thread-safe async operations
  - JSON-based price data exchange

#### Technical Details
- **Client**: BepInEx plugin with Harmony patches
- **Server**: SPT module providing price data endpoints
- **Endpoints**:
  - `/showMeTheMoney/getStaticPriceTable` - Static base prices
  - `/showMeTheMoney/getDynamicPriceTable` - Real-time flea prices
  - `/showMeTheMoney/getCurrencyPurchasePrices` - Currency exchange rates

#### Known Limitations
- Dynamic prices require server module installation
- First-time dynamic price loading may take 30-60 seconds
- Background color feature may conflict with other color mods (e.g., ColorConverterAPI)
- Trader prices require opening trader interface at least once after game launch

---

## [Unreleased]

### Planned Features
- English localization for config options
- Price history tracking
- Export price data to CSV
- Custom color scheme support
- Community price database integration

---

## Version Naming Convention

- **Major.Minor.Patch** (e.g., 1.0.0)
  - **Major**: Significant changes, may break compatibility
  - **Minor**: New features, backward compatible
  - **Patch**: Bug fixes and minor improvements

---

## Git Commit Hash Reference

- **1.0.0**: `a5f762c9` - Full project rename and successful build
- **Initial**: `8a48c933` - Initial commit with source code

---

[1.0.0]: https://github.com/2324834989/quickprice-spt/releases/tag/v1.0.0
