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

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private static uint GetActiveWindowProcessPid()
        {
            IntPtr hwnd = GetForegroundWindow();
            GetWindowThreadProcessId(hwnd, out uint pid);
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

        public static void HideConsoleWindow()
        {
            const int SW_HIDE = 0;
            IntPtr handle = GetConsoleWindow();
            ShowWindow(handle, SW_HIDE);
        }
    }
}
