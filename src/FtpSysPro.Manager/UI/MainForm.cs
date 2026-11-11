using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Windows.Forms;
using FtpSysPro.Core.Config;
using FtpSysPro.Core.Ipc;
using FtpSysPro.Core.Models;
using FtpSysPro.Core.Resources;

namespace FtpSysPro.Manager.UI
{
    public class MainForm : Form
    {
        private readonly ConfigStore _configStore;
        private readonly PipeClient _pipeClient;
        private AppConfig _currentConfig;

        private ToolStrip toolStrip;
        private ToolStripButton btnAdd;
        private ToolStripButton btnEdit;
        private ToolStripButton btnClone;
        private ToolStripButton btnDelete;
        private ToolStripSeparator sep1;
        private ToolStripButton btnRunFull;
        private ToolStripButton btnRunIncr;
        private ToolStripButton btnStop;
        private ToolStripSeparator sep2;
        private ToolStripButton btnRestore;
        private ToolStripButton btnSettings;
        private ToolStripButton btnRefresh;

        private DataGridView gridJobs;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel lblServiceStatus;
        private ToolStripStatusLabel lblServiceAction;
        private ToolStripProgressBar progressBar;

        // Side Detail Panel
        private SplitContainer splitContainer;
        private Label lblDetailName;
        private Label lblDetailSourceSize;
        private Label lblDetailTransfer;
        private Label lblDetailDest;
        private TextBox txtDetailSources;
        private Label lblDetailLast;
        private Label lblDetailNext;

        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;

        private Timer refreshTimer;
        private bool _isClosingForReal;
        private bool _wasRunning;

        public MainForm()
        {
            _configStore = new ConfigStore();
            _pipeClient = new PipeClient();

            InitializeComponents();
            ApplyLanguage();
        }

        private void InitializeComponents()
        {
            Text = Strings.AppName;
            Size = new Size(1150, 650);
            MinimumSize = new Size(950, 520);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);
            Icon = SystemIcons.Shield; // Standard professional icon

            // ToolStrip
            toolStrip = new ToolStrip { ImageScalingSize = new Size(20, 20), RenderMode = ToolStripRenderMode.System };

            btnAdd = new ToolStripButton(Strings.Add) { DisplayStyle = ToolStripItemDisplayStyle.Text, Font = new Font(Font, FontStyle.Bold) };
            btnEdit = new ToolStripButton(Strings.Edit) { DisplayStyle = ToolStripItemDisplayStyle.Text };
            btnClone = new ToolStripButton(Strings.Clone) { DisplayStyle = ToolStripItemDisplayStyle.Text };
            btnDelete = new ToolStripButton(Strings.Delete) { DisplayStyle = ToolStripItemDisplayStyle.Text };

            sep1 = new ToolStripSeparator();

            btnRunFull = new ToolStripButton(Strings.RunFull) { DisplayStyle = ToolStripItemDisplayStyle.Text };
            btnRunIncr = new ToolStripButton(Strings.RunIncr) { DisplayStyle = ToolStripItemDisplayStyle.Text };
            btnStop = new ToolStripButton(Strings.Stop) { DisplayStyle = ToolStripItemDisplayStyle.Text, Enabled = false };

            sep2 = new ToolStripSeparator();

            btnRestore = new ToolStripButton(Strings.Restore) { DisplayStyle = ToolStripItemDisplayStyle.Text };
            btnSettings = new ToolStripButton(Strings.Settings) { DisplayStyle = ToolStripItemDisplayStyle.Text };
            btnRefresh = new ToolStripButton(Strings.Refresh) { DisplayStyle = ToolStripItemDisplayStyle.Text };

            toolStrip.Items.AddRange(new ToolStripItem[] {
                btnAdd, btnEdit, btnClone, btnDelete,
                sep1,
                btnRunFull, btnRunIncr, btnStop,
                sep2,
                btnRestore, btnSettings, btnRefresh
            });

            // Grid
            gridJobs = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = Color.White,
                RowHeadersVisible = false,
                BorderStyle = BorderStyle.None
            };

