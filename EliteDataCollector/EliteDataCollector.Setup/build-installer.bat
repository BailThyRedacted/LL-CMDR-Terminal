@echo off
REM Build script to prepare installer package
REM This creates the final distribution with all necessary files

setlocal enabledelayedexpansion

echo.
echo ========================================
echo Elite Data Collector - Build Installer
echo ========================================
echo.

set "SOURCE_DIR=..\EliteDataCollector.Host\bin\Release\net10.0-windows"
set "OUTPUT_DIR=dist"

echo Creating output directory...
if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

echo Copying application files...
xcopy /E /I /Y "%SOURCE_DIR%\*" "%OUTPUT_DIR%\" >nul 2>&1

if %errorlevel% equ 0 (
    echo [OK] Application files copied
) else (
    echo [ERROR] Failed to copy files from %SOURCE_DIR%
    pause
    exit /b 1
)

echo Copying installer scripts...
copy "install.bat" "%OUTPUT_DIR%\" >nul 2>&1
copy "uninstall.bat" "%OUTPUT_DIR%\" >nul 2>&1
copy "createShortcut.vbs" "%OUTPUT_DIR%\" >nul 2>&1
copy "INSTALL_GUIDE.md" "%OUTPUT_DIR%\" >nul 2>&1

echo.
echo ========================================
echo Build Complete!
echo ========================================
echo.
echo Files are prepared in the 'dist' folder.
echo You can now:
echo   1. Run install.bat from the dist folder
echo   2. Distribute the entire dist folder to users
echo.
pause
