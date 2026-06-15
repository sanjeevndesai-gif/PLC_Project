# Bring CopaFormGui windows to foreground
$p = Get-Process -Name CopaFormGui -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $p) { Write-Output "NOT_RUNNING"; exit 0 }
$procId = $p.Id

$source = @"
using System;
using System.Runtime.InteropServices;
public class Win32 {
  [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr hWnd,int nCmdShow);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
  public struct RECT{ public int Left; public int Top; public int Right; public int Bottom; }
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
  public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
  public static void RestoreAndFocus(uint pid){
    EnumWindows(delegate(IntPtr hWnd, IntPtr lParam){
      uint p; GetWindowThreadProcessId(hWnd, out p);
      if(p==pid){
        RECT r; GetWindowRect(hWnd,out r);
        System.Console.WriteLine("HWND:"+hWnd+" Rect:"+r.Left+","+r.Top+"-"+r.Right+","+r.Bottom+" Visible:"+IsWindowVisible(hWnd));
        ShowWindowAsync(hWnd,9);
        SetForegroundWindow(hWnd);
      }
      return true;
    }, IntPtr.Zero);
  }
}
"@

Add-Type $source
[Win32]::RestoreAndFocus([uint32]$procId)
