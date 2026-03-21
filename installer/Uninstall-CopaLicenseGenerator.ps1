param(
    [string]$InstallRoot = ${env:ProgramFiles},
    [string]$ProductFolderName = "CopaLicenseGenerator"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Test-IsAdministrator {
    $currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentIdentity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-IsAdministrator)) {
    Start-Process -FilePath "powershell.exe" -Verb RunAs -ArgumentList @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", ('"{0}"' -f $PSCommandPath),
        "-InstallRoot", ('"{0}"' -f $InstallRoot),
        "-ProductFolderName", ('"{0}"' -f $ProductFolderName)
    )
    exit 0
}

$installDir = Join-Path $InstallRoot $ProductFolderName
$desktopShortcut = Join-Path ([Environment]::GetFolderPath("Desktop")) "Copa License Generator.lnk"

if (Test-Path -Path $installDir) {
    Remove-Item -Path $installDir -Recurse -Force
}

if (Test-Path -Path $desktopShortcut) {
    Remove-Item -Path $desktopShortcut -Force
}

Write-Host "Copa License Generator removed."