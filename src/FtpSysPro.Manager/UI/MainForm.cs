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
        private ToolStripButton btnRun;
        private ToolStripButton btnStop;
        private ToolStripSeparator sep2;
        private ToolStripButton btnRestore;
        private ToolStripButton btnLogs;
        private ToolStripButton btnSettings;
        private ToolStripButton btnRefresh;

        private DataGridView gridJobs;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel lblServiceStatus;
        private ToolStripStatusLabel lblServiceAction;
        private ToolStripProgressBar progressBar;

        // Side Detail Panel
        private SplitContainer splitContainer;
        private Panel pnlDetailCard;
        private Label lblCardTitle;
        private TableLayoutPanel tblDetail;
        private Label lblDetailName;
        private Label lblDetailBadge;
        private Label lblDetailDesc;
        private Label lblHeaderSources;
        private Label lblDetailSources;
        private Label lblHeaderSourceSize;
        private Label lblDetailSourceSize;
        private Label lblHeaderDest;
        private Label lblDetailDest;
        private Label lblHeaderTransfer;
        private Label lblDetailTransfer;
        private Label lblHeaderLast;
        private Label lblDetailLast;
        private Label lblDetailLastResult;
        private Label lblHeaderNext;
        private Label lblDetailNext;

        // Context menus
        private ToolStripMenuItem itemRun;
        private ToolStripMenuItem itemStop;
        private ToolStripMenuItem itemEdit;
        private ToolStripMenuItem itemClone;
        private ToolStripMenuItem itemDelete;
        private ToolStripMenuItem itemLogs;
        private ToolStripMenuItem itemTrayOpen;
        private ToolStripMenuItem itemTrayExit;

        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;

        private Timer refreshTimer;
        private bool _isClosingForReal;
        private bool _wasRunning;

        public MainForm()
        {
            _configStore = new ConfigStore();
            _pipeClient = new PipeClient();

            ApplyLanguage();
            InitializeComponents();
            UpdateUiLanguage();
        }

        private void InitializeComponents()
        {
            Text = $"{Strings.AppName} v{Strings.AppVersion}";
            Size = new Size(1150, 650);
            MinimumSize = new Size(950, 520);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);
            Icon = SystemIcons.Shield; // Standard professional icon

            // ToolStrip
            toolStrip = new ToolStrip { ImageScalingSize = new Size(20, 20), RenderMode = ToolStripRenderMode.System };

            btnAdd = new ToolStripButton("➕ " + Strings.Add) { DisplayStyle = ToolStripItemDisplayStyle.Text, Font = new Font(Font, FontStyle.Bold) };
            btnEdit = new ToolStripButton("✏ " + Strings.Edit) { DisplayStyle = ToolStripItemDisplayStyle.Text };
            btnDelete = new ToolStripButton("🗑 " + Strings.Delete) { DisplayStyle = ToolStripItemDisplayStyle.Text };
            btnClone = new ToolStripButton("📋 " + Strings.Clone) { DisplayStyle = ToolStripItemDisplayStyle.Text };

            sep1 = new ToolStripSeparator();

            btnRun = new ToolStripButton("▶ " + Strings.Run) { DisplayStyle = ToolStripItemDisplayStyle.Text, Font = new Font(Font, FontStyle.Bold) };
            btnStop = new ToolStripButton("■ " + Strings.Stop) { DisplayStyle = ToolStripItemDisplayStyle.Text, Enabled = false };

            sep2 = new ToolStripSeparator();

            btnRestore = new ToolStripButton("🔄 " + Strings.Restore) { DisplayStyle = ToolStripItemDisplayStyle.Text };
            btnLogs = new ToolStripButton("📋 " + Strings.Logs) { DisplayStyle = ToolStripItemDisplayStyle.Text };
            btnSettings = new ToolStripButton("⚙ " + Strings.Settings) { DisplayStyle = ToolStripItemDisplayStyle.Text };
            btnRefresh = new ToolStripButton("↻ " + Strings.Refresh) { DisplayStyle = ToolStripItemDisplayStyle.Text };

            toolStrip.Items.AddRange(new ToolStripItem[] {
                btnAdd, btnEdit, btnDelete, btnClone,
                sep1,
                btnRun, btnStop,
                sep2,
                btnRestore, btnLogs, btnSettings, btnRefresh
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

            gridJobs.Columns.Add(new DataGridViewTextBoxColumn { Name = "colJobName", HeaderText = Strings.ColJobName, DataPropertyName = "Name", Width = 150 });
            gridJobs.Columns.Add(new DataGridViewCheckBoxColumn { Name = "colEnabled", HeaderText = Strings.ColEnabled, DataPropertyName = "Enabled", Width = 45 });
            gridJobs.Columns.Add(new DataGridViewTextBoxColumn { Name = "colBackupType", HeaderText = Strings.ColBackupType, DataPropertyName = "BackupTypeDisplay", Width = 85 });
            gridJobs.Columns.Add(new DataGridViewTextBoxColumn { Name = "colSource", HeaderText = Strings.ColSource, DataPropertyName = "SourceDisplay", Width = 230 });
            gridJobs.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDestination", HeaderText = Strings.ColDestination, DataPropertyName = "DestinationDisplay", Width = 210 });
            gridJobs.Columns.Add(new DataGridViewTextBoxColumn { Name = "colSourceSize", HeaderText = Strings.ColSourceSize, DataPropertyName = "SourceSizeDisplay", Width = 130 });
            gridJobs.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTransferMode", HeaderText = Strings.ColTransferMode, DataPropertyName = "CompressionDisplay", Width = 110 });
            gridJobs.Columns.Add(new DataGridViewTextBoxColumn { Name = "colLastRun", HeaderText = Strings.ColLastRun, DataPropertyName = "LastRunDisplay", Width = 120 });
            gridJobs.Columns.Add(new DataGridViewTextBoxColumn { Name = "colStatus", HeaderText = Strings.ColStatus, DataPropertyName = "StatusDisplay", Width = 90 });
            gridJobs.Columns.Add(new DataGridViewTextBoxColumn { Name = "colNextRun", HeaderText = Strings.ColNextRun, DataPropertyName = "NextRunDisplay", Width = 120 });
            gridJobs.Columns.Add(new DataGridViewTextBoxColumn { Name = "colSize", HeaderText = Strings.ColSize, DataPropertyName = "SizeDisplay", Width = 100 });

            gridJobs.CellDoubleClick += (s, e) => EditSelectedJob();
            gridJobs.SelectionChanged += (s, e) => UpdateSelectedJobDetail();

            var jobMenu = new ContextMenuStrip();
            itemRun = new ToolStripMenuItem("▶ " + Strings.Run, null, async (s, e) => await RunSelectedJobAsync());
            itemStop = new ToolStripMenuItem("■ " + Strings.Stop, null, async (s, e) => await StopSelectedJobAsync());
            itemEdit = new ToolStripMenuItem("✏ " + Strings.Edit, null, (s, e) => EditSelectedJob());
            itemClone = new ToolStripMenuItem("📋 " + Strings.Clone, null, (s, e) => CloneSelectedJob());
            itemDelete = new ToolStripMenuItem("🗑 " + Strings.Delete, null, (s, e) => DeleteSelectedJob());
            itemLogs = new ToolStripMenuItem("📋 " + Strings.ViewJobLog, null, (s, e) =>
            {
                var job = GetSelectedJob();
                OpenLogViewer(job?.Name);
            });
            jobMenu.Items.AddRange(new ToolStripItem[] {
                itemRun, itemStop, new ToolStripSeparator(),
                itemEdit, itemClone, itemDelete, new ToolStripSeparator(),
                itemLogs
            });
            gridJobs.ContextMenuStrip = jobMenu;

            gridJobs.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    var hit = gridJobs.HitTest(e.X, e.Y);
                    if (hit.RowIndex >= 0)
                    {
                        gridJobs.ClearSelection();
                        gridJobs.Rows[hit.RowIndex].Selected = true;
                        UpdateSelectedJobDetail();
                    }
                }
            };

            // Right side Job Detail Card (modern borderless design)
            pnlDetailCard = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(250, 251, 253)
            };

            var pnlDetailHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 36,
                BackColor = Color.FromArgb(242, 246, 251),
                Padding = new Padding(12, 0, 12, 0)
            };

            lblCardTitle = new Label
            {
                Text = "📋 " + Strings.DetailCardTitle,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(20, 45, 85),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            pnlDetailHeader.Controls.Add(lblCardTitle);

            var pnlHeaderDivider = new Panel
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = Color.FromArgb(220, 228, 238)
            };

            var pnlDetailScroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(14, 10, 14, 20),
                BackColor = Color.FromArgb(250, 251, 253)
            };

            tblDetail = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                Padding = new Padding(0)
            };
            tblDetail.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            // Controls initialization
            lblDetailName = new Label
            {
                Text = Strings.NoJobSelected,
                Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(12, 35, 75)
            };

            lblDetailBadge = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(30, 120, 50)
            };

            lblDetailDesc = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = Color.FromArgb(100, 110, 125),
                Visible = false
            };

            lblHeaderSources = new Label
            {
                Text = Strings.HeaderSources,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 110, 130)
            };

            lblDetailSources = new Label
            {
                Text = "-",
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(20, 30, 50)
            };

            lblHeaderSourceSize = new Label
            {
                Text = Strings.HeaderSourceSize,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 110, 130)
            };

            lblDetailSourceSize = new Label
            {
                Text = "-",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.DarkBlue
            };

            lblHeaderDest = new Label
            {
                Text = Strings.HeaderDest,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 110, 130)
            };

            lblDetailDest = new Label
            {
                Text = "-",
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(20, 30, 50)
            };

            lblHeaderTransfer = new Label
            {
                Text = Strings.HeaderTransfer,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 110, 130)
            };

            lblDetailTransfer = new Label
            {
                Text = "-",
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(20, 30, 50)
            };

            lblHeaderLast = new Label
            {
                Text = Strings.HeaderLast,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 110, 130)
            };

            lblDetailLast = new Label
            {
                Text = "-",
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(20, 30, 50)
            };

            lblDetailLastResult = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 8F),
                ForeColor = Color.DimGray,
                Visible = false
            };

            lblHeaderNext = new Label
            {
                Text = Strings.HeaderNext,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 110, 130)
            };

            lblDetailNext = new Label
            {
                Text = "-",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 90, 180)
            };

            // Add rows to TableLayoutPanel (simple, clean, matching job grid)
            AddDetailRow(lblDetailName, 2, 2);
            AddDetailRow(lblDetailBadge, 0, 4);
            AddDetailRow(lblDetailDesc, 0, 6);

            AddDetailRow(lblHeaderSources, 8, 2);
            AddDetailRow(lblDetailSources, 0, 6);

            AddDetailRow(lblHeaderSourceSize, 6, 2);
            AddDetailRow(lblDetailSourceSize, 0, 6);

            AddDetailRow(lblHeaderDest, 6, 2);
            AddDetailRow(lblDetailDest, 0, 6);

            AddDetailRow(lblHeaderTransfer, 6, 2);
            AddDetailRow(lblDetailTransfer, 0, 6);

            AddDetailRow(lblHeaderLast, 6, 2);
            AddDetailRow(lblDetailLast, 0, 2);
            AddDetailRow(lblDetailLastResult, 0, 6);

            AddDetailRow(lblHeaderNext, 6, 2);
            AddDetailRow(lblDetailNext, 0, 8);

            pnlDetailScroll.Controls.Add(tblDetail);

            pnlDetailCard.Controls.Add(pnlDetailScroll);
            pnlDetailCard.Controls.Add(pnlHeaderDivider);
            pnlDetailCard.Controls.Add(pnlDetailHeader);

            // SplitContainer
            splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical
            };
            splitContainer.Panel1.Controls.Add(gridJobs);
            splitContainer.Panel2.Controls.Add(pnlDetailCard);

            // StatusStrip
            statusStrip = new StatusStrip();
            lblServiceStatus = new ToolStripStatusLabel { Text = Strings.ServiceStatusDisconnected };
            lblServiceAction = new ToolStripStatusLabel { IsLink = true, Text = "" };
            lblServiceAction.Click += async (s, e) => await StartOrRestartServiceAsync();

            progressBar = new ToolStripProgressBar { Visible = false, Style = ProgressBarStyle.Marquee };

            statusStrip.Items.AddRange(new ToolStripItem[] { lblServiceStatus, lblServiceAction, progressBar });

            // Tray
            trayMenu = new ContextMenuStrip();
            itemTrayOpen = new ToolStripMenuItem(Strings.TrayOpen, null, (s, e) => ShowMainWindow());
            var itemSep = new ToolStripSeparator();
            itemTrayExit = new ToolStripMenuItem(Strings.TrayExit, null, (s, e) => ExitApplication());
            trayMenu.Items.AddRange(new ToolStripItem[] { itemTrayOpen, itemSep, itemTrayExit });

            trayIcon = new NotifyIcon
            {
                Text = $"FTPSys Pro v{Strings.AppVersion}",
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
            btnRun.Click += async (s, e) => await RunSelectedJobAsync();
            btnStop.Click += async (s, e) => await StopSelectedJobAsync();
            btnRestore.Click += (s, e) => OpenRestoreWizard();
            btnLogs.Click += (s, e) => OpenLogViewer();
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
            Shown += (s, e) => AdjustSplitterDistance();
            Resize += (s, e) => AdjustSplitterDistance();
        }

        private void AddDetailRow(Control ctrl, int topMargin = 0, int bottomMargin = 4)
        {
            ctrl.Dock = DockStyle.Top;
            ctrl.AutoSize = true;
            ctrl.Margin = new Padding(0, topMargin, 0, bottomMargin);
            tblDetail.Controls.Add(ctrl);
        }

        private void ApplyLanguage()
        {
            _currentConfig = _configStore.LoadConfig() ?? new AppConfig();
            if (_currentConfig.Jobs == null)
                _currentConfig.Jobs = new List<BackupJob>();
            if (_currentConfig.Settings == null)
                _currentConfig.Settings = new AppSettings();

            Strings.SetLanguage(_currentConfig.Settings.Language ?? "en");
        }

        public void UpdateUiLanguage()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(UpdateUiLanguage));
                return;
            }

            // Window Title
            Text = $"{Strings.AppName} v{Strings.AppVersion}";

            // ToolStrip buttons
            if (btnAdd != null) btnAdd.Text = "➕ " + Strings.Add;
            if (btnEdit != null) btnEdit.Text = "✏ " + Strings.Edit;
            if (btnDelete != null) btnDelete.Text = "🗑 " + Strings.Delete;
            if (btnClone != null) btnClone.Text = "📋 " + Strings.Clone;
            if (btnRun != null) btnRun.Text = "▶ " + Strings.Run;
            if (btnStop != null) btnStop.Text = "■ " + Strings.Stop;
            if (btnRestore != null) btnRestore.Text = "🔄 " + Strings.Restore;
            if (btnLogs != null) btnLogs.Text = "📋 " + Strings.Logs;
            if (btnSettings != null) btnSettings.Text = "⚙ " + Strings.Settings;
            if (btnRefresh != null) btnRefresh.Text = "↻ " + Strings.Refresh;

            // Grid Column Headers
            if (gridJobs != null)
            {
                if (gridJobs.Columns["colJobName"] != null) gridJobs.Columns["colJobName"].HeaderText = Strings.ColJobName;
                if (gridJobs.Columns["colEnabled"] != null) gridJobs.Columns["colEnabled"].HeaderText = Strings.ColEnabled;
                if (gridJobs.Columns["colBackupType"] != null) gridJobs.Columns["colBackupType"].HeaderText = Strings.ColBackupType;
                if (gridJobs.Columns["colSource"] != null) gridJobs.Columns["colSource"].HeaderText = Strings.ColSource;
                if (gridJobs.Columns["colDestination"] != null) gridJobs.Columns["colDestination"].HeaderText = Strings.ColDestination;
                if (gridJobs.Columns["colSourceSize"] != null) gridJobs.Columns["colSourceSize"].HeaderText = Strings.ColSourceSize;
                if (gridJobs.Columns["colTransferMode"] != null) gridJobs.Columns["colTransferMode"].HeaderText = Strings.ColTransferMode;
                if (gridJobs.Columns["colLastRun"] != null) gridJobs.Columns["colLastRun"].HeaderText = Strings.ColLastRun;
                if (gridJobs.Columns["colStatus"] != null) gridJobs.Columns["colStatus"].HeaderText = Strings.ColStatus;
                if (gridJobs.Columns["colNextRun"] != null) gridJobs.Columns["colNextRun"].HeaderText = Strings.ColNextRun;
                if (gridJobs.Columns["colSize"] != null) gridJobs.Columns["colSize"].HeaderText = Strings.ColSize;
            }

            // Context Menu Items
            if (itemRun != null) itemRun.Text = "▶ " + Strings.Run;
            if (itemStop != null) itemStop.Text = "■ " + Strings.Stop;
            if (itemEdit != null) itemEdit.Text = "✏ " + Strings.Edit;
            if (itemClone != null) itemClone.Text = "📋 " + Strings.Clone;
            if (itemDelete != null) itemDelete.Text = "🗑 " + Strings.Delete;
            if (itemLogs != null) itemLogs.Text = "📋 " + Strings.ViewJobLog;

            // Detail Card
            if (lblCardTitle != null) lblCardTitle.Text = "📋 " + Strings.DetailCardTitle;
            if (lblHeaderSources != null) lblHeaderSources.Text = Strings.HeaderSources;
            if (lblHeaderSourceSize != null) lblHeaderSourceSize.Text = Strings.HeaderSourceSize;
            if (lblHeaderDest != null) lblHeaderDest.Text = Strings.HeaderDest;
            if (lblHeaderTransfer != null) lblHeaderTransfer.Text = Strings.HeaderTransfer;
            if (lblHeaderLast != null) lblHeaderLast.Text = Strings.HeaderLast;
            if (lblHeaderNext != null) lblHeaderNext.Text = Strings.HeaderNext;

            // Tray
            if (itemTrayOpen != null) itemTrayOpen.Text = Strings.TrayOpen;
            if (itemTrayExit != null) itemTrayExit.Text = Strings.TrayExit;
            if (trayIcon != null) trayIcon.Text = $"FTPSys Pro v{Strings.AppVersion}";

            // Status strip
            if (lblServiceStatus != null && lblServiceAction != null)
            {
                if (lblServiceStatus.ForeColor == Color.DarkGreen)
                {
                    lblServiceStatus.Text = Strings.ServiceStatusConnected;
                    lblServiceAction.Text = "";
                }
                else
                {
                    lblServiceStatus.Text = Strings.ServiceStatusDisconnected;
                    lblServiceAction.Text = Strings.ServiceStartPrompt;
                }
            }

            // Refresh grid data to re-evaluate ViewModel string properties
            RefreshJobGrid();
        }

        private void EnsureConfig()
        {
            if (_currentConfig == null) _currentConfig = new AppConfig();
            if (_currentConfig.Jobs == null) _currentConfig.Jobs = new List<BackupJob>();
            if (_currentConfig.Settings == null) _currentConfig.Settings = new AppSettings();
        }

        private BackupJob GetSelectedJob()
        {
            if (gridJobs.SelectedRows.Count == 0) return null;
            var viewItem = gridJobs.SelectedRows[0].DataBoundItem as JobViewModel;
            return viewItem?.Job;
        }

        private void AddNewJob()
        {
            EnsureConfig();
            var newJob = new BackupJob { Name = Strings.DefaultNewJobName(_currentConfig.Jobs.Count + 1) };
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
            EnsureConfig();
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
            EnsureConfig();
            var job = GetSelectedJob();
            if (job == null) return;

            var copy = job.Clone();
            _currentConfig.Jobs.Add(copy);
            SaveConfigToService();
            RefreshJobGrid();
        }

        private void DeleteSelectedJob()
        {
            EnsureConfig();
            var job = GetSelectedJob();
            if (job == null) return;

            if (MessageBox.Show(this, $"{Strings.ConfirmDeleteJob}\n\nJob: {job.Name}", Strings.ConfirmDeleteTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _currentConfig.Jobs.RemoveAll(j => j.Id == job.Id);
                SaveConfigToService();
                RefreshJobGrid();
            }
        }

        private async Task RunSelectedJobAsync()
        {
            var job = GetSelectedJob();
            if (job == null) return;

            btnRun.Enabled = false;

            try
            {
                var req = new RunJobRequest { JobId = job.Id, BackupType = job.BackupType };
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
                    MessageBox.Show(this, Strings.UnableToStartJob + resp?.ErrorMessage, Strings.ExecutionErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            finally
            {
                btnRun.Enabled = true;
            }
        }

        private async Task StopSelectedJobAsync()
        {
            var job = GetSelectedJob();
            if (job == null) return;

            if (MessageBox.Show(this, Strings.ConfirmStopJob, Strings.StopJobTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
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
                    Strings.SetLanguage(_currentConfig.Settings.Language);
                    UpdateUiLanguage();
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
                        var serviceConfig = Newtonsoft.Json.JsonConvert.DeserializeObject<AppConfig>(resp.PayloadJson);
                        if (serviceConfig?.Settings?.Language != null &&
                            !string.Equals(serviceConfig.Settings.Language, _currentConfig?.Settings?.Language, StringComparison.OrdinalIgnoreCase))
                        {
                            _currentConfig = serviceConfig;
                            Strings.SetLanguage(_currentConfig.Settings.Language);
                            UpdateUiLanguage();
                        }
                        else
                        {
                            _currentConfig = serviceConfig;
                        }
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
                            btnRun.Enabled = false;
                            _wasRunning = true;
                        }
                        else
                        {
                            progressBar.Visible = false;
                            btnStop.Enabled = false;
                            btnRun.Enabled = true;

                            if (_wasRunning)
                            {
                                _wasRunning = false;
                                await RefreshAllAsync();
                                try
                                {
                                    trayIcon.ShowBalloonTip(3000, Strings.AppName, Strings.TrayBalloonSuccess, ToolTipIcon.Info);
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

        private void OpenLogViewer(string initialJob = null)
        {
            using (var dlg = new LogViewerDialog(_configStore, _currentConfig, initialJob))
            {
                dlg.ShowDialog(this);
            }
        }

        private async Task StartOrRestartServiceAsync()
        {
            lblServiceAction.Text = Strings.StartingService;
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
                catch (Exception)
                {
                    // Fallback: If current process is not elevated, request UAC elevation specifically for starting the service
                    try
                    {
                        var psi = new System.Diagnostics.ProcessStartInfo("sc.exe", "start FtpSysProService")
                        {
                            Verb = "runas",
                            UseShellExecute = true,
                            WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                        };
                        var proc = System.Diagnostics.Process.Start(psi);
                        proc?.WaitForExit(15000);

                        using (var sc = new ServiceController("FtpSysProService"))
                        {
                            sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, Strings.UnableToStartService + ex.Message, Strings.ServiceErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            });
            await RefreshAllAsync();
        }

        private void RefreshJobGrid()
        {
            if (_currentConfig?.Jobs == null)
            {
                gridJobs.DataSource = new List<JobViewModel>();
                UpdateSelectedJobDetail();
                return;
            }

            var list = _currentConfig.Jobs.Select(j => new JobViewModel(j)).ToList();
            gridJobs.DataSource = list;
            UpdateSelectedJobDetail();
        }

        private void UpdateSelectedJobDetail()
        {
            if (gridJobs.SelectedRows.Count > 0 && gridJobs.SelectedRows[0].DataBoundItem is JobViewModel vm)
            {
                var job = vm.Job;
                if (job == null)
                {
                    ResetJobDetail();
                    return;
                }

                // 1. Job Name & Status Badge & Description
                lblDetailName.Text = job.Name ?? string.Empty;
                lblDetailBadge.Text = $"● {(job.Enabled ? Strings.BadgeEnabled : Strings.BadgeDisabled)}  •  {Strings.BackupMode}: {vm.BackupTypeDisplay}  •  {vm.StatusDisplay}";
                lblDetailBadge.ForeColor = job.Enabled ? Color.FromArgb(30, 120, 50) : Color.Gray;

                if (!string.IsNullOrWhiteSpace(job.Description))
                {
                    lblDetailDesc.Text = "❝ " + job.Description + " ❞";
                    lblDetailDesc.Visible = true;
                }
                else
                {
                    lblDetailDesc.Visible = false;
                }

                // 2. Source Directories (borderless text, clean lines)
                if (job.SourceDirectories != null && job.SourceDirectories.Count > 0)
                {
                    lblDetailSources.Text = string.Join(Environment.NewLine, job.SourceDirectories);
                }
                else
                {
                    lblDetailSources.Text = Strings.NoneOrEmpty;
                }

                // 3. Source Size
                lblDetailSourceSize.Text = job.SourceSizeBytes > 0
                    ? $"{JobViewModel.FormatBytes(job.SourceSizeBytes)} ({job.SourceFileCount:N0} {Strings.FilesUnit})"
                    : (job.SourceDirectories != null && job.SourceDirectories.Count > 0 ? Strings.DetailNotScannedOrZero : "-");

                // 4. Destination Repository
                lblDetailDest.Text = vm.DestinationDisplay;

                // 5. Transfer / Packaging Mode
                lblDetailTransfer.Text = job.Compression != null && job.Compression.Enabled
                    ? Strings.DetailZipLevel(job.Compression.Level) + (job.Compression.EncryptWithPassword ? " [AES-256]" : "")
                    : Strings.DetailDirectCopy;

                // 6. Last Run & Backup Size
                lblDetailLast.Text = job.LastRunUtc.HasValue
                    ? $"{job.LastRunUtc.Value.ToLocalTime():yyyy-MM-dd HH:mm}  ({vm.StatusDisplay})  •  {(vm.SizeDisplay != "-" ? vm.SizeDisplay : Strings.NotScanned)}"
                    : Strings.NeverExecuted;

                if (!string.IsNullOrWhiteSpace(job.LastMessage))
                {
                    lblDetailLastResult.Text = job.LastMessage;
                    lblDetailLastResult.Visible = true;
                }
                else
                {
                    lblDetailLastResult.Text = "";
                    lblDetailLastResult.Visible = false;
                }

                // 7. Next Run & Schedule summary
                if (job.Schedules != null && job.Schedules.Count > 0)
                {
                    var schedSummary = string.Join("; ", job.Schedules.Select(s => JobEditDialog.FormatScheduleDetail(s)));
                    lblDetailNext.Text = $"{vm.NextRunDisplay} ({schedSummary})";
                }
                else
                {
                    lblDetailNext.Text = $"{vm.NextRunDisplay} ({Strings.ScheduleManualOnly})";
                }
            }
            else
            {
                ResetJobDetail();
            }
        }

        private void ResetJobDetail()
        {
            lblDetailName.Text = Strings.NoJobSelected;
            lblDetailBadge.Text = "";
            lblDetailDesc.Visible = false;
            lblDetailSources.Text = "-";
            lblDetailSourceSize.Text = "-";
            lblDetailDest.Text = "-";
            lblDetailTransfer.Text = "-";
            lblDetailLast.Text = "-";
            lblDetailLastResult.Text = "";
            lblDetailLastResult.Visible = false;
            lblDetailNext.Text = "-";
        }

        private void AdjustSplitterDistance()
        {
            try
            {
                if (splitContainer == null) return;

                int width = splitContainer.Width;
                int minPanel1 = 350;
                int minPanel2 = 300;

                if (width > (minPanel1 + minPanel2))
                {
                    splitContainer.Panel1MinSize = minPanel1;
                    splitContainer.Panel2MinSize = minPanel2;
                    splitContainer.FixedPanel = FixedPanel.Panel2;

                    int desiredDetailWidth = 350;
                    int maxDistance = width - splitContainer.Panel2MinSize;
                    int minDistance = splitContainer.Panel1MinSize;
                    int calculated = width - desiredDetailWidth;
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
                    trayIcon.ShowBalloonTip(2000, Strings.AppName, Strings.TrayRunningBackground, ToolTipIcon.Info);
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
            Job = job ?? new BackupJob();
        }

        public string Name => Job.Name ?? string.Empty;
        public bool Enabled => Job.Enabled;
        public string BackupTypeDisplay => Job.BackupType == BackupType.Full
            ? Strings.ModeFullShort
            : Strings.ModeIncrementalShort;

        public string SourceDisplay => (Job.SourceDirectories != null && Job.SourceDirectories.Count > 0)
            ? string.Join("; ", Job.SourceDirectories)
            : "-";

        public string SourceSizeDisplay => Job.SourceSizeBytes > 0
            ? $"{FormatBytes(Job.SourceSizeBytes)} ({Job.SourceFileCount:N0} {Strings.FilesUnit})"
            : (Job.SourceDirectories != null && Job.SourceDirectories.Count > 0 ? Strings.NotScanned : "-");

        public string CompressionDisplay => Job.Compression != null && Job.Compression.Enabled
            ? Strings.ZipCompression + (Job.Compression.EncryptWithPassword ? " [AES-256]" : "")
            : Strings.DirectCopy;

        public string DestinationDisplay
        {
            get
            {
                if (Job.Destination == null) return "-";
                switch (Job.Destination.Type)
                {
                    case DestinationType.Ftp: return $"FTP: {Job.Destination.FtpHost}:{Job.Destination.FtpPort}{Job.Destination.FtpRemotePath}";
                    case DestinationType.Sftp: return $"SFTP: {Job.Destination.SftpHost}:{Job.Destination.SftpPort}{Job.Destination.SftpRemotePath}";
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
