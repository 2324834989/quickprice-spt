@echo off
chcp 65001 >nul
echo ========================================
echo   QuickPrice 自动部署脚本
echo ========================================
echo.

:: 设置 SPT 安装目录（请修改为您的实际路径）
set SPT_DIR=C:\SPT

:: 检查 SPT 目录是否存在
if not exist "%SPT_DIR%" (
    echo ❌ 错误: SPT 目录不存在: %SPT_DIR%
    echo 请编辑此脚本，修改 SPT_DIR 为您的实际 SPT 安装路径
    pause
    exit /b 1
)

echo 📂 SPT 目录: %SPT_DIR%
echo.

:: ========== 部署客户端 ==========
echo 🖥️  正在部署客户端...
set CLIENT_ZIP=Sources\Client\release\QuickPrice-1.0.0-df325771-dirty.zip
set CLIENT_TARGET=%SPT_DIR%\BepInEx\plugins\QuickPrice

if not exist "%CLIENT_ZIP%" (
    echo ❌ 客户端文件不存在: %CLIENT_ZIP%
    echo 请先编译客户端
    pause
    exit /b 1
)

:: 创建目标目录
if not exist "%CLIENT_TARGET%" mkdir "%CLIENT_TARGET%"

:: 解压客户端（需要PowerShell）
echo    正在解压客户端文件...
powershell -command "Expand-Archive -Path '%CLIENT_ZIP%' -DestinationPath '%SPT_DIR%' -Force"

if exist "%CLIENT_TARGET%\QuickPrice.dll" (
    echo ✅ 客户端部署成功: %CLIENT_TARGET%\QuickPrice.dll
) else (
    echo ❌ 客户端部署失败
)

echo.

:: ========== 部署服务端 ==========
echo 🔧 正在部署服务端...
set SERVER_DLL=Sources\Server\bin\Release\quickprice.dll
set SERVER_JSON=Sources\Server\bin\Release\mod.json
set SERVER_TARGET=%SPT_DIR%\user\mods\QuickPrice

if not exist "%SERVER_DLL%" (
    echo ❌ 服务端DLL不存在: %SERVER_DLL%
    echo 请先编译服务端
    pause
    exit /b 1
)

:: 创建服务端目录
if not exist "%SERVER_TARGET%" mkdir "%SERVER_TARGET%"

:: 复制文件
copy /Y "%SERVER_DLL%" "%SERVER_TARGET%\quickprice.dll" >nul
copy /Y "%SERVER_JSON%" "%SERVER_TARGET%\mod.json" >nul

if exist "%SERVER_TARGET%\quickprice.dll" (
    echo ✅ 服务端部署成功: %SERVER_TARGET%\
    echo    - quickprice.dll
    echo    - mod.json
) else (
    echo ❌ 服务端部署失败
)

echo.
echo ========================================
echo   🎉 部署完成！
echo ========================================
echo.
echo 📋 客户端位置: %CLIENT_TARGET%
echo 📋 服务端位置: %SERVER_TARGET%
echo.
echo 💡 下一步：
echo    1. 重启 SPT 服务器
echo    2. 启动游戏
echo    3. 鼠标悬停物品查看价格
echo.
pause
