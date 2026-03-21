@echo off
setlocal
if exist "%~dp0app" rmdir /s /q "%~dp0app"
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Expand-Archive -Path '%~dp0CopaFormGui-Payload.zip' -DestinationPath '%~dp0app' -Force; & '%~dp0Install-CopaFormGui.ps1' -SourceDir '%~dp0app'"
if errorlevel 1 (
  echo Installation failed.
  pause
  exit /b 1
)
echo Installation finished.
pause
