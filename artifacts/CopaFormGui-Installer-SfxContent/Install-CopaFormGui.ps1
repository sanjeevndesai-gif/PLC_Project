param(
    [string]$InstallRoot = ${env:ProgramFiles},
    [string]$ProductFolderName = "CopaFormGui",
    [string]$SourceDir = (Join-Path $PSScriptRoot "app"),
    [string]$LicenseFileName = "license.json",
    [switch]$CreateDesktopShortcut = $true
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Windows.Forms

function Test-IsAdministrator {
    $currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentIdentity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Ensure-Administrator {
    if (Test-IsAdministrator) {
        return
    }

    $arguments = @(
        "-NoProfile"
        "-ExecutionPolicy", "Bypass"
        "-File", ('"{0}"' -f $PSCommandPath)
        "-InstallRoot", ('"{0}"' -f $InstallRoot)
        "-ProductFolderName", ('"{0}"' -f $ProductFolderName)
        "-SourceDir", ('"{0}"' -f $SourceDir)
        "-LicenseFileName", ('"{0}"' -f $LicenseFileName)
    )

    if ($CreateDesktopShortcut) {
        $arguments += "-CreateDesktopShortcut"
    }

    Start-Process -FilePath "powershell.exe" -Verb RunAs -ArgumentList $arguments
    exit 0
}

Ensure-Administrator

if (-not (Test-Path -Path $SourceDir -PathType Container)) {
    throw "Application payload folder not found: $SourceDir"
}

$installDir = Join-Path $InstallRoot $ProductFolderName
if (-not (Test-Path -Path $installDir -PathType Container)) {
    New-Item -Path $installDir -ItemType Directory -Force | Out-Null
}

Copy-Item -Path (Join-Path $SourceDir '*') -Destination $installDir -Recurse -Force

$programDataDir = Join-Path $env:ProgramData $ProductFolderName
if (-not (Test-Path -Path $programDataDir -PathType Container)) {
    New-Item -Path $programDataDir -ItemType Directory -Force | Out-Null
}

$sourceLicensePath = Join-Path $installDir $LicenseFileName
if (Test-Path -Path $sourceLicensePath -PathType Leaf) {
    Copy-Item -Path $sourceLicensePath -Destination (Join-Path $programDataDir $LicenseFileName) -Force
}

$exePath = Join-Path $installDir "CopaFormGui.exe"
$shortcutPath = Join-Path ([Environment]::GetFolderPath("Desktop")) "Copa Form GUI.lnk"
if ($CreateDesktopShortcut -and (Test-Path -Path $exePath -PathType Leaf)) {
    $wshShell = New-Object -ComObject WScript.Shell
    $shortcut = $wshShell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $exePath
    $shortcut.WorkingDirectory = $installDir
    $shortcut.IconLocation = $exePath
    $shortcut.Save()
}

$uninstallScript = Join-Path $installDir "Uninstall-CopaFormGui.ps1"
if (-not (Test-Path -Path $uninstallScript -PathType Leaf)) {
    throw "Uninstall script missing from installation payload: $uninstallScript"
}

Write-Host "Copa Form GUI installed to $installDir"
Write-Host "License copied to $(Join-Path $programDataDir $LicenseFileName)"
if ($CreateDesktopShortcut) {
    Write-Host "Desktop shortcut created: $shortcutPath"
}

[System.Windows.Forms.MessageBox]::Show(
    "Copa Form GUI installation completed successfully.",
    "Copa Form GUI Installer",
    [System.Windows.Forms.MessageBoxButtons]::OK,
    [System.Windows.Forms.MessageBoxIcon]::Information
) | Out-Null