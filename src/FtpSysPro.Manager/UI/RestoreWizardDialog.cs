using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using FtpSysPro.Core.Engine;
using FtpSysPro.Core.Ipc;
using FtpSysPro.Core.Models;
using FtpSysPro.Core.Resources;
using FtpSysPro.Core.Config;
using Newtonsoft.Json;

namespace FtpSysPro.Manager.UI
{
    public class RestoreWizardDialog : Form
    {
        private readonly List<BackupJob> _jobs;
        private readonly PipeClient _pipeClient;
        private readonly ConfigStore _configStore;

        // Source Mode Radios
        private FlowLayoutPanel pnlRadios;
        private RadioButton rdoJob;
        private RadioButton rdoFolder;
        private RadioButton rdoRemote;

        // Job Source Panel
        private Panel pnlJob;
        private ComboBox cboJob;

        // Folder Source Panel
        private Panel pnlFolder;
        private TextBox txtSourceFolder;
        private Button btnBrowseSource;
        private Button btnScanFolder;

        // Remote FTP/SFTP Source Panel
        private Panel pnlRemote;
        private ComboBox cboRemoteProto;
        private TextBox txtRemoteHost;
        private NumericUpDown numRemotePort;
        private TextBox txtRemoteUser;
        private TextBox txtRemotePass;
        private TextBox txtRemotePath;
        private Button btnRemoteScan;

        // Step 2: Restore Points
        private ListView lvBackupSets;

        // Step 3: Target & Options
        private TextBox txtTargetFolder;
        private Button btnBrowseTarget;
        private CheckBox chkSelectiveFolder;
        private TextBox txtSelectiveFolder;
        private CheckBox chkOverwrite;
        private TextBox txtPassword;
        private Label lblPassword;

        // Progress & Execution
        private ProgressBar progressBar;
        private Label lblStatus;
        private Button btnRestore;
        private Button btnCancel;

        private List<BackupSetManifest> _loadedSets = new List<BackupSetManifest>();

        public RestoreWizardDialog(List<BackupJob> jobs, PipeClient pipeClient, string preselectedJobId = null, ConfigStore configStore = null)
        {
            _jobs = jobs ?? new List<BackupJob>();
            _pipeClient = pipeClient;
            _configStore = configStore ?? new ConfigStore();

            InitializeComponents();
            PopulateJobs(preselectedJobId);
        }

        private void InitializeComponents()
        {
            Text = Strings.AppName + " - " + Strings.Restore;
            ClientSize = new Size(780, 605);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = new Font("Segoe UI", 9F);

            // ==========================================
            // Step 1: Nguồn phục hồi (Restoration Source)
            // ==========================================
            var lblSource = new Label
            {
                Text = Strings.RestoreSource,
                Location = new Point(20, 12),
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold)
            };

            pnlRadios = new FlowLayoutPanel
            {
                Location = new Point(20, 36),
                Size = new Size(740, 28),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = false
            };

            rdoJob = new RadioButton
            {
                Text = Strings.RestoreFromJob,
                AutoSize = true,
                Checked = true,
                Margin = new Padding(0, 0, 20, 0)
            };
            rdoFolder = new RadioButton
            {
                Text = Strings.RestoreFromFolder,
                AutoSize = true,
                Margin = new Padding(0, 0, 20, 0)
            };
            rdoRemote = new RadioButton
            {
                Text = Strings.RestoreFromRemote,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 0)
            };

            rdoJob.CheckedChanged += (s, e) => UpdateSourceModeUi();
            rdoFolder.CheckedChanged += (s, e) => UpdateSourceModeUi();
            rdoRemote.CheckedChanged += (s, e) => UpdateSourceModeUi();

            pnlRadios.Controls.AddRange(new Control[] { rdoJob, rdoFolder, rdoRemote });

