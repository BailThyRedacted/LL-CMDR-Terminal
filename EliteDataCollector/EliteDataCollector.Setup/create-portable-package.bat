@echo off
REM Create a portable ZIP installer package

setlocal enabledelayedexpansion

set "VERSION=2.1.0.0"
set "SOURCE_DIR=..\EliteDataCollector.UI\bin\Release\net8.0-windows10.0.19041.0\win-x64"
set "PACKAGE_NAME=Elite-Data-Collector-%VERSION%.zip"

echo.
echo ========================================
echo Creating Portable Package
echo ========================================
echo.

if not exist "%SOURCE_DIR%" (
    echo [ERROR] Source directory not found: %SOURCE_DIR%
    pause
    exit /b 1
)

REM Check for PowerShell to create ZIP
powershell -NoProfile -Command ^
    "Add-Type -AssemblyName System.IO.Compression.FileSystem; ^
    $files = Get-ChildItem -Path '%CD%\..\EliteDataCollector.UI\bin\Release\net8.0-windows10.0.19041.0\win-x64' -Recurse; ^
    $zip = '%CD%\%PACKAGE_NAME%'; ^
    if (Test-Path $zip) { Remove-Item $zip }; ^
    [System.IO.Compression.ZipFile]::CreateFromDirectory('%CD%\..\EliteDataCollector.UI\bin\Release\net8.0-windows10.0.19041.0\win-x64', $zip); ^
    Write-Host 'Package created: ' + $zip"

echo.
echo ========================================
echo Package Created!
echo ========================================
echo.
echo Package: %PACKAGE_NAME%
echo.
pause
