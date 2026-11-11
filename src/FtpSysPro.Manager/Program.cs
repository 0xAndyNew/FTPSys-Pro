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

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                MessageBox.Show("Đã xảy ra lỗi khởi động FtpSysPro Manager:\n\n" + (ex?.Message ?? e.ExceptionObject?.ToString()),
                    "FtpSysPro - Lỗi khởi động", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            Application.ThreadException += (s, e) =>
            {
                MessageBox.Show("Đã xảy ra lỗi:\n\n" + e.Exception.Message,
                    "FtpSysPro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            bool startMinimized = false;
            if (args != null)
            {
                foreach (var a in args)
                {
                    if (string.Equals(a, "--minimized", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(a, "-m", StringComparison.OrdinalIgnoreCase))
                    {
                        startMinimized = true;
                        break;
                    }
                }
            }

            var mainForm = new MainForm();
            if (startMinimized)
            {
                mainForm.WindowState = FormWindowState.Minimized;
                mainForm.ShowInTaskbar = false;
            }

            Application.Run(mainForm);

            GC.KeepAlive(_singleInstanceMutex);
        }
    }
}
