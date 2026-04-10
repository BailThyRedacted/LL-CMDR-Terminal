@echo off
REM ============================================================
REM  Elite Data Collector - Build Standalone .exe + MSI Installer
REM  
REM  This script:
REM    1. Publishes a self-contained app (no .NET install needed)
REM    2. Builds an MSI installer for easy distribution
REM
REM  Output:
REM    publish\EliteDataCollector.UI.exe  (+ dependencies)
REM    EliteDataCollector.Setup\output\EliteDataCollector-Setup.msi
REM ============================================================

echo.
echo ========================================
echo   Step 1: Publishing application...
echo ========================================
echo.

dotnet publish EliteDataCollector.UI\EliteDataCollector.UI.csproj ^
    -c Release ^
    -o publish

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo   PUBLISH FAILED - see errors above
    pause
    exit /b 1
)

echo.
echo ========================================
echo   Step 2: Building MSI installer...
echo ========================================
echo.

pushd EliteDataCollector.Setup
wix build -o output\EliteDataCollector-Setup.msi Product.wxs -ext WixToolset.UI.wixext
popd

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo   MSI BUILD FAILED - see errors above
    pause
    exit /b 1
)

echo.
echo ========================================
echo   BUILD SUCCEEDED
echo ========================================
echo.
echo MSI installer ready at:
echo   EliteDataCollector.Setup\output\EliteDataCollector-Setup.msi
echo.
echo Give EliteDataCollector-Setup.msi to the user.
echo They double-click it to install, then launch from Desktop or Start Menu.
echo.
pause


