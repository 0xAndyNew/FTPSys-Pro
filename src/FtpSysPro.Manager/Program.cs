using System;
using System.Threading;
using System.Windows.Forms;
using FtpSysPro.Manager.UI;

namespace FtpSysPro.Manager
{
    internal static class Program
    {
        private static Mutex _singleInstanceMutex;

        [STAThread]
        private static void Main(string[] args)
        {
            // Enforce single instance via named mutex
            bool isFirstInstance;
            _singleInstanceMutex = new Mutex(true, "FtpSysPro.Manager.SingleInstanceMutex", out isFirstInstance);

            if (!isFirstInstance)
            {
                // App is already running in tray/desktop
                MessageBox.Show("FtpSysPro Manager đã đang chạy trong khay hệ thống.", "FtpSysPro", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

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
