using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using FtpSysPro.Core.Config;
using FtpSysPro.Core.Engine;
using FtpSysPro.Core.Models;
using FtpSysPro.Core.Resources;

namespace FtpSysPro.Manager.UI
{
    public class JobEditDialog : Form
    {
        private readonly BackupJob _job;
        private readonly bool _isNew;

        private TabControl tabControl;
        private TextBox txtName;
        private TextBox txtDescription;
        private CheckBox chkEnabled;
        private RadioButton rbModeIncremental;
        private RadioButton rbModeFull;

        // Stepper Navigation
        private Panel pnlStepHeader;
        private Button[] _stepButtons;
        private Label lblStepCounter;
        private Button btnBack;
        private Button btnNext;
        private Button btnSave;
        private Button btnCancel;

        private static readonly string[] StepNames = new[]
        {
            "1. Chung",
            "2. Nguồn",
            "3. Đích đến",
            "4. Tuỳ chọn",
            "5. Lập lịch",
            "6. Giữ bản",
            "7. Email"
        };

        private static readonly string[] StepDescriptions = new[]
        {
            "Thông tin chung & Chế độ",
            "Thư mục nguồn dữ liệu",
            "Đích đến sao lưu",
            "Nén & Độ tin cậy",
            "Lập lịch chạy tự động",
            "Chính sách giữ bản sao",
            "Cấu hình thông báo Email"
        };

        // Sources
        private ListBox lstSources;
        private TextBox txtExcludePatterns;
        private Label lblSourceSummary;

        // Destination
        private ComboBox cboDestType;
        private Panel pnlLocal;
        private Panel pnlFtp;
        private Panel pnlSftp;

        private TextBox txtLocalPath;
        private TextBox txtUncUser;
        private TextBox txtUncPass;

        private TextBox txtFtpHost;
        private NumericUpDown numFtpPort;
        private TextBox txtFtpUser;
        private TextBox txtFtpPass;
        private TextBox txtFtpRemotePath;
        private ComboBox cboFtpEncryption;
        private CheckBox chkFtpPassive;

        private TextBox txtSftpHost;
        private NumericUpDown numSftpPort;
        private TextBox txtSftpUser;
        private TextBox txtSftpPass;
        private TextBox txtSftpKeyPath;
        private TextBox txtSftpKeyPass;
        private TextBox txtSftpRemotePath;

        // Compression
        private CheckBox chkCompressionEnabled;
        private GroupBox grpZip;
        private ComboBox cboCompressionLevel;
        private CheckBox chkEncryptZip;
        private TextBox txtZipPassword;

        // Integrity & Reliability
        private CheckBox chkEnableVss;
        private CheckBox chkCheckDiskSpace;
        private CheckBox chkVerifyChecksum;
        private NumericUpDown numNetworkRetry;

        // Schedules
        private ListView lvSchedules;

        // Retention
        private RadioButton rbKeepFullCount;
        private NumericUpDown numKeepFullCount;
        private RadioButton rbKeepDays;
        private NumericUpDown numKeepDays;
        private RadioButton rbKeepAll;

        // Notification
        private CheckBox chkNotificationEnabled;
        private ComboBox cboNotificationCondition;
        private TextBox txtNotificationRecipients;
        private TextBox txtNotificationSubject;

        public BackupJob ResultJob => _job;

        public JobEditDialog(BackupJob job, bool isNew = false)
        {
            _job = job != null ? job.Clone() : new BackupJob();
            if (!isNew && job != null)
            {
                // Preserve original ID
                _job.Id = job.Id;
                _job.Name = job.Name;
            }
            _isNew = isNew;

            InitializeComponents();
            LoadData();
        }

