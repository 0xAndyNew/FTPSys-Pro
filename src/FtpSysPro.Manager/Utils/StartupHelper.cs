using System;
using System.Diagnostics;
using System.Windows.Forms;
using Microsoft.Win32;

namespace FtpSysPro.Manager.Utils
{
    public static class StartupHelper
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppRunName = "FTPSys Pro Manager";

        public static bool IsRunAtStartup()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    return key?.GetValue(AppRunName) != null;
                }
            }
            catch
            {
                return false;
            }
        }

        public static void SetRunAtStartup(bool enable, bool minimized = true)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key == null) return;
                    if (enable)
                    {
                        string exePath = Application.ExecutablePath;
                        string val = minimized ? $"\"{exePath}\" --minimized" : $"\"{exePath}\"";
                        key.SetValue(AppRunName, val);
                    }
                    else
                    {
                        if (key.GetValue(AppRunName) != null)
                        {
                            key.DeleteValue(AppRunName, false);
                        }
                    }
                }
            }
            catch { }

            // Clean up any legacy scheduled task if it was created by older installers
            try
            {
                if (!enable)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "schtasks.exe",
                        Arguments = "/Delete /TN \"FTPSys Pro Manager Startup\" /F",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                }
            }
            catch { }
        }
    }
}
