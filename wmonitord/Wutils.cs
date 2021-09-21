using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace wmonitord
{
    internal static class Wutils
    {

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowThreadProcessId(IntPtr hWnd, out uint ProcessId);

        private static uint GetActiveWindowProcessPid()
        {
            IntPtr hwnd = GetForegroundWindow();
            uint pid;
            GetWindowThreadProcessId(hwnd, out pid);
            return pid;            
        }

        public static (string title, string filename, uint pid) GetCurrentActiveWindowData()
        {
            uint pid = GetActiveWindowProcessPid();
            Process activeWindowProcess = Process.GetProcessById((int)pid);
            try
            {
                return (activeWindowProcess.MainWindowTitle, activeWindowProcess.MainModule.ModuleName, pid);
            }
            catch (Win32Exception)
            {
                return (string.Empty, string.Empty, 0);
            }            
        }
    }
}
