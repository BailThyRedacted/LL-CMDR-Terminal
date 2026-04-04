#Requires -RunAsAdministrator
<#
.SYNOPSIS
EliteDataCollector Installation Script (PowerShell)

.DESCRIPTION
Publishes and installs Elite Data Collector with full system integration.
Runs dotnet publish (self-contained, win-x64) then copies to Program Files.

.PARAMETER InstallPath
Installation directory (default: Program Files\Elite Data Collector)

.PARAMETER CreateDesktopShortcut
Create desktop shortcut (default: $true)

.PARAMETER CreateStartMenuShortcut
Create Start Menu shortcut (default: $true)

.PARAMETER SkipPrompt
Skip the confirmation prompt (default: $false)
#>

param(
    [string]$InstallPath = $null,
    [bool]$CreateDesktopShortcut = $true,
    [bool]$CreateStartMenuShortcut = $true,
    [bool]$SkipPrompt = $false
)

$ErrorActionPreference = "Stop"

# Configuration
$AppName        = "Elite Data Collector"
$AppVersion     = "2.1.0.0"
$AppExecutable  = "EliteDataCollector.Host.exe"
$Manufacturer   = "LL CMDR Terminal"

# Determine installation path
if ([string]::IsNullOrEmpty($InstallPath)) {
    $InstallPath = Join-Path $env:ProgramFiles $AppName
}

# Script and project locations
$ScriptDir   = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectFile = Join-Path $ScriptDir "..\EliteDataCollector.Host\EliteDataCollector.Host.csproj"
$PublishDir  = Join-Path $ScriptDir "publish"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " $AppName v$AppVersion - Installer" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Target: $InstallPath"
Write-Host ""

# Verify dotnet SDK
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "[ERROR] dotnet SDK not found. Download from: https://dot.net/download" -ForegroundColor Red
    exit 1
}

# Confirm installation
if (-not $SkipPrompt) {
    $response = Read-Host "Continue with installation? (Y/N)"
    if ($response -notmatch '^y(es)?$') {
        Write-Host "Installation cancelled."
        exit 0
    }
}

# Build self-contained package
Write-Host "[1/4] Building self-contained package..." -ForegroundColor Cyan
try {
    $result = dotnet publish $ProjectFile `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        --output $PublishDir `
        -p:PublishSingleFile=false `
        /nologo /consoleloggerparameters:ErrorsOnly
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish exited with code $LASTEXITCODE" }
    Write-Host "[OK]   Build complete" -ForegroundColor Green
}
catch {
    Write-Host "[ERROR] Build failed: $_" -ForegroundColor Red
    exit 1
}

# Source is now the publish output
$SourcePath = $PublishDir

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
Write-Host "[2/4] Installing to $InstallPath..." -ForegroundColor Cyan
try {
    Copy-Item -Path "$SourcePath\*" -Destination $InstallPath -Recurse -Force

    # Copy uninstaller tools so the Add/Remove Programs entry works standalone
    Copy-Item -Path (Join-Path $ScriptDir "uninstall.ps1")      -Destination $InstallPath -Force
    Copy-Item -Path (Join-Path $ScriptDir "createShortcut.vbs") -Destination $InstallPath -Force

    Write-Host "[OK]   Files installed" -ForegroundColor Green
}
catch {
    Write-Host "[ERROR] Failed to copy application files: $_" -ForegroundColor Red
    exit 1
}

# Create shortcuts
Write-Host "[3/4] Creating shortcuts..." -ForegroundColor Cyan
$DesktopPath    = [Environment]::GetFolderPath("CommonDesktopDirectory")
$StartMenuPath  = [Environment]::GetFolderPath("CommonStartMenu")
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
        $AppExePath   = Join-Path $InstallPath $AppExecutable
        if (Create-Shortcut -ShortcutPath $ShortcutPath -TargetPath $AppExePath -WorkingDirectory $InstallPath) {
            Write-Host "[OK]   Start Menu shortcut" -ForegroundColor Green
        }
    }
    catch {
        Write-Host "[WARN] Failed to create Start Menu shortcut: $_" -ForegroundColor Yellow
    }
}

if ($CreateDesktopShortcut) {
    try {
        $ShortcutPath = Join-Path $DesktopPath "$AppName.lnk"
        $AppExePath   = Join-Path $InstallPath $AppExecutable
        if (Create-Shortcut -ShortcutPath $ShortcutPath -TargetPath $AppExePath -WorkingDirectory $InstallPath) {
            Write-Host "[OK]   Desktop shortcut" -ForegroundColor Green
        }
    }
    catch {
        Write-Host "[WARN] Failed to create Desktop shortcut: $_" -ForegroundColor Yellow
    }
}

# Add to registry (Add/Remove Programs) — HKLM for system-wide install
Write-Host "[4/4] Registering with Windows..." -ForegroundColor Cyan
try {
    $RegPath       = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\$AppName"
    $UninstallCmd  = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$InstallPath\uninstall.ps1`""
    if (-not (Test-Path $RegPath)) {
        New-Item -Path $RegPath -Force | Out-Null
    }

    Set-ItemProperty -Path $RegPath -Name "DisplayName"     -Value $AppName
    Set-ItemProperty -Path $RegPath -Name "DisplayVersion"  -Value $AppVersion
    Set-ItemProperty -Path $RegPath -Name "Publisher"       -Value $Manufacturer
    Set-ItemProperty -Path $RegPath -Name "InstallLocation" -Value $InstallPath
    Set-ItemProperty -Path $RegPath -Name "UninstallString" -Value $UninstallCmd
    Set-ItemProperty -Path $RegPath -Name "NoModify"        -Value 1 -Type DWord
    Set-ItemProperty -Path $RegPath -Name "NoRepair"        -Value 1 -Type DWord

    Write-Host "[OK]   Registered" -ForegroundColor Green
}
catch {
    Write-Host "[WARN] Failed to add registry entries: $_" -ForegroundColor Yellow
}

# Final message
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Installation complete!" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host " Location : $InstallPath" -ForegroundColor Green
Write-Host " Shortcuts: Desktop and Start Menu" -ForegroundColor Green
Write-Host ""
