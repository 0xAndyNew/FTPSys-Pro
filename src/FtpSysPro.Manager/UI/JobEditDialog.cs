using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
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
        private Panel pnlRetentionModeBanner;
        private Label lblRetentionModeTitle;
        private Label lblRetentionModeDesc;
        private GroupBox grpRetention;
        private RadioButton rbKeepFullCount;
        private NumericUpDown numKeepFullCount;
        private Label lblKeepFullCountSuffix;
        private Label lblKeepFullCountDetail;
        private RadioButton rbKeepDays;
        private NumericUpDown numKeepDays;
        private Label lblKeepDaysSuffix;
        private Label lblKeepDaysDetail;
        private RadioButton rbKeepAll;
        private Label lblKeepAllDetail;

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
            UpdateRetentionUI();
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

            rbModeIncremental.CheckedChanged += (s, e) => { if (rbModeIncremental.Checked) UpdateRetentionUI(); };
            rbModeFull.CheckedChanged += (s, e) => { if (rbModeFull.Checked) UpdateRetentionUI(); };

            grpMode.Controls.AddRange(new Control[] { rbModeIncremental, lblIncrDesc, rbModeFull, lblFullDesc });

            page.Controls.AddRange(new Control[] { lblName, txtName, lblDesc, txtDescription, chkEnabled, grpMode });
            return page;
        }

        private TabPage CreateSourcesTab()
        {
            var page = new TabPage(Strings.TabSources) { Padding = new Padding(15) };

            var lblSources = new Label { Text = "Danh sách nguồn sao lưu (thư mục, cả ổ đĩa hoặc đường dẫn mạng UNC):", Location = new Point(15, 15), AutoSize = true };
            lstSources = new ListBox { Location = new Point(15, 38), Size = new Size(490, 190) };

            var btnAddFolder = new Button { Text = "📁 Thêm thư mục...", Location = new Point(515, 38), Size = new Size(140, 28) };
            var btnAddDrive = new Button { Text = "💾 Thêm ổ đĩa...", Location = new Point(515, 71), Size = new Size(140, 28) };
            var btnAddPath = new Button { Text = "✏ Nhập đường dẫn...", Location = new Point(515, 104), Size = new Size(140, 28) };
            var btnRemove = new Button { Text = "🗑 " + Strings.Delete, Location = new Point(515, 137), Size = new Size(140, 28) };
            var btnClear = new Button { Text = "✖ Xoá tất cả", Location = new Point(515, 170), Size = new Size(140, 28) };

            lblSourceSummary = new Label
            {
                Text = "Tổng dung lượng cần backup: 0 B (0 files)",
                Location = new Point(15, 235),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.DarkBlue
            };

            btnAddFolder.Click += (s, e) =>
            {
                var folder = FolderBrowserEx.SelectFolder(this, null, "Chọn thư mục hoặc ổ đĩa nguồn cần backup");
                if (!string.IsNullOrEmpty(folder) && !lstSources.Items.Contains(folder))
                {
                    lstSources.Items.Add(folder);
                    UpdateSourceSummary();
                }
            };

            btnAddDrive.Click += (s, e) =>
            {
                var driveMenu = new ContextMenuStrip();
                try
                {
                    var readyDrives = DriveInfo.GetDrives().Where(d => d.IsReady).ToList();
                    foreach (var drive in readyDrives)
                    {
                        string label = $"{drive.Name} [{drive.DriveType}] - {FormatBytes(drive.AvailableFreeSpace)} trống";
                        if (!string.IsNullOrEmpty(drive.VolumeLabel))
                        {
                            label = $"{drive.Name} ({drive.VolumeLabel}) - {FormatBytes(drive.AvailableFreeSpace)} trống";
                        }
                        string drivePath = drive.RootDirectory.FullName;
                        var item = new ToolStripMenuItem($"💾 {label}", null, (s2, e2) =>
                        {
                            if (!lstSources.Items.Contains(drivePath))
                            {
                                lstSources.Items.Add(drivePath);
                                UpdateSourceSummary();
                            }
                        });
                        driveMenu.Items.Add(item);
                    }
                }
                catch { }

                if (driveMenu.Items.Count > 0)
                {
                    driveMenu.Show(btnAddDrive, new Point(0, btnAddDrive.Height));
                }
                else
                {
                    MessageBox.Show(this, "Không tìm thấy ổ đĩa sẵn sàng.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            btnAddPath.Click += (s, e) =>
            {
                using (var prompt = new Form
                {
                    Text = "Nhập đường dẫn nguồn sao lưu",
                    Size = new Size(520, 175),
                    StartPosition = FormStartPosition.CenterParent,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false,
                    MinimizeBox = false,
                    Font = new Font("Segoe UI", 9F)
                })
                {
                    var lbl = new Label
                    {
                        Text = "Nhập đường dẫn ổ đĩa (vd: D:\\, E:\\), thư mục hoặc đường dẫn mạng (\\\\server\\share):",
                        Location = new Point(15, 15),
                        AutoSize = true
                    };
                    var txt = new TextBox { Location = new Point(15, 42), Width = 475 };
                    var btnOk = new Button { Text = "Thêm", DialogResult = DialogResult.OK, Location = new Point(305, 85), Size = new Size(85, 30) };
                    var btnCancel = new Button { Text = "Huỷ", DialogResult = DialogResult.Cancel, Location = new Point(400, 85), Size = new Size(85, 30) };
                    prompt.Controls.AddRange(new Control[] { lbl, txt, btnOk, btnCancel });
                    prompt.AcceptButton = btnOk;
                    prompt.CancelButton = btnCancel;

                    if (prompt.ShowDialog(this) == DialogResult.OK)
                    {
                        var path = txt.Text.Trim();
                        if (!string.IsNullOrEmpty(path))
                        {
                            if (path.Length == 2 && char.IsLetter(path[0]) && path[1] == ':') path += "\\";
                            if (!lstSources.Items.Contains(path))
                            {
                                lstSources.Items.Add(path);
                                UpdateSourceSummary();
                            }
                        }
                    }
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

            btnClear.Click += (s, e) =>
            {
                if (lstSources.Items.Count > 0)
                {
                    if (MessageBox.Show(this, "Bạn có chắc chắn muốn xoá toàn bộ danh sách nguồn không?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        lstSources.Items.Clear();
                        UpdateSourceSummary();
                    }
                }
            };

            var lblExclude = new Label { Text = "Mẫu file loại trừ (phân cách dấu phẩy, vd: *.tmp, ~$*, Thumbs.db):", Location = new Point(15, 265), AutoSize = true };
            txtExcludePatterns = new TextBox { Location = new Point(15, 288), Width = 500 };
            txtExcludePatterns.TextChanged += (s, e) => UpdateSourceSummary();

            page.Controls.AddRange(new Control[] { lblSources, lstSources, btnAddFolder, btnAddDrive, btnAddPath, btnRemove, btnClear, lblSourceSummary, lblExclude, txtExcludePatterns });
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

            var lblPath = new Label { Text = "Đường dẫn thư mục hoặc ổ đĩa đích (Local / Ổ đĩa / \\\\server\\share):", Location = new Point(0, 5), AutoSize = true };
            txtLocalPath = new TextBox { Location = new Point(0, 28), Width = 380 };
            var btnBrowse = new Button { Text = "📁 " + Strings.Browse + "...", Location = new Point(390, 26), Size = new Size(95, 27) };
            var btnDrive = new Button { Text = "💾 Ổ đĩa...", Location = new Point(495, 26), Size = new Size(85, 27) };

            btnBrowse.Click += (s, e) =>
            {
                var folder = FolderBrowserEx.SelectFolder(this, txtLocalPath.Text, "Chọn thư mục hoặc ổ đĩa đích sao lưu");
                if (!string.IsNullOrEmpty(folder)) txtLocalPath.Text = folder;
            };

            btnDrive.Click += (s, e) =>
            {
                var menu = new ContextMenuStrip();
                try
                {
                    var readyDrives = DriveInfo.GetDrives().Where(d => d.IsReady).ToList();
                    foreach (var drive in readyDrives)
                    {
                        string label = $"{drive.Name} [{drive.DriveType}] - {FormatBytes(drive.AvailableFreeSpace)} trống";
                        if (!string.IsNullOrEmpty(drive.VolumeLabel))
                        {
                            label = $"{drive.Name} ({drive.VolumeLabel}) - {FormatBytes(drive.AvailableFreeSpace)} trống";
                        }
                        string path = drive.RootDirectory.FullName;
                        menu.Items.Add(new ToolStripMenuItem($"💾 {label}", null, (s2, e2) => txtLocalPath.Text = path));
                    }
                }
                catch { }

                if (menu.Items.Count > 0)
                {
                    menu.Show(btnDrive, new Point(0, btnDrive.Height));
                }
                else
                {
                    MessageBox.Show(this, "Không tìm thấy ổ đĩa sẵn sàng.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            var grpUnc = new GroupBox { Text = "Thông tin đăng nhập mạng UNC (nếu có)", Location = new Point(0, 70), Size = new Size(570, 110) };
            var lblUncUser = new Label { Text = "Tài khoản (Domain\\User):", Location = new Point(15, 28), AutoSize = true };
            txtUncUser = new TextBox { Location = new Point(180, 25), Width = 250 };
            var lblUncPass = new Label { Text = "Mật khẩu:", Location = new Point(15, 63), AutoSize = true };
            txtUncPass = new TextBox { Location = new Point(180, 60), Width = 250, UseSystemPasswordChar = true };
            grpUnc.Controls.AddRange(new Control[] { lblUncUser, txtUncUser, lblUncPass, txtUncPass });

            pnl.Controls.AddRange(new Control[] { lblPath, txtLocalPath, btnBrowse, btnDrive, grpUnc });
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

            grpZip = new GroupBox { Text = "Tuỳ chọn nén ZIP", Location = new Point(15, 55), Size = new Size(685, 170) };

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

            var grpIntegrity = new GroupBox { Text = "Độ tin cậy & Đảm bảo an toàn dữ liệu (Khuyên dùng)", Location = new Point(15, 235), Size = new Size(685, 160) };

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

            var lblTitle = new Label { Text = "Các lịch chạy tự động của tác vụ này (chế độ sao lưu kế thừa theo Bước 1):", Location = new Point(15, 15), AutoSize = true };

            lvSchedules = new ListView
            {
                Location = new Point(15, 40),
                Size = new Size(505, 230),
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false
            };
            lvSchedules.Columns.Add("Lịch chạy / Schedule", 145);
            lvSchedules.Columns.Add("Thời gian / Time", 130);
            lvSchedules.Columns.Add("Chi tiết / Detail", 160);
            lvSchedules.Columns.Add("Trạng thái", 70);

            var btnAdd = new Button { Text = "➕ " + Strings.Add + "...", Location = new Point(530, 40), Size = new Size(105, 32) };
            var btnEdit = new Button { Text = "✏ " + Strings.Edit + "...", Location = new Point(530, 78), Size = new Size(105, 32) };
            var btnDelete = new Button { Text = "🗑 " + Strings.Delete, Location = new Point(530, 116), Size = new Size(105, 32) };

            btnAdd.Click += (s, e) =>
            {
                var currentBackupType = (rbModeFull != null && rbModeFull.Checked) ? BackupType.Full : BackupType.Incremental;
                using (var dlg = new ScheduleEditDialog(new ScheduleDefinition(), currentBackupType, true))
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
                    var currentBackupType = (rbModeFull != null && rbModeFull.Checked) ? BackupType.Full : BackupType.Incremental;
                    using (var dlg = new ScheduleEditDialog(sched, currentBackupType, false))
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
                var item = new ListViewItem(FormatIntervalType(s.IntervalType));
                item.SubItems.Add(FormatScheduleTimeDisplay(s));
                item.SubItems.Add(FormatScheduleDetail(s));
                item.SubItems.Add(s.Enabled ? "🟢 Bật" : "⚪ Tắt");
                lvSchedules.Items.Add(item);
            }
        }

        public static string FormatIntervalType(ScheduleIntervalType type)
        {
            switch (type)
            {
                case ScheduleIntervalType.Once: return "Một lần (Once)";
                case ScheduleIntervalType.Daily: return "Hằng ngày (Daily)";
                case ScheduleIntervalType.Weekly: return "Hằng tuần (Weekly)";
                case ScheduleIntervalType.Monthly: return "Hằng tháng (Monthly)";
                case ScheduleIntervalType.RepeatEvery: return "Lặp định kỳ (Repeat)";
                default: return type.ToString();
            }
        }

        private static string FormatScheduleTimeDisplay(ScheduleDefinition s)
        {
            switch (s.IntervalType)
            {
                case ScheduleIntervalType.Once:
                    return s.RunOnceLocalTime.HasValue ? s.RunOnceLocalTime.Value.ToString("yyyy-MM-dd HH:mm") : "-";
                case ScheduleIntervalType.RepeatEvery:
                    return $"{s.RepeatStartTime} - {s.RepeatEndTime}";
                default:
                    if (TimeSpan.TryParse(s.TimeOfDay, out var ts))
                    {
                        var dt = DateTime.Today.Add(ts);
                        return $"{s.TimeOfDay} ({dt:hh:mm tt})";
                    }
                    return s.TimeOfDay ?? "-";
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

            pnlRetentionModeBanner = new Panel
            {
                Location = new Point(15, 12),
                Size = new Size(685, 56),
                BackColor = Color.FromArgb(238, 245, 255),
                Padding = new Padding(10, 8, 10, 8)
            };
            pnlRetentionModeBanner.Paint += (s, e) =>
            {
                Color borderCol = (rbModeFull != null && rbModeFull.Checked) ? Color.FromArgb(180, 210, 245) : Color.FromArgb(167, 243, 208);
                using (var pen = new Pen(borderCol, 1))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, pnlRetentionModeBanner.Width - 1, pnlRetentionModeBanner.Height - 1);
                }
            };

            lblRetentionModeTitle = new Label
            {
                Text = "🔵 Tác vụ đang ở chế độ: Sao lưu toàn bộ (Full Backup)",
                Location = new Point(10, 8),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 76, 153)
            };
            lblRetentionModeDesc = new Label
            {
                Text = "Mỗi lần chạy tạo một bản sao lưu độc lập, chứa toàn bộ dữ liệu nguồn.",
                Location = new Point(10, 29),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.25F, FontStyle.Italic),
                ForeColor = Color.FromArgb(70, 80, 90)
            };
            pnlRetentionModeBanner.Controls.AddRange(new Control[] { lblRetentionModeTitle, lblRetentionModeDesc });

            grpRetention = new GroupBox
            {
                Text = "Chính sách lưu trữ bản sao lưu trên nơi lưu trữ đích",
                Location = new Point(15, 78),
                Size = new Size(685, 275),
                Font = new Font("Segoe UI", 9F)
            };

            // Option 1: Keep Count
            rbKeepFullCount = new RadioButton { Text = "Chỉ giữ lại", Location = new Point(20, 28), AutoSize = true, Checked = true };
            numKeepFullCount = new NumericUpDown { Location = new Point(115, 26), Width = 65, Minimum = 1, Maximum = 100, Value = 5 };
            lblKeepFullCountSuffix = new Label { Text = "bản sao lưu gần nhất", Location = new Point(188, 28), AutoSize = true };
            lblKeepFullCountDetail = new Label
            {
                Text = "Hệ thống sẽ giữ lại N bản sao lưu Full mới nhất và tự động xoá các bản Full cũ hơn.",
                Location = new Point(42, 54),
                AutoSize = true,
                MaximumSize = new Size(620, 0),
                ForeColor = Color.FromArgb(90, 100, 110),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic)
            };

            // Option 2: Keep Days
            rbKeepDays = new RadioButton { Text = "Chỉ giữ trong", Location = new Point(20, 100), AutoSize = true };
            numKeepDays = new NumericUpDown { Location = new Point(115, 98), Width = 65, Minimum = 1, Maximum = 3650, Value = 30, Enabled = false };
            lblKeepDaysSuffix = new Label { Text = "ngày gần nhất", Location = new Point(188, 100), AutoSize = true };
            lblKeepDaysDetail = new Label
            {
                Text = "Tự động dọn dẹp các bản sao lưu có thời gian tạo cũ hơn số ngày quy định.",
                Location = new Point(42, 126),
                AutoSize = true,
                MaximumSize = new Size(620, 0),
                ForeColor = Color.FromArgb(90, 100, 110),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic)
            };

            // Option 3: Keep All
            rbKeepAll = new RadioButton { Text = "Giữ tất cả bản sao lưu (Không bao giờ tự động xoá)", Location = new Point(20, 172), AutoSize = true };
            lblKeepAllDetail = new Label
            {
                Text = "Dữ liệu sao lưu sẽ tích luỹ liên tục trên nơi lưu trữ đích.",
                Location = new Point(42, 198),
                AutoSize = true,
                MaximumSize = new Size(620, 0),
                ForeColor = Color.FromArgb(90, 100, 110),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic)
            };

            rbKeepFullCount.CheckedChanged += (s, e) => numKeepFullCount.Enabled = rbKeepFullCount.Checked;
            rbKeepDays.CheckedChanged += (s, e) => numKeepDays.Enabled = rbKeepDays.Checked;

            grpRetention.Controls.AddRange(new Control[] {
                rbKeepFullCount, numKeepFullCount, lblKeepFullCountSuffix, lblKeepFullCountDetail,
                rbKeepDays, numKeepDays, lblKeepDaysSuffix, lblKeepDaysDetail,
                rbKeepAll, lblKeepAllDetail
            });

            page.Controls.AddRange(new Control[] { pnlRetentionModeBanner, grpRetention });
            return page;
        }

        private void UpdateRetentionUI()
        {
            if (pnlRetentionModeBanner == null || grpRetention == null) return;

            bool isFull = rbModeFull != null && rbModeFull.Checked;

            grpRetention.Text = "Chính sách lưu trữ bản sao lưu trên nơi lưu trữ đích";

            if (isFull)
            {
                pnlRetentionModeBanner.BackColor = Color.FromArgb(238, 245, 255);
                lblRetentionModeTitle.Text = "🔵 Tác vụ đang ở chế độ: Sao lưu toàn bộ (Full Backup)";
                lblRetentionModeTitle.ForeColor = Color.FromArgb(0, 76, 153);
                lblRetentionModeDesc.Text = "Mỗi lần chạy tạo ra một bản sao lưu hoàn chỉnh, độc lập ở đích.";

                rbKeepFullCount.Text = "Chỉ giữ lại";
                lblKeepFullCountSuffix.Text = "bản sao lưu Full gần nhất";
                lblKeepFullCountDetail.Text = "Hệ thống sẽ giữ lại N bản sao lưu Full mới nhất và tự động xoá các bản Full cũ hơn để giải phóng dung lượng.";

                rbKeepDays.Text = "Chỉ giữ trong";
                lblKeepDaysSuffix.Text = "ngày gần nhất";
                lblKeepDaysDetail.Text = "Tự động dọn dẹp các bản sao lưu Full có thời gian tạo cũ hơn số ngày quy định.";

                rbKeepAll.Text = "Giữ tất cả bản sao lưu (Không bao giờ tự động xoá)";
                lblKeepAllDetail.Text = "Dữ liệu sao lưu sẽ tích luỹ liên tục trên nơi lưu trữ đích. Cần theo dõi dung lượng ổ cứng.";
            }
            else
            {
                pnlRetentionModeBanner.BackColor = Color.FromArgb(236, 253, 245);
                lblRetentionModeTitle.Text = "🟢 Tác vụ đang ở chế độ: Sao lưu tăng dần (Incremental Backup)";
                lblRetentionModeTitle.ForeColor = Color.FromArgb(6, 95, 70);
                lblRetentionModeDesc.Text = "Gồm 1 bản Full mốc ban đầu và chuỗi các bản Incremental chỉ lưu file mới hoặc thay đổi.";

                rbKeepFullCount.Text = "Chỉ giữ lại";
                lblKeepFullCountSuffix.Text = "chu kỳ sao lưu (Full + Incremental) gần nhất";
                lblKeepFullCountDetail.Text = "Mỗi chu kỳ gồm 1 bản Full mốc và các bản Incremental. Hệ thống luôn bảo toàn bản Full gốc chừng nào các bản Incremental phụ thuộc vẫn còn được lưu.";

                rbKeepDays.Text = "Chỉ giữ trong";
                lblKeepDaysSuffix.Text = "ngày gần nhất";
                lblKeepDaysDetail.Text = "Tự động dọn dẹp các bản sao lưu quá hạn ngày, đảm bảo không xoá bản Full mốc khi các bản Incremental phụ thuộc vẫn còn trong thời hạn lưu trữ.";

                rbKeepAll.Text = "Giữ tất cả bản sao lưu (Không bao giờ tự động xoá)";
                lblKeepAllDetail.Text = "Lưu trữ toàn bộ bản Full và tất cả các bản Incremental từ trước đến nay để bảo toàn lịch sử sao lưu.";
            }

            // Always dynamically position numeric inputs and suffix labels based on actual radio button width to prevent any clipping/overlap
            numKeepFullCount.Location = new Point(Math.Max(115, rbKeepFullCount.Right + 8), 26);
            lblKeepFullCountSuffix.Location = new Point(numKeepFullCount.Right + 8, 28);

            numKeepDays.Location = new Point(Math.Max(115, rbKeepDays.Right + 8), 98);
            lblKeepDaysSuffix.Location = new Point(numKeepDays.Right + 8, 100);

            pnlRetentionModeBanner.Invalidate();
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

            var grp = new GroupBox { Text = "Chi tiết thông báo", Location = new Point(15, 55), Size = new Size(685, 220) };

            var lblCond = new Label { Text = "Điều kiện gửi:", Location = new Point(15, 30), AutoSize = true };
            cboNotificationCondition = new ComboBox { Location = new Point(150, 27), Width = 250, DropDownStyle = ComboBoxStyle.DropDownList };
            cboNotificationCondition.Items.AddRange(new object[] { "Luôn luôn gửi (Always)", "Chỉ khi thất bại (On Failure Only)", "Chỉ khi thành công (On Success Only)" });
            cboNotificationCondition.SelectedIndex = 0;

            var lblRecipients = new Label { Text = "Email người nhận (phân cách bằng dấu phẩy):", Location = new Point(15, 75), AutoSize = true };
            txtNotificationRecipients = new TextBox { Location = new Point(15, 98), Width = 645 };

            var lblSubject = new Label { Text = "Tiền tố tiêu đề email:", Location = new Point(15, 140), AutoSize = true };
            txtNotificationSubject = new TextBox { Location = new Point(15, 163), Width = 250, Text = "[FtpSysPro]" };

            chkNotificationEnabled.CheckedChanged += (s, e) => grp.Enabled = chkNotificationEnabled.Checked;
            grp.Enabled = false;

            grp.Controls.AddRange(new Control[] { lblCond, cboNotificationCondition, lblRecipients, txtNotificationRecipients, lblSubject, txtNotificationSubject });

            var pnlFinishBanner = new Panel
            {
                Location = new Point(15, 290),
                Size = new Size(685, 75),
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
                Size = new Size(655, 32),
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

            // Synchronize all schedules with the job's configured backup mode
            if (_job.Schedules != null)
            {
                foreach (var s in _job.Schedules)
                {
                    s.BackupType = _job.BackupType;
                }
            }

            return true;
        }
    }

    public class ScheduleEditDialog : Form
    {
        private readonly ScheduleDefinition _schedule;
        private readonly BackupType _jobBackupType;
        private readonly bool _isNew;

        private CheckBox chkEnabled;
        private ComboBox cboIntervalType;

        private FlowLayoutPanel pnlTimeFormat;
        private RadioButton rbTime24h;
        private RadioButton rbTime12h;

        private Panel pnlDaily;
        private Panel pnlWeekly;
        private Panel pnlMonthly;
        private Panel pnlRepeat;
        private Panel pnlOnce;

        private DateTimePicker dtpTimeOfDay;
        private DateTimePicker dtpTimeOfDayWeekly;
        private DateTimePicker dtpTimeOfDayMonthly;
        private CheckedListBox clbDaysOfWeek;
        private NumericUpDown numDayOfMonth;
        private NumericUpDown numRepeatInterval;
        private DateTimePicker dtpRepeatStart;
        private DateTimePicker dtpRepeatEnd;
        private DateTimePicker dtpOnce;

        private bool _isSyncingTime;

        public ScheduleDefinition ResultSchedule => _schedule;

        public ScheduleEditDialog(ScheduleDefinition schedule, BackupType jobBackupType, bool isNew = false)
        {
            _schedule = schedule ?? new ScheduleDefinition();
            _jobBackupType = jobBackupType;
            _isNew = isNew;

            InitializeComponents();
            LoadData();
        }

        private void InitializeComponents()
        {
            Text = _isNew ? "Thêm lịch chạy / Add Schedule" : "Sửa lịch chạy / Edit Schedule";
            Size = new Size(530, 440);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = new Font("Segoe UI", 9F);
            BackColor = Color.White;

            // 1. Top Mode Banner (informative, non-intrusive)
            bool isFull = _jobBackupType == BackupType.Full;
            var pnlModeBanner = new Panel
            {
                Location = new Point(20, 14),
                Size = new Size(475, 46),
                BackColor = isFull ? Color.FromArgb(238, 245, 255) : Color.FromArgb(236, 253, 245)
            };
            pnlModeBanner.Paint += (s, e) =>
            {
                Color border = isFull ? Color.FromArgb(180, 210, 245) : Color.FromArgb(167, 243, 208);
                using (var pen = new Pen(border, 1))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, pnlModeBanner.Width - 1, pnlModeBanner.Height - 1);
                }
            };

            var lblModeTitle = new Label
            {
                Text = isFull ? "🔵 Chế độ sao lưu: Sao lưu toàn bộ (Full Backup)" : "🟢 Chế độ sao lưu: Sao lưu tăng dần (Incremental Backup)",
                Location = new Point(12, 6),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = isFull ? Color.FromArgb(0, 76, 153) : Color.FromArgb(6, 95, 70)
            };
            var lblModeSub = new Label
            {
                Text = "Tự động kế thừa theo cấu hình tại Bước 1 (không cần chọn lại ở đây)",
                Location = new Point(12, 25),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.25F, FontStyle.Regular),
                ForeColor = Color.FromArgb(100, 100, 100)
            };
            pnlModeBanner.Controls.AddRange(new Control[] { lblModeTitle, lblModeSub });

            // 2. Schedule Interval Selection Row
            var lblInterval = new Label
            {
                Text = "Tần suất lặp:",
                Location = new Point(20, 72),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            cboIntervalType = new ComboBox
            {
                Location = new Point(115, 68),
                Width = 230,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboIntervalType.Items.AddRange(new object[] {
                "Một lần (Once)",
                "Hằng ngày (Daily)",
                "Hằng tuần (Weekly)",
                "Hằng tháng (Monthly)",
                "Lặp định kỳ (Repeat Every)"
            });
            cboIntervalType.SelectedIndex = 1;

            chkEnabled = new CheckBox
            {
                Text = "Kích hoạt lịch này",
                Location = new Point(365, 70),
                AutoSize = true,
                Checked = true
            };

            // 3. Compact 12h / 24h format selector (dynamically positioned next to the active time picker!)
            pnlTimeFormat = new FlowLayoutPanel
            {
                Size = new Size(165, 26),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0)
            };
            rbTime24h = new RadioButton { Text = "24 giờ", AutoSize = true, Checked = true, Margin = new Padding(0, 3, 10, 0) };
            rbTime12h = new RadioButton { Text = "12 giờ AM/PM", AutoSize = true, Margin = new Padding(0, 3, 0, 0) };
            pnlTimeFormat.Controls.AddRange(new Control[] { rbTime24h, rbTime12h });

            rbTime24h.CheckedChanged += (s, e) => { if (rbTime24h.Checked) UpdateTimePickerFormats(); };
            rbTime12h.CheckedChanged += (s, e) => { if (rbTime12h.Checked) UpdateTimePickerFormats(); };

            // 4. Main Settings GroupBox
            var grpSettings = new GroupBox
            {
                Text = "Thiết lập thời gian & chu kỳ chạy",
                Location = new Point(20, 105),
                Size = new Size(475, 230),
                Font = new Font("Segoe UI", 9F)
            };

            // Sub-panels
            pnlDaily = CreateDailyPanel();
            pnlWeekly = CreateWeeklyPanel();
            pnlMonthly = CreateMonthlyPanel();
            pnlRepeat = CreateRepeatPanel();
            pnlOnce = CreateOncePanel();

            pnlDaily.Location = new Point(15, 24);
            pnlWeekly.Location = new Point(15, 24);
            pnlMonthly.Location = new Point(15, 24);
            pnlRepeat.Location = new Point(15, 24);
            pnlOnce.Location = new Point(15, 24);

            grpSettings.Controls.AddRange(new Control[] { pnlDaily, pnlWeekly, pnlMonthly, pnlRepeat, pnlOnce });

            cboIntervalType.SelectedIndexChanged += (s, e) =>
            {
                pnlOnce.Visible = cboIntervalType.SelectedIndex == 0;
                pnlDaily.Visible = cboIntervalType.SelectedIndex == 1;
                pnlWeekly.Visible = cboIntervalType.SelectedIndex == 2;
                pnlMonthly.Visible = cboIntervalType.SelectedIndex == 3;
                pnlRepeat.Visible = cboIntervalType.SelectedIndex == 4;
                PositionTimeFormatPanel();
            };

            // 5. Bottom Action Buttons
            var btnOk = new Button
            {
                Text = "💾 Lưu lịch",
                Location = new Point(285, 350),
                Size = new Size(105, 32),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            btnOk.FlatAppearance.BorderSize = 0;

            var btnCancel = new Button
            {
                Text = Strings.Cancel,
                DialogResult = DialogResult.Cancel,
                Location = new Point(400, 350),
                Size = new Size(95, 32),
                FlatStyle = FlatStyle.Flat
            };

            btnOk.Click += (s, e) =>
            {
                SaveData();
                DialogResult = DialogResult.OK;
                Close();
            };

            Controls.AddRange(new Control[] {
                pnlModeBanner,
                lblInterval, cboIntervalType, chkEnabled,
                grpSettings,
                btnOk, btnCancel
            });

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            PositionTimeFormatPanel();
        }

        private void PositionTimeFormatPanel()
        {
            if (pnlTimeFormat == null) return;
            switch (cboIntervalType.SelectedIndex)
            {
                case 0: // Once
                    if (pnlOnce != null)
                    {
                        pnlOnce.Controls.Add(pnlTimeFormat);
                        pnlTimeFormat.Location = new Point(275, 12);
                        pnlTimeFormat.BringToFront();
                    }
                    break;
                case 1: // Daily
                    if (pnlDaily != null)
                    {
                        pnlDaily.Controls.Add(pnlTimeFormat);
                        pnlTimeFormat.Location = new Point(215, 12);
                        pnlTimeFormat.BringToFront();
                    }
                    break;
                case 2: // Weekly
                    if (pnlWeekly != null)
                    {
                        pnlWeekly.Controls.Add(pnlTimeFormat);
                        pnlTimeFormat.Location = new Point(215, 10);
                        pnlTimeFormat.BringToFront();
                    }
                    break;
                case 3: // Monthly
                    if (pnlMonthly != null)
                    {
                        pnlMonthly.Controls.Add(pnlTimeFormat);
                        pnlTimeFormat.Location = new Point(275, 48);
                        pnlTimeFormat.BringToFront();
                    }
                    break;
                case 4: // Repeat
                    if (pnlRepeat != null)
                    {
                        pnlRepeat.Controls.Add(pnlTimeFormat);
                        pnlTimeFormat.Location = new Point(325, 48);
                        pnlTimeFormat.BringToFront();
                    }
                    break;
            }
        }

        private void UpdateTimePickerFormats()
        {
            bool is12h = rbTime12h != null && rbTime12h.Checked;
            string timeFormat = is12h ? "hh:mm tt" : "HH:mm";
            string dateTimeFormat = is12h ? "yyyy-MM-dd hh:mm tt" : "yyyy-MM-dd HH:mm";
            int timeWidth = is12h ? 105 : 90;

            if (dtpTimeOfDay != null)
            {
                dtpTimeOfDay.Format = DateTimePickerFormat.Custom;
                dtpTimeOfDay.CustomFormat = timeFormat;
                dtpTimeOfDay.Width = timeWidth;
            }

            if (dtpTimeOfDayWeekly != null)
            {
                dtpTimeOfDayWeekly.Format = DateTimePickerFormat.Custom;
                dtpTimeOfDayWeekly.CustomFormat = timeFormat;
                dtpTimeOfDayWeekly.Width = timeWidth;
            }

            if (dtpTimeOfDayMonthly != null)
            {
                dtpTimeOfDayMonthly.Format = DateTimePickerFormat.Custom;
                dtpTimeOfDayMonthly.CustomFormat = timeFormat;
                dtpTimeOfDayMonthly.Width = timeWidth;
            }

            if (dtpRepeatStart != null)
            {
                dtpRepeatStart.Format = DateTimePickerFormat.Custom;
                dtpRepeatStart.CustomFormat = timeFormat;
                dtpRepeatStart.Width = timeWidth;
            }

            if (dtpRepeatEnd != null)
            {
                dtpRepeatEnd.Format = DateTimePickerFormat.Custom;
                dtpRepeatEnd.CustomFormat = timeFormat;
                dtpRepeatEnd.Width = timeWidth;
            }

            if (dtpOnce != null)
            {
                dtpOnce.Format = DateTimePickerFormat.Custom;
                dtpOnce.CustomFormat = dateTimeFormat;
                dtpOnce.Width = is12h ? 190 : 155;
            }

            PositionTimeFormatPanel();
        }

        private void SyncTimePickers(DateTimePicker source)
        {
            if (_isSyncingTime || source == null) return;
            _isSyncingTime = true;
            try
            {
                var val = source.Value;
                if (dtpTimeOfDay != null && dtpTimeOfDay != source) dtpTimeOfDay.Value = val;
                if (dtpTimeOfDayWeekly != null && dtpTimeOfDayWeekly != source) dtpTimeOfDayWeekly.Value = val;
                if (dtpTimeOfDayMonthly != null && dtpTimeOfDayMonthly != source) dtpTimeOfDayMonthly.Value = val;
            }
            finally
            {
                _isSyncingTime = false;
            }
        }

        private Panel CreateDailyPanel()
        {
            var pnl = new Panel { Size = new Size(445, 190), Visible = true };
            var lblTime = new Label { Text = "Thời gian chạy:", Location = new Point(0, 15), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            dtpTimeOfDay = new DateTimePicker { Location = new Point(115, 12), Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Width = 90 };
            dtpTimeOfDay.ValueChanged += (s, e) => SyncTimePickers(dtpTimeOfDay);

            var lblDesc = new Label
            {
                Text = "ℹ Tác vụ sao lưu sẽ được hệ thống tự động kích hoạt hằng ngày vào thời điểm này.",
                Location = new Point(0, 60),
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 100, 100),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic)
            };

            pnl.Controls.AddRange(new Control[] { lblTime, dtpTimeOfDay, lblDesc });
            return pnl;
        }

        private Panel CreateWeeklyPanel()
        {
            var pnl = new Panel { Size = new Size(445, 190), Visible = false };
            var lblTime = new Label { Text = "Thời gian chạy:", Location = new Point(0, 12), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            dtpTimeOfDayWeekly = new DateTimePicker { Location = new Point(115, 10), Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Width = 90 };
            dtpTimeOfDayWeekly.ValueChanged += (s, e) => SyncTimePickers(dtpTimeOfDayWeekly);

            var lblDays = new Label { Text = "Chọn các ngày chạy trong tuần:", Location = new Point(0, 45), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            clbDaysOfWeek = new CheckedListBox
            {
                Location = new Point(0, 68),
                Size = new Size(440, 75),
                MultiColumn = true,
                ColumnWidth = 105,
                CheckOnClick = true
            };
            clbDaysOfWeek.Items.AddRange(new object[] { "Chủ nhật", "Thứ Hai", "Thứ Ba", "Thứ Tư", "Thứ Năm", "Thứ Sáu", "Thứ Bảy" });

            var btnWeekdays = new LinkLabel { Text = "Chọn T2 - T6", Location = new Point(220, 46), AutoSize = true };
            btnWeekdays.LinkClicked += (s, e) =>
            {
                for (int i = 0; i < 7; i++)
                    clbDaysOfWeek.SetItemChecked(i, i >= 1 && i <= 5);
            };

            var btnAllWeek = new LinkLabel { Text = "Chọn cả tuần", Location = new Point(310, 46), AutoSize = true };
            btnAllWeek.LinkClicked += (s, e) =>
            {
                for (int i = 0; i < 7; i++)
                    clbDaysOfWeek.SetItemChecked(i, true);
            };

            var lblDesc = new Label
            {
                Text = "ℹ Tác vụ sẽ chạy vào giờ đã chọn của các ngày được đánh dấu.",
                Location = new Point(0, 150),
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 100, 100),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic)
            };

            pnl.Controls.AddRange(new Control[] { lblTime, dtpTimeOfDayWeekly, lblDays, btnWeekdays, btnAllWeek, clbDaysOfWeek, lblDesc });
            return pnl;
        }

        private Panel CreateMonthlyPanel()
        {
            var pnl = new Panel { Size = new Size(445, 190), Visible = false };
            var lblDay = new Label { Text = "Ngày trong tháng (1-31):", Location = new Point(0, 15), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            numDayOfMonth = new NumericUpDown { Location = new Point(175, 12), Width = 65, Minimum = 1, Maximum = 31, Value = 1 };

            var lblTime = new Label { Text = "Thời gian chạy trong ngày:", Location = new Point(0, 52), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            dtpTimeOfDayMonthly = new DateTimePicker { Location = new Point(175, 48), Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Width = 90 };
            dtpTimeOfDayMonthly.ValueChanged += (s, e) => SyncTimePickers(dtpTimeOfDayMonthly);

            var lblDesc = new Label
            {
                Text = "ℹ Nếu tháng có ít hơn ngày đã chọn (ví dụ ngày 31 trong tháng 30 ngày), tác vụ sẽ tự động chạy vào ngày cuối cùng của tháng đó.",
                Location = new Point(0, 95),
                Size = new Size(440, 36),
                ForeColor = Color.FromArgb(100, 100, 100),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic)
            };

            pnl.Controls.AddRange(new Control[] { lblDay, numDayOfMonth, lblTime, dtpTimeOfDayMonthly, lblDesc });
            return pnl;
        }

        private Panel CreateRepeatPanel()
        {
            var pnl = new Panel { Size = new Size(445, 190), Visible = false };
            var lblEvery = new Label { Text = "Lặp lại sau mỗi:", Location = new Point(0, 15), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            numRepeatInterval = new NumericUpDown { Location = new Point(115, 12), Width = 70, Minimum = 1, Maximum = 1440, Value = 60 };
            var lblUnit = new Label { Text = "phút (1 - 1440)", Location = new Point(190, 15), AutoSize = true };

            var lblRange = new Label { Text = "Trong khung giờ:", Location = new Point(0, 52), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            dtpRepeatStart = new DateTimePicker { Location = new Point(115, 48), Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Width = 85 };
            var lblTo = new Label { Text = "đến", Location = new Point(205, 52), AutoSize = true };
            dtpRepeatEnd = new DateTimePicker { Location = new Point(230, 48), Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Width = 85 };

            var lblDesc = new Label
            {
                Text = "ℹ Tác vụ sẽ tự động lặp lại sau mỗi khoảng phút đã chỉ định trong khung giờ quy định.",
                Location = new Point(0, 95),
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 100, 100),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic)
            };

            pnl.Controls.AddRange(new Control[] { lblEvery, numRepeatInterval, lblUnit, lblRange, dtpRepeatStart, lblTo, dtpRepeatEnd, lblDesc });
            return pnl;
        }

        private Panel CreateOncePanel()
        {
            var pnl = new Panel { Size = new Size(445, 190), Visible = false };
            var lblOnce = new Label { Text = "Thời điểm chạy:", Location = new Point(0, 15), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            dtpOnce = new DateTimePicker { Location = new Point(115, 12), CustomFormat = "yyyy-MM-dd HH:mm", Format = DateTimePickerFormat.Custom, Width = 155 };

            var lblDesc = new Label
            {
                Text = "ℹ Tác vụ sẽ chỉ chạy duy nhất 1 lần vào thời điểm đã đặt, sau đó tự động chuyển sang trạng thái tắt.",
                Location = new Point(0, 60),
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 100, 100),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic)
            };

            pnl.Controls.AddRange(new Control[] { lblOnce, dtpOnce, lblDesc });
            return pnl;
        }

        private void LoadData()
        {
            chkEnabled.Checked = _schedule.Enabled;
            cboIntervalType.SelectedIndex = (int)_schedule.IntervalType;

            if (TimeSpan.TryParse(_schedule.TimeOfDay, out var ts))
            {
                var dt = DateTime.Today.Add(ts);
                dtpTimeOfDay.Value = dt;
                if (dtpTimeOfDayWeekly != null) dtpTimeOfDayWeekly.Value = dt;
                if (dtpTimeOfDayMonthly != null) dtpTimeOfDayMonthly.Value = dt;
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

            UpdateTimePickerFormats();
        }

        private void SaveData()
        {
            _schedule.Name = JobEditDialog.FormatIntervalType((ScheduleIntervalType)cboIntervalType.SelectedIndex);
            _schedule.Enabled = chkEnabled.Checked;
            _schedule.BackupType = _jobBackupType;
            _schedule.IntervalType = (ScheduleIntervalType)cboIntervalType.SelectedIndex;

            if (_schedule.IntervalType == ScheduleIntervalType.Weekly && dtpTimeOfDayWeekly != null)
            {
                _schedule.TimeOfDay = dtpTimeOfDayWeekly.Value.ToString("HH:mm");
            }
            else if (_schedule.IntervalType == ScheduleIntervalType.Monthly && dtpTimeOfDayMonthly != null)
            {
                _schedule.TimeOfDay = dtpTimeOfDayMonthly.Value.ToString("HH:mm");
            }
            else if (dtpTimeOfDay != null)
            {
                _schedule.TimeOfDay = dtpTimeOfDay.Value.ToString("HH:mm");
            }

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
