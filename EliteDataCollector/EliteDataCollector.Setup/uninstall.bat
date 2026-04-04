@echo off
REM Elite Data Collector Uninstaller
REM This script removes Elite Data Collector from the system

setlocal enabledelayedexpansion

REM Define installation paths
if "%ProgramFiles(x86)%" neq "" (
    set "INSTALL_PATH=%ProgramFiles(x86)%\Elite Data Collector"
) else (
    set "INSTALL_PATH=%ProgramFiles%\Elite Data Collector"
)

echo.
echo ========================================
echo  Elite Data Collector - Uninstaller
echo ========================================
echo.

set /p confirm="Are you sure you want to uninstall Elite Data Collector? (Y/N): "
if /i not "%confirm%"=="Y" (
    echo Uninstallation cancelled.
    exit /b 0
)

REM ── Guard: abort if the application is still running ──────────────────────
tasklist /FI "IMAGENAME eq EliteDataCollector.Host.exe" 2>nul | find /I "EliteDataCollector.Host.exe" >nul
if %errorlevel% equ 0 (
    echo.
    echo [ERROR] Elite Data Collector is currently running.
    echo         Please close the application and run the uninstaller again.
    echo.
    pause
    exit /b 1
)

REM Remove shortcuts
echo Removing shortcuts...
del "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Elite Data Collector\Elite Data Collector.lnk" /f /q 2>nul
del "%USERPROFILE%\Desktop\Elite Data Collector.lnk" /f /q 2>nul
rmdir "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Elite Data Collector" 2>nul

REM Remove installation directory
echo Removing application files...
if exist "%INSTALL_PATH%" (
    rmdir /s /q "%INSTALL_PATH%"
)

REM Remove registry entries
echo Removing registry entries...
reg delete "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Elite Data Collector" /f 2>nul
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\Elite Data Collector" /f 2>nul
reg delete "HKCU\Software\Elite Data Collector" /f 2>nul

echo.
echo ========================================
echo  Uninstallation complete!
echo ========================================
echo.
pause
exit /b 0
