@echo off
REM Elite Data Collector Uninstaller
REM This script removes Elite Data Collector from the system

setlocal enabledelayedexpansion

set "APP_NAME=Elite Data Collector"
set "INSTALL_PATH=%ProgramFiles%\Elite Data Collector"

echo.
echo ========================================
echo  %APP_NAME% - Uninstaller
echo ========================================
echo.

REM ── Step 0: UAC self-elevation ─────────────────────────────────────────────
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo  Administrator rights required. Requesting elevation...
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs -Wait"
    exit /b
)

set /p confirm="Are you sure you want to uninstall %APP_NAME%? (Y/N): "
if /i not "%confirm%"=="Y" (
    echo Uninstallation cancelled.
    exit /b 0
)

REM ── Guard: abort if the application is still running ──────────────────────
tasklist /FI "IMAGENAME eq EliteDataCollector.UI.exe" 2>nul | find /I "EliteDataCollector.UI.exe" >nul
if %errorlevel% equ 0 (
    echo.
    echo [ERROR] Elite Data Collector GUI is currently running.
    echo         Please close the application and run the uninstaller again.
    echo.
    pause
    exit /b 1
)
tasklist /FI "IMAGENAME eq EliteDataCollector.Host.exe" 2>nul | find /I "EliteDataCollector.Host.exe" >nul
if %errorlevel% equ 0 (
    echo.
    echo [ERROR] Elite Data Collector Terminal is currently running.
    echo         Please close the application and run the uninstaller again.
    echo.
    pause
    exit /b 1
)

REM Remove shortcuts (system-wide locations matching install.bat)
echo Removing shortcuts...
del "%ProgramData%\Microsoft\Windows\Start Menu\Programs\%APP_NAME%\%APP_NAME%.lnk" /f /q 2>nul
rmdir "%ProgramData%\Microsoft\Windows\Start Menu\Programs\%APP_NAME%" 2>nul
del "%PUBLIC%\Desktop\%APP_NAME%.lnk" /f /q 2>nul

REM Remove installation directory
echo Removing application files...
if exist "%INSTALL_PATH%" (
    rmdir /s /q "%INSTALL_PATH%"
)

REM Remove registry entries
echo Removing registry entries...
reg delete "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\%APP_NAME%" /f 2>nul
reg delete "HKCU\Software\%APP_NAME%" /f 2>nul

echo.
echo ========================================
echo  Uninstallation complete!
echo ========================================
echo.
pause
exit /b 0