        private void InitializeComponents()
        {
            Text = _isNew ? (Strings.AppName + " - " + Strings.Add) : (Strings.AppName + " - " + Strings.Edit);
            Size = new Size(760, 650);
            MinimumSize = new Size(760, 650);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = new Font("Segoe UI", 9F);

            // Stepper top banner
            pnlStepHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 46,
                BackColor = Color.FromArgb(240, 244, 248),
                Padding = new Padding(6, 5, 6, 5)
            };
            pnlStepHeader.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(218, 224, 233), 1))
                {
                    e.Graphics.DrawLine(pen, 0, pnlStepHeader.Height - 1, pnlStepHeader.Width, pnlStepHeader.Height - 1);
                }
            };

            var tblSteps = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 7,
                RowCount = 1,
                Margin = new Padding(0)
            };
            for (int i = 0; i < 7; i++)
            {
                tblSteps.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 7f));
            }

            _stepButtons = new Button[7];
            for (int i = 0; i < 7; i++)
            {
                int stepIdx = i;
                _stepButtons[i] = new Button
                {
                    Text = StepNames[i],
                    Dock = DockStyle.Fill,
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand,
                    Margin = new Padding(2)
                };
                _stepButtons[i].FlatAppearance.BorderSize = 1;
                _stepButtons[i].FlatAppearance.BorderColor = Color.FromArgb(210, 218, 228);
                _stepButtons[i].Click += (s, e) => GoToStep(stepIdx);
                tblSteps.Controls.Add(_stepButtons[i], i, 0);
            }
            pnlStepHeader.Controls.Add(tblSteps);

            // TabControl (raw tab headers hidden, controlled via Stepper)
            tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Appearance = TabAppearance.Buttons,
                ItemSize = new Size(0, 1),
                SizeMode = TabSizeMode.Fixed
            };

            tabControl.TabPages.Add(CreateGeneralTab());
            tabControl.TabPages.Add(CreateSourcesTab());
            tabControl.TabPages.Add(CreateDestinationTab());
            tabControl.TabPages.Add(CreateOptionsTab());
            tabControl.TabPages.Add(CreateScheduleTab());
            tabControl.TabPages.Add(CreateRetentionTab());
            tabControl.TabPages.Add(CreateNotificationTab());

            // Bottom navigation panel
            var pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 56,
                BackColor = Color.FromArgb(245, 247, 250),
                Padding = new Padding(15, 0, 15, 0)
            };
            pnlBottom.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(218, 224, 233), 1))
                {
                    e.Graphics.DrawLine(pen, 0, 0, pnlBottom.Width, 0);
                }
            };

            lblStepCounter = new Label
            {
                Text = "Bước 1 / 7: Thông tin chung & Chế độ",
                Location = new Point(15, 18),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 60, 80)
            };

            var pnlActionButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 11, 12, 0)
            };

            btnBack = new Button
            {
                Text = Strings.Back,
                Size = new Size(95, 34),
                Enabled = false,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(238, 242, 246),
                ForeColor = Color.FromArgb(60, 70, 80),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Margin = new Padding(4, 0, 4, 0),
                Cursor = Cursors.Hand
            };
            btnBack.FlatAppearance.BorderColor = Color.FromArgb(205, 212, 220);
            btnBack.Click += (s, e) => GoToStep(tabControl.SelectedIndex - 1);

            btnNext = new Button
            {
                Text = Strings.Next,
                Size = new Size(110, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 102, 204),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Margin = new Padding(4, 0, 4, 0),
                Cursor = Cursors.Hand
            };
            btnNext.FlatAppearance.BorderSize = 0;
            btnNext.Click += (s, e) => GoToStep(tabControl.SelectedIndex + 1);

            btnSave = new Button
            {
                Text = Strings.Save,
                Size = new Size(95, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(240, 243, 246),
                ForeColor = Color.FromArgb(60, 70, 80),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Margin = new Padding(4, 0, 4, 0),
                Cursor = Cursors.Hand
            };
            btnSave.FlatAppearance.BorderColor = Color.FromArgb(205, 212, 220);
            btnSave.Click += (s, e) =>
            {
                if (SaveData())
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }
            };

            btnCancel = new Button
            {
                Text = Strings.Cancel,
                Size = new Size(80, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(245, 247, 250),
                ForeColor = Color.FromArgb(80, 90, 100),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Margin = new Padding(4, 0, 4, 0),
                DialogResult = DialogResult.Cancel,
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(215, 220, 226);

            pnlActionButtons.Controls.AddRange(new Control[] { btnBack, btnNext, btnSave, btnCancel });

            pnlBottom.Controls.Add(lblStepCounter);
            pnlBottom.Controls.Add(pnlActionButtons);

            Controls.Add(tabControl);
            Controls.Add(pnlStepHeader);
            Controls.Add(pnlBottom);

            CancelButton = btnCancel;

            UpdateStepUI();
        }

        private bool ValidateCurrentStep(int stepIndex, bool showWarning = true)
        {
            if (stepIndex == 0)
            {
                if (string.IsNullOrWhiteSpace(txtName.Text))
                {
                    if (showWarning)
                    {
                        MessageBox.Show(this, "Vui lòng nhập tên tác vụ sao lưu.", "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        txtName.Focus();
                    }
                    return false;
                }
            }
            else if (stepIndex == 1)
            {
                if (lstSources.Items.Count == 0)
                {
                    if (showWarning)
                    {
                        MessageBox.Show(this, "Vui lòng chọn ít nhất một thư mục nguồn cần sao lưu.", "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    return false;
                }
            }
            else if (stepIndex == 2)
            {
                if (cboDestType.SelectedIndex == 0)
                {
                    if (string.IsNullOrWhiteSpace(txtLocalPath.Text))
                    {
                        if (showWarning)
                        {
                            MessageBox.Show(this, "Vui lòng nhập đường dẫn thư mục đích đến.", "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            txtLocalPath.Focus();
                        }
                        return false;
                    }
                }
                else if (cboDestType.SelectedIndex == 1)
                {
                    if (string.IsNullOrWhiteSpace(txtFtpHost.Text))
                    {
                        if (showWarning)
                        {
                            MessageBox.Show(this, "Vui lòng nhập địa chỉ máy chủ FTP.", "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            txtFtpHost.Focus();
                        }
                        return false;
                    }
                }
                else if (cboDestType.SelectedIndex == 2)
                {
                    if (string.IsNullOrWhiteSpace(txtSftpHost.Text))
                    {
                        if (showWarning)
                        {
                            MessageBox.Show(this, "Vui lòng nhập địa chỉ máy chủ SFTP.", "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            txtSftpHost.Focus();
                        }
                        return false;
                    }
                }
            }
            return true;
        }

        private void GoToStep(int targetIndex)
        {
            if (targetIndex < 0 || targetIndex >= tabControl.TabPages.Count) return;

            int current = tabControl.SelectedIndex;
            if (targetIndex > current)
            {
                for (int i = current; i < targetIndex; i++)
                {
                    if (!ValidateCurrentStep(i, true))
                    {
                        tabControl.SelectedIndex = i;
                        UpdateStepUI();
                        return;
                    }
                }
            }

            tabControl.SelectedIndex = targetIndex;
            UpdateStepUI();
        }

        private void UpdateStepUI()
        {
            int current = tabControl.SelectedIndex;
            if (_stepButtons != null)
            {
                for (int i = 0; i < _stepButtons.Length; i++)
                {
                    if (i == current)
                    {
                        _stepButtons[i].BackColor = Color.FromArgb(0, 102, 204);
                        _stepButtons[i].ForeColor = Color.White;
                        _stepButtons[i].Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                    }
                    else if (i < current)
                    {
                        _stepButtons[i].BackColor = Color.FromArgb(232, 245, 233);
                        _stepButtons[i].ForeColor = Color.FromArgb(46, 125, 50);
                        _stepButtons[i].Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
                    }
                    else
                    {
                        _stepButtons[i].BackColor = Color.FromArgb(245, 247, 250);
                        _stepButtons[i].ForeColor = Color.FromArgb(100, 110, 120);
                        _stepButtons[i].Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
                    }
                }
            }

            if (lblStepCounter != null && current >= 0 && current < StepDescriptions.Length)
            {
                lblStepCounter.Text = $"Bước {current + 1} / {StepDescriptions.Length}: {StepDescriptions[current]}";
            }

            bool isLastStep = (current == tabControl.TabPages.Count - 1);

            if (btnBack != null)
            {
                btnBack.Enabled = (current > 0);
            }

            if (btnNext != null)
            {
                btnNext.Visible = !isLastStep;
                btnNext.Text = Strings.Next;
            }

            if (btnSave != null)
            {
                if (isLastStep)
                {
                    btnSave.Text = "✔ " + Strings.Finish;
                    btnSave.Size = new Size(130, 34);
                    btnSave.BackColor = Color.FromArgb(40, 167, 69);
                    btnSave.ForeColor = Color.White;
                    btnSave.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
                    btnSave.FlatAppearance.BorderSize = 0;
                }
                else
                {
                    btnSave.Text = Strings.Save;
                    btnSave.Size = new Size(95, 34);
                    btnSave.BackColor = Color.FromArgb(240, 243, 246);
                    btnSave.ForeColor = Color.FromArgb(60, 70, 80);
                    btnSave.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
                    btnSave.FlatAppearance.BorderSize = 1;
                    btnSave.FlatAppearance.BorderColor = Color.FromArgb(205, 212, 220);
                }
            }

            AcceptButton = isLastStep ? btnSave : btnNext;
        }

        private TabPage CreateGeneralTab()
        {
            var page = new TabPage(Strings.TabGeneral) { Padding = new Padding(15) };

            var lblName = new Label { Text = "Tên tác vụ / Job Name (*):", Location = new Point(15, 20), AutoSize = true };
            txtName = new TextBox { Location = new Point(15, 42), Width = 450 };

            var lblDesc = new Label { Text = "Mô tả / Description:", Location = new Point(15, 80), AutoSize = true };
            txtDescription = new TextBox { Location = new Point(15, 102), Width = 580, Height = 75, Multiline = true };

            chkEnabled = new CheckBox { Text = "Kích hoạt tác vụ này (Enabled)", Location = new Point(15, 195), AutoSize = true, Checked = true };

            var grpMode = new GroupBox
            {
                Text = "Chế độ sao lưu mặc định / Default Backup Mode (*)",
                Location = new Point(15, 230),
                Size = new Size(580, 130),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            rbModeIncremental = new RadioButton
            {
                Text = "Sao lưu tăng dần (Incremental) - Chỉ copy file mới / đã sửa đổi (Khuyên dùng)",
                Location = new Point(20, 28),
                AutoSize = true,
                Checked = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            var lblIncrDesc = new Label
            {
                Text = "Tối ưu tốc độ và dung lượng đường truyền. File không đổi sẽ không truyền lại.",
                Location = new Point(42, 52),
                AutoSize = true,
                ForeColor = Color.DimGray,
                Font = new Font("Segoe UI", 8.25F, FontStyle.Italic)
            };

            rbModeFull = new RadioButton
            {
                Text = "Sao lưu toàn bộ (Full) - Quét và sao lưu toàn bộ tất cả file mỗi lần chạy",
                Location = new Point(20, 76),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            var lblFullDesc = new Label
            {
                Text = "Đảm bảo tính độc lập hoàn toàn cho mỗi bản sao lưu nhưng tốn thời gian và dung lượng hơn.",
                Location = new Point(42, 100),
                AutoSize = true,
                ForeColor = Color.DimGray,
                Font = new Font("Segoe UI", 8.25F, FontStyle.Italic)
            };

            grpMode.Controls.AddRange(new Control[] { rbModeIncremental, lblIncrDesc, rbModeFull, lblFullDesc });

            page.Controls.AddRange(new Control[] { lblName, txtName, lblDesc, txtDescription, chkEnabled, grpMode });
            return page;
        }

        private TabPage CreateSourcesTab()
        {
            var page = new TabPage(Strings.TabSources) { Padding = new Padding(15) };

            var lblSources = new Label { Text = "Danh sách thư mục nguồn cần sao lưu:", Location = new Point(15, 15), AutoSize = true };
            lstSources = new ListBox { Location = new Point(15, 38), Size = new Size(500, 190) };

            var btnAdd = new Button { Text = Strings.Add + "...", Location = new Point(525, 38), Size = new Size(110, 30) };
            var btnRemove = new Button { Text = Strings.Delete, Location = new Point(525, 75), Size = new Size(110, 30) };

            lblSourceSummary = new Label
            {
                Text = "Tổng dung lượng cần backup: 0 B (0 files)",
                Location = new Point(15, 235),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.DarkBlue
            };

            btnAdd.Click += (s, e) =>
            {
                var folder = FolderBrowserEx.SelectFolder(this, null, "Chọn thư mục nguồn cần backup");
                if (!string.IsNullOrEmpty(folder) && !lstSources.Items.Contains(folder))
                {
                    lstSources.Items.Add(folder);
                    UpdateSourceSummary();
                }
            };

            btnRemove.Click += (s, e) =>
            {
                if (lstSources.SelectedIndex >= 0)
                {
                    lstSources.Items.RemoveAt(lstSources.SelectedIndex);
                    UpdateSourceSummary();
                }
            };

            var lblExclude = new Label { Text = "Mẫu file loại trừ (phân cách dấu phẩy, vd: *.tmp, ~$*, Thumbs.db):", Location = new Point(15, 265), AutoSize = true };
            txtExcludePatterns = new TextBox { Location = new Point(15, 288), Width = 500 };
            txtExcludePatterns.TextChanged += (s, e) => UpdateSourceSummary();

            page.Controls.AddRange(new Control[] { lblSources, lstSources, btnAdd, btnRemove, lblSourceSummary, lblExclude, txtExcludePatterns });
            return page;
        }

        private void UpdateSourceSummary()
        {
            try
            {
                var dirs = new List<string>();
                foreach (var item in lstSources.Items)
                {
                    var d = item.ToString();
                    if (!string.IsNullOrWhiteSpace(d)) dirs.Add(d);
                }

                if (dirs.Count == 0)
                {
                    lblSourceSummary.Text = "Tổng dung lượng cần backup: 0 B (0 files)";
                    return;
                }

                var patterns = txtExcludePatterns.Text
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrEmpty(p))
                    .ToList();

                var scan = FileScanner.Scan(dirs, patterns);
                lblSourceSummary.Text = $"Tổng dung lượng cần backup ước tính: {FormatBytes(scan.TotalSizeBytes)} ({scan.Files.Count:N0} files)";
            }
            catch
            {
                lblSourceSummary.Text = "Tổng dung lượng cần backup: -";
            }
        }

        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int i = 0;
            double dbl = bytes;
            while (dbl >= 1024 && i < suffixes.Length - 1)
            {
                dbl /= 1024;
                i++;
            }
            return $"{dbl:0.##} {suffixes[i]}";
        }

        private TabPage CreateDestinationTab()
        {
            var page = new TabPage(Strings.TabDestination) { Padding = new Padding(15) };

            var lblType = new Label { Text = "Loại đích đến / Destination Type:", Location = new Point(15, 15), AutoSize = true };
            cboDestType = new ComboBox { Location = new Point(15, 38), Width = 300, DropDownStyle = ComboBoxStyle.DropDownList };
            cboDestType.Items.Add(Strings.DestLocal);
            cboDestType.Items.Add(Strings.DestFtp);
            cboDestType.Items.Add(Strings.DestSftp);
            cboDestType.SelectedIndex = 0;

            // Panels for each type
            pnlLocal = CreateLocalPanel();
            pnlFtp = CreateFtpPanel();
            pnlSftp = CreateSftpPanel();

            pnlLocal.Location = new Point(15, 80);
            pnlFtp.Location = new Point(15, 80);
            pnlSftp.Location = new Point(15, 80);

            cboDestType.SelectedIndexChanged += (s, e) =>
            {
                pnlLocal.Visible = cboDestType.SelectedIndex == 0;
                pnlFtp.Visible = cboDestType.SelectedIndex == 1;
                pnlSftp.Visible = cboDestType.SelectedIndex == 2;
            };

            page.Controls.AddRange(new Control[] { lblType, cboDestType, pnlLocal, pnlFtp, pnlSftp });
            return page;
        }

        private Panel CreateLocalPanel()
        {
            var pnl = new Panel { Size = new Size(620, 320), Visible = true };

            var lblPath = new Label { Text = "Đường dẫn thư mục đích (Local / Ổ đĩa / \\\\server\\share):", Location = new Point(0, 5), AutoSize = true };
            txtLocalPath = new TextBox { Location = new Point(0, 28), Width = 480 };
            var btnBrowse = new Button { Text = Strings.Browse, Location = new Point(490, 26), Size = new Size(80, 26) };
            btnBrowse.Click += (s, e) =>
            {
                var folder = FolderBrowserEx.SelectFolder(this, txtLocalPath.Text, "Chọn thư mục đích sao lưu");
                if (!string.IsNullOrEmpty(folder)) txtLocalPath.Text = folder;
            };

            var grpUnc = new GroupBox { Text = "Thông tin đăng nhập mạng UNC (nếu có)", Location = new Point(0, 70), Size = new Size(570, 110) };
            var lblUncUser = new Label { Text = "Tài khoản (Domain\\User):", Location = new Point(15, 28), AutoSize = true };
            txtUncUser = new TextBox { Location = new Point(180, 25), Width = 250 };
            var lblUncPass = new Label { Text = "Mật khẩu:", Location = new Point(15, 63), AutoSize = true };
            txtUncPass = new TextBox { Location = new Point(180, 60), Width = 250, UseSystemPasswordChar = true };
            grpUnc.Controls.AddRange(new Control[] { lblUncUser, txtUncUser, lblUncPass, txtUncPass });

            pnl.Controls.AddRange(new Control[] { lblPath, txtLocalPath, btnBrowse, grpUnc });
            return pnl;
        }

        private Panel CreateFtpPanel()
        {
            var pnl = new Panel { Size = new Size(620, 320), Visible = false };

            var lblHost = new Label { Text = "Máy chủ FTP / Host:", Location = new Point(0, 5), AutoSize = true };
            txtFtpHost = new TextBox { Location = new Point(0, 28), Width = 320 };
            var lblPort = new Label { Text = "Cổng / Port:", Location = new Point(335, 5), AutoSize = true };
            numFtpPort = new NumericUpDown { Location = new Point(335, 28), Width = 80, Minimum = 1, Maximum = 65535, Value = 21 };

            var lblUser = new Label { Text = "Tài khoản / User:", Location = new Point(0, 65), AutoSize = true };
            txtFtpUser = new TextBox { Location = new Point(0, 88), Width = 250 };
            var lblPass = new Label { Text = "Mật khẩu / Pass:", Location = new Point(265, 65), AutoSize = true };
            txtFtpPass = new TextBox { Location = new Point(265, 88), Width = 250, UseSystemPasswordChar = true };

            var lblRemote = new Label { Text = "Thư mục từ xa / Remote path:", Location = new Point(0, 125), AutoSize = true };
            txtFtpRemotePath = new TextBox { Location = new Point(0, 148), Width = 350, Text = "/" };

            var lblEnc = new Label { Text = "Mã hoá / Encryption:", Location = new Point(0, 185), AutoSize = true };
            cboFtpEncryption = new ComboBox { Location = new Point(0, 208), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            cboFtpEncryption.Items.AddRange(new object[] { "None (FTP thường)", "Explicit (FTPS TLS)", "Implicit (Port 990)" });
            cboFtpEncryption.SelectedIndex = 0;

            chkFtpPassive = new CheckBox { Text = "Passive Mode (Khuyến nghị)", Location = new Point(220, 210), AutoSize = true, Checked = true };

            pnl.Controls.AddRange(new Control[] {
                lblHost, txtFtpHost, lblPort, numFtpPort,
                lblUser, txtFtpUser, lblPass, txtFtpPass,
                lblRemote, txtFtpRemotePath,
                lblEnc, cboFtpEncryption, chkFtpPassive
            });
            return pnl;
        }

        private Panel CreateSftpPanel()
        {
            var pnl = new Panel { Size = new Size(620, 320), Visible = false };

            var lblHost = new Label { Text = "Máy chủ SFTP / Host:", Location = new Point(0, 5), AutoSize = true };
            txtSftpHost = new TextBox { Location = new Point(0, 28), Width = 320 };
            var lblPort = new Label { Text = "Cổng / Port:", Location = new Point(335, 5), AutoSize = true };
            numSftpPort = new NumericUpDown { Location = new Point(335, 28), Width = 80, Minimum = 1, Maximum = 65535, Value = 22 };

            var lblUser = new Label { Text = "Tài khoản / User:", Location = new Point(0, 65), AutoSize = true };
            txtSftpUser = new TextBox { Location = new Point(0, 88), Width = 250 };
            var lblPass = new Label { Text = "Mật khẩu / Password:", Location = new Point(265, 65), AutoSize = true };
            txtSftpPass = new TextBox { Location = new Point(265, 88), Width = 250, UseSystemPasswordChar = true };

            var lblKey = new Label { Text = "Private Key File (tuỳ chọn nếu dùng key):", Location = new Point(0, 125), AutoSize = true };
            txtSftpKeyPath = new TextBox { Location = new Point(0, 148), Width = 400 };
            var btnKeyBrowse = new Button { Text = Strings.Browse, Location = new Point(410, 146), Size = new Size(80, 26) };
            btnKeyBrowse.Click += (s, e) =>
            {
                using (var ofd = new OpenFileDialog { Filter = "Key files (*.pem;*.ppk;id_*)|*.pem;*.ppk;id_*|All files (*.*)|*.*" })
                {
                    if (ofd.ShowDialog(this) == DialogResult.OK)
                        txtSftpKeyPath.Text = ofd.FileName;
                }
            };

            var lblKeyPass = new Label { Text = "Passphrase của Key (nếu có):", Location = new Point(0, 185), AutoSize = true };
            txtSftpKeyPass = new TextBox { Location = new Point(0, 208), Width = 250, UseSystemPasswordChar = true };

            var lblRemote = new Label { Text = "Thư mục từ xa / Remote path:", Location = new Point(265, 185), AutoSize = true };
            txtSftpRemotePath = new TextBox { Location = new Point(265, 208), Width = 250, Text = "/" };

            pnl.Controls.AddRange(new Control[] {
                lblHost, txtSftpHost, lblPort, numSftpPort,
                lblUser, txtSftpUser, lblPass, txtSftpPass,
                lblKey, txtSftpKeyPath, btnKeyBrowse,
                lblKeyPass, txtSftpKeyPass, lblRemote, txtSftpRemotePath
            });
            return pnl;
        }

        private TabPage CreateOptionsTab()
        {
            var page = new TabPage(Strings.TabOptions) { Padding = new Padding(15) };

            chkCompressionEnabled = new CheckBox
            {
                Text = "Nén thành file .ZIP (Nếu KHÔNG chọn: hệ thống sẽ đẩy thẳng cả thư mục sang đích)",
                Location = new Point(15, 20),
                AutoSize = true,
                Checked = false
            };

            grpZip = new GroupBox { Text = "Tuỳ chọn nén ZIP", Location = new Point(15, 55), Size = new Size(550, 170) };

            var lblLevel = new Label { Text = "Mức độ nén:", Location = new Point(15, 30), AutoSize = true };
            cboCompressionLevel = new ComboBox { Location = new Point(130, 27), Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
            cboCompressionLevel.Items.AddRange(new object[] { "Không nén (Store)", "Nhanh nhất (Fastest)", "Bình thường (Normal)", "Tối đa (Maximum)" });
            cboCompressionLevel.SelectedIndex = 2;

            chkEncryptZip = new CheckBox { Text = "Mã hoá file ZIP bằng mật khẩu (AES-256)", Location = new Point(15, 75), AutoSize = true };

            var lblZipPass = new Label { Text = "Mật khẩu:", Location = new Point(35, 108), AutoSize = true };
            txtZipPassword = new TextBox { Location = new Point(130, 105), Width = 250, UseSystemPasswordChar = true, Enabled = false };

            chkEncryptZip.CheckedChanged += (s, e) => txtZipPassword.Enabled = chkEncryptZip.Checked;
            chkCompressionEnabled.CheckedChanged += (s, e) => grpZip.Enabled = chkCompressionEnabled.Checked;

            grpZip.Controls.AddRange(new Control[] { lblLevel, cboCompressionLevel, chkEncryptZip, lblZipPass, txtZipPassword });

            var grpIntegrity = new GroupBox { Text = "Độ tin cậy & Đảm bảo an toàn dữ liệu (Khuyên dùng)", Location = new Point(15, 235), Size = new Size(550, 160) };

            chkEnableVss = new CheckBox
            {
                Text = "Volume Shadow Copy (VSS): Tự động chụp snapshot để backup file đang bị khoá / đang mở",
                Location = new Point(15, 25),
                AutoSize = true,
                Checked = true
            };

            chkCheckDiskSpace = new CheckBox
            {
                Text = "Kiểm tra dung lượng trống trước khi chạy (chống tràn ổ đĩa Temp và ổ đích)",
                Location = new Point(15, 55),
                AutoSize = true,
                Checked = true
            };

            chkVerifyChecksum = new CheckBox
            {
                Text = "Xác thực mã băm SHA-256 sau khi upload (đảm bảo file ở đích không bị lỗi/hỏng)",
                Location = new Point(15, 85),
                AutoSize = true,
                Checked = true
            };

            var lblRetry = new Label { Text = "Tự động thử lại khi mất mạng:", Location = new Point(15, 118), AutoSize = true };
            numNetworkRetry = new NumericUpDown { Location = new Point(210, 115), Width = 60, Minimum = 1, Maximum = 10, Value = 3 };
            var lblRetrySuffix = new Label { Text = "lần (với khoảng chờ tăng dần)", Location = new Point(275, 118), AutoSize = true };

            grpIntegrity.Controls.AddRange(new Control[] {
                chkEnableVss, chkCheckDiskSpace, chkVerifyChecksum,
                lblRetry, numNetworkRetry, lblRetrySuffix
            });

            page.Controls.AddRange(new Control[] { chkCompressionEnabled, grpZip, grpIntegrity });
            return page;
        }

        private TabPage CreateScheduleTab()
        {
            var page = new TabPage(Strings.TabSchedule) { Padding = new Padding(15) };

            var lblTitle = new Label { Text = "Các lịch chạy tự động của tác vụ này:", Location = new Point(15, 15), AutoSize = true };

            lvSchedules = new ListView
            {
                Location = new Point(15, 40),
                Size = new Size(500, 230),
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false
            };
            lvSchedules.Columns.Add("Tên / Name", 140);
            lvSchedules.Columns.Add("Loại / Type", 90);
            lvSchedules.Columns.Add("Kiểu lặp / Interval", 110);
            lvSchedules.Columns.Add("Chi tiết / Detail", 150);

            var btnAdd = new Button { Text = Strings.Add + "...", Location = new Point(525, 40), Size = new Size(110, 30) };
            var btnEdit = new Button { Text = Strings.Edit + "...", Location = new Point(525, 75), Size = new Size(110, 30) };
            var btnDelete = new Button { Text = Strings.Delete, Location = new Point(525, 110), Size = new Size(110, 30) };

            btnAdd.Click += (s, e) =>
            {
                using (var dlg = new ScheduleEditDialog(new ScheduleDefinition(), true))
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        _job.Schedules.Add(dlg.ResultSchedule);
                        RefreshScheduleList();
                    }
                }
            };

            btnEdit.Click += (s, e) =>
            {
                if (lvSchedules.SelectedIndices.Count > 0)
                {
                    int idx = lvSchedules.SelectedIndices[0];
                    var sched = _job.Schedules[idx];
                    using (var dlg = new ScheduleEditDialog(sched, false))
                    {
                        if (dlg.ShowDialog(this) == DialogResult.OK)
                        {
                            _job.Schedules[idx] = dlg.ResultSchedule;
                            RefreshScheduleList();
                        }
                    }
                }
            };

            btnDelete.Click += (s, e) =>
            {
                if (lvSchedules.SelectedIndices.Count > 0)
                {
                    int idx = lvSchedules.SelectedIndices[0];
                    _job.Schedules.RemoveAt(idx);
                    RefreshScheduleList();
                }
            };

            page.Controls.AddRange(new Control[] { lblTitle, lvSchedules, btnAdd, btnEdit, btnDelete });
            return page;
        }

        private void RefreshScheduleList()
        {
            lvSchedules.Items.Clear();
            foreach (var s in _job.Schedules)
            {
                var item = new ListViewItem(string.IsNullOrEmpty(s.Name) ? s.IntervalType.ToString() : s.Name);
                item.SubItems.Add(s.BackupType.ToString());
                item.SubItems.Add(s.IntervalType.ToString());
                item.SubItems.Add(FormatScheduleDetail(s));
                lvSchedules.Items.Add(item);
            }
        }

        private static string FormatScheduleDetail(ScheduleDefinition s)
        {
            switch (s.IntervalType)
            {
                case ScheduleIntervalType.Daily:
                    return $"Mỗi ngày lúc {s.TimeOfDay}";
                case ScheduleIntervalType.Weekly:
                    return $"Thứ {string.Join(",", s.DaysOfWeek)} lúc {s.TimeOfDay}";
                case ScheduleIntervalType.Monthly:
                    return $"Ngày {s.DayOfMonth} hàng tháng lúc {s.TimeOfDay}";
                case ScheduleIntervalType.RepeatEvery:
                    return $"Lặp mỗi {s.RepeatIntervalMinutes}p ({s.RepeatStartTime}-{s.RepeatEndTime})";
                case ScheduleIntervalType.Once:
                    return s.RunOnceLocalTime.HasValue ? s.RunOnceLocalTime.Value.ToString("yyyy-MM-dd HH:mm") : "Một lần";
                default:
                    return "";
            }
        }

        private TabPage CreateRetentionTab()
        {
            var page = new TabPage(Strings.TabRetention) { Padding = new Padding(15) };

            var grp = new GroupBox { Text = "Chính sách dọn dẹp các bản backup cũ ở đích", Location = new Point(15, 15), Size = new Size(550, 200) };

            rbKeepFullCount = new RadioButton { Text = "Chỉ giữ lại", Location = new Point(20, 35), AutoSize = true, Checked = true };
            numKeepFullCount = new NumericUpDown { Location = new Point(120, 33), Width = 70, Minimum = 1, Maximum = 100, Value = 5 };
            var lblFullSuffix = new Label { Text = "bộ Full gần nhất (kèm các bản Incremental của chúng)", Location = new Point(200, 35), AutoSize = true };

            rbKeepDays = new RadioButton { Text = "Chỉ giữ trong vòng", Location = new Point(20, 75), AutoSize = true };
            numKeepDays = new NumericUpDown { Location = new Point(160, 73), Width = 70, Minimum = 1, Maximum = 3650, Value = 30, Enabled = false };
            var lblDaysSuffix = new Label { Text = "ngày gần nhất", Location = new Point(240, 75), AutoSize = true };

            rbKeepAll = new RadioButton { Text = "Giữ tất cả (không tự động xoá bản nào)", Location = new Point(20, 120), AutoSize = true };

            rbKeepFullCount.CheckedChanged += (s, e) => numKeepFullCount.Enabled = rbKeepFullCount.Checked;
            rbKeepDays.CheckedChanged += (s, e) => numKeepDays.Enabled = rbKeepDays.Checked;

            grp.Controls.AddRange(new Control[] {
                rbKeepFullCount, numKeepFullCount, lblFullSuffix,
                rbKeepDays, numKeepDays, lblDaysSuffix,
                rbKeepAll
            });

            page.Controls.Add(grp);
            return page;
        }

        private TabPage CreateNotificationTab()
        {
            var page = new TabPage(Strings.TabNotification) { Padding = new Padding(15) };

            chkNotificationEnabled = new CheckBox
            {
                Text = "Gửi thông báo email sau khi tác vụ hoàn thành",
                Location = new Point(15, 20),
                AutoSize = true
            };

            var grp = new GroupBox { Text = "Chi tiết thông báo", Location = new Point(15, 55), Size = new Size(550, 220) };

            var lblCond = new Label { Text = "Điều kiện gửi:", Location = new Point(15, 30), AutoSize = true };
            cboNotificationCondition = new ComboBox { Location = new Point(150, 27), Width = 250, DropDownStyle = ComboBoxStyle.DropDownList };
            cboNotificationCondition.Items.AddRange(new object[] { "Luôn luôn gửi (Always)", "Chỉ khi thất bại (On Failure Only)", "Chỉ khi thành công (On Success Only)" });
            cboNotificationCondition.SelectedIndex = 0;

            var lblRecipients = new Label { Text = "Email người nhận (phân cách bằng dấu phẩy):", Location = new Point(15, 75), AutoSize = true };
            txtNotificationRecipients = new TextBox { Location = new Point(15, 98), Width = 500 };

            var lblSubject = new Label { Text = "Tiền tố tiêu đề email:", Location = new Point(15, 140), AutoSize = true };
            txtNotificationSubject = new TextBox { Location = new Point(15, 163), Width = 250, Text = "[FtpSysPro]" };

            chkNotificationEnabled.CheckedChanged += (s, e) => grp.Enabled = chkNotificationEnabled.Checked;
            grp.Enabled = false;

            grp.Controls.AddRange(new Control[] { lblCond, cboNotificationCondition, lblRecipients, txtNotificationRecipients, lblSubject, txtNotificationSubject });

            var pnlFinishBanner = new Panel
            {
                Location = new Point(15, 290),
                Size = new Size(550, 75),
                BackColor = Color.FromArgb(235, 248, 238),
                Padding = new Padding(12)
            };
            pnlFinishBanner.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(163, 217, 175), 1))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, pnlFinishBanner.Width - 1, pnlFinishBanner.Height - 1);
                }
            };

            var lblFinishTitle = new Label
            {
                Text = "✔ Đã hoàn tất các bước cấu hình!",
                Location = new Point(12, 12),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(21, 87, 36)
            };

            var lblFinishDesc = new Label
            {
                Text = "Nhấn nút '✔ Hoàn tất' ở góc dưới bên phải để lưu tác vụ sao lưu này vào hệ thống và kích hoạt lịch chạy tự động.",
                Location = new Point(12, 36),
                Size = new Size(520, 32),
                ForeColor = Color.FromArgb(21, 87, 36),
                Font = new Font("Segoe UI", 9F)
            };

            pnlFinishBanner.Controls.AddRange(new Control[] { lblFinishTitle, lblFinishDesc });

            page.Controls.AddRange(new Control[] { chkNotificationEnabled, grp, pnlFinishBanner });
            return page;
        }

        private void LoadData()
        {
            txtName.Text = _job.Name;
            txtDescription.Text = _job.Description;
            chkEnabled.Checked = _job.Enabled;
            if (_job.BackupType == BackupType.Full)
                rbModeFull.Checked = true;
            else
                rbModeIncremental.Checked = true;

            lstSources.Items.Clear();
            foreach (var s in _job.SourceDirectories)
                lstSources.Items.Add(s);

            txtExcludePatterns.Text = string.Join(", ", _job.ExcludePatterns);
            UpdateSourceSummary();

            // Destination
            cboDestType.SelectedIndex = (int)_job.Destination.Type;
            txtLocalPath.Text = _job.Destination.LocalPath;
            txtUncUser.Text = _job.Destination.UncUsername;
            txtUncPass.Text = SecretProtector.Unprotect(_job.Destination.UncPasswordProtected);

            txtFtpHost.Text = _job.Destination.FtpHost;
            numFtpPort.Value = _job.Destination.FtpPort > 0 ? _job.Destination.FtpPort : 21;
            txtFtpUser.Text = _job.Destination.FtpUsername;
            txtFtpPass.Text = SecretProtector.Unprotect(_job.Destination.FtpPasswordProtected);
            txtFtpRemotePath.Text = string.IsNullOrEmpty(_job.Destination.FtpRemotePath) ? "/" : _job.Destination.FtpRemotePath;
            cboFtpEncryption.SelectedIndex = (int)_job.Destination.FtpEncryption;
            chkFtpPassive.Checked = _job.Destination.FtpPassiveMode;

            txtSftpHost.Text = _job.Destination.SftpHost;
            numSftpPort.Value = _job.Destination.SftpPort > 0 ? _job.Destination.SftpPort : 22;
            txtSftpUser.Text = _job.Destination.SftpUsername;
            txtSftpPass.Text = SecretProtector.Unprotect(_job.Destination.SftpPasswordProtected);
            txtSftpKeyPath.Text = _job.Destination.SftpPrivateKeyPath;
            txtSftpKeyPass.Text = SecretProtector.Unprotect(_job.Destination.SftpPrivateKeyPassphraseProtected);
            txtSftpRemotePath.Text = string.IsNullOrEmpty(_job.Destination.SftpRemotePath) ? "/" : _job.Destination.SftpRemotePath;

            // Options
            chkCompressionEnabled.Checked = _job.Compression.Enabled;
            grpZip.Enabled = _job.Compression.Enabled;
            switch (_job.Compression.Level)
            {
                case CompressionLevel.None: cboCompressionLevel.SelectedIndex = 0; break;
                case CompressionLevel.Fastest: cboCompressionLevel.SelectedIndex = 1; break;
                case CompressionLevel.Maximum: cboCompressionLevel.SelectedIndex = 3; break;
                default: cboCompressionLevel.SelectedIndex = 2; break;
            }
            chkEncryptZip.Checked = _job.Compression.EncryptWithPassword;
            txtZipPassword.Text = SecretProtector.Unprotect(_job.Compression.PasswordProtected);

            // Integrity
            chkEnableVss.Checked = _job.Integrity?.EnableVss ?? true;
            chkCheckDiskSpace.Checked = _job.Integrity?.CheckFreeDiskSpace ?? true;
            chkVerifyChecksum.Checked = _job.Integrity?.VerifySha256Checksum ?? true;
            numNetworkRetry.Value = Math.Max(1, Math.Min(10, _job.Integrity?.NetworkRetryCount ?? 3));

            // Schedules
            RefreshScheduleList();

            // Retention
            switch (_job.Retention.Type)
            {
                case RetentionType.KeepDays:
                    rbKeepDays.Checked = true;
                    numKeepDays.Value = Math.Max(1, _job.Retention.KeepDays);
                    break;
                case RetentionType.KeepAll:
                    rbKeepAll.Checked = true;
                    break;
                default:
                    rbKeepFullCount.Checked = true;
                    numKeepFullCount.Value = Math.Max(1, _job.Retention.KeepFullCount);
                    break;
            }

            // Notification
            chkNotificationEnabled.Checked = _job.Notification.Enabled;
            cboNotificationCondition.SelectedIndex = Math.Min(2, (int)_job.Notification.Condition);
            txtNotificationRecipients.Text = string.Join(", ", _job.Notification.Recipients);
            txtNotificationSubject.Text = string.IsNullOrEmpty(_job.Notification.CustomSubjectPrefix) ? "[FtpSysPro]" : _job.Notification.CustomSubjectPrefix;
        }

        private bool SaveData()
        {
            if (!ValidateCurrentStep(0, true))
            {
                GoToStep(0);
                return false;
            }

            if (!ValidateCurrentStep(1, true))
            {
                GoToStep(1);
                return false;
            }

            if (!ValidateCurrentStep(2, true))
            {
                GoToStep(2);
                return false;
            }

            _job.Name = txtName.Text.Trim();
            _job.Description = txtDescription.Text.Trim();
            _job.Enabled = chkEnabled.Checked;
            _job.BackupType = rbModeFull.Checked ? BackupType.Full : BackupType.Incremental;

            _job.SourceDirectories.Clear();
            foreach (var item in lstSources.Items)
                _job.SourceDirectories.Add(item.ToString());

            _job.ExcludePatterns = txtExcludePatterns.Text
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrEmpty(p))
                .ToList();

            // Calculate source size immediately upon saving
            try
            {
                var scan = FileScanner.Scan(_job.SourceDirectories, _job.ExcludePatterns);
                _job.SourceSizeBytes = scan.TotalSizeBytes;
                _job.SourceFileCount = scan.Files.Count;
            }
            catch { }

            // Destination
            _job.Destination.Type = (DestinationType)cboDestType.SelectedIndex;
            _job.Destination.LocalPath = txtLocalPath.Text.Trim();
            _job.Destination.UncUsername = txtUncUser.Text.Trim();
            _job.Destination.UncPasswordProtected = SecretProtector.Protect(txtUncPass.Text);

            _job.Destination.FtpHost = txtFtpHost.Text.Trim();
            _job.Destination.FtpPort = (int)numFtpPort.Value;
            _job.Destination.FtpUsername = txtFtpUser.Text.Trim();
            _job.Destination.FtpPasswordProtected = SecretProtector.Protect(txtFtpPass.Text);
            _job.Destination.FtpRemotePath = txtFtpRemotePath.Text.Trim();
            _job.Destination.FtpEncryption = (FtpEncryptionMode)cboFtpEncryption.SelectedIndex;
            _job.Destination.FtpPassiveMode = chkFtpPassive.Checked;

            _job.Destination.SftpHost = txtSftpHost.Text.Trim();
            _job.Destination.SftpPort = (int)numSftpPort.Value;
            _job.Destination.SftpUsername = txtSftpUser.Text.Trim();
            _job.Destination.SftpPasswordProtected = SecretProtector.Protect(txtSftpPass.Text);
            _job.Destination.SftpPrivateKeyPath = txtSftpKeyPath.Text.Trim();
            _job.Destination.SftpPrivateKeyPassphraseProtected = SecretProtector.Protect(txtSftpKeyPass.Text);
            _job.Destination.SftpRemotePath = txtSftpRemotePath.Text.Trim();

            // Options
            _job.Compression.Enabled = chkCompressionEnabled.Checked;
            switch (cboCompressionLevel.SelectedIndex)
            {
                case 0: _job.Compression.Level = CompressionLevel.None; break;
                case 1: _job.Compression.Level = CompressionLevel.Fastest; break;
                case 3: _job.Compression.Level = CompressionLevel.Maximum; break;
                default: _job.Compression.Level = CompressionLevel.Normal; break;
            }
            _job.Compression.EncryptWithPassword = chkEncryptZip.Checked;
            _job.Compression.PasswordProtected = SecretProtector.Protect(txtZipPassword.Text);

            // Integrity
            if (_job.Integrity == null) _job.Integrity = new IntegrityOptions();
            _job.Integrity.EnableVss = chkEnableVss.Checked;
            _job.Integrity.CheckFreeDiskSpace = chkCheckDiskSpace.Checked;
            _job.Integrity.VerifySha256Checksum = chkVerifyChecksum.Checked;
            _job.Integrity.NetworkRetryCount = (int)numNetworkRetry.Value;

            // Retention
            if (rbKeepDays.Checked)
            {
                _job.Retention.Type = RetentionType.KeepDays;
                _job.Retention.KeepDays = (int)numKeepDays.Value;
            }
            else if (rbKeepAll.Checked)
            {
                _job.Retention.Type = RetentionType.KeepAll;
            }
            else
            {
                _job.Retention.Type = RetentionType.KeepFullCount;
                _job.Retention.KeepFullCount = (int)numKeepFullCount.Value;
            }

            // Notification
            _job.Notification.Enabled = chkNotificationEnabled.Checked;
            _job.Notification.Condition = (NotificationCondition)cboNotificationCondition.SelectedIndex;
            _job.Notification.Recipients = txtNotificationRecipients.Text
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(r => r.Trim())
                .Where(r => !string.IsNullOrEmpty(r))
                .ToList();
            _job.Notification.CustomSubjectPrefix = txtNotificationSubject.Text.Trim();

            return true;
        }
    }

    public class ScheduleEditDialog : Form
    {
        private readonly ScheduleDefinition _schedule;
        private readonly bool _isNew;

        private TextBox txtName;
        private CheckBox chkEnabled;
        private ComboBox cboBackupType;
        private ComboBox cboIntervalType;

        private Panel pnlDaily;
        private Panel pnlWeekly;
        private Panel pnlMonthly;
        private Panel pnlRepeat;
        private Panel pnlOnce;

        private DateTimePicker dtpTimeOfDay;
        private CheckedListBox clbDaysOfWeek;
        private NumericUpDown numDayOfMonth;
        private NumericUpDown numRepeatInterval;
        private DateTimePicker dtpRepeatStart;
        private DateTimePicker dtpRepeatEnd;
        private DateTimePicker dtpOnce;

        public ScheduleDefinition ResultSchedule => _schedule;

        public ScheduleEditDialog(ScheduleDefinition schedule, bool isNew = false)
        {
            _schedule = schedule ?? new ScheduleDefinition();
            _isNew = isNew;

            InitializeComponents();
            LoadData();
        }

        private void InitializeComponents()
        {
            Text = _isNew ? "Thêm lịch chạy / Add Schedule" : "Sửa lịch chạy / Edit Schedule";
            Size = new Size(500, 420);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = new Font("Segoe UI", 9F);

            var lblName = new Label { Text = "Tên lịch:", Location = new Point(20, 20), AutoSize = true };
            txtName = new TextBox { Location = new Point(140, 18), Width = 300 };

            chkEnabled = new CheckBox { Text = "Bật lịch này", Location = new Point(140, 50), AutoSize = true, Checked = true };

            var lblType = new Label { Text = "Loại sao lưu:", Location = new Point(20, 85), AutoSize = true };
            cboBackupType = new ComboBox { Location = new Point(140, 82), Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
            cboBackupType.Items.AddRange(new object[] { "Full (Đầy đủ)", "Incremental (Gia tăng)" });
            cboBackupType.SelectedIndex = 1;

            var lblInterval = new Label { Text = "Tần suất lặp:", Location = new Point(20, 125), AutoSize = true };
            cboIntervalType = new ComboBox { Location = new Point(140, 122), Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
            cboIntervalType.Items.AddRange(new object[] { "Một lần (Once)", "Hằng ngày (Daily)", "Hằng tuần (Weekly)", "Hằng tháng (Monthly)", "Lặp mỗi N phút (Repeat Every)" });
            cboIntervalType.SelectedIndex = 1;

            // Panels
            pnlDaily = CreateDailyPanel();
            pnlWeekly = CreateWeeklyPanel();
            pnlMonthly = CreateMonthlyPanel();
            pnlRepeat = CreateRepeatPanel();
            pnlOnce = CreateOncePanel();

            pnlDaily.Location = new Point(20, 165);
            pnlWeekly.Location = new Point(20, 165);
            pnlMonthly.Location = new Point(20, 165);
            pnlRepeat.Location = new Point(20, 165);
            pnlOnce.Location = new Point(20, 165);

            cboIntervalType.SelectedIndexChanged += (s, e) =>
            {
                pnlOnce.Visible = cboIntervalType.SelectedIndex == 0;
                pnlDaily.Visible = cboIntervalType.SelectedIndex == 1;
                pnlWeekly.Visible = cboIntervalType.SelectedIndex == 2;
                pnlMonthly.Visible = cboIntervalType.SelectedIndex == 3;
                pnlRepeat.Visible = cboIntervalType.SelectedIndex == 4;
            };

            var btnOk = new Button { Text = Strings.Save, DialogResult = DialogResult.OK, Location = new Point(275, 335), Size = new Size(85, 30) };
            var btnCancel = new Button { Text = Strings.Cancel, DialogResult = DialogResult.Cancel, Location = new Point(370, 335), Size = new Size(80, 30) };

            btnOk.Click += (s, e) =>
            {
                SaveData();
                DialogResult = DialogResult.OK;
                Close();
            };

            Controls.AddRange(new Control[] {
                lblName, txtName, chkEnabled,
                lblType, cboBackupType,
                lblInterval, cboIntervalType,
                pnlDaily, pnlWeekly, pnlMonthly, pnlRepeat, pnlOnce,
                btnOk, btnCancel
            });

            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        private Panel CreateDailyPanel()
        {
            var pnl = new Panel { Size = new Size(440, 150), Visible = true };
            var lblTime = new Label { Text = "Thời gian chạy trong ngày:", Location = new Point(0, 15), AutoSize = true };
            dtpTimeOfDay = new DateTimePicker { Location = new Point(170, 12), Format = DateTimePickerFormat.Time, ShowUpDown = true, Width = 100 };
            pnl.Controls.AddRange(new Control[] { lblTime, dtpTimeOfDay });
            return pnl;
        }

        private Panel CreateWeeklyPanel()
        {
            var pnl = new Panel { Size = new Size(440, 150), Visible = false };
            var lblDays = new Label { Text = "Các ngày trong tuần:", Location = new Point(0, 10), AutoSize = true };
            clbDaysOfWeek = new CheckedListBox { Location = new Point(0, 32), Size = new Size(200, 110) };
            clbDaysOfWeek.Items.AddRange(new object[] { "Chủ nhật", "Thứ Hai", "Thứ Ba", "Thứ Tư", "Thứ Năm", "Thứ Sáu", "Thứ Bảy" });
            pnl.Controls.AddRange(new Control[] { lblDays, clbDaysOfWeek });
            return pnl;
        }

        private Panel CreateMonthlyPanel()
        {
            var pnl = new Panel { Size = new Size(440, 150), Visible = false };
            var lblDay = new Label { Text = "Ngày trong tháng (1-31):", Location = new Point(0, 15), AutoSize = true };
            numDayOfMonth = new NumericUpDown { Location = new Point(160, 12), Width = 70, Minimum = 1, Maximum = 31, Value = 1 };
            pnl.Controls.AddRange(new Control[] { lblDay, numDayOfMonth });
            return pnl;
        }

        private Panel CreateRepeatPanel()
        {
            var pnl = new Panel { Size = new Size(440, 150), Visible = false };
            var lblEvery = new Label { Text = "Lặp mỗi (phút):", Location = new Point(0, 15), AutoSize = true };
            numRepeatInterval = new NumericUpDown { Location = new Point(120, 12), Width = 80, Minimum = 1, Maximum = 1440, Value = 60 };

            var lblRange = new Label { Text = "Trong khung giờ:", Location = new Point(0, 55), AutoSize = true };
            dtpRepeatStart = new DateTimePicker { Location = new Point(120, 52), Format = DateTimePickerFormat.Time, ShowUpDown = true, Width = 90 };
            var lblTo = new Label { Text = "đến", Location = new Point(220, 55), AutoSize = true };
            dtpRepeatEnd = new DateTimePicker { Location = new Point(255, 52), Format = DateTimePickerFormat.Time, ShowUpDown = true, Width = 90 };

            pnl.Controls.AddRange(new Control[] { lblEvery, numRepeatInterval, lblRange, dtpRepeatStart, lblTo, dtpRepeatEnd });
            return pnl;
        }

        private Panel CreateOncePanel()
        {
            var pnl = new Panel { Size = new Size(440, 150), Visible = false };
            var lblOnce = new Label { Text = "Thời điểm chạy:", Location = new Point(0, 15), AutoSize = true };
            dtpOnce = new DateTimePicker { Location = new Point(120, 12), CustomFormat = "yyyy-MM-dd HH:mm", Format = DateTimePickerFormat.Custom, Width = 180 };
            pnl.Controls.AddRange(new Control[] { lblOnce, dtpOnce });
            return pnl;
        }

        private void LoadData()
        {
            txtName.Text = _schedule.Name;
            chkEnabled.Checked = _schedule.Enabled;
            cboBackupType.SelectedIndex = _schedule.BackupType == BackupType.Full ? 0 : 1;
            cboIntervalType.SelectedIndex = (int)_schedule.IntervalType;

            if (TimeSpan.TryParse(_schedule.TimeOfDay, out var ts))
            {
                dtpTimeOfDay.Value = DateTime.Today.Add(ts);
            }

            for (int i = 0; i < 7; i++)
            {
                clbDaysOfWeek.SetItemChecked(i, _schedule.DaysOfWeek != null && _schedule.DaysOfWeek.Contains(i));
            }

            numDayOfMonth.Value = Math.Max(1, Math.Min(31, _schedule.DayOfMonth));
            numRepeatInterval.Value = Math.Max(1, _schedule.RepeatIntervalMinutes);

            if (TimeSpan.TryParse(_schedule.RepeatStartTime, out var tsStart))
                dtpRepeatStart.Value = DateTime.Today.Add(tsStart);
            if (TimeSpan.TryParse(_schedule.RepeatEndTime, out var tsEnd))
                dtpRepeatEnd.Value = DateTime.Today.Add(tsEnd);

            if (_schedule.RunOnceLocalTime.HasValue)
                dtpOnce.Value = _schedule.RunOnceLocalTime.Value;
        }

        private void SaveData()
        {
            _schedule.Name = txtName.Text.Trim();
            _schedule.Enabled = chkEnabled.Checked;
            _schedule.BackupType = cboBackupType.SelectedIndex == 0 ? BackupType.Full : BackupType.Incremental;
            _schedule.IntervalType = (ScheduleIntervalType)cboIntervalType.SelectedIndex;
            _schedule.TimeOfDay = dtpTimeOfDay.Value.ToString("HH:mm");

            _schedule.DaysOfWeek = new System.Collections.Generic.List<int>();
            for (int i = 0; i < 7; i++)
            {
                if (clbDaysOfWeek.GetItemChecked(i))
                    _schedule.DaysOfWeek.Add(i);
            }

            _schedule.DayOfMonth = (int)numDayOfMonth.Value;
            _schedule.RepeatIntervalMinutes = (int)numRepeatInterval.Value;
            _schedule.RepeatStartTime = dtpRepeatStart.Value.ToString("HH:mm");
            _schedule.RepeatEndTime = dtpRepeatEnd.Value.ToString("HH:mm");
            _schedule.RunOnceLocalTime = dtpOnce.Value;
        }
    }
}
