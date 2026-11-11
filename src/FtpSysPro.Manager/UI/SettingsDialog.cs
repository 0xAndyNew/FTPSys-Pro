using System;
using System.Drawing;
using System.Windows.Forms;
using FtpSysPro.Core.Config;
using FtpSysPro.Core.Ipc;
using FtpSysPro.Core.Models;
using FtpSysPro.Core.Resources;

namespace FtpSysPro.Manager.UI
{
    public class SettingsDialog : Form
    {
        private readonly AppSettings _settings;
        private readonly PipeClient _pipeClient;

        private ComboBox cboLanguage;
        private NumericUpDown numMaxConcurrent;
        private NumericUpDown numKeepLogsDays;
        private CheckBox chkStartMinimized;
        private CheckBox chkMinimizeOnClose;

        // SMTP
        private CheckBox chkSmtpEnabled;
        private TextBox txtSmtpHost;
        private NumericUpDown numSmtpPort;
        private CheckBox chkSmtpSsl;
        private CheckBox chkSmtpStartTls;
        private TextBox txtSmtpUser;
        private TextBox txtSmtpPass;
        private TextBox txtSmtpFrom;
        private TextBox txtSmtpDisplayName;
        private Button btnTestEmail;

        public AppSettings ResultSettings => _settings;

        public SettingsDialog(AppSettings settings, PipeClient pipeClient)
        {
            _settings = settings ?? new AppSettings();
            _pipeClient = pipeClient;

            InitializeComponents();
            LoadData();
        }

