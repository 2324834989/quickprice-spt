# Contributing to QuickPrice

Thank you for your interest in contributing to QuickPrice! This document provides guidelines and instructions for contributing.

---

## 📋 Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Setup](#development-setup)
- [Making Changes](#making-changes)
- [Submitting Changes](#submitting-changes)
- [Coding Guidelines](#coding-guidelines)
- [Testing Guidelines](#testing-guidelines)

---

## 🤝 Code of Conduct

- Be respectful and constructive
- Focus on what is best for the community
- Show empathy towards other contributors
- Accept constructive criticism gracefully

---

## 🚀 Getting Started

1. **Fork the repository** to your GitHub account
2. **Clone your fork** locally:
   ```bash
   git clone https://github.com/YOUR-USERNAME/quickprice-spt.git
   cd quickprice-spt
   ```
3. **Add upstream remote**:
   ```bash
   git remote add upstream https://github.com/quickprice/quickprice-spt.git
   ```

---

## 🛠️ Development Setup

### Prerequisites

- Visual Studio 2022 or Rider
- .NET Framework 4.7.1 SDK
- .NET 9.0 SDK
- SPT 4.0.0 installation
- BepInEx 5.4.22+

### Client Development

1. Update SPT assembly paths in `Sources/Client/QuickPrice.csproj`:
   ```xml
   <TarkovDir>YOUR_SPT_INSTALL_PATH</TarkovDir>
   ```

2. Build the client:
   ```bash
   cd Sources/Client
   dotnet build -c Debug
   ```

3. The DLL will be automatically copied to your SPT plugins folder

### Server Development

1. Update SPT assembly paths in `Sources/Server/QuickPrice.Server.csproj`

2. Build the server:
   ```bash
   cd Sources/Server
   dotnet build -c Debug
   ```

---

## 🔄 Making Changes

### Branch Naming

- **feature/**: New features (`feature/add-price-history`)
- **fix/**: Bug fixes (`fix/container-calculation-crash`)
- **docs/**: Documentation updates (`docs/update-readme`)
- **refactor/**: Code refactoring (`refactor/simplify-color-coding`)
- **perf/**: Performance improvements (`perf/optimize-cache`)

### Commit Messages

Follow the [Conventional Commits](https://www.conventionalcommits.org/) format:

```
<type>(<scope>): <subject>

<body>

<footer>
```

**Types**:
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation changes
- `style`: Code style changes (formatting, etc.)
- `refactor`: Code refactoring
- `perf`: Performance improvements
- `test`: Adding or updating tests
- `chore`: Build process or auxiliary tool changes

**Examples**:
```
feat(client): add price history tracking

Implements local price history storage and display in tooltips.
Includes configuration option to enable/disable feature.

Closes #123
```

```
fix(server): resolve currency conversion crash

Fixed null reference exception when trader data is unavailable.
Added defensive null checks and fallback values.

Fixes #456
```

---

## 📤 Submitting Changes

1. **Keep your fork updated**:
   ```bash
   git fetch upstream
   git merge upstream/master
   ```

2. **Create a feature branch**:
   ```bash
   git checkout -b feature/your-feature-name
   ```

3. **Make your changes** and commit them:
   ```bash
   git add .
   git commit -m "feat: add awesome feature"
   ```

4. **Push to your fork**:
   ```bash
   git push origin feature/your-feature-name
   ```

5. **Create a Pull Request** on GitHub:
   - Provide a clear title and description
   - Reference any related issues
   - Include screenshots for UI changes
   - Ensure all tests pass

---

## 💻 Coding Guidelines

### C# Style

- Follow [C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use **PascalCase** for public members
- Use **camelCase** for private fields with `_` prefix
- Use **meaningful names** for variables and methods
- Add **XML documentation comments** for public APIs

**Example**:
```csharp
/// <summary>
/// Calculates the total price of a weapon including all mods
/// </summary>
/// <param name="weapon">The weapon to calculate price for</param>
/// <returns>Total price in roubles</returns>
public double CalculateWeaponPrice(Weapon weapon)
{
    var basePrice = _priceService.GetPrice(weapon.TemplateId);
    var modsPrice = CalculateModsPrice(weapon.Mods);
    return basePrice + modsPrice;
}
```

### Architecture

- **Client**: BepInEx plugin with Harmony patches
  - `Patches/`: Harmony patches for game hooks
  - `Services/`: Business logic and data management
  - `Utils/`: Helper classes and utilities
  - `Config/`: Configuration management

- **Server**: SPT module with HTTP endpoints
  - Minimal dependencies
  - Stateless request handling
  - Fast response times

### Performance

- **Avoid blocking operations** in main thread
- **Use async/await** for I/O operations
- **Cache expensive calculations**
- **Limit recursion depth** for nested structures
- **Profile before optimizing**

---

## 🧪 Testing Guidelines

### Manual Testing

1. **Test in SPT 4.0.0**:
   - Fresh profile
   - Existing profile with many items
   - Various container sizes
   - Different item types (weapons, ammo, armor, etc.)

2. **Test Configuration**:
   - All config options
   - Edge cases (min/max values)
   - Toggle all features on/off

3. **Performance Testing**:
   - Large containers (100+ items)
   - Nested containers (5+ levels)
   - Rapid tooltip triggering
   - Memory usage over time

### Checklist Before Submitting

- [ ] Code builds without errors
- [ ] Code builds without warnings (if possible)
- [ ] Feature works in SPT 4.0.0
- [ ] No console errors or warnings
- [ ] No performance degradation
- [ ] Configuration options work correctly
- [ ] Chinese localization is maintained
- [ ] Documentation is updated
- [ ] CHANGELOG.md is updated

---

## 🌐 Localization

### Adding Translations

When adding new configuration options:

1. Use Chinese for the config key name:
   ```csharp
   config.Bind("2. 显示设置", "新功能名称", defaultValue, "功能说明")
   ```

2. Update documentation in both `README.md` and `README_CN.md`

3. Add tooltip descriptions in Chinese

---

## 📝 Documentation

### When to Update Documentation

- Adding new features
- Changing configuration options
- Modifying installation steps
- Updating dependencies or requirements
- Fixing bugs that affect user behavior

### Documentation Files

- `README.md` - English main documentation
- `README_CN.md` - Chinese documentation
- `CHANGELOG.md` - Version history
- `CONTRIBUTING.md` - This file
- Code comments for complex logic

---

## 🐛 Bug Reports

When reporting bugs, please include:

1. **SPT version** (e.g., 4.0.0)
2. **QuickPrice version** (e.g., 1.0.0)
3. **Steps to reproduce**
4. **Expected behavior**
5. **Actual behavior**
6. **BepInEx log** (if applicable)
7. **Server log** (if applicable)
8. **Screenshots** (if applicable)

---

## 💡 Feature Requests

When suggesting features:

1. **Describe the problem** the feature solves
2. **Explain the proposed solution**
3. **Provide use cases** and examples
4. **Consider performance impact**
5. **Consider configuration complexity**

---

## ❓ Questions?

- Open a [GitHub Discussion](https://github.com/quickprice/quickprice-spt/discussions)
- Check existing [Issues](https://github.com/quickprice/quickprice-spt/issues)
- Review the [README](README.md)

---

**Thank you for contributing to QuickPrice! 🎉**
