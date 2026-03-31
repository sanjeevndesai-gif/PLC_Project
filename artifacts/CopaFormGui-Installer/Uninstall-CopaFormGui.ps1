# Uninstall-CopaFormGui.ps1
$installDir = Join-Path ${env:ProgramFiles} "CopaFormGui"
if (Test-Path $installDir) {
    Remove-Item -Path $installDir -Recurse -Force
    Write-Host "CopaFormGui uninstalled from $installDir"
} else {
    Write-Host "CopaFormGui not found in $installDir"
}
# Optionally remove desktop shortcut
$shortcutPath = Join-Path ([Environment]::GetFolderPath("Desktop")) "Copa Form GUI.lnk"
if (Test-Path $shortcutPath) {
    Remove-Item $shortcutPath -Force
    Write-Host "Desktop shortcut removed."
}