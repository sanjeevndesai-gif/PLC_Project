# Copy latest Release build to installer app folder before running installer
$buildDir = "C:\Sanjeev Project\akshay project\PLC_Project\CopaFormGui\bin\Release\net48"
$installerAppDir = "C:\Sanjeev Project\akshay project\PLC_Project\artifacts\CopaFormGui-Installer\app"

Write-Host "Copying latest build output from $buildDir to $installerAppDir ..."
if (!(Test-Path $buildDir)) {
    Write-Host "[ERROR] Build directory not found: $buildDir"
    exit 1
}
if (!(Test-Path $installerAppDir)) {
    Write-Host "[INFO] Creating installer app directory: $installerAppDir"
    New-Item -Path $installerAppDir -ItemType Directory -Force | Out-Null
}

Copy-Item -Path (Join-Path $buildDir '*') -Destination $installerAppDir -Recurse -Force
Write-Host "[SUCCESS] Build output copied to installer app folder."
