param(
    [Parameter(Mandatory = $true)]
    [string]$InstallDir,

    [string]$ProductFolderName = "CopaFormGui",
    [string]$LicenseFileName = "license.json"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$sourceLicensePath = Join-Path $InstallDir $LicenseFileName
if (-not (Test-Path -Path $sourceLicensePath -PathType Leaf)) {
    throw "License file not found at $sourceLicensePath"
}

$targetDir = Join-Path $env:ProgramData $ProductFolderName
$targetLicensePath = Join-Path $targetDir $LicenseFileName

if (-not (Test-Path -Path $targetDir -PathType Container)) {
    New-Item -Path $targetDir -ItemType Directory -Force | Out-Null
}

Copy-Item -Path $sourceLicensePath -Destination $targetLicensePath -Force
Write-Host "License copied to $targetLicensePath"
