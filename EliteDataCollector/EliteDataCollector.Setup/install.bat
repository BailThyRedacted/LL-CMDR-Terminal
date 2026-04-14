@echo off
setlocal enabledelayedexpansion

set "APP_NAME=Elite Data Collector"
set "APP_VERSION=2.1.0.0"
set "APP_EXE=EliteDataCollector.UI.exe"
set "SCRIPT_DIR=%~dp0"
set "PROJECT_FILE=%SCRIPT_DIR%..\EliteDataCollector.UI\EliteDataCollector.UI.csproj"
set "PUBLISH_DIR=%SCRIPT_DIR%publish"
set "INSTALL_PATH=%ProgramFiles%\Elite Data Collector"

echo.
echo ========================================
echo  %APP_NAME% v%APP_VERSION% - Installer
echo ========================================
echo.

REM ── Step 1: UAC self-elevation ─────────────────────────────────────────────
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo  Administrator rights required. Requesting elevation...
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs -Wait"
    exit /b
)

REM ── Step 2: Verify dotnet SDK is available ─────────────────────────────────
where dotnet >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] The .NET SDK was not found on PATH.
    echo         Download it from: https://dot.net/download
    echo.
    pause
    exit /b 1
)

REM ── Step 3: Publish self-contained application ─────────────────────────────
echo [1/4] Building self-contained package...
dotnet publish "%PROJECT_FILE%" ^
    --configuration Release ^
    --output "%PUBLISH_DIR%" ^
    /nologo /consoleloggerparameters:ErrorsOnly

if %errorlevel% neq 0 (
    echo [ERROR] Build failed. See output above for details.
    echo.
    pause
    exit /b 1
)
echo [OK]   Build complete

REM ── Step 4: Install files ──────────────────────────────────────────────────
echo [2/4] Installing to %INSTALL_PATH%...
if not exist "%INSTALL_PATH%" mkdir "%INSTALL_PATH%"

robocopy "%PUBLISH_DIR%" "%INSTALL_PATH%" /E /NJH /NJS /NFL /NDL >nul
if %errorlevel% geq 8 (
    echo [ERROR] Failed to copy application files (robocopy error %errorlevel%).
    echo.
    pause
    exit /b 1
)

REM Copy uninstaller tools so Add/Remove Programs entry works standalone
copy /y "%SCRIPT_DIR%uninstall.bat"      "%INSTALL_PATH%\uninstall.bat"      >nul
copy /y "%SCRIPT_DIR%createShortcut.vbs" "%INSTALL_PATH%\createShortcut.vbs" >nul
echo [OK]   Files installed

REM ── Step 5: Create shortcuts ───────────────────────────────────────────────
echo [3/4] Creating shortcuts...

set "START_MENU=%ProgramData%\Microsoft\Windows\Start Menu\Programs\%APP_NAME%"
if not exist "%START_MENU%" mkdir "%START_MENU%"

cscript //nologo "%INSTALL_PATH%\createShortcut.vbs" ^
    "%START_MENU%\%APP_NAME%.lnk" ^
    "%INSTALL_PATH%\%APP_EXE%" ^
    "%INSTALL_PATH%" >nul 2>&1
if %errorlevel% equ 0 (echo [OK]   Start Menu shortcut) else (echo [WARN] Start Menu shortcut failed)

cscript //nologo "%INSTALL_PATH%\createShortcut.vbs" ^
    "%PUBLIC%\Desktop\%APP_NAME%.lnk" ^
    "%INSTALL_PATH%\%APP_EXE%" ^
    "%INSTALL_PATH%" >nul 2>&1
if %errorlevel% equ 0 (echo [OK]   Desktop shortcut) else (echo [WARN] Desktop shortcut failed)

REM ── Step 6: Register with Add/Remove Programs (HKLM = system-wide) ─────────
echo [4/4] Registering with Windows...
set "REG_PATH=HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\%APP_NAME%"

reg add "%REG_PATH%" /v "DisplayName"     /d "%APP_NAME%"                        /f >nul 2>&1
reg add "%REG_PATH%" /v "DisplayVersion"  /d "%APP_VERSION%"                     /f >nul 2>&1
reg add "%REG_PATH%" /v "Publisher"       /d "LL CMDR Terminal"                  /f >nul 2>&1
reg add "%REG_PATH%" /v "InstallLocation" /d "%INSTALL_PATH%"                    /f >nul 2>&1
reg add "%REG_PATH%" /v "UninstallString" /d "\"%INSTALL_PATH%\uninstall.bat\""  /f >nul 2>&1
reg add "%REG_PATH%" /v "NoModify"        /t REG_DWORD /d 1                      /f >nul 2>&1
reg add "%REG_PATH%" /v "NoRepair"        /t REG_DWORD /d 1                      /f >nul 2>&1

if %errorlevel% equ 0 (echo [OK]   Registered) else (echo [WARN] Registry write failed)

echo.
echo ========================================
echo  Installation complete!
echo ========================================
echo.
echo  Location : %INSTALL_PATH%
echo  Shortcuts: Desktop and Start Menu
echo.
pause
exit /b 0
