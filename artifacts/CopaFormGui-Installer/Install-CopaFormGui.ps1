
param(
    [string]$InstallRoot = ${env:ProgramFiles},
    [string]$ProductFolderName = "CopaFormGui",
    [string]$SourceDir = (Join-Path $PSScriptRoot "app"),
    [string]$LicenseFileName = "license.json",
    [switch]$CreateDesktopShortcut = $true
)

Write-Host "[INFO] Starting CopaFormGui installer script..."
Write-Host "[INFO] InstallRoot: $InstallRoot"
Write-Host "[INFO] ProductFolderName: $ProductFolderName"
Write-Host "[INFO] SourceDir: $SourceDir"
Write-Host "[INFO] LicenseFileName: $LicenseFileName"
Write-Host "[INFO] CreateDesktopShortcut: $CreateDesktopShortcut"


Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Windows.Forms


function Test-IsAdministrator {
    $currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentIdentity)
    $isAdmin = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    Write-Host "[INFO] Is administrator: $isAdmin"
    return $isAdmin
}


function Ensure-Administrator {
    if (Test-IsAdministrator) {
        Write-Host "[INFO] Running as administrator."
        return
    }

    Write-Host "[INFO] Not running as administrator. Attempting to relaunch as admin..."
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

    Write-Host "[INFO] Relaunching with arguments: $arguments"
    Start-Process -FilePath "powershell.exe" -Verb RunAs -ArgumentList $arguments
    exit 0
}


Write-Host "[INFO] Checking administrator privileges..."
Ensure-Administrator


Write-Host "[INFO] Checking if application payload folder exists: $SourceDir"
if (-not (Test-Path -Path $SourceDir -PathType Container)) {
    Write-Host "[ERROR] Application payload folder not found: $SourceDir"
    throw "Application payload folder not found: $SourceDir"
}
Write-Host "[INFO] Application payload folder found."


$installDir = Join-Path $InstallRoot $ProductFolderName
Write-Host "[INFO] Target install directory: $installDir"
if (-not (Test-Path -Path $installDir -PathType Container)) {
    Write-Host "[INFO] Creating install directory: $installDir"
    New-Item -Path $installDir -ItemType Directory -Force | Out-Null
}
Write-Host "[INFO] Checking for running CopaFormGui.exe processes..."
$procList = Get-Process -Name "CopaFormGui" -ErrorAction SilentlyContinue
if ($procList) {
    Write-Host "[INFO] Found running CopaFormGui processes. Attempting to stop them..."
    foreach ($proc in $procList) {
        try {
            $proc.Kill()
            Write-Host "[INFO] Killed process ID $($proc.Id)"
        } catch {
            Write-Host "[WARN] Failed to kill process ID $($proc.Id): $_"
        }
    }
    Start-Sleep -Seconds 2
} else {
    Write-Host "[INFO] No running CopaFormGui.exe processes found."
}
Write-Host "[INFO] Copying files from $SourceDir to $installDir ..."
Copy-Item -Path (Join-Path $SourceDir '*') -Destination $installDir -Recurse -Force
Write-Host "[INFO] Files copied."


$programDataDir = Join-Path $env:ProgramData $ProductFolderName
Write-Host "[INFO] ProgramData directory: $programDataDir"
if (-not (Test-Path -Path $programDataDir -PathType Container)) {
    Write-Host "[INFO] Creating ProgramData directory: $programDataDir"
    New-Item -Path $programDataDir -ItemType Directory -Force | Out-Null
}

$sourceLicensePath = Join-Path $installDir $LicenseFileName
Write-Host "[INFO] License file path: $sourceLicensePath"

if (Test-Path -Path $sourceLicensePath -PathType Leaf) {
    Write-Host "[INFO] Copying license file to ProgramData..."
    Copy-Item -Path $sourceLicensePath -Destination (Join-Path $programDataDir $LicenseFileName) -Force
    Write-Host "[INFO] License file copied."
} else {
    Write-Host "[WARN] License file not found: $sourceLicensePath"
}


$exePath = Join-Path $installDir "CopaFormGui.exe"
$shortcutPath = Join-Path ([Environment]::GetFolderPath("Desktop")) "Copa Form GUI.lnk"
Write-Host "[INFO] EXE path: $exePath"
if ($CreateDesktopShortcut -and (Test-Path -Path $exePath -PathType Leaf)) {
    Write-Host "[INFO] Creating desktop shortcut: $shortcutPath"
    $wshShell = New-Object -ComObject WScript.Shell
    $shortcut = $wshShell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $exePath
    $shortcut.WorkingDirectory = $installDir
    $shortcut.IconLocation = $exePath
    $shortcut.Save()
    Write-Host "[INFO] Desktop shortcut created."
} else {
    Write-Host "[WARN] EXE not found for shortcut: $exePath"
}


$uninstallScript = Join-Path $installDir "Uninstall-CopaFormGui.ps1"
Write-Host "[INFO] Checking for uninstall script: $uninstallScript"
if (-not (Test-Path -Path $uninstallScript -PathType Leaf)) {
    Write-Host "[ERROR] Uninstall script missing from installation payload: $uninstallScript"
    throw "Uninstall script missing from installation payload: $uninstallScript"
}
Write-Host "[INFO] Uninstall script found."


Write-Host "[SUCCESS] Copa Form GUI installed to $installDir"
Write-Host "[SUCCESS] License copied to $(Join-Path $programDataDir $LicenseFileName)"
if ($CreateDesktopShortcut) {
    Write-Host "[SUCCESS] Desktop shortcut created: $shortcutPath"
}


Write-Host "[INFO] Showing completion message box."
[System.Windows.Forms.MessageBox]::Show(
    "Copa Form GUI installation completed successfully.",
    "Copa Form GUI Installer",
    [System.Windows.Forms.MessageBoxButtons]::OK,
    [System.Windows.Forms.MessageBoxIcon]::Information
) | Out-Null