            // Panel 1: Job
            pnlJob = new Panel { Location = new Point(20, 68), Size = new Size(740, 62), Visible = true };
            var lblSelectJob = new Label { Text = Strings.IsVi ? "Chọn Job:" : "Select Job:", Location = new Point(0, 6), AutoSize = true };
            cboJob = new ComboBox { Location = new Point(80, 3), Width = 520, DropDownStyle = ComboBoxStyle.DropDownList };
            cboJob.SelectedIndexChanged += async (s, e) => await LoadBackupSetsForSelectedJobAsync();
            pnlJob.Controls.AddRange(new Control[] { lblSelectJob, cboJob });

            // Panel 2: Folder
            pnlFolder = new Panel { Location = new Point(20, 68), Size = new Size(740, 62), Visible = false };
            var lblFolder = new Label { Text = Strings.IsVi ? "Thư mục:" : "Directory:", Location = new Point(0, 6), AutoSize = true };
            txtSourceFolder = new TextBox { Location = new Point(80, 3), Width = 450 };
            txtSourceFolder.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    btnScanFolder.PerformClick();
                }
            };

            btnBrowseSource = new Button { Text = Strings.Browse, Location = new Point(538, 2), Size = new Size(85, 26) };
            btnBrowseSource.Click += async (s, e) =>
            {
                var folder = FolderBrowserEx.SelectFolder(this, txtSourceFolder.Text, Strings.IsVi ? "Chọn thư mục chứa các bản sao lưu" : "Select folder containing backup sets");
                if (!string.IsNullOrEmpty(folder))
                {
                    txtSourceFolder.Text = folder;
                    await LoadBackupSetsForFolderAsync();
                }
            };
            btnScanFolder = new Button { Text = Strings.RestoreScan, Location = new Point(628, 2), Size = new Size(110, 26), AutoSize = true, AutoSizeMode = AutoSizeMode.GrowOnly };
            btnScanFolder.Click += async (s, e) => await LoadBackupSetsForFolderAsync();
            var lblFolderHint = new Label
            {
                Text = Strings.IsVi ? "(Chọn thư mục trên máy tính, ổ cứng USB hoặc chia sẻ mạng UNC chứa dữ liệu sao lưu)" : "(Select local directory, USB drive, or UNC share containing backup sets)",
                Location = new Point(80, 32),
                AutoSize = true,
                ForeColor = Color.DimGray
            };
            pnlFolder.Controls.AddRange(new Control[] { lblFolder, txtSourceFolder, btnBrowseSource, btnScanFolder, lblFolderHint });

            // Panel 3: Remote FTP/SFTP
            pnlRemote = new Panel { Location = new Point(20, 68), Size = new Size(740, 62), Visible = false };
            var lblProto = new Label { Text = Strings.IsVi ? "Giao thức:" : "Proto:", Location = new Point(0, 6), AutoSize = true };
            cboRemoteProto = new ComboBox { Location = new Point(65, 3), Width = 85, DropDownStyle = ComboBoxStyle.DropDownList };
            cboRemoteProto.Items.AddRange(new object[] { "FTP / FTPS", "SFTP" });
            cboRemoteProto.SelectedIndex = 0;
            cboRemoteProto.SelectedIndexChanged += (s, e) =>
            {
                numRemotePort.Value = cboRemoteProto.SelectedIndex == 1 ? 22 : 21;
            };

            var lblHost = new Label { Text = "Host:", Location = new Point(155, 6), AutoSize = true };
            txtRemoteHost = new TextBox { Location = new Point(190, 3), Width = 150 };

            var lblPort = new Label { Text = "Port:", Location = new Point(345, 6), AutoSize = true };
            numRemotePort = new NumericUpDown { Location = new Point(380, 3), Width = 55, Minimum = 1, Maximum = 65535, Value = 21 };

            var lblUser = new Label { Text = "User:", Location = new Point(440, 6), AutoSize = true };
            txtRemoteUser = new TextBox { Location = new Point(478, 3), Width = 100 };

            var lblPass = new Label { Text = "Pass:", Location = new Point(585, 6), AutoSize = true };
            txtRemotePass = new TextBox { Location = new Point(625, 3), Width = 110, UseSystemPasswordChar = true };

            var lblRemotePath = new Label { Text = Strings.IsVi ? "Đường dẫn từ xa:" : "Remote Path:", Location = new Point(0, 35), AutoSize = true };
            txtRemotePath = new TextBox { Location = new Point(105, 32), Width = 235, Text = "/" };

            btnRemoteScan = new Button { Text = "🔌 " + Strings.RestoreConnectAndScan, Location = new Point(350, 30), Size = new Size(135, 27) };
            btnRemoteScan.Click += async (s, e) => await LoadBackupSetsForRemoteAsync();

            pnlRemote.Controls.AddRange(new Control[] {
                lblProto, cboRemoteProto,
                lblHost, txtRemoteHost,
                lblPort, numRemotePort,
                lblUser, txtRemoteUser,
                lblPass, txtRemotePass,
                lblRemotePath, txtRemotePath,
                btnRemoteScan
            });

            // ==========================================
            // Step 2: Lựa chọn điểm phục hồi (Restore Point)
            // ==========================================
            var lblSets = new Label
            {
                Text = Strings.IsVi ? "2. Lựa chọn điểm phục hồi cần khôi phục (Full & Incremental):" : "2. Select Restore Point to restore (Full & Incremental):",
                Location = new Point(20, 138),
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold)
            };
            lvBackupSets = new ListView
            {
                Location = new Point(20, 160),
                Size = new Size(740, 175),
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                HideSelection = false
            };
            lvBackupSets.Columns.Add(Strings.IsVi ? "Thời điểm tạo (UTC)" : "Created (UTC)", 140);
            lvBackupSets.Columns.Add(Strings.IsVi ? "Chế độ" : "Mode", 85);
            lvBackupSets.Columns.Add(Strings.IsVi ? "Mã điểm phục hồi" : "Restore Point ID", 225);
            lvBackupSets.Columns.Add(Strings.IsVi ? "Số tệp" : "Files", 65);
            lvBackupSets.Columns.Add(Strings.IsVi ? "Dung lượng" : "Size", 90);
            lvBackupSets.Columns.Add(Strings.IsVi ? "Định dạng" : "Format", 100);

            lvBackupSets.SelectedIndexChanged += (s, e) =>
            {
                btnRestore.Enabled = lvBackupSets.SelectedItems.Count > 0;
                if (lvBackupSets.SelectedItems.Count > 0 && string.IsNullOrWhiteSpace(txtTargetFolder.Text))
                {
                    var set = lvBackupSets.SelectedItems[0].Tag as BackupSetManifest;
                    var name = !string.IsNullOrEmpty(set?.JobName) ? set.JobName : "Restored";
                    txtTargetFolder.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Restored_" + name);
                }
            };

            lvBackupSets.DoubleClick += async (s, e) =>
            {
                if (btnRestore.Enabled)
                {
                    await ExecuteRestoreAsync();
                }
            };

            // ==========================================
            // Step 3: Cấu hình phục hồi & Thư mục đích
            // ==========================================
            var lblTarget = new Label
            {
                Text = Strings.IsVi ? "3. Cấu hình phục hồi dữ liệu:" : "3. Restoration Configuration:",
                Location = new Point(20, 345),
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold)
            };

            var lblTargetDir = new Label { Text = Strings.IsVi ? "Thư mục đích (*):" : "Target Folder (*):", Location = new Point(20, 373), AutoSize = true };
            txtTargetFolder = new TextBox { Location = new Point(130, 370), Width = 515 };
            btnBrowseTarget = new Button { Text = Strings.Browse, Location = new Point(655, 368), Size = new Size(105, 27) };
            btnBrowseTarget.Click += (s, e) =>
            {
                var folder = FolderBrowserEx.SelectFolder(this, txtTargetFolder.Text, Strings.IsVi ? "Chọn thư mục đích tiếp nhận dữ liệu phục hồi" : "Select target directory for data restoration");
                if (!string.IsNullOrEmpty(folder)) txtTargetFolder.Text = folder;
            };

            chkSelectiveFolder = new CheckBox
            {
                Text = Strings.RestoreSelectiveSubFolder,
                Location = new Point(20, 407),
                AutoSize = true
            };
            txtSelectiveFolder = new TextBox
            {
                Location = new Point(230, 405),
                Width = 240,
                Enabled = false
            };
            chkSelectiveFolder.CheckedChanged += (s, e) =>
            {
                txtSelectiveFolder.Enabled = chkSelectiveFolder.Checked;
                if (chkSelectiveFolder.Checked) txtSelectiveFolder.Focus();
            };

            var lblSelectiveHint = new Label
            {
                Text = Strings.RestoreSelectivePlaceholder,
                Location = new Point(475, 408),
                AutoSize = true,
                ForeColor = Color.DimGray
            };

            chkOverwrite = new CheckBox
            {
                Text = Strings.IsVi ? "Ghi đè tệp nếu đã tồn tại ở thư mục đích" : "Overwrite existing files in target directory",
                Location = new Point(20, 437),
                AutoSize = true,
                Checked = true
            };

            lblPassword = new Label
            {
                Text = Strings.IsVi ? "Mật khẩu giải mã (AES-256):" : "Decryption Password:",
                Location = new Point(340, 438),
                AutoSize = true
            };
            txtPassword = new TextBox { Location = new Point(530, 435), Width = 230, UseSystemPasswordChar = true };

            // ==========================================
            // Step 4: Tiến độ & Nút thực thi
            // ==========================================
            progressBar = new ProgressBar { Location = new Point(20, 475), Size = new Size(740, 18), Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 0, Visible = false };
            lblStatus = new Label { Location = new Point(20, 498), Size = new Size(740, 30), AutoSize = false, Text = "" };

            btnRestore = new Button
            {
                Text = "🔄 " + Strings.Restore,
                Location = new Point(480, 545),
                Size = new Size(175, 36),
                BackColor = SystemColors.ControlLight,
                ForeColor = SystemColors.GrayText,
                FlatStyle = FlatStyle.Flat,
                Font = new Font(Font, FontStyle.Bold),
                Enabled = false
            };
            btnRestore.FlatAppearance.BorderSize = 1;
            btnRestore.FlatAppearance.BorderColor = Color.FromArgb(0, 120, 215);

            btnRestore.EnabledChanged += (s, e) =>
            {
                if (btnRestore.Enabled)
                {
                    btnRestore.BackColor = Color.FromArgb(0, 120, 215);
                    btnRestore.ForeColor = Color.White;
                }
                else
                {
                    btnRestore.BackColor = SystemColors.ControlLight;
                    btnRestore.ForeColor = SystemColors.GrayText;
                }
            };

            btnCancel = new Button { Text = Strings.Close, DialogResult = DialogResult.Cancel, Location = new Point(665, 545), Size = new Size(95, 36) };

            btnRestore.Click += async (s, e) => await ExecuteRestoreAsync();

            Controls.AddRange(new Control[] {
                lblSource, pnlRadios,
                pnlJob, pnlFolder, pnlRemote,
                lblSets, lvBackupSets,
                lblTarget, lblTargetDir, txtTargetFolder, btnBrowseTarget,
                chkSelectiveFolder, txtSelectiveFolder, lblSelectiveHint,
                chkOverwrite, lblPassword, txtPassword,
                progressBar, lblStatus,
                btnRestore, btnCancel
            });
        }

        private void UpdateSourceModeUi()
        {
            pnlJob.Visible = rdoJob.Checked;
            pnlFolder.Visible = rdoFolder.Checked;
            pnlRemote.Visible = rdoRemote.Checked;

            lvBackupSets.Items.Clear();
            _loadedSets.Clear();
            lblStatus.Text = "";
            btnRestore.Enabled = false;

            if (rdoJob.Checked && cboJob.Items.Count > 0 && cboJob.SelectedIndex >= 0)
            {
                _ = LoadBackupSetsForSelectedJobAsync();
            }
        }

        private void PopulateJobs(string preselectedJobId)
        {
            cboJob.Items.Clear();
            int selectedIndex = 0;
            for (int i = 0; i < _jobs.Count; i++)
            {
                var j = _jobs[i];
                cboJob.Items.Add(j.Name);
                if (!string.IsNullOrEmpty(preselectedJobId) && j.Id == preselectedJobId)
                {
                    selectedIndex = i;
                }
            }

            if (_jobs.Count > 0)
            {
                rdoJob.Checked = true;
                cboJob.SelectedIndex = selectedIndex;
            }
            else
            {
                // If no jobs configured, fallback to Folder restore automatically
                rdoJob.Enabled = false;
                rdoJob.Text = Strings.IsVi ? "Từ Job đã lưu (Không có Job)" : "From Configured Job (None)";
                rdoFolder.Checked = true;
            }
        }

        private async System.Threading.Tasks.Task LoadBackupSetsForSelectedJobAsync()
        {
            if (cboJob.SelectedIndex < 0 || cboJob.SelectedIndex >= _jobs.Count)
                return;

            var job = _jobs[cboJob.SelectedIndex];
            var req = new ListBackupSetsRequest { JobId = job.Id };
            await QueryBackupSetsAsync(req, Strings.IsVi
                ? $"Đang đọc danh sách điểm sao lưu của Job '{job.Name}'..."
                : $"Loading restore points for job '{job.Name}'...");
        }

        private async System.Threading.Tasks.Task LoadBackupSetsForFolderAsync()
        {
            var folder = txtSourceFolder.Text.Trim();
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                MessageBox.Show(this,
                    Strings.IsVi ? "Vui lòng chọn một thư mục sao lưu hợp lệ." : "Please select a valid backup folder.",
                    Strings.IsVi ? "Thông báo" : "Notice",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var dest = new DestinationConfig
            {
                Type = DestinationType.LocalOrUnc,
                LocalPath = folder
            };

            var req = new ListBackupSetsRequest
            {
                CustomDestination = dest,
                RemoteRoot = ""
            };

            await QueryBackupSetsAsync(req, Strings.IsVi
                ? $"Đang quét các điểm sao lưu trong thư mục: {folder}..."
                : $"Scanning backup sets in folder: {folder}...");
        }

        private async System.Threading.Tasks.Task LoadBackupSetsForRemoteAsync()
        {
            var host = txtRemoteHost.Text.Trim();
            if (string.IsNullOrWhiteSpace(host))
            {
                MessageBox.Show(this,
                    Strings.IsVi ? "Vui lòng nhập địa chỉ máy chủ từ xa." : "Please enter remote host address.",
                    Strings.IsVi ? "Thông báo" : "Notice",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtRemoteHost.Focus();
                return;
            }

            var isSftp = cboRemoteProto.SelectedIndex == 1;
            var dest = new DestinationConfig
            {
                Type = isSftp ? DestinationType.Sftp : DestinationType.Ftp,
                FtpHost = host,
                FtpPort = (int)numRemotePort.Value,
                FtpUsername = txtRemoteUser.Text.Trim(),
                FtpPasswordProtected = SecretProtector.Protect(txtRemotePass.Text),
                FtpRemotePath = string.IsNullOrWhiteSpace(txtRemotePath.Text) ? "/" : txtRemotePath.Text.Trim(),
                SftpHost = host,
                SftpPort = (int)numRemotePort.Value,
                SftpUsername = txtRemoteUser.Text.Trim(),
                SftpPasswordProtected = SecretProtector.Protect(txtRemotePass.Text),
                SftpRemotePath = string.IsNullOrWhiteSpace(txtRemotePath.Text) ? "/" : txtRemotePath.Text.Trim()
            };

            var req = new ListBackupSetsRequest
            {
                CustomDestination = dest,
                RemoteRoot = isSftp ? dest.SftpRemotePath : dest.FtpRemotePath
            };

            await QueryBackupSetsAsync(req, Strings.IsVi
                ? $"Đang kết nối máy chủ {(isSftp ? "SFTP" : "FTP")} và quét điểm sao lưu..."
                : $"Connecting to {(isSftp ? "SFTP" : "FTP")} server and scanning backup sets...");
        }

        private void PopulateBackupSetsListView()
        {
            lvBackupSets.Items.Clear();
            foreach (var s in _loadedSets)
            {
                var item = new ListViewItem(s.CreatedUtc.ToString("yyyy-MM-dd HH:mm:ss"));
                item.SubItems.Add(s.BackupType == BackupType.Full ? (Strings.IsVi ? "Toàn phần" : "Full") : (Strings.IsVi ? "Gia tăng" : "Incremental"));
                item.SubItems.Add(s.BackupSetId);
                item.SubItems.Add(s.FileCount.ToString("N0"));
                item.SubItems.Add(FormatBytes(s.TotalSizeBytes));
                item.SubItems.Add((s.IsCompressed ? "ZIP" : "Raw") + (s.IsEncrypted ? " [AES-256]" : ""));
                item.Tag = s;
                lvBackupSets.Items.Add(item);
            }

            lblStatus.Text = Strings.IsVi
                ? $"Đã tìm thấy {_loadedSets.Count} điểm phục hồi khả dụng (Full & Incremental)."
                : $"Found {_loadedSets.Count} available restore points (Full & Incremental).";

            if (lvBackupSets.Items.Count > 0)
            {
                lvBackupSets.Items[0].Selected = true;
                lvBackupSets.Items[0].Focused = true;
                lvBackupSets.Focus();
                btnRestore.Enabled = true;
            }
            else
            {
                btnRestore.Enabled = false;
            }
        }

        private async System.Threading.Tasks.Task QueryBackupSetsAsync(ListBackupSetsRequest req, string statusText)
        {
            lvBackupSets.Items.Clear();
            _loadedSets.Clear();
            lblStatus.Text = statusText;
            btnRestore.Enabled = false;

            bool fetchedViaIpc = false;
            if (_pipeClient != null)
            {
                try
                {
                    var resp = await _pipeClient.SendAsync(new IpcMessage
                    {
                        Action = IpcActions.ListBackupSets,
                        PayloadJson = JsonConvert.SerializeObject(req)
                    }, 5000);

                    if (resp != null && resp.Success)
                    {
                        _loadedSets = JsonConvert.DeserializeObject<List<BackupSetManifest>>(resp.PayloadJson) ?? new List<BackupSetManifest>();
                        fetchedViaIpc = true;
                    }
                }
                catch
                {
                    // IPC unavailable or timed out, will fallback to direct engine below
                }
            }

            if (!fetchedViaIpc)
            {
                try
                {
                    var restoreEngine = new RestoreEngine(_configStore);
                    if (req.CustomDestination != null)
                    {
                        _loadedSets = await restoreEngine.ListAvailableBackupSetsFromDestinationAsync(
                            req.CustomDestination, req.RemoteRoot ?? "");
                    }
                    else if (!string.IsNullOrEmpty(req.JobId))
                    {
                        var job = _jobs.FirstOrDefault(j => j.Id == req.JobId);
                        if (job != null)
                        {
                            _loadedSets = await restoreEngine.ListAvailableBackupSetsAsync(job);
                        }
                    }
                }
                catch (Exception ex)
                {
                    lblStatus.Text = (Strings.IsVi ? "Lỗi đọc điểm phục hồi: " : "Failed to load restore points: ") + ex.Message;
                    return;
                }
            }

            PopulateBackupSetsListView();
        }

        private async System.Threading.Tasks.Task ExecuteRestoreAsync()
        {
            if (lvBackupSets.SelectedItems.Count == 0)
            {
                MessageBox.Show(this,
                    Strings.IsVi ? "Vui lòng chọn một điểm phục hồi cần khôi phục." : "Please select a restore point to restore.",
                    Strings.IsVi ? "Thông báo" : "Notice",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtTargetFolder.Text))
            {
                MessageBox.Show(this,
                    Strings.IsVi ? "Vui lòng chỉ định thư mục đích tiếp nhận dữ liệu phục hồi." : "Please specify a target directory for restoration.",
                    Strings.IsVi ? "Thông báo" : "Notice",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtTargetFolder.Focus();
                return;
            }

            var selectedSet = (BackupSetManifest)lvBackupSets.SelectedItems[0].Tag;

            var selectiveSubFolder = (chkSelectiveFolder.Checked && !string.IsNullOrWhiteSpace(txtSelectiveFolder.Text))
                ? txtSelectiveFolder.Text.Trim()
                : string.Empty;

            var subFolderDesc = string.IsNullOrEmpty(selectiveSubFolder)
                ? (Strings.IsVi ? "Toàn bộ dữ liệu" : "All data")
                : (Strings.IsVi ? $"Chỉ thư mục con '{selectiveSubFolder}'" : $"Subfolder '{selectiveSubFolder}' only");

            var confirmMsg = Strings.IsVi
                ? $"Chuẩn bị tiến hành phục hồi dữ liệu:\n\n• Điểm phục hồi: {selectedSet.BackupSetId}\n• Phạm vi: {subFolderDesc}\n• Thư mục đích: {txtTargetFolder.Text}\n\nHệ thống sẽ tự động ghép chuỗi Full + Incremental tương ứng.\nBạn có muốn tiếp tục không?"
                : $"Ready to restore data:\n\n• Restore Point: {selectedSet.BackupSetId}\n• Scope: {subFolderDesc}\n• Target: {txtTargetFolder.Text}\n\nSystem will automatically reconstruct the Full + Incremental chain.\nDo you want to proceed?";

            if (MessageBox.Show(this, confirmMsg, Strings.IsVi ? "Xác nhận phục hồi dữ liệu" : "Confirm Data Restoration", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            btnRestore.Enabled = false;
            btnCancel.Enabled = false;
            progressBar.Visible = true;
            progressBar.MarqueeAnimationSpeed = 30;
            lblStatus.Text = Strings.IsVi
                ? "Đang tiến hành phục hồi dữ liệu (tải và tái tạo chuỗi Full + Incremental)..."
                : "Restoring data (downloading and reconstructing Full + Incremental backup chain)...";

            try
            {
                var req = new RestoreRequest
                {
                    BackupSetId = selectedSet.BackupSetId,
                    TargetDirectory = txtTargetFolder.Text.Trim(),
                    OverwriteExisting = chkOverwrite.Checked,
                    ZipPassword = txtPassword.Text,
                    SelectedSubFolder = selectiveSubFolder
                };

                if (rdoJob.Checked && cboJob.SelectedIndex >= 0)
                {
                    req.JobId = _jobs[cboJob.SelectedIndex].Id;
                }
                else if (rdoFolder.Checked)
                {
                    req.CustomDestination = new DestinationConfig
                    {
                        Type = DestinationType.LocalOrUnc,
                        LocalPath = txtSourceFolder.Text.Trim()
                    };
                    req.RemoteRoot = "";
                }
                else if (rdoRemote.Checked)
                {
                    var isSftp = cboRemoteProto.SelectedIndex == 1;
                    req.CustomDestination = new DestinationConfig
                    {
                        Type = isSftp ? DestinationType.Sftp : DestinationType.Ftp,
                        FtpHost = txtRemoteHost.Text.Trim(),
                        FtpPort = (int)numRemotePort.Value,
                        FtpUsername = txtRemoteUser.Text.Trim(),
                        FtpPasswordProtected = SecretProtector.Protect(txtRemotePass.Text),
                        FtpRemotePath = string.IsNullOrWhiteSpace(txtRemotePath.Text) ? "/" : txtRemotePath.Text.Trim(),
                        SftpHost = txtRemoteHost.Text.Trim(),
                        SftpPort = (int)numRemotePort.Value,
                        SftpUsername = txtRemoteUser.Text.Trim(),
                        SftpPasswordProtected = SecretProtector.Protect(txtRemotePass.Text),
                        SftpRemotePath = string.IsNullOrWhiteSpace(txtRemotePath.Text) ? "/" : txtRemotePath.Text.Trim()
                    };
                    req.RemoteRoot = isSftp ? req.CustomDestination.SftpRemotePath : req.CustomDestination.FtpRemotePath;
                }

                RestoreResult result = null;

                // 1. Try IPC via Service if available
                if (_pipeClient != null)
                {
                    try
                    {
                        var resp = await _pipeClient.SendAsync(new IpcMessage
                        {
                            Action = IpcActions.Restore,
                            PayloadJson = JsonConvert.SerializeObject(req)
                        }, 300000);

                        if (resp != null && resp.Success)
                        {
                            result = JsonConvert.DeserializeObject<RestoreResult>(resp.PayloadJson);
                        }
                    }
                    catch
                    {
                        // IPC disconnected/failed, fallback to in-process restore
                    }
                }

                // 2. Direct in-process restore fallback
                if (result == null)
                {
                    var restoreEngine = new RestoreEngine(_configStore);
                    var restoreOpts = new RestoreOptions
                    {
                        TargetDirectory = req.TargetDirectory,
                        OverwriteExisting = req.OverwriteExisting,
                        ZipPassword = req.ZipPassword,
                        SelectedSubFolder = req.SelectedSubFolder
                    };

                    if (req.CustomDestination != null)
                    {
                        result = await restoreEngine.RestoreFromDestinationToPointInTimeAsync(
                            req.CustomDestination,
                            req.RemoteRoot ?? "",
                            req.BackupSetId,
                            restoreOpts,
                            req.ZipPassword);
                    }
                    else
                    {
                        var job = _jobs.FirstOrDefault(j => j.Id == req.JobId);
                        if (job != null)
                        {
                            result = await restoreEngine.RestoreToPointInTimeAsync(
                                job,
                                req.BackupSetId,
                                restoreOpts);
                        }
                        else
                        {
                            result = new RestoreResult
                            {
                                Success = false,
                                Message = Strings.IsVi ? "Không tìm thấy cấu hình Job tương ứng." : "Configured Job not found."
                            };
                        }
                    }
                }

                if (result != null && result.Success)
                {
                    MessageBox.Show(this, result.Message, Strings.IsVi ? "Phục hồi dữ liệu thành công" : "Restoration Succeeded", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Close();
                }
                else
                {
                    var errMsg = result?.Message;
                    if (string.IsNullOrWhiteSpace(errMsg) && result?.Errors?.Count > 0)
                        errMsg = string.Join("; ", result.Errors);

                    MessageBox.Show(this,
                        (Strings.IsVi ? "Phục hồi dữ liệu thất bại: " : "Restoration failed: ") + errMsg,
                        Strings.IsVi ? "Lỗi thực thi" : "Execution Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    (Strings.IsVi ? "Lỗi: " : "Error: ") + ex.Message,
                    Strings.IsVi ? "Lỗi" : "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnRestore.Enabled = lvBackupSets.SelectedItems.Count > 0;
                btnCancel.Enabled = true;
                progressBar.Visible = false;
                lblStatus.Text = "";
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
    }
}
