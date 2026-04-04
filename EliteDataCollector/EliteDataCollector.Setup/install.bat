@echo off
REM Elite Data Collector Installer
REM Usage: install.bat [options]

setlocal enabledelayedexpansion

REM Define installation paths
if "%ProgramFiles(x86)%" neq "" (
    set "INSTALL_PATH=%ProgramFiles(x86)%\Elite Data Collector"
) else (
    set "INSTALL_PATH=%ProgramFiles%\Elite Data Collector"
)

set "SOURCE_PATH=%~dp0"

echo.
echo ========================================
echo Elite Data Collector - Installation
echo ========================================
echo.
echo Installing to: %INSTALL_PATH%
echo.

REM Create installation directory
if not exist "%INSTALL_PATH%" (
    mkdir "%INSTALL_PATH%"
    echo [OK] Created installation directory
) else (
    echo [INFO] Installation directory already exists
)

REM Copy application files
echo Copying application files...
xcopy /E /I /Y "%SOURCE_PATH%bin\Release\net10.0-windows\*" "%INSTALL_PATH%\" >nul 2>&1

if %errorlevel% equ 0 (
    echo [OK] Application files copied
) else (
    echo [ERROR] Failed to copy application files
    pause
    exit /b 1
)

REM Create Start Menu shortcut
echo Creating Start Menu shortcut...
set "APPDATA_PATH=%APPDATA%\Microsoft\Windows\Start Menu\Programs"
if not exist "%APPDATA_PATH%\Elite Data Collector" (
    mkdir "%APPDATA_PATH%\Elite Data Collector"
)

REM Create shortcut using VBScript (requires Windows Script Host)
call :createShortcut "%APPDATA_PATH%\Elite Data Collector\Elite Data Collector.lnk" "%INSTALL_PATH%\EliteDataCollector.Host.exe" "%INSTALL_PATH%"

REM Create Desktop shortcut
call :createShortcut "%USERPROFILE%\Desktop\Elite Data Collector.lnk" "%INSTALL_PATH%\EliteDataCollector.Host.exe" "%INSTALL_PATH%"

REM Add to Programs and Features uninstaller registry
echo Adding to Programs and Features...
setlocal enabledelayedexpansion
for /f "delims=" %%i in ('wmic os get currentversion /value ^| find "="') do set "WINVER=%%i"
set "REGPATH=HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\Elite Data Collector"

REM Create Add/Remove Programs entry
reg add "%REGPATH%" /v "DisplayName" /d "Elite Data Collector" /f >nul 2>&1
reg add "%REGPATH%" /v "DisplayVersion" /d "1.0.0.0" /f >nul 2>&1
reg add "%REGPATH%" /v "InstallLocation" /d "%INSTALL_PATH%" /f >nul 2>&1
reg add "%REGPATH%" /v "UninstallString" /d "%INSTALL_PATH%\uninstall.bat" /f >nul 2>&1

echo.
echo ========================================
echo Installation Complete!
echo ========================================
echo.
echo Elite Data Collector has been successfully installed to:
echo %INSTALL_PATH%
echo.
echo You can find shortcuts on your Desktop and Start Menu.
echo.
pause
exit /b 0

REM Function to create shortcuts
:createShortcut
setlocal
set "shortcutPath=%~1"
set "targetPath=%~2"
set "workingDir=%~3"

if exist "!shortcutPath!" del "!shortcutPath!"

cscript //nologo "%~dp0createShortcut.vbs" "!shortcutPath!" "!targetPath!" "!workingDir!" 2>nul

if %errorlevel% equ 0 (
    echo [OK] Shortcut created: !shortcutPath!
) else (
    echo [WARNING] Could not create shortcut: !shortcutPath!
)

endlocal
exit /b 0
