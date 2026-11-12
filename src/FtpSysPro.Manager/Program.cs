using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using FtpSysPro.Manager.UI;

namespace FtpSysPro.Manager
{
    internal static class Program
    {
        private static Mutex _singleInstanceMutex;

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern uint RegisterWindowMessage(string lpString);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        public static readonly IntPtr HWND_BROADCAST = new IntPtr(0xffff);
        public static readonly uint WM_SHOW_FTPSYSPRO = RegisterWindowMessage("FtpSysPro_Manager_ShowMainWindow");

        public static bool IsAdministrator()
        {
            try
            {
                using (var identity = System.Security.Principal.WindowsIdentity.GetCurrent())
                {
                    var principal = new System.Security.Principal.WindowsPrincipal(identity);
                    return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }

        [STAThread]
        private static void Main(string[] args)
        {
            // Enforce single instance via named mutex
            bool isFirstInstance;
            _singleInstanceMutex = new Mutex(true, "FtpSysPro.Manager.SingleInstanceMutex", out isFirstInstance);

            if (!isFirstInstance)
            {
                // App is already running in tray/desktop -> bring existing window to front
                PostMessage(HWND_BROADCAST, WM_SHOW_FTPSYSPRO, IntPtr.Zero, IntPtr.Zero);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            // Initialize application localization before opening UI forms
            try
            {
                var cfg = new FtpSysPro.Core.Config.ConfigStore().LoadConfig();
                FtpSysPro.Core.Resources.Strings.SetLanguage(cfg?.Settings?.Language ?? "en");
            }
            catch
            {
                FtpSysPro.Core.Resources.Strings.SetLanguage("en");
            }

            // Handle optional elevation request (--admin / --runas)
            bool runAsAdmin = false;
            bool startMinimized = false;
            if (args != null)
            {
                foreach (var a in args)
                {
                    if (string.Equals(a, "--admin", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(a, "--runas", StringComparison.OrdinalIgnoreCase))
                    {
                        runAsAdmin = true;
                    }
                    else if (string.Equals(a, "--minimized", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(a, "-m", StringComparison.OrdinalIgnoreCase))
                    {
                        startMinimized = true;
                    }
                }
            }

            if (runAsAdmin && !IsAdministrator())
            {
                try
                {
                    var startInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = Application.ExecutablePath,
                        UseShellExecute = true,
                        Verb = "runas"
                    };

                    var passArgs = new System.Collections.Generic.List<string>();
                    if (args != null)
                    {
                        foreach (var a in args)
                        {
                            if (!string.Equals(a, "--admin", StringComparison.OrdinalIgnoreCase) &&
                                !string.Equals(a, "--runas", StringComparison.OrdinalIgnoreCase))
                            {
                                passArgs.Add(a);
                            }
                        }
                    }
                    if (passArgs.Count > 0)
                    {
                        startInfo.Arguments = string.Join(" ", passArgs);
                    }

                    // Release mutex before launching elevated instance so it can acquire it
                    _singleInstanceMutex.ReleaseMutex();
                    _singleInstanceMutex.Dispose();
                    _singleInstanceMutex = null;

                    System.Diagnostics.Process.Start(startInfo);
                    return; // Exit non-elevated instance
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    // User cancelled the Windows UAC consent prompt; re-acquire mutex and continue standard
                    _singleInstanceMutex = new Mutex(true, "FtpSysPro.Manager.SingleInstanceMutex", out _);
                }
                catch (Exception ex)
                {
                    string errMsg = FtpSysPro.Core.Resources.Strings.IsVi
                        ? "Không thể khởi chạy với quyền Administrator: " + ex.Message
                        : "Could not restart as Administrator: " + ex.Message;
                    MessageBox.Show(errMsg, FtpSysPro.Core.Resources.Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _singleInstanceMutex = new Mutex(true, "FtpSysPro.Manager.SingleInstanceMutex", out _);
                }
            }

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                string msg = FtpSysPro.Core.Resources.Strings.IsVi
                    ? "Đã xảy ra lỗi khởi động FtpSysPro Manager:\n\n" + (ex?.Message ?? e.ExceptionObject?.ToString())
                    : "An error occurred starting FtpSysPro Manager:\n\n" + (ex?.Message ?? e.ExceptionObject?.ToString());
                MessageBox.Show(msg, FtpSysPro.Core.Resources.Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            Application.ThreadException += (s, e) =>
            {
                string msg = (FtpSysPro.Core.Resources.Strings.IsVi ? "Đã xảy ra lỗi:\n\n" : "An error occurred:\n\n") + e.Exception.Message;
                MessageBox.Show(msg, FtpSysPro.Core.Resources.Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            try
            {
                var mainForm = new MainForm();
                if (startMinimized)
                {
                    mainForm.WindowState = FormWindowState.Minimized;
                    mainForm.ShowInTaskbar = false;
                }

                Application.Run(mainForm);
            }
            catch (Exception ex)
            {
                string msg = FtpSysPro.Core.Resources.Strings.IsVi
                    ? "Đã xảy ra lỗi khởi động FtpSysPro Manager:\n\n" + ex.Message
                    : "An error occurred starting FtpSysPro Manager:\n\n" + ex.Message;
                MessageBox.Show(msg, FtpSysPro.Core.Resources.Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            GC.KeepAlive(_singleInstanceMutex);
        }
    }
}
