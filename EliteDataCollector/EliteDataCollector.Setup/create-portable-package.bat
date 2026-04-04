@echo off
REM Create a portable ZIP installer package

setlocal enabledelayedexpansion

set "VERSION=1.0.0.0"
set "SOURCE_DIR=..\EliteDataCollector.Host\bin\Release\net10.0-windows"
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
    $files = Get-ChildItem -Path '%CD%\..\EliteDataCollector.Host\bin\Release\net10.0-windows' -Recurse; ^
    $zip = '%CD%\%PACKAGE_NAME%'; ^
    if (Test-Path $zip) { Remove-Item $zip }; ^
    [System.IO.Compression.ZipFile]::CreateFromDirectory('%CD%\..\EliteDataCollector.Host\bin\Release\net10.0-windows', $zip); ^
    Write-Host 'Package created: ' + $zip"

echo.
echo ========================================
echo Package Created!
echo ========================================
echo.
echo Package: %PACKAGE_NAME%
echo.
pause