            gridJobs.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Strings.ColJobName, DataPropertyName = "Name", Width = 160 });
            gridJobs.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = Strings.ColEnabled, DataPropertyName = "Enabled", Width = 50 });
            gridJobs.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Strings.ColSourceSize, DataPropertyName = "SourceSizeDisplay", Width = 150 });
            gridJobs.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Strings.ColTransferMode, DataPropertyName = "CompressionDisplay", Width = 115 });
            gridJobs.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Strings.ColDestination, DataPropertyName = "DestinationDisplay", Width = 150 });
            gridJobs.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Strings.ColLastRun, DataPropertyName = "LastRunDisplay", Width = 125 });
            gridJobs.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Strings.ColStatus, DataPropertyName = "StatusDisplay", Width = 95 });
            gridJobs.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Strings.ColNextRun, DataPropertyName = "NextRunDisplay", Width = 125 });
            gridJobs.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Strings.ColSize, DataPropertyName = "SizeDisplay", Width = 110 });

            gridJobs.CellDoubleClick += (s, e) => EditSelectedJob();
            gridJobs.SelectionChanged += (s, e) => UpdateSelectedJobDetail();

            // Right side Job Detail Card
            var grpDetail = new GroupBox
            {
                Text = "Chi tiết tác vụ / Job Details",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Padding = new Padding(12),
                BackColor = Color.FromArgb(250, 251, 252)
            };

            var pnlDetailContent = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true
            };

            lblDetailName = new Label
            {
                Text = "(Chưa chọn job)",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 30, 60),
                Location = new Point(5, 5),
                AutoSize = true
            };

            var lblHeaderSourceSize = new Label
            {
                Text = "TỔNG DUNG LƯỢNG CẦN BACKUP:",
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.DimGray,
                Location = new Point(5, 38),
                AutoSize = true
            };

            lblDetailSourceSize = new Label
            {
                Text = "-",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.DarkBlue,
                Location = new Point(5, 56),
                AutoSize = true
            };

            var lblHeaderTransfer = new Label
            {
                Text = "PHƯƠNG THỨC TRUYỀN DỮ LIỆU:",
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.DimGray,
                Location = new Point(5, 90),
                AutoSize = true
            };

            lblDetailTransfer = new Label
            {
                Text = "-",
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.Black,
                Location = new Point(5, 108),
                AutoSize = true
            };

            var lblHeaderDest = new Label
            {
                Text = "NƠI LƯU TRỮ (ĐÍCH ĐẾN):",
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.DimGray,
                Location = new Point(5, 138),
                AutoSize = true
            };

            lblDetailDest = new Label
            {
                Text = "-",
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.Black,
                Location = new Point(5, 156),
                AutoSize = true
            };

            var lblHeaderSources = new Label
            {
                Text = "THƯ MỤC NGUỒN:",
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.DimGray,
                Location = new Point(5, 186),
                AutoSize = true
            };

            txtDetailSources = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                BackColor = Color.White,
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(5, 206),
                Size = new Size(260, 90),
                Font = new Font("Consolas", 8.25F)
            };

            var lblHeaderLast = new Label
            {
                Text = "LẦN CHẠY GẦN NHẤT:",
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.DimGray,
                Location = new Point(5, 308),
                AutoSize = true
            };

            lblDetailLast = new Label
            {
                Text = "-",
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.Black,
                Location = new Point(5, 326),
                AutoSize = true
            };

            var lblHeaderNext = new Label
            {
                Text = "LỊCH CHẠY KẾ TIẾP:",
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.DimGray,
                Location = new Point(5, 356),
                AutoSize = true
            };

            lblDetailNext = new Label
            {
                Text = "-",
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.Black,
                Location = new Point(5, 374),
                AutoSize = true
            };

            pnlDetailContent.Controls.AddRange(new Control[] {
                lblDetailName,
                lblHeaderSourceSize, lblDetailSourceSize,
                lblHeaderTransfer, lblDetailTransfer,
                lblHeaderDest, lblDetailDest,
                lblHeaderSources, txtDetailSources,
                lblHeaderLast, lblDetailLast,
                lblHeaderNext, lblDetailNext
            });
            grpDetail.Controls.Add(pnlDetailContent);

            // SplitContainer
            splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                Panel1MinSize = 350,
                Panel2MinSize = 270,
                FixedPanel = FixedPanel.Panel2
            };
            splitContainer.Panel1.Controls.Add(gridJobs);
            splitContainer.Panel2.Controls.Add(grpDetail);

            // StatusStrip
            statusStrip = new StatusStrip();
            lblServiceStatus = new ToolStripStatusLabel { Text = Strings.ServiceStatusDisconnected };
            lblServiceAction = new ToolStripStatusLabel { IsLink = true, Text = "" };
            lblServiceAction.Click += async (s, e) => await StartOrRestartServiceAsync();

            progressBar = new ToolStripProgressBar { Visible = false, Style = ProgressBarStyle.Marquee };

            statusStrip.Items.AddRange(new ToolStripItem[] { lblServiceStatus, lblServiceAction, progressBar });

            // Tray
            trayMenu = new ContextMenuStrip();
            var itemOpen = new ToolStripMenuItem(Strings.TrayOpen, null, (s, e) => ShowMainWindow());
            var itemSep = new ToolStripSeparator();
            var itemExit = new ToolStripMenuItem(Strings.TrayExit, null, (s, e) => ExitApplication());
            trayMenu.Items.AddRange(new ToolStripItem[] { itemOpen, itemSep, itemExit });

            trayIcon = new NotifyIcon
            {
                Text = Strings.AppName,
                Icon = SystemIcons.Shield,
                ContextMenuStrip = trayMenu,
                Visible = true
            };
            trayIcon.DoubleClick += (s, e) => ShowMainWindow();

            // Layout
            Controls.Add(splitContainer);
            Controls.Add(toolStrip);
            Controls.Add(statusStrip);

            // Wire events
            btnAdd.Click += (s, e) => AddNewJob();
            btnEdit.Click += (s, e) => EditSelectedJob();
            btnClone.Click += (s, e) => CloneSelectedJob();
            btnDelete.Click += (s, e) => DeleteSelectedJob();
            btnRunFull.Click += async (s, e) => await RunSelectedJobAsync(BackupType.Full);
            btnRunIncr.Click += async (s, e) => await RunSelectedJobAsync(BackupType.Incremental);
            btnStop.Click += async (s, e) => await StopSelectedJobAsync();
            btnRestore.Click += (s, e) => OpenRestoreWizard();
            btnSettings.Click += (s, e) => OpenSettings();
            btnRefresh.Click += async (s, e) => await RefreshAllAsync();

            // Timer
            refreshTimer = new Timer { Interval = 3000 };
            refreshTimer.Tick += async (s, e) => await PeriodicCheckAsync();
            refreshTimer.Start();

            FormClosing += OnMainFormClosing;
            Load += async (s, e) =>
            {
                AdjustSplitterDistance();
                await RefreshAllAsync();
            };
            Resize += (s, e) => AdjustSplitterDistance();
        }

        private void ApplyLanguage()
        {
            var cfg = _configStore.LoadConfig();
            Strings.SetLanguage(cfg?.Settings?.Language ?? "vi");
        }

        private BackupJob GetSelectedJob()
        {
            if (gridJobs.SelectedRows.Count == 0) return null;
            var viewItem = gridJobs.SelectedRows[0].DataBoundItem as JobViewModel;
            return viewItem?.Job;
        }

        private void AddNewJob()
        {
            var newJob = new BackupJob { Name = "Tác vụ sao lưu " + (_currentConfig.Jobs.Count + 1) };
            using (var dlg = new JobEditDialog(newJob, true))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _currentConfig.Jobs.Add(dlg.ResultJob);
                    SaveConfigToService();
                    RefreshJobGrid();
                }
            }
        }

        private void EditSelectedJob()
        {
            var job = GetSelectedJob();
            if (job == null) return;

            using (var dlg = new JobEditDialog(job, false))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    var idx = _currentConfig.Jobs.FindIndex(j => j.Id == job.Id);
                    if (idx >= 0)
                    {
                        _currentConfig.Jobs[idx] = dlg.ResultJob;
                        SaveConfigToService();
                        RefreshJobGrid();
                    }
                }
            }
        }

        private void CloneSelectedJob()
        {
            var job = GetSelectedJob();
            if (job == null) return;

            var copy = job.Clone();
            _currentConfig.Jobs.Add(copy);
            SaveConfigToService();
            RefreshJobGrid();
        }

        private void DeleteSelectedJob()
        {
            var job = GetSelectedJob();
            if (job == null) return;

            if (MessageBox.Show(this, $"{Strings.ConfirmDeleteJob}\n({job.Name})", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _currentConfig.Jobs.RemoveAll(j => j.Id == job.Id);
                SaveConfigToService();
                RefreshJobGrid();
            }
        }

        private async Task RunSelectedJobAsync(BackupType type)
        {
            var job = GetSelectedJob();
            if (job == null) return;

            btnRunFull.Enabled = false;
            btnRunIncr.Enabled = false;

            try
            {
                var req = new RunJobRequest { JobId = job.Id, BackupType = type };
                var resp = await _pipeClient.SendAsync(new IpcMessage
                {
                    Action = IpcActions.RunJob,
                    PayloadJson = Newtonsoft.Json.JsonConvert.SerializeObject(req)
                });

                if (resp != null && resp.Success)
                {
                    btnStop.Enabled = true;
                    await RefreshAllAsync();
                }
                else
                {
                    MessageBox.Show(this, "Không thể khởi động tác vụ: " + resp?.ErrorMessage, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            finally
            {
                btnRunFull.Enabled = true;
                btnRunIncr.Enabled = true;
            }
        }

        private async Task StopSelectedJobAsync()
        {
            var job = GetSelectedJob();
            if (job == null) return;

            if (MessageBox.Show(this, Strings.ConfirmStopJob, "Dừng tác vụ", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                await _pipeClient.SendAsync(new IpcMessage
                {
                    Action = IpcActions.StopJob,
                    PayloadJson = job.Id
                });
                await RefreshAllAsync();
            }
        }

        private void OpenRestoreWizard()
        {
            var selectedJob = GetSelectedJob();
            using (var dlg = new RestoreWizardDialog(_currentConfig.Jobs, _pipeClient, selectedJob?.Id))
            {
                dlg.ShowDialog(this);
            }
        }

        private void OpenSettings()
        {
            using (var dlg = new SettingsDialog(_currentConfig.Settings, _pipeClient))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _currentConfig.Settings = dlg.ResultSettings;
                    SaveConfigToService();
                }
            }
        }

        private async void SaveConfigToService()
        {
            _configStore.SaveConfig(_currentConfig);
            try
            {
                await _pipeClient.SendAsync(new IpcMessage
                {
                    Action = IpcActions.SaveConfig,
                    PayloadJson = Newtonsoft.Json.JsonConvert.SerializeObject(_currentConfig)
                });
            }
            catch { }
        }

        private async Task RefreshAllAsync()
        {
            // Check service availability
            var isServiceUp = await _pipeClient.IsServiceAvailableAsync();
            if (isServiceUp)
            {
                lblServiceStatus.Text = Strings.ServiceStatusConnected;
                lblServiceStatus.ForeColor = Color.DarkGreen;
                lblServiceAction.Text = "";

                // Get config from service
                try
                {
                    var resp = await _pipeClient.SendAsync(new IpcMessage { Action = IpcActions.GetConfig });
                    if (resp != null && resp.Success)
                    {
                        _currentConfig = Newtonsoft.Json.JsonConvert.DeserializeObject<AppConfig>(resp.PayloadJson);
                    }
                }
                catch { }
            }
            else
            {
                lblServiceStatus.Text = Strings.ServiceStatusDisconnected;
                lblServiceStatus.ForeColor = Color.Red;
                lblServiceAction.Text = Strings.ServiceStartPrompt;

                // Load config locally
                _currentConfig = _configStore.LoadConfig();
            }

            if (_currentConfig == null)
                _currentConfig = new AppConfig();

            RefreshJobGrid();
        }

        private async Task PeriodicCheckAsync()
        {
            try
            {
                var isServiceUp = await _pipeClient.IsServiceAvailableAsync(500);
                if (isServiceUp)
                {
                    lblServiceStatus.Text = Strings.ServiceStatusConnected;
                    lblServiceStatus.ForeColor = Color.DarkGreen;
                    lblServiceAction.Text = "";

                    // Check running jobs
                    var resp = await _pipeClient.SendAsync(new IpcMessage { Action = IpcActions.GetRunningJobs }, 1000);
                    if (resp != null && resp.Success)
                    {
                        var running = Newtonsoft.Json.JsonConvert.DeserializeObject<List<JobProgressInfo>>(resp.PayloadJson);
                        if (running != null && running.Count > 0)
                        {
                            progressBar.Visible = true;
                            btnStop.Enabled = true;
                            _wasRunning = true;
                        }
                        else
                        {
                            progressBar.Visible = false;
                            btnStop.Enabled = false;

                            if (_wasRunning)
                            {
                                _wasRunning = false;
                                await RefreshAllAsync();
                                try
                                {
                                    trayIcon.ShowBalloonTip(3000, Strings.AppName, "Tiến trình sao lưu đã hoàn tất.", ToolTipIcon.Info);
                                }
                                catch { }
                            }
                        }
                    }
                }
                else
                {
                    lblServiceStatus.Text = Strings.ServiceStatusDisconnected;
                    lblServiceStatus.ForeColor = Color.Red;
                    lblServiceAction.Text = Strings.ServiceStartPrompt;
                    progressBar.Visible = false;
                }
            }
            catch { }
        }

        private async Task StartOrRestartServiceAsync()
        {
            lblServiceAction.Text = "Đang bật service...";
            await Task.Run(() =>
            {
                try
                {
                    using (var sc = new ServiceController("FtpSysProService"))
                    {
                        if (sc.Status != ServiceControllerStatus.Running)
                        {
                            sc.Start();
                            sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Không thể khởi động Windows Service: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            });
            await RefreshAllAsync();
        }

        private void RefreshJobGrid()
        {
            var list = _currentConfig.Jobs.Select(j => new JobViewModel(j)).ToList();
            gridJobs.DataSource = list;
            UpdateSelectedJobDetail();
        }

        private void UpdateSelectedJobDetail()
        {
            if (gridJobs.SelectedRows.Count > 0 && gridJobs.SelectedRows[0].DataBoundItem is JobViewModel vm)
            {
                var job = vm.Job;
                lblDetailName.Text = job.Name;
                lblDetailSourceSize.Text = job.SourceSizeBytes > 0
                    ? $"{JobViewModel.FormatBytes(job.SourceSizeBytes)} ({job.SourceFileCount:N0} files)"
                    : (job.SourceDirectories != null && job.SourceDirectories.Count > 0 ? "(Chưa quét hoặc 0 B)" : "-");
                lblDetailTransfer.Text = job.Compression.Enabled
                    ? $"Nén file .ZIP (Mức: {job.Compression.Level})" + (job.Compression.EncryptWithPassword ? " + AES-256" : "")
                    : "Đẩy cả thư mục (Không nén)";
                lblDetailDest.Text = vm.DestinationDisplay;
                txtDetailSources.Lines = job.SourceDirectories != null ? job.SourceDirectories.ToArray() : new string[0];
                lblDetailLast.Text = job.LastRunUtc.HasValue
                    ? $"{job.LastRunUtc.Value.ToLocalTime():yyyy-MM-dd HH:mm} ({vm.StatusDisplay})"
                    : "Chưa chạy";
                lblDetailNext.Text = vm.NextRunDisplay;
            }
            else
            {
                lblDetailName.Text = "(Chưa chọn job)";
                lblDetailSourceSize.Text = "-";
                lblDetailTransfer.Text = "-";
                lblDetailDest.Text = "-";
                txtDetailSources.Text = "";
                lblDetailLast.Text = "-";
                lblDetailNext.Text = "-";
            }
        }

        private void AdjustSplitterDistance()
        {
            try
            {
                if (splitContainer != null && splitContainer.Width > (splitContainer.Panel1MinSize + splitContainer.Panel2MinSize))
                {
                    int desiredDetailWidth = 320;
                    int maxDistance = splitContainer.Width - splitContainer.Panel2MinSize;
                    int minDistance = splitContainer.Panel1MinSize;
                    int calculated = splitContainer.Width - desiredDetailWidth;
                    splitContainer.SplitterDistance = Math.Max(minDistance, Math.Min(calculated, maxDistance));
                }
            }
            catch { }
        }

        public void ShowMainWindow()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(ShowMainWindow));
                return;
            }

            Show();
            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Normal;
            }
            ShowInTaskbar = true;
            BringToFront();
            Activate();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == (int)Program.WM_SHOW_FTPSYSPRO)
            {
                ShowMainWindow();
                return;
            }
            base.WndProc(ref m);
        }

        private void ExitApplication()
        {
            _isClosingForReal = true;
            trayIcon.Visible = false;
            Close();
            Application.Exit();
        }

        private void OnMainFormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && !_isClosingForReal && _currentConfig?.Settings?.MinimizeToTrayOnClose == true)
            {
                e.Cancel = true;
                Hide();
                try
                {
                    trayIcon.ShowBalloonTip(2000, Strings.AppName, "Ứng dụng đang chạy ngầm trong khay hệ thống.", ToolTipIcon.Info);
                }
                catch { }
            }
        }
    }

    public class JobViewModel
    {
        public BackupJob Job { get; }

        public JobViewModel(BackupJob job)
        {
            Job = job;
        }

        public string Name => Job.Name;
        public bool Enabled => Job.Enabled;

        public string SourceSizeDisplay => Job.SourceSizeBytes > 0
            ? $"{FormatBytes(Job.SourceSizeBytes)} ({Job.SourceFileCount:N0} files)"
            : (Job.SourceDirectories != null && Job.SourceDirectories.Count > 0 ? "Chưa quét" : "-");

        public string CompressionDisplay => Job.Compression.Enabled
            ? "Nén ZIP" + (Job.Compression.EncryptWithPassword ? " (AES)" : "")
            : "Đẩy cả thư mục";

        public string DestinationDisplay
        {
            get
            {
                switch (Job.Destination.Type)
                {
                    case DestinationType.Ftp: return $"FTP: {Job.Destination.FtpHost}";
                    case DestinationType.Sftp: return $"SFTP: {Job.Destination.SftpHost}";
                    default: return string.IsNullOrEmpty(Job.Destination.LocalPath) ? "(Local)" : Job.Destination.LocalPath;
                }
            }
        }

        public string LastRunDisplay => Job.LastRunUtc.HasValue ? Job.LastRunUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "-";

        public string StatusDisplay
        {
            get
            {
                switch (Job.LastStatus)
                {
                    case JobExecutionStatus.Success: return Strings.StatusSuccess;
                    case JobExecutionStatus.Warning: return Strings.StatusWarning;
                    case JobExecutionStatus.Failed: return Strings.StatusFailed;
                    case JobExecutionStatus.Running: return Strings.StatusRunning;
                    case JobExecutionStatus.Cancelled: return Strings.StatusCancelled;
                    default: return Strings.StatusIdle;
                }
            }
        }

        public string NextRunDisplay => Job.NextRunUtc.HasValue ? Job.NextRunUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "-";

        public string SizeDisplay => Job.LastSizeBytes > 0 ? FormatBytes(Job.LastSizeBytes) : "-";

        public static string FormatBytes(long bytes)
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
