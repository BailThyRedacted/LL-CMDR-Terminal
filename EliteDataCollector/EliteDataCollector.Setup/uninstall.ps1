#Requires -RunAsAdministrator
<#
.SYNOPSIS
EliteDataCollector Uninstallation Script (PowerShell)

.DESCRIPTION
Removes EliteDataCollector from the system
#>

$ErrorActionPreference = "Stop"

# Configuration
$AppName = "Elite Data Collector"
$InstallPath = $null

# Find installation path from registry (HKLM — matches install.ps1)
$RegPath = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\$AppName"
if (Test-Path $RegPath) {
    $InstallPath = (Get-ItemProperty -Path $RegPath -Name "InstallLocation" -ErrorAction SilentlyContinue).InstallLocation
}

# Ask user for installation path if not found
if ([string]::IsNullOrEmpty($InstallPath)) {
    $InstallPath = Join-Path $env:ProgramFiles $AppName
    Write-Host "Could not find installation location in registry. Using default: $InstallPath"
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " $AppName - Uninstaller" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Installation path: $InstallPath"
Write-Host ""

# Guard: abort if the application is still running
$procUI = Get-Process -Name "EliteDataCollector.UI" -ErrorAction SilentlyContinue
$procHost = Get-Process -Name "EliteDataCollector.Host" -ErrorAction SilentlyContinue
if ($procUI -or $procHost) {
    Write-Host "[ERROR] Elite Data Collector is currently running." -ForegroundColor Red
    Write-Host "        Please close the application and run the uninstaller again." -ForegroundColor Red
    exit 1
}

$confirm = Read-Host "Are you sure you want to uninstall $AppName? (Y/N)"
if ($confirm -notmatch '^y(es)?$') {
    Write-Host "Uninstallation cancelled."
    exit 0
}

# Remove shortcuts (system-wide locations matching install.ps1)
Write-Host "Removing shortcuts..."
$DesktopPath    = [Environment]::GetFolderPath("CommonDesktopDirectory")
$StartMenuPath  = [Environment]::GetFolderPath("CommonStartMenu")

Remove-Item -Path (Join-Path $DesktopPath "$AppName.lnk") -Force -ErrorAction SilentlyContinue
Remove-Item -Path (Join-Path $StartMenuPath "Programs\$AppName") -Recurse -Force -ErrorAction SilentlyContinue

# Remove installation directory
Write-Host "Removing application files..."
if (Test-Path $InstallPath) {
    Remove-Item -Path $InstallPath -Recurse -Force -ErrorAction Stop
    Write-Host "[OK] Application files removed" -ForegroundColor Green
}

# Remove registry entries
Write-Host "Removing registry entries..."
Remove-Item -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\$AppName" -Force -ErrorAction SilentlyContinue
Remove-Item -Path "HKCU:\Software\$AppName" -Recurse -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Uninstallation complete!" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
