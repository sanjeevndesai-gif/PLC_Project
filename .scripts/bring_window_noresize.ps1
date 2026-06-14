$path = 'C:\Sanjeev Project\akshay project\PLC_Project\CopaFormGui\bin\Debug\net48\CopaFormGui.exe'
# Stop existing processes
Get-Process -Name CopaFormGui -ErrorAction SilentlyContinue | ForEach-Object { try{ Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue } catch{} }
# Start process
$proc = Start-Process -FilePath $path -PassThru -ErrorAction Stop
# Wait for main window handle
$wait=0
while($wait -lt 50){ Start-Sleep -Milliseconds 200; $proc.Refresh(); if($proc.MainWindowHandle -ne 0){ break }; $wait++ }
if($proc.MainWindowHandle -eq 0){ Write-Output "NO_WINDOW_HANDLE"; $proc | Format-List Id,ProcessName,StartTime,HasExited; exit 1 }
$signature = @'
using System;
using System.Runtime.InteropServices;
public static class Win32 {
 [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd,int nCmdShow);
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
'@
Add-Type -TypeDefinition $signature -ErrorAction SilentlyContinue
$hwnd = [IntPtr]$proc.MainWindowHandle
[Win32]::ShowWindow($hwnd,9) | Out-Null
[Win32]::SetForegroundWindow($hwnd) | Out-Null
Write-Output "OK_WINDOW_HANDLE_NORESIZE:$($proc.MainWindowHandle) PID:$($proc.Id)"