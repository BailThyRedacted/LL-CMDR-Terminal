#Requires -RunAsAdministrator
<#
.SYNOPSIS
EliteDataCollector Installation Script (PowerShell)

.DESCRIPTION
Installs Elite Data Collector with full system integration

.PARAMETER InstallPath
Installation directory (default: Program Files)

.PARAMETER CreateDesktopShortcut
Create desktop shortcut (default: $true)

.PARAMETER CreateStartMenuShortcut
Create Start Menu shortcut (default: $true)
#>

param(
    [string]$InstallPath = $null,
    [bool]$CreateDesktopShortcut = $true,
    [bool]$CreateStartMenuShortcut = $true,
    [bool]$SkipPrompt = $false
)

$ErrorActionPreference = "Stop"

# Configuration
$AppName = "Elite Data Collector"
$AppVersion = "1.0.0.0"
$AppExecutable = "EliteDataCollector.Host.exe"
$Manufacturer = "Elite Data Collector"

# Determine installation path
if ([string]::IsNullOrEmpty($InstallPath)) {
    if ([Environment]::Is64BitOperatingSystem) {
        $InstallPath = Join-Path $env:ProgramFiles $AppName
    } else {
        $InstallPath = Join-Path ${env:ProgramFiles(x86)} $AppName
    }
}

# Get source directory (where this script is run from)
$SourcePath = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "$AppName - Installation" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Version: $AppVersion"
Write-Host "Source:  $SourcePath"
Write-Host "Target:  $InstallPath"
Write-Host ""

# Confirm installation
if (-not $SkipPrompt) {
    $response = Read-Host "Continue with installation? (Y/N)"
    if ($response -ne "Y" -and $response -ne "yes") {
        Write-Host "Installation cancelled."
        exit 0
    }
}

# Create installation directory
try {
    if (-not (Test-Path $InstallPath)) {
        New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
        Write-Host "[OK] Created installation directory" -ForegroundColor Green
    } else {
        Write-Host "[INFO] Installation directory already exists" -ForegroundColor Yellow
    }
}
catch {
    Write-Host "[ERROR] Failed to create installation directory: $_" -ForegroundColor Red
    exit 1
}

# Copy application files
try {
    Write-Host "Copying application files..."
    Get-ChildItem -Path $SourcePath -Exclude "*.ps1", "*.bat", "*.vbs", "*.md", "*.wxs", "*.wixproj" | 
        ForEach-Object {
            if ($_.PSIsContainer) {
                Copy-Item -Path $_.FullName -Destination $InstallPath -Recurse -Force
            } else {
                Copy-Item -Path $_.FullName -Destination $InstallPath -Force
            }
        }
    
    Write-Host "[OK] Application files copied" -ForegroundColor Green
}
catch {
    Write-Host "[ERROR] Failed to copy application files: $_" -ForegroundColor Red
    exit 1
}

# Create shortcuts
$DesktopPath = [Environment]::GetFolderPath("Desktop")
$StartMenuPath = [Environment]::GetFolderPath("StartMenu")
$AppShortcutDir = Join-Path $StartMenuPath "Programs\$AppName"

function Create-Shortcut {
    param(
        [string]$ShortcutPath,
        [string]$TargetPath,
        [string]$WorkingDirectory = $null
    )
    
    try {
        $WshShell = New-Object -ComObject WScript.Shell
        $Shortcut = $WshShell.CreateShortcut($ShortcutPath)
        $Shortcut.TargetPath = $TargetPath
        if ($WorkingDirectory) {
            $Shortcut.WorkingDirectory = $WorkingDirectory
        }
        $Shortcut.Description = "Monitor Elite Dangerous gameplay and collect exobiology data"
        $Shortcut.Save()
        return $true
    }
    catch {
        Write-Host "[WARNING] Could not create shortcut: $_" -ForegroundColor Yellow
        return $false
    }
}

if ($CreateStartMenuShortcut) {
    try {
        if (-not (Test-Path $AppShortcutDir)) {
            New-Item -ItemType Directory -Path $AppShortcutDir -Force | Out-Null
        }
        $ShortcutPath = Join-Path $AppShortcutDir "$AppName.lnk"
        $AppExePath = Join-Path $InstallPath $AppExecutable
        Create-Shortcut -ShortcutPath $ShortcutPath -TargetPath $AppExePath -WorkingDirectory $InstallPath
        Write-Host "[OK] Start Menu shortcut created" -ForegroundColor Green
    }
    catch {
        Write-Host "[WARNING] Failed to create Start Menu shortcut: $_" -ForegroundColor Yellow
    }
}

if ($CreateDesktopShortcut) {
    try {
        $ShortcutPath = Join-Path $DesktopPath "$AppName.lnk"
        $AppExePath = Join-Path $InstallPath $AppExecutable
        Create-Shortcut -ShortcutPath $ShortcutPath -TargetPath $AppExePath -WorkingDirectory $InstallPath
        Write-Host "[OK] Desktop shortcut created" -ForegroundColor Green
    }
    catch {
        Write-Host "[WARNING] Failed to create Desktop shortcut: $_" -ForegroundColor Yellow
    }
}

# Add to registry (Add/Remove Programs)
try {
    $RegPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$AppName"
    if (-not (Test-Path $RegPath)) {
        New-Item -Path $RegPath -Force | Out-Null
    }
    
    Set-ItemProperty -Path $RegPath -Name "DisplayName" -Value $AppName
    Set-ItemProperty -Path $RegPath -Name "DisplayVersion" -Value $AppVersion
    Set-ItemProperty -Path $RegPath -Name "Manufacturer" -Value $Manufacturer
    Set-ItemProperty -Path $RegPath -Name "InstallLocation" -Value $InstallPath
    Set-ItemProperty -Path $RegPath -Name "UninstallString" -Value "$PSHOME\powershell.exe -NoProfile -Command `"Remove-Item -Recurse -Force '$InstallPath'`""
    
    Write-Host "[OK] Added to Add/Remove Programs" -ForegroundColor Green
}
catch {
    Write-Host "[WARNING] Failed to add registry entries: $_" -ForegroundColor Yellow
}

# Final message
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Installation Complete!" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "$AppName has been installed to:" -ForegroundColor Green
Write-Host $InstallPath -ForegroundColor Green
Write-Host ""
Write-Host "You can find shortcuts on your Desktop and Start Menu." -ForegroundColor Green
Write-Host ""
