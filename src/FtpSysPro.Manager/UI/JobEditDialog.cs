using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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

        private static string[] GetStepNames() => new[]
        {
            Strings.TabGeneral,
            Strings.TabSources,
            Strings.TabDestination,
            Strings.TabOptions,
            Strings.TabSchedule,
            Strings.TabRetention,
            Strings.TabNotification
        };

        private static string[] GetStepDescriptions() => new[]
        {
            Strings.StepTitle1,
            Strings.StepTitle2,
            Strings.StepTitle3,
            Strings.StepTitle4,
            Strings.StepTitle5,
            Strings.StepTitle6,
            Strings.StepTitle7
        };

        // Sources
        private ListView lvSources;
        private TextBox txtExcludePatterns;
        private Label lblSourceSummary;
        private System.Threading.CancellationTokenSource _sourceScanCts;
        private Timer _debounceTimer;
        private long _lastCalculatedSize = 0;
        private int _lastCalculatedFiles = 0;

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
        private GroupBox grpRetention;
        private RadioButton rbKeepFullCount;
        private NumericUpDown numKeepFullCount;
        private Label lblKeepFullCountSuffix;
        private RadioButton rbKeepDays;
        private NumericUpDown numKeepDays;
        private Label lblKeepDaysSuffix;
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

            var stepNames = GetStepNames();
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
                    Text = stepNames[i],
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

            // TabControl (tab headers hidden, controlled via Stepper)
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
                Text = Strings.StepIndicator(0, 7) + ": " + GetStepDescriptions()[0],
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
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 0, 8, 0)
            };
            btnBack.FlatAppearance.BorderColor = Color.FromArgb(205, 212, 220);
            btnBack.Click += (s, e) =>
            {
                if (tabControl.SelectedIndex > 0)
                    GoToStep(tabControl.SelectedIndex - 1);
            };

            btnNext = new Button
            {
                Text = Strings.Next,
                Size = new Size(110, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Margin = new Padding(0, 0, 8, 0)
            };
            btnNext.FlatAppearance.BorderSize = 0;
            btnNext.Click += (s, e) =>
            {
                int current = tabControl.SelectedIndex;
                if (ValidateCurrentStep(current, true))
                {
                    if (current < tabControl.TabPages.Count - 1)
                        GoToStep(current + 1);
                }
            };

            btnSave = new Button
            {
                Text = Strings.Save,
                Size = new Size(95, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(240, 243, 246),
                ForeColor = Color.FromArgb(60, 70, 80),
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 0, 8, 0)
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
                DialogResult = DialogResult.Cancel,
                Size = new Size(85, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(80, 80, 80),
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0)
            };
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(205, 212, 220);

            pnlActionButtons.Controls.Add(btnBack);
            pnlActionButtons.Controls.Add(btnNext);
            pnlActionButtons.Controls.Add(btnSave);
            pnlActionButtons.Controls.Add(btnCancel);

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
                        MessageBox.Show(this,
                            Strings.IsVi ? "Vui lòng nhập tên Job sao lưu." : "Please enter a backup job name.",
                            Strings.IsVi ? "Thiếu thông tin" : "Missing Information",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        txtName.Focus();
                    }
                    return false;
                }
            }
            else if (stepIndex == 1)
            {
                if (lvSources.Items.Count == 0)
                {
                    if (showWarning)
                    {
                        MessageBox.Show(this,
                            Strings.IsVi ? "Vui lòng chọn ít nhất một thư mục hoặc ổ đĩa nguồn cần sao lưu." : "Please select at least one source folder or drive to back up.",
                            Strings.IsVi ? "Thiếu thông tin" : "Missing Information",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                            MessageBox.Show(this,
                                Strings.IsVi ? "Vui lòng nhập hoặc chọn đường dẫn kho lưu trữ đích." : "Please specify or browse to a destination repository path.",
                                Strings.IsVi ? "Thiếu thông tin" : "Missing Information",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                            MessageBox.Show(this,
                                Strings.IsVi ? "Vui lòng nhập địa chỉ máy chủ FTP." : "Please enter the FTP server host.",
                                Strings.IsVi ? "Thiếu thông tin" : "Missing Information",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                            MessageBox.Show(this,
                                Strings.IsVi ? "Vui lòng nhập địa chỉ máy chủ SFTP." : "Please enter the SFTP server host.",
                                Strings.IsVi ? "Thiếu thông tin" : "Missing Information",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

            var descriptions = GetStepDescriptions();
            if (lblStepCounter != null && current >= 0 && current < descriptions.Length)
            {
                lblStepCounter.Text = Strings.StepIndicator(current, descriptions.Length) + ": " + descriptions[current];
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

            var lblName = new Label { Text = Strings.JobName, Location = new Point(15, 20), AutoSize = true };
            txtName = new TextBox { Location = new Point(15, 42), Width = 450 };

            var lblDesc = new Label { Text = Strings.JobDescription, Location = new Point(15, 80), AutoSize = true };
            txtDescription = new TextBox { Location = new Point(15, 102), Width = 685, Height = 75, Multiline = true };

            chkEnabled = new CheckBox { Text = Strings.JobEnabled, Location = new Point(15, 195), AutoSize = true, Checked = true };

            var grpMode = new GroupBox
            {
                Text = Strings.BackupMode,
                Location = new Point(15, 230),
                Size = new Size(685, 95),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            rbModeIncremental = new RadioButton
            {
                Text = Strings.ModeIncremental,
                Location = new Point(20, 26),
                AutoSize = true,
                Checked = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            rbModeFull = new RadioButton
            {
                Text = Strings.ModeFull,
                Location = new Point(20, 56),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            rbModeIncremental.CheckedChanged += (s, e) => { if (rbModeIncremental.Checked) UpdateRetentionUI(); };
            rbModeFull.CheckedChanged += (s, e) => { if (rbModeFull.Checked) UpdateRetentionUI(); };

            grpMode.Controls.AddRange(new Control[] { rbModeIncremental, rbModeFull });

            page.Controls.AddRange(new Control[] { lblName, txtName, lblDesc, txtDescription, chkEnabled, grpMode });
            return page;
        }

        private TabPage CreateSourcesTab()
        {
            var page = new TabPage(Strings.TabSources) { Padding = new Padding(15) };

            var lblSources = new Label
            {
                Text = Strings.IsVi
                    ? "Danh sách dữ liệu nguồn cần sao lưu (Thư mục, Ổ đĩa hoặc Mạng UNC):"
                    : "Backup sources to protect (Directories, Drives, or UNC Network Shares):",
                Location = new Point(15, 15),
                AutoSize = true
            };
            lvSources = new ListView
            {
                Location = new Point(15, 38),
                Size = new Size(490, 190),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false,
                HeaderStyle = ColumnHeaderStyle.Nonclickable
            };
            lvSources.Columns.Add(Strings.IsVi ? "Đường dẫn nguồn" : "Source Path", 330);
            lvSources.Columns.Add(Strings.IsVi ? "Dung lượng" : "Size", 140);

            var btnAddFolder = new Button { Text = "📁 " + (Strings.IsVi ? "Thêm thư mục..." : "Add Folder..."), Location = new Point(515, 38), Size = new Size(160, 28) };
            var btnAddDrive = new Button { Text = "💾 " + (Strings.IsVi ? "Thêm ổ đĩa..." : "Add Drive..."), Location = new Point(515, 71), Size = new Size(160, 28) };
            var btnAddPath = new Button { Text = "✏ " + (Strings.IsVi ? "Nhập đường dẫn..." : "Enter Path..."), Location = new Point(515, 104), Size = new Size(160, 28) };
            var btnRemove = new Button { Text = "🗑 " + (Strings.IsVi ? "Xóa mục chọn" : "Remove"), Location = new Point(515, 137), Size = new Size(160, 28) };
            var btnClear = new Button { Text = "❌ " + (Strings.IsVi ? "Xóa tất cả" : "Clear All"), Location = new Point(515, 170), Size = new Size(160, 28) };

            lblSourceSummary = new Label
            {
                Text = Strings.TotalSourceSize + $" 0 B (0 {Strings.FilesUnit})",
                Location = new Point(15, 235),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.DarkBlue
            };

            btnAddFolder.Click += (s, e) =>
            {
                var folder = FolderBrowserEx.SelectFolder(this, null, Strings.IsVi ? "Chọn thư mục nguồn cần sao lưu" : "Select source folder to back up");
                if (!string.IsNullOrEmpty(folder))
                {
                    AddSourceItem(folder);
                    UpdateSourceSummary();
                }
            };

            btnAddDrive.Click += async (s, e) =>
            {
                btnAddDrive.Enabled = false;
                Cursor = Cursors.WaitCursor;
                try
                {
                    var readyDrives = await Task.Run(() =>
                    {
                        var list = new List<DriveInfo>();
                        try
                        {
                            foreach (var d in DriveInfo.GetDrives())
                            {
                                try
                                {
                                    if (d.IsReady) list.Add(d);
                                }
                                catch { }
                            }
                        }
                        catch { }
                        return list;
                    });

                    var driveMenu = new ContextMenuStrip();
                    string freeUnit = Strings.IsVi ? "trống" : "free";
                    foreach (var drive in readyDrives)
                    {
                        string label = $"{drive.Name} [{drive.DriveType}] - {FormatBytes(drive.AvailableFreeSpace)} {freeUnit}";
                        try
                        {
                            if (!string.IsNullOrEmpty(drive.VolumeLabel))
                            {
                                label = $"{drive.Name} ({drive.VolumeLabel}) - {FormatBytes(drive.AvailableFreeSpace)} {freeUnit}";
                            }
                        }
                        catch { }
                        string drivePath = drive.RootDirectory.FullName;
                        var item = new ToolStripMenuItem($"💾 {label}", null, (s2, e2) =>
                        {
                            AddSourceItem(drivePath);
                            UpdateSourceSummary();
                        });
                        driveMenu.Items.Add(item);
                    }

                    if (driveMenu.Items.Count > 0)
                    {
                        driveMenu.Show(btnAddDrive, new Point(0, btnAddDrive.Height));
                    }
                    else
                    {
                        MessageBox.Show(this,
                            Strings.IsVi ? "Không tìm thấy ổ đĩa cục bộ sẵn sàng." : "No ready local drives found.",
                            Strings.IsVi ? "Thông báo" : "Notice",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                finally
                {
                    btnAddDrive.Enabled = true;
                    Cursor = Cursors.Default;
                }
            };

            btnAddPath.Click += (s, e) =>
            {
                using (var prompt = new Form
                {
                    Text = Strings.IsVi ? "Nhập đường dẫn dữ liệu nguồn sao lưu" : "Enter Backup Source Path",
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
                        Text = Strings.IsVi
                            ? "Nhập đường dẫn ổ đĩa (vd: D:\\, E:\\), thư mục hoặc đường dẫn mạng UNC (\\\\server\\share):"
                            : "Enter drive path (e.g. D:\\, E:\\), folder, or UNC network share (\\\\server\\share):",
                        Location = new Point(15, 15),
                        AutoSize = true
                    };
                    var txt = new TextBox { Location = new Point(15, 42), Width = 475 };
                    var btnOk = new Button { Text = Strings.IsVi ? "Thêm" : "Add", DialogResult = DialogResult.OK, Location = new Point(305, 85), Size = new Size(85, 30) };
                    var btnPromptCancel = new Button { Text = Strings.Cancel, DialogResult = DialogResult.Cancel, Location = new Point(400, 85), Size = new Size(85, 30) };
                    prompt.Controls.AddRange(new Control[] { lbl, txt, btnOk, btnPromptCancel });
                    prompt.AcceptButton = btnOk;
                    prompt.CancelButton = btnPromptCancel;

                    if (prompt.ShowDialog(this) == DialogResult.OK)
                    {
                        var path = txt.Text.Trim();
                        if (!string.IsNullOrEmpty(path))
                        {
                            AddSourceItem(path);
                            UpdateSourceSummary();
                        }
                    }
                }
            };

            btnRemove.Click += (s, e) =>
            {
                if (lvSources.SelectedItems.Count > 0)
                {
                    lvSources.Items.Remove(lvSources.SelectedItems[0]);
                    UpdateSourceSummary();
                }
            };

            btnClear.Click += (s, e) =>
            {
                if (lvSources.Items.Count > 0)
                {
                    if (MessageBox.Show(this,
                        Strings.IsVi ? "Bạn có chắc chắn muốn xóa toàn bộ danh sách nguồn sao lưu không?" : "Are you sure you want to clear all backup sources?",
                        Strings.IsVi ? "Xác nhận" : "Confirm",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        lvSources.Items.Clear();
                        UpdateSourceSummary();
                    }
                }
            };

            var lblExclude = new Label
            {
                Text = Strings.IsVi
                    ? "Mẫu tệp tin loại trừ (phân cách bằng dấu phẩy, vd: *.tmp, ~$*, Thumbs.db):"
                    : "Exclude patterns (comma separated, e.g. *.tmp, ~$*, Thumbs.db):",
                Location = new Point(15, 265),
                AutoSize = true
            };
            txtExcludePatterns = new TextBox { Location = new Point(15, 288), Width = 500 };

            _debounceTimer = new Timer { Interval = 600 };
            _debounceTimer.Tick += (s, e) =>
            {
                _debounceTimer.Stop();
                UpdateSourceSummary();
            };
            txtExcludePatterns.TextChanged += (s, e) =>
            {
                _debounceTimer.Stop();
                _debounceTimer.Start();
            };

            page.Controls.AddRange(new Control[] { lblSources, lvSources, btnAddFolder, btnAddDrive, btnAddPath, btnRemove, btnClear, lblSourceSummary, lblExclude, txtExcludePatterns });
            return page;
        }

        private static bool IsDriveRoot(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            try
            {
                var root = Path.GetPathRoot(path);
                if (string.IsNullOrEmpty(root)) return false;
                return string.Equals(root.TrimEnd('\\', '/'), path.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private void AddSourceItem(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            path = path.Trim();
            if (path.Length == 2 && char.IsLetter(path[0]) && path[1] == ':') path += "\\";

            foreach (ListViewItem existing in lvSources.Items)
            {
                if (string.Equals(existing.Text, path, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            var item = new ListViewItem(path);
            if (IsDriveRoot(path))
            {
                long used = 0;
                try
                {
                    var d = new DriveInfo(path);
                    if (d.IsReady)
                    {
                        used = d.TotalSize - d.AvailableFreeSpace;
                    }
                }
                catch { }
                item.SubItems.Add(used > 0 ? FormatBytes(used) : "-");
            }
            else
            {
                item.SubItems.Add(Strings.IsVi ? "Đang tính..." : "Calculating...");
            }

            lvSources.Items.Add(item);
        }

        private async void UpdateSourceSummary()
        {
            try
            {
                _sourceScanCts?.Cancel();
                _sourceScanCts?.Dispose();
            }
            catch { }

            _sourceScanCts = new System.Threading.CancellationTokenSource();
            var token = _sourceScanCts.Token;

            var items = new List<ListViewItem>();
            foreach (ListViewItem item in lvSources.Items)
            {
                items.Add(item);
            }

            if (items.Count == 0)
            {
                _lastCalculatedSize = 0;
                _lastCalculatedFiles = 0;
                lblSourceSummary.Text = Strings.TotalSourceSize + $" 0 B (0 {Strings.FilesUnit})";
                return;
            }

            var patterns = txtExcludePatterns.Text
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrEmpty(p))
                .ToList();

            lblSourceSummary.Text = Strings.TotalSourceSize + (Strings.IsVi ? " Đang tính toán..." : " Calculating...");

            try
            {
                long totalBytes = 0;
                int totalFiles = 0;

                foreach (var lvi in items)
                {
                    if (token.IsCancellationRequested) return;

                    string path = lvi.Text;
                    if (string.IsNullOrWhiteSpace(path)) continue;

                    if (IsDriveRoot(path))
                    {
                        long used = 0;
                        try
                        {
                            var d = new DriveInfo(path);
                            if (d.IsReady)
                            {
                                used = d.TotalSize - d.AvailableFreeSpace;
                            }
                        }
                        catch { }

                        totalBytes += used;
                        if (!token.IsCancellationRequested && !IsDisposed)
                        {
                            string sizeStr = used > 0 ? FormatBytes(used) : "-";
                            if (lvi.SubItems.Count > 1)
                                lvi.SubItems[1].Text = sizeStr;
                            else
                                lvi.SubItems.Add(sizeStr);
                        }
                    }
                    else
                    {
                        var scan = await Task.Run(() =>
                        {
                            try
                            {
                                return FileScanner.Scan(new[] { path }, patterns, token);
                            }
                            catch
                            {
                                return new ScanResult();
                            }
                        }, token);

                        if (!token.IsCancellationRequested && !IsDisposed)
                        {
                            totalBytes += scan.TotalSizeBytes;
                            totalFiles += scan.Files.Count;
                            string sizeStr = $"{FormatBytes(scan.TotalSizeBytes)} ({scan.Files.Count:N0} {Strings.FilesUnit})";
                            if (lvi.SubItems.Count > 1)
                                lvi.SubItems[1].Text = sizeStr;
                            else
                                lvi.SubItems.Add(sizeStr);
                        }
                    }
                }

                if (!token.IsCancellationRequested && !IsDisposed)
                {
                    _lastCalculatedSize = totalBytes;
                    _lastCalculatedFiles = totalFiles;
                    lblSourceSummary.Text = totalFiles > 0
                        ? $"{Strings.TotalSourceSize} {FormatBytes(totalBytes)} ({totalFiles:N0} {Strings.FilesUnit})"
                        : $"{Strings.TotalSourceSize} {FormatBytes(totalBytes)}";
                }
            }
            catch (OperationCanceledException)
            {
                // Task was cancelled by a newer scan, ignore
            }
            catch
            {
                if (!IsDisposed)
                {
                    lblSourceSummary.Text = $"{Strings.TotalSourceSize} -";
                }
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

            var lblType = new Label { Text = Strings.DestType, Location = new Point(15, 15), AutoSize = true };
            cboDestType = new ComboBox { Location = new Point(15, 38), Width = 350, DropDownStyle = ComboBoxStyle.DropDownList };
            cboDestType.Items.Add(Strings.DestLocal);
            cboDestType.Items.Add(Strings.DestFtp);
            cboDestType.Items.Add(Strings.DestSftp);
            cboDestType.SelectedIndex = 0;

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
            var pnl = new Panel { Size = new Size(685, 320), Visible = true };

            var lblPath = new Label { Text = Strings.DestPath, Location = new Point(0, 5), AutoSize = true };
            txtLocalPath = new TextBox { Location = new Point(0, 28), Width = 440 };
            var btnBrowse = new Button { Text = "📁 " + Strings.Browse, Location = new Point(450, 26), Size = new Size(100, 27) };
            var btnDrive = new Button { Text = "💾 " + (Strings.IsVi ? "Ổ đĩa..." : "Drives..."), Location = new Point(555, 26), Size = new Size(90, 27) };

            btnBrowse.Click += (s, e) =>
            {
                var folder = FolderBrowserEx.SelectFolder(this, txtLocalPath.Text, Strings.IsVi ? "Chọn kho lưu trữ đích" : "Select destination repository path");
                if (!string.IsNullOrEmpty(folder)) txtLocalPath.Text = folder;
            };

            btnDrive.Click += async (s, e) =>
            {
                btnDrive.Enabled = false;
                Cursor = Cursors.WaitCursor;
                try
                {
                    var readyDrives = await Task.Run(() =>
                    {
                        var list = new List<DriveInfo>();
                        try
                        {
                            foreach (var d in DriveInfo.GetDrives())
                            {
                                try
                                {
                                    if (d.IsReady) list.Add(d);
                                }
                                catch { }
                            }
                        }
                        catch { }
                        return list;
                    });

                    var menu = new ContextMenuStrip();
                    string freeUnit = Strings.IsVi ? "trống" : "free";
                    foreach (var drive in readyDrives)
                    {
                        string label = $"{drive.Name} [{drive.DriveType}] - {FormatBytes(drive.AvailableFreeSpace)} {freeUnit}";
                        try
                        {
                            if (!string.IsNullOrEmpty(drive.VolumeLabel))
                            {
                                label = $"{drive.Name} ({drive.VolumeLabel}) - {FormatBytes(drive.AvailableFreeSpace)} {freeUnit}";
                            }
                        }
                        catch { }
                        string path = drive.RootDirectory.FullName;
                        menu.Items.Add(new ToolStripMenuItem($"💾 {label}", null, (s2, e2) => txtLocalPath.Text = path));
                    }

                    if (menu.Items.Count > 0)
                    {
                        menu.Show(btnDrive, new Point(0, btnDrive.Height));
                    }
                    else
                    {
                        MessageBox.Show(this,
                            Strings.IsVi ? "Không tìm thấy ổ đĩa cục bộ sẵn sàng." : "No ready local drives found.",
                            Strings.IsVi ? "Thông báo" : "Notice",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                finally
                {
                    btnDrive.Enabled = true;
                    Cursor = Cursors.Default;
                }
            };

            var grpUnc = new GroupBox
            {
                Text = Strings.IsVi ? "Thông tin xác thực mạng chia sẻ UNC (Domain\\Username & Mật khẩu)" : "UNC Network Share Credentials (Domain\\Username & Password)",
                Location = new Point(0, 70),
                Size = new Size(685, 110)
            };
            var lblUncUser = new Label { Text = Strings.IsVi ? "Tài khoản (Domain\\Username):" : "Username (Domain\\User):", Location = new Point(15, 28), AutoSize = true };
            txtUncUser = new TextBox { Location = new Point(230, 25), Width = 250 };
            var lblUncPass = new Label { Text = Strings.IsVi ? "Mật khẩu xác thực:" : "Password:", Location = new Point(15, 63), AutoSize = true };
            txtUncPass = new TextBox { Location = new Point(230, 60), Width = 250, UseSystemPasswordChar = true };
            grpUnc.Controls.AddRange(new Control[] { lblUncUser, txtUncUser, lblUncPass, txtUncPass });

            pnl.Controls.AddRange(new Control[] { lblPath, txtLocalPath, btnBrowse, btnDrive, grpUnc });
            return pnl;
        }

        private Panel CreateFtpPanel()
        {
            var pnl = new Panel { Size = new Size(685, 320), Visible = false };

            var lblHost = new Label { Text = Strings.FtpHost, Location = new Point(0, 5), AutoSize = true };
            txtFtpHost = new TextBox { Location = new Point(0, 28), Width = 320 };
            var lblPort = new Label { Text = Strings.FtpPort, Location = new Point(335, 5), AutoSize = true };
            numFtpPort = new NumericUpDown { Location = new Point(335, 28), Width = 80, Minimum = 1, Maximum = 65535, Value = 21 };

            var lblUser = new Label { Text = Strings.Username, Location = new Point(0, 65), AutoSize = true };
            txtFtpUser = new TextBox { Location = new Point(0, 88), Width = 250 };
            var lblPass = new Label { Text = Strings.Password, Location = new Point(265, 65), AutoSize = true };
            txtFtpPass = new TextBox { Location = new Point(265, 88), Width = 250, UseSystemPasswordChar = true };

            var lblRemote = new Label { Text = Strings.RemotePath, Location = new Point(0, 125), AutoSize = true };
            txtFtpRemotePath = new TextBox { Location = new Point(0, 148), Width = 350, Text = "/" };

            var lblEnc = new Label { Text = Strings.SecurityProtocol, Location = new Point(0, 185), AutoSize = true };
            cboFtpEncryption = new ComboBox { Location = new Point(0, 208), Width = 240, DropDownStyle = ComboBoxStyle.DropDownList };
            if (Strings.IsVi)
            {
                cboFtpEncryption.Items.AddRange(new object[] { "None (FTP tiêu chuẩn)", "Explicit TLS (FTPS bảo mật)", "Implicit TLS (Cổng 990)" });
            }
            else
            {
                cboFtpEncryption.Items.AddRange(new object[] { "None (Standard FTP)", "Explicit TLS (FTPS)", "Implicit TLS (Port 990)" });
            }
            cboFtpEncryption.SelectedIndex = 0;

            chkFtpPassive = new CheckBox { Text = Strings.FtpPassiveMode, Location = new Point(260, 210), AutoSize = true, Checked = true };

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
            var pnl = new Panel { Size = new Size(685, 320), Visible = false };

            var lblHost = new Label { Text = Strings.SftpHost, Location = new Point(0, 5), AutoSize = true };
            txtSftpHost = new TextBox { Location = new Point(0, 28), Width = 320 };
            var lblPort = new Label { Text = Strings.FtpPort, Location = new Point(335, 5), AutoSize = true };
            numSftpPort = new NumericUpDown { Location = new Point(335, 28), Width = 80, Minimum = 1, Maximum = 65535, Value = 22 };

            var lblUser = new Label { Text = Strings.Username, Location = new Point(0, 65), AutoSize = true };
            txtSftpUser = new TextBox { Location = new Point(0, 88), Width = 250 };
            var lblPass = new Label { Text = Strings.Password, Location = new Point(265, 65), AutoSize = true };
            txtSftpPass = new TextBox { Location = new Point(265, 88), Width = 250, UseSystemPasswordChar = true };

            var lblKey = new Label { Text = Strings.PrivateKey, Location = new Point(0, 125), AutoSize = true };
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

            var lblKeyPass = new Label { Text = Strings.KeyPassphrase, Location = new Point(0, 185), AutoSize = true };
            txtSftpKeyPass = new TextBox { Location = new Point(0, 208), Width = 250, UseSystemPasswordChar = true };

            var lblRemote = new Label { Text = Strings.RemotePath, Location = new Point(265, 185), AutoSize = true };
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
                Text = Strings.TransferZip,
                Location = new Point(15, 20),
                AutoSize = true,
                Checked = false
            };

            grpZip = new GroupBox
            {
                Text = Strings.IsVi ? "Cấu hình nén & Mã hóa ZIP" : "ZIP Compression & Encryption",
                Location = new Point(15, 55),
                Size = new Size(685, 150)
            };

            var lblLevel = new Label { Text = Strings.CompressionLevel, Location = new Point(15, 30), AutoSize = true };
            cboCompressionLevel = new ComboBox { Location = new Point(145, 27), Width = 240, DropDownStyle = ComboBoxStyle.DropDownList };
            if (Strings.IsVi)
            {
                cboCompressionLevel.Items.AddRange(new object[] { "Không nén (Store)", "Nhanh nhất (Fastest)", "Tiêu chuẩn (Normal)", "Tối đa (Maximum)" });
            }
            else
            {
                cboCompressionLevel.Items.AddRange(new object[] { "Store (No compression)", "Fastest", "Normal", "Maximum" });
            }
            cboCompressionLevel.SelectedIndex = 2;

            chkEncryptZip = new CheckBox { Text = Strings.EncryptZip, Location = new Point(15, 68), AutoSize = true };

            var lblZipPass = new Label { Text = Strings.ZipPassword, Location = new Point(35, 100), AutoSize = true };
            txtZipPassword = new TextBox { Location = new Point(145, 97), Width = 250, UseSystemPasswordChar = true, Enabled = false };

            chkEncryptZip.CheckedChanged += (s, e) => txtZipPassword.Enabled = chkEncryptZip.Checked;
            chkCompressionEnabled.CheckedChanged += (s, e) => grpZip.Enabled = chkCompressionEnabled.Checked;

            grpZip.Controls.AddRange(new Control[] { lblLevel, cboCompressionLevel, chkEncryptZip, lblZipPass, txtZipPassword });

            var grpIntegrity = new GroupBox
            {
                Text = Strings.IsVi ? "Cơ chế an toàn & Toàn vẹn dữ liệu" : "Data Integrity & Reliability",
                Location = new Point(15, 220),
                Size = new Size(685, 155)
            };

            chkEnableVss = new CheckBox
            {
                Text = Strings.VssSnapshot,
                Location = new Point(15, 25),
                AutoSize = true,
                Checked = true
            };

            chkCheckDiskSpace = new CheckBox
            {
                Text = Strings.IsVi ? "Kiểm tra dung lượng khả dụng trước khi thực thi (chống tràn đĩa)" : "Pre-flight disk space check",
                Location = new Point(15, 55),
                AutoSize = true,
                Checked = true
            };

            chkVerifyChecksum = new CheckBox
            {
                Text = Strings.IsVi ? "Xác thực mã băm SHA-256 sau khi truyền dữ liệu" : "Verify SHA-256 checksum after transfer",
                Location = new Point(15, 85),
                AutoSize = true,
                Checked = true
            };

            var lblRetry = new Label { Text = Strings.RetryAttempts, Location = new Point(15, 118), AutoSize = true };
            numNetworkRetry = new NumericUpDown { Location = new Point(310, 115), Width = 60, Minimum = 1, Maximum = 10, Value = 3 };
            var lblRetrySuffix = new Label { Text = Strings.RetryTimesSuffix, Location = new Point(375, 118), AutoSize = true };

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

            var lblTitle = new Label
            {
                Text = Strings.IsVi
                    ? "Danh sách lịch thực thi tự động của Job sao lưu:"
                    : "Automated backup execution schedules:",
                Location = new Point(15, 15),
                AutoSize = true
            };

            lvSchedules = new ListView
            {
                Location = new Point(15, 40),
                Size = new Size(505, 230),
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false
            };
            lvSchedules.Columns.Add(Strings.ScheduleColType, 150);
            lvSchedules.Columns.Add(Strings.ScheduleColTime, 130);
            lvSchedules.Columns.Add(Strings.ScheduleColDetails, 160);
            lvSchedules.Columns.Add(Strings.IsVi ? "Trạng thái" : "Status", 70);

            var btnAdd = new Button { Text = "➕ " + Strings.AddScheduleBtn, Location = new Point(530, 40), Size = new Size(130, 32) };
            var btnEdit = new Button { Text = "✏ " + Strings.EditScheduleBtn, Location = new Point(530, 78), Size = new Size(130, 32) };
            var btnDelete = new Button { Text = "🗑 " + Strings.DeleteScheduleBtn, Location = new Point(530, 116), Size = new Size(130, 32) };

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
                item.SubItems.Add(s.Enabled ? (Strings.IsVi ? "🟢 Bật" : "🟢 Active") : (Strings.IsVi ? "⚪ Tắt" : "⚪ Disabled"));
                lvSchedules.Items.Add(item);
            }
        }

        public static string FormatIntervalType(ScheduleIntervalType type)
        {
            if (Strings.IsVi)
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
            else
            {
                switch (type)
                {
                    case ScheduleIntervalType.Once: return "Once";
                    case ScheduleIntervalType.Daily: return "Daily";
                    case ScheduleIntervalType.Weekly: return "Weekly";
                    case ScheduleIntervalType.Monthly: return "Monthly";
                    case ScheduleIntervalType.RepeatEvery: return "Repeat Every";
                    default: return type.ToString();
                }
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
            if (Strings.IsVi)
            {
                switch (s.IntervalType)
                {
                    case ScheduleIntervalType.Daily:
                        return $"Hằng ngày vào lúc {s.TimeOfDay}";
                    case ScheduleIntervalType.Weekly:
                        return $"Thứ {string.Join(", ", s.DaysOfWeek)} vào lúc {s.TimeOfDay}";
                    case ScheduleIntervalType.Monthly:
                        return $"Ngày {s.DayOfMonth} hằng tháng vào lúc {s.TimeOfDay}";
                    case ScheduleIntervalType.RepeatEvery:
                        return $"Lặp mỗi {s.RepeatIntervalMinutes}p ({s.RepeatStartTime} - {s.RepeatEndTime})";
                    case ScheduleIntervalType.Once:
                        return s.RunOnceLocalTime.HasValue ? s.RunOnceLocalTime.Value.ToString("yyyy-MM-dd HH:mm") : "Một lần";
                    default:
                        return "";
                }
            }
            else
            {
                switch (s.IntervalType)
                {
                    case ScheduleIntervalType.Daily:
                        return $"Daily at {s.TimeOfDay}";
                    case ScheduleIntervalType.Weekly:
                        return $"Weekly on {string.Join(", ", s.DaysOfWeek)} at {s.TimeOfDay}";
                    case ScheduleIntervalType.Monthly:
                        return $"Monthly on day {s.DayOfMonth} at {s.TimeOfDay}";
                    case ScheduleIntervalType.RepeatEvery:
                        return $"Every {s.RepeatIntervalMinutes} mins ({s.RepeatStartTime} - {s.RepeatEndTime})";
                    case ScheduleIntervalType.Once:
                        return s.RunOnceLocalTime.HasValue ? s.RunOnceLocalTime.Value.ToString("yyyy-MM-dd HH:mm") : "Once";
                    default:
                        return "";
                }
            }
        }

        private TabPage CreateRetentionTab()
        {
            var page = new TabPage(Strings.TabRetention) { Padding = new Padding(15) };

            pnlRetentionModeBanner = new Panel
            {
                Location = new Point(15, 12),
                Size = new Size(685, 40),
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
                Text = "🔵 " + Strings.BackupMode + ": " + Strings.ModeFull,
                Location = new Point(10, 10),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 76, 153)
            };
            pnlRetentionModeBanner.Controls.Add(lblRetentionModeTitle);

            grpRetention = new GroupBox
            {
                Text = Strings.RetentionPolicyTitle,
                Location = new Point(15, 62),
                Size = new Size(685, 160),
                Font = new Font("Segoe UI", 9F)
            };

            // Option 1: Keep Count
            rbKeepFullCount = new RadioButton { Text = Strings.RetentionKeepCount, Location = new Point(20, 28), AutoSize = true, Checked = true };
            numKeepFullCount = new NumericUpDown { Location = new Point(220, 26), Width = 65, Minimum = 1, Maximum = 100, Value = 5 };
            lblKeepFullCountSuffix = new Label { Text = Strings.RetentionSetsUnit, Location = new Point(295, 28), AutoSize = true };

            // Option 2: Keep Days
            rbKeepDays = new RadioButton { Text = Strings.RetentionKeepDays, Location = new Point(20, 70), AutoSize = true };
            numKeepDays = new NumericUpDown { Location = new Point(220, 68), Width = 65, Minimum = 1, Maximum = 3650, Value = 30, Enabled = false };
            lblKeepDaysSuffix = new Label { Text = Strings.RetentionDaysUnit, Location = new Point(295, 70), AutoSize = true };

            // Option 3: Keep All
            rbKeepAll = new RadioButton { Text = Strings.RetentionKeepAll, Location = new Point(20, 112), AutoSize = true };

            rbKeepFullCount.CheckedChanged += (s, e) => numKeepFullCount.Enabled = rbKeepFullCount.Checked;
            rbKeepDays.CheckedChanged += (s, e) => numKeepDays.Enabled = rbKeepDays.Checked;

            grpRetention.Controls.AddRange(new Control[] {
                rbKeepFullCount, numKeepFullCount, lblKeepFullCountSuffix,
                rbKeepDays, numKeepDays, lblKeepDaysSuffix,
                rbKeepAll
            });

            page.Controls.AddRange(new Control[] { pnlRetentionModeBanner, grpRetention });
            return page;
        }

        private void UpdateRetentionUI()
        {
            if (pnlRetentionModeBanner == null || grpRetention == null) return;

            bool isFull = rbModeFull != null && rbModeFull.Checked;
            grpRetention.Text = Strings.RetentionPolicyTitle;

            if (isFull)
            {
                pnlRetentionModeBanner.BackColor = Color.FromArgb(238, 245, 255);
                lblRetentionModeTitle.Text = "🔵 " + Strings.BackupMode + ": " + Strings.ModeFull;
                lblRetentionModeTitle.ForeColor = Color.FromArgb(0, 76, 153);

                rbKeepFullCount.Text = Strings.RetentionKeepCount;
                lblKeepFullCountSuffix.Text = Strings.IsVi ? "điểm Full gần nhất" : "latest Full points";

                rbKeepDays.Text = Strings.RetentionKeepDays;
                lblKeepDaysSuffix.Text = Strings.IsVi ? "ngày gần nhất" : "days";
                rbKeepAll.Text = Strings.RetentionKeepAll;
            }
            else
            {
                pnlRetentionModeBanner.BackColor = Color.FromArgb(236, 253, 245);
                lblRetentionModeTitle.Text = "🟢 " + Strings.BackupMode + ": " + Strings.ModeIncremental;
                lblRetentionModeTitle.ForeColor = Color.FromArgb(6, 95, 70);

                rbKeepFullCount.Text = Strings.RetentionKeepCount;
                lblKeepFullCountSuffix.Text = Strings.IsVi ? "chuỗi Full + Incremental gần nhất" : "latest Full + Incremental chains";

                rbKeepDays.Text = Strings.RetentionKeepDays;
                lblKeepDaysSuffix.Text = Strings.IsVi ? "ngày gần nhất" : "days";
                rbKeepAll.Text = Strings.RetentionKeepAll;
            }

            numKeepFullCount.Location = new Point(Math.Max(220, rbKeepFullCount.Right + 8), 26);
            lblKeepFullCountSuffix.Location = new Point(numKeepFullCount.Right + 8, 28);

            numKeepDays.Location = new Point(Math.Max(220, rbKeepDays.Right + 8), 68);
            lblKeepDaysSuffix.Location = new Point(numKeepDays.Right + 8, 70);

            pnlRetentionModeBanner.Invalidate();
        }

        private TabPage CreateNotificationTab()
        {
            var page = new TabPage(Strings.TabNotification) { Padding = new Padding(15) };

            chkNotificationEnabled = new CheckBox
            {
                Text = Strings.AlertEnable,
                Location = new Point(15, 20),
                AutoSize = true
            };

            var grp = new GroupBox { Text = Strings.AlertEmailSettings, Location = new Point(15, 55), Size = new Size(685, 220) };

            var lblCond = new Label { Text = Strings.AlertCondition, Location = new Point(15, 30), AutoSize = true };
            cboNotificationCondition = new ComboBox { Location = new Point(175, 27), Width = 280, DropDownStyle = ComboBoxStyle.DropDownList };
            cboNotificationCondition.Items.AddRange(new object[] { Strings.ConditionAlways, Strings.ConditionFailureOnly, Strings.ConditionSuccessOnly });
            cboNotificationCondition.SelectedIndex = 0;

            var lblRecipients = new Label { Text = Strings.AlertRecipients, Location = new Point(15, 75), AutoSize = true };
            txtNotificationRecipients = new TextBox { Location = new Point(15, 98), Width = 645 };

            var lblSubject = new Label { Text = Strings.AlertSubjectPrefix, Location = new Point(15, 140), AutoSize = true };
            txtNotificationSubject = new TextBox { Location = new Point(15, 163), Width = 250, Text = "[FtpSysPro]" };

            chkNotificationEnabled.CheckedChanged += (s, e) => grp.Enabled = chkNotificationEnabled.Checked;
            grp.Enabled = false;

            grp.Controls.AddRange(new Control[] { lblCond, cboNotificationCondition, lblRecipients, txtNotificationRecipients, lblSubject, txtNotificationSubject });

            var pnlFinishBanner = new Panel
            {
                Location = new Point(15, 290),
                Size = new Size(685, 65),
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
                Text = Strings.IsVi ? "✔ Đã hoàn tất cấu hình Job sao lưu!" : "✔ Backup Job Configuration Complete!",
                Location = new Point(12, 10),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(21, 87, 36)
            };

            var lblFinishDesc = new Label
            {
                Text = Strings.IsVi
                    ? "Nhấn nút '✔ Hoàn tất' để lưu cấu hình Job vào hệ thống và kích hoạt lịch thực thi tự động."
                    : "Click '✔ Finish' to save the job configuration and enable automated background scheduling.",
                Location = new Point(12, 34),
                Size = new Size(655, 25),
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

            lvSources.Items.Clear();
            if (_job.SourceDirectories != null)
            {
                foreach (var s in _job.SourceDirectories)
                    AddSourceItem(s);
            }

            txtExcludePatterns.Text = string.Join(", ", _job.ExcludePatterns ?? new List<string>());
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
            foreach (ListViewItem item in lvSources.Items)
                _job.SourceDirectories.Add(item.Text);

            _job.ExcludePatterns = txtExcludePatterns.Text
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrEmpty(p))
                .ToList();

            _job.SourceSizeBytes = _lastCalculatedSize;
            _job.SourceFileCount = _lastCalculatedFiles;

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

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            try
            {
                _sourceScanCts?.Cancel();
                _sourceScanCts?.Dispose();
                _debounceTimer?.Stop();
                _debounceTimer?.Dispose();
            }
            catch { }
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
            Text = _isNew
                ? (Strings.IsVi ? "Thêm lịch thực thi sao lưu" : "Add Backup Schedule")
                : (Strings.IsVi ? "Chỉnh sửa lịch thực thi sao lưu" : "Edit Backup Schedule");
            Size = new Size(530, 420);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = new Font("Segoe UI", 9F);
            BackColor = Color.White;

            // 1. Top Mode Banner
            bool isFull = _jobBackupType == BackupType.Full;
            var pnlModeBanner = new Panel
            {
                Location = new Point(20, 14),
                Size = new Size(475, 36),
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
                Text = isFull ? ("🔵 " + Strings.BackupMode + ": " + Strings.ModeFull) : ("🟢 " + Strings.BackupMode + ": " + Strings.ModeIncremental),
                Location = new Point(12, 9),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = isFull ? Color.FromArgb(0, 76, 153) : Color.FromArgb(6, 95, 70)
            };
            pnlModeBanner.Controls.Add(lblModeTitle);

            // 2. Schedule Interval Selection Row
            var lblInterval = new Label
            {
                Text = Strings.IsVi ? "Chu kỳ lặp:" : "Frequency:",
                Location = new Point(20, 62),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            cboIntervalType = new ComboBox
            {
                Location = new Point(115, 58),
                Width = 220,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            if (Strings.IsVi)
            {
                cboIntervalType.Items.AddRange(new object[] {
                    "Một lần (Once)",
                    "Hằng ngày (Daily)",
                    "Hằng tuần (Weekly)",
                    "Hằng tháng (Monthly)",
                    "Lặp định kỳ (Repeat Every)"
                });
            }
            else
            {
                cboIntervalType.Items.AddRange(new object[] {
                    "Once",
                    "Daily",
                    "Weekly",
                    "Monthly",
                    "Repeat Every"
                });
            }
            cboIntervalType.SelectedIndex = 1;

            chkEnabled = new CheckBox
            {
                Text = Strings.IsVi ? "Kích hoạt lịch này" : "Enable schedule",
                Location = new Point(355, 60),
                AutoSize = true,
                Checked = true
            };

            // 3. Compact 12h / 24h format selector
            pnlTimeFormat = new FlowLayoutPanel
            {
                Size = new Size(165, 26),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0)
            };
            rbTime24h = new RadioButton { Text = Strings.IsVi ? "24 giờ" : "24-hour", AutoSize = true, Checked = true, Margin = new Padding(0, 3, 10, 0) };
            rbTime12h = new RadioButton { Text = Strings.IsVi ? "12 giờ AM/PM" : "12-hour", AutoSize = true, Margin = new Padding(0, 3, 0, 0) };
            pnlTimeFormat.Controls.AddRange(new Control[] { rbTime24h, rbTime12h });

            rbTime24h.CheckedChanged += (s, e) => { if (rbTime24h.Checked) UpdateTimePickerFormats(); };
            rbTime12h.CheckedChanged += (s, e) => { if (rbTime12h.Checked) UpdateTimePickerFormats(); };

            // 4. Main Settings GroupBox
            var grpSettings = new GroupBox
            {
                Text = Strings.IsVi ? "Thiết lập thời gian & Chu kỳ" : "Time & Interval Settings",
                Location = new Point(20, 95),
                Size = new Size(475, 230),
                Font = new Font("Segoe UI", 9F)
            };

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
                Text = "💾 " + Strings.Save,
                Location = new Point(285, 338),
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
                Location = new Point(400, 338),
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
                case 0:
                    if (pnlOnce != null)
                    {
                        pnlOnce.Controls.Add(pnlTimeFormat);
                        pnlTimeFormat.Location = new Point(275, 12);
                        pnlTimeFormat.BringToFront();
                    }
                    break;
                case 1:
                    if (pnlDaily != null)
                    {
                        pnlDaily.Controls.Add(pnlTimeFormat);
                        pnlTimeFormat.Location = new Point(215, 12);
                        pnlTimeFormat.BringToFront();
                    }
                    break;
                case 2:
                    if (pnlWeekly != null)
                    {
                        pnlWeekly.Controls.Add(pnlTimeFormat);
                        pnlTimeFormat.Location = new Point(215, 10);
                        pnlTimeFormat.BringToFront();
                    }
                    break;
                case 3:
                    if (pnlMonthly != null)
                    {
                        pnlMonthly.Controls.Add(pnlTimeFormat);
                        pnlTimeFormat.Location = new Point(275, 48);
                        pnlTimeFormat.BringToFront();
                    }
                    break;
                case 4:
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
                dtpOnce.CustomFormat = is12h ? "yyyy-MM-dd hh:mm tt" : "yyyy-MM-dd HH:mm";
                dtpOnce.Width = is12h ? 175 : 155;
            }
        }

        private void SyncTimePickers(DateTimePicker source)
        {
            if (_isSyncingTime || source == null) return;
            try
            {
                _isSyncingTime = true;
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
            var lblTime = new Label
            {
                Text = Strings.IsVi ? "Thời điểm thực thi:" : "Execution time:",
                Location = new Point(0, 15),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            dtpTimeOfDay = new DateTimePicker { Location = new Point(125, 12), Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Width = 90 };
            dtpTimeOfDay.ValueChanged += (s, e) => SyncTimePickers(dtpTimeOfDay);

            pnl.Controls.AddRange(new Control[] { lblTime, dtpTimeOfDay });
            return pnl;
        }

        private Panel CreateWeeklyPanel()
        {
            var pnl = new Panel { Size = new Size(445, 190), Visible = false };
            var lblTime = new Label
            {
                Text = Strings.IsVi ? "Thời điểm thực thi:" : "Execution time:",
                Location = new Point(0, 12),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            dtpTimeOfDayWeekly = new DateTimePicker { Location = new Point(125, 10), Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Width = 90 };
            dtpTimeOfDayWeekly.ValueChanged += (s, e) => SyncTimePickers(dtpTimeOfDayWeekly);

            var lblDays = new Label
            {
                Text = Strings.IsVi ? "Ngày thực thi trong tuần:" : "Days of the week:",
                Location = new Point(0, 45),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            clbDaysOfWeek = new CheckedListBox
            {
                Location = new Point(0, 68),
                Size = new Size(440, 85),
                MultiColumn = true,
                ColumnWidth = 105,
                CheckOnClick = true
            };
            if (Strings.IsVi)
            {
                clbDaysOfWeek.Items.AddRange(new object[] { "Chủ nhật", "Thứ Hai", "Thứ Ba", "Thứ Tư", "Thứ Năm", "Thứ Sáu", "Thứ Bảy" });
            }
            else
            {
                clbDaysOfWeek.Items.AddRange(new object[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" });
            }

            var btnWeekdays = new LinkLabel { Text = Strings.IsVi ? "Chọn T2 - T6" : "Weekdays (Mon-Fri)", Location = new Point(220, 46), AutoSize = true };
            btnWeekdays.LinkClicked += (s, e) =>
            {
                for (int i = 0; i < 7; i++)
                    clbDaysOfWeek.SetItemChecked(i, i >= 1 && i <= 5);
            };

            var btnAllWeek = new LinkLabel { Text = Strings.IsVi ? "Chọn cả tuần" : "All Week", Location = new Point(340, 46), AutoSize = true };
            btnAllWeek.LinkClicked += (s, e) =>
            {
                for (int i = 0; i < 7; i++)
                    clbDaysOfWeek.SetItemChecked(i, true);
            };

            pnl.Controls.AddRange(new Control[] { lblTime, dtpTimeOfDayWeekly, lblDays, btnWeekdays, btnAllWeek, clbDaysOfWeek });
            return pnl;
        }

        private Panel CreateMonthlyPanel()
        {
            var pnl = new Panel { Size = new Size(445, 190), Visible = false };
            var lblDay = new Label
            {
                Text = Strings.IsVi ? "Ngày trong tháng (1 - 31):" : "Day of month (1 - 31):",
                Location = new Point(0, 15),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            numDayOfMonth = new NumericUpDown { Location = new Point(175, 12), Width = 65, Minimum = 1, Maximum = 31, Value = 1 };

            var lblTime = new Label
            {
                Text = Strings.IsVi ? "Thời điểm thực thi:" : "Execution time:",
                Location = new Point(0, 52),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            dtpTimeOfDayMonthly = new DateTimePicker { Location = new Point(175, 48), Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Width = 90 };
            dtpTimeOfDayMonthly.ValueChanged += (s, e) => SyncTimePickers(dtpTimeOfDayMonthly);

            pnl.Controls.AddRange(new Control[] { lblDay, numDayOfMonth, lblTime, dtpTimeOfDayMonthly });
            return pnl;
        }

        private Panel CreateRepeatPanel()
        {
            var pnl = new Panel { Size = new Size(445, 190), Visible = false };
            var lblEvery = new Label
            {
                Text = Strings.IsVi ? "Chu kỳ lặp lại:" : "Repeat every:",
                Location = new Point(0, 15),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            numRepeatInterval = new NumericUpDown { Location = new Point(115, 12), Width = 70, Minimum = 1, Maximum = 1440, Value = 60 };
            var lblUnit = new Label { Text = Strings.IsVi ? "phút" : "minutes", Location = new Point(190, 15), AutoSize = true };

            var lblRange = new Label
            {
                Text = Strings.IsVi ? "Trong khung giờ:" : "Between hours:",
                Location = new Point(0, 52),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            dtpRepeatStart = new DateTimePicker { Location = new Point(115, 48), Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Width = 85 };
            var lblTo = new Label { Text = Strings.IsVi ? "đến" : "to", Location = new Point(205, 52), AutoSize = true };
            dtpRepeatEnd = new DateTimePicker { Location = new Point(230, 48), Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Width = 85 };

            pnl.Controls.AddRange(new Control[] { lblEvery, numRepeatInterval, lblUnit, lblRange, dtpRepeatStart, lblTo, dtpRepeatEnd });
            return pnl;
        }

        private Panel CreateOncePanel()
        {
            var pnl = new Panel { Size = new Size(445, 190), Visible = false };
            var lblOnce = new Label
            {
                Text = Strings.IsVi ? "Thời điểm thực thi:" : "Execution time:",
                Location = new Point(0, 15),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            dtpOnce = new DateTimePicker { Location = new Point(125, 12), CustomFormat = "yyyy-MM-dd HH:mm", Format = DateTimePickerFormat.Custom, Width = 155 };

            pnl.Controls.AddRange(new Control[] { lblOnce, dtpOnce });
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

            _schedule.DaysOfWeek = new List<int>();
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