        private void InitializeComponents()
        {
            Text = Strings.AppName + " - Cài đặt hệ thống / System Settings";
            Size = new Size(620, 530);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = new Font("Segoe UI", 9F);

            var tabControl = new TabControl { Dock = DockStyle.Fill };

            // General Tab
            var pageGeneral = new TabPage(Strings.TabGeneral) { Padding = new Padding(15) };

            var lblLang = new Label { Text = "Ngôn ngữ hiển thị / UI Language:", Location = new Point(15, 20), AutoSize = true };
            cboLanguage = new ComboBox { Location = new Point(250, 17), Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
            cboLanguage.Items.AddRange(new object[] { "Tiếng Việt (Mặc định)", "English" });
            cboLanguage.SelectedIndex = 0;

            var lblConcurrent = new Label { Text = "Giới hạn số Job chạy đồng thời:", Location = new Point(15, 60), AutoSize = true };
            numMaxConcurrent = new NumericUpDown { Location = new Point(250, 58), Width = 80, Minimum = 1, Maximum = 10, Value = 2 };

            var lblKeepLogs = new Label { Text = "Thời hạn lưu trữ nhật ký vận hành (ngày):", Location = new Point(15, 100), AutoSize = true };
            numKeepLogsDays = new NumericUpDown { Location = new Point(250, 98), Width = 80, Minimum = 1, Maximum = 365, Value = 30 };

            chkStartMinimized = new CheckBox { Text = "Tự động khởi động cùng Windows và thu nhỏ xuống khay hệ thống", Location = new Point(15, 145), AutoSize = true, Checked = true };
            chkMinimizeOnClose = new CheckBox { Text = "Duy trì chạy ngầm trong khay hệ thống khi đóng cửa sổ chính (X)", Location = new Point(15, 175), AutoSize = true, Checked = true };

            pageGeneral.Controls.AddRange(new Control[] {
                lblLang, cboLanguage,
                lblConcurrent, numMaxConcurrent,
                lblKeepLogs, numKeepLogsDays,
                chkStartMinimized, chkMinimizeOnClose
            });

            // SMTP Tab
            var pageSmtp = new TabPage("Máy chủ Email (SMTP)") { Padding = new Padding(15) };

            chkSmtpEnabled = new CheckBox { Text = "Kích hoạt dịch vụ thông báo & cảnh báo vận hành qua Email", Location = new Point(15, 15), AutoSize = true };

            var grp = new GroupBox { Text = "Cấu hình máy chủ gửi thư SMTP / SMTP Server Settings", Location = new Point(15, 45), Size = new Size(560, 290) };

            var lblHost = new Label { Text = "Máy chủ SMTP / Host:", Location = new Point(15, 28), AutoSize = true };
            txtSmtpHost = new TextBox { Location = new Point(180, 25), Width = 230 };
            var lblPort = new Label { Text = "Cổng / Port:", Location = new Point(420, 28), AutoSize = true };
            numSmtpPort = new NumericUpDown { Location = new Point(475, 25), Width = 65, Minimum = 1, Maximum = 65535, Value = 587 };

            chkSmtpSsl = new CheckBox { Text = "SSL trực tiếp (Port 465)", Location = new Point(180, 60), AutoSize = true };
            chkSmtpStartTls = new CheckBox { Text = "STARTTLS mã hóa TLS (Khuyến nghị - Port 587)", Location = new Point(320, 60), AutoSize = true, Checked = true };

            var lblUser = new Label { Text = "Tài khoản xác thực SMTP / User:", Location = new Point(15, 95), AutoSize = true };
            txtSmtpUser = new TextBox { Location = new Point(180, 92), Width = 280 };

            var lblPass = new Label { Text = "Mật khẩu / App Password:", Location = new Point(15, 130), AutoSize = true };
            txtSmtpPass = new TextBox { Location = new Point(180, 127), Width = 280, UseSystemPasswordChar = true };

            var lblFrom = new Label { Text = "Địa chỉ email gửi (From):", Location = new Point(15, 165), AutoSize = true };
            txtSmtpFrom = new TextBox { Location = new Point(180, 162), Width = 280 };

            var lblDisplayName = new Label { Text = "Tên hiển thị người gửi:", Location = new Point(15, 200), AutoSize = true };
            txtSmtpDisplayName = new TextBox { Location = new Point(180, 197), Width = 280, Text = "FtpSysPro Enterprise Backup" };

            btnTestEmail = new Button { Text = Strings.SendTestEmail + "...", Location = new Point(180, 240), Size = new Size(160, 30) };
            btnTestEmail.Click += async (s, e) =>
            {
                var recipient = PromptForEmail(this, "Nhập địa chỉ email nhận báo cáo thử nghiệm:", "Gửi email kiểm tra hệ thống", txtSmtpFrom.Text);
                if (string.IsNullOrWhiteSpace(recipient)) return;

                btnTestEmail.Enabled = false;
                btnTestEmail.Text = "Đang gửi...";
                try
                {
                    var testSettings = GetSmtpSettingsFromUi();
                    var req = new TestEmailRequest { Settings = testSettings, Recipient = recipient };
                    var resp = await _pipeClient.SendAsync(new IpcMessage
                    {
                        Action = IpcActions.SendTestEmail,
                        PayloadJson = Newtonsoft.Json.JsonConvert.SerializeObject(req)
                    });

                    if (resp != null && resp.Success)
                    {
                        MessageBox.Show(this, "Email thử nghiệm hệ thống đã được gửi thành công! Máy chủ SMTP sẵn sàng.", "Thông báo thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show(this, "Gửi email thử nghiệm thất bại: " + resp?.ErrorMessage, "Lỗi kết nối SMTP", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Lỗi: " + ex.Message, "Lỗi kết nối", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    btnTestEmail.Enabled = true;
                    btnTestEmail.Text = Strings.SendTestEmail + "...";
                }
            };

            chkSmtpEnabled.CheckedChanged += (s, e) => grp.Enabled = chkSmtpEnabled.Checked;

            grp.Controls.AddRange(new Control[] {
                lblHost, txtSmtpHost, lblPort, numSmtpPort,
                chkSmtpSsl, chkSmtpStartTls,
                lblUser, txtSmtpUser, lblPass, txtSmtpPass,
                lblFrom, txtSmtpFrom, lblDisplayName, txtSmtpDisplayName,
                btnTestEmail
            });

            pageSmtp.Controls.AddRange(new Control[] { chkSmtpEnabled, grp });

            tabControl.TabPages.Add(pageGeneral);
            tabControl.TabPages.Add(pageSmtp);

            var pnlBottom = new Panel { Dock = DockStyle.Bottom, Height = 48 };
            var btnOk = new Button { Text = "💾 " + Strings.Save, DialogResult = DialogResult.OK, Location = new Point(410, 10), Size = new Size(95, 30), BackColor = Color.FromArgb(0, 120, 215), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font(Font, FontStyle.Bold) };
            var btnCancel = new Button { Text = Strings.Cancel, DialogResult = DialogResult.Cancel, Location = new Point(515, 10), Size = new Size(80, 30) };

            btnOk.Click += (s, e) =>
            {
                SaveData();
                DialogResult = DialogResult.OK;
                Close();
            };

            pnlBottom.Controls.Add(btnOk);
            pnlBottom.Controls.Add(btnCancel);

            Controls.Add(tabControl);
            Controls.Add(pnlBottom);

            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        private void LoadData()
        {
            cboLanguage.SelectedIndex = string.Equals(_settings.Language, "en", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            numMaxConcurrent.Value = Math.Max(1, _settings.MaxConcurrentJobs);
            numKeepLogsDays.Value = Math.Max(1, _settings.KeepLogsDays);
            chkStartMinimized.Checked = _settings.StartMinimizedToTray;
            chkMinimizeOnClose.Checked = _settings.MinimizeToTrayOnClose;

            var smtp = _settings.Smtp ?? new SmtpSettings();
            chkSmtpEnabled.Checked = smtp.Enabled;
            txtSmtpHost.Text = smtp.Host;
            numSmtpPort.Value = smtp.Port > 0 ? smtp.Port : 587;
            chkSmtpSsl.Checked = smtp.UseSsl;
            chkSmtpStartTls.Checked = smtp.UseStartTls;
            txtSmtpUser.Text = smtp.Username;
            txtSmtpPass.Text = SecretProtector.Unprotect(smtp.PasswordProtected);
            txtSmtpFrom.Text = smtp.FromAddress;
            txtSmtpDisplayName.Text = smtp.FromDisplayName;
        }

        private SmtpSettings GetSmtpSettingsFromUi()
        {
            return new SmtpSettings
            {
                Enabled = chkSmtpEnabled.Checked,
                Host = txtSmtpHost.Text.Trim(),
                Port = (int)numSmtpPort.Value,
                UseSsl = chkSmtpSsl.Checked,
                UseStartTls = chkSmtpStartTls.Checked,
                Username = txtSmtpUser.Text.Trim(),
                PasswordProtected = SecretProtector.Protect(txtSmtpPass.Text),
                FromAddress = txtSmtpFrom.Text.Trim(),
                FromDisplayName = txtSmtpDisplayName.Text.Trim()
            };
        }

        private void SaveData()
        {
            _settings.Language = cboLanguage.SelectedIndex == 1 ? "en" : "vi";
            _settings.MaxConcurrentJobs = (int)numMaxConcurrent.Value;
            _settings.KeepLogsDays = (int)numKeepLogsDays.Value;
            _settings.StartMinimizedToTray = chkStartMinimized.Checked;
            _settings.MinimizeToTrayOnClose = chkMinimizeOnClose.Checked;
            _settings.Smtp = GetSmtpSettingsFromUi();

            Strings.SetLanguage(_settings.Language);
        }

        private static string PromptForEmail(IWin32Window owner, string prompt, string title, string defaultResponse)
        {
            using (var form = new Form
            {
                Text = title,
                Width = 420,
                Height = 160,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false
            })
            {
                var lbl = new Label { Left = 20, Top = 15, Text = prompt, AutoSize = true };
                var txt = new TextBox { Left = 20, Top = 40, Width = 360, Text = defaultResponse ?? "" };
                var btnOk = new Button { Text = "OK", Left = 200, Top = 75, Width = 85, DialogResult = DialogResult.OK };
                var btnCancel = new Button { Text = "Cancel", Left = 295, Top = 75, Width = 85, DialogResult = DialogResult.Cancel };
                form.Controls.AddRange(new Control[] { lbl, txt, btnOk, btnCancel });
                form.AcceptButton = btnOk;
                form.CancelButton = btnCancel;
                return form.ShowDialog(owner) == DialogResult.OK ? txt.Text.Trim() : null;
            }
        }
    }
}
