using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using FtpSysPro.Core.Config;
using FtpSysPro.Core.Ipc;
using FtpSysPro.Core.Logging;
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

        // Right-side Cobian-style TabControl
        private SplitContainer splitContainer;
        private TabControl tabRight;
        private TabPage tabProperties;
        private TabPage tabHistory;
        private TabPage tabLog;

        // Tab 1: Task Properties
        private Panel pnlPropHeader;
        private Label lblPropJobName;
        private Label lblPropJobBadge;
        private DataGridView gridProperties;
        private DataGridViewTextBoxColumn colPropName;
        private DataGridViewTextBoxColumn colPropValue;

        // Tab 2: Execution History
        private DataGridView gridHistory;
        private Panel pnlHistTop;
        private Button btnHistRefresh;
        private Button btnHistClear;
        private Button btnHistDetails;
        private Label lblHistSummary;

        // Tab 3: Embedded Log Viewer
        private OperationLogControl operationLogControl;

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
            Text = $"{Strings.AppName} v{Strings.AppVersion}" + (Program.IsAdministrator() ? Strings.AdministratorBadge : "");
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

            gridJobs.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colJobName",
                HeaderText = Strings.ColJobName,
                DataPropertyName = "Name",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 140
            });
            gridJobs.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colSize",
                HeaderText = Strings.ColSize,
                DataPropertyName = "SizeDisplay",
                Width = 110,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
            });

            gridJobs.CellDoubleClick += (s, e) => EditSelectedJob();
            gridJobs.SelectionChanged += (s, e) => UpdateSelectedJobDetail();

            var jobMenu = new ContextMenuStrip();
            itemRun = new ToolStripMenuItem("▶ " + Strings.Run, null, async (s, e) => await RunSelectedJobAsync());
            itemStop = new ToolStripMenuItem("■ " + Strings.Stop, null, async (s, e) => await StopSelectedJobAsync());
            itemEdit = new ToolStripMenuItem("✏ " + Strings.Edit, null, (s, e) => EditSelectedJob());
            itemClone = new ToolStripMenuItem("📋 " + Strings.Clone, null, (s, e) => CloneSelectedJob());
            itemDelete = new ToolStripMenuItem("🗑 " + Strings.Delete, null, (s, e) => DeleteSelectedJob());
            var itemHistory = new ToolStripMenuItem("⏰ " + Strings.TabHistory, null, (s, e) =>
            {
                if (tabRight != null) tabRight.SelectedTab = tabHistory;
            });
            itemLogs = new ToolStripMenuItem("📝 " + Strings.ViewJobLog, null, (s, e) =>
            {
                if (tabRight != null) tabRight.SelectedTab = tabLog;
                var job = GetSelectedJob();
                operationLogControl?.SetCurrentJob(job);
            });
            jobMenu.Items.AddRange(new ToolStripItem[] {
                itemRun, itemStop, new ToolStripSeparator(),
                itemEdit, itemClone, itemDelete, new ToolStripSeparator(),
                itemHistory, itemLogs
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

            // Right-side Cobian-style TabControl (Tabs aligned at bottom matching Cobian Backup 11 Gravity)
            tabRight = new TabControl
            {
                Dock = DockStyle.Fill,
                Alignment = TabAlignment.Bottom,
                Font = new Font("Segoe UI", 9F)
            };

            // -----------------------------------------------------------------
            // Tab 1: Task properties (Thuộc tính Job)
            // -----------------------------------------------------------------
            tabProperties = new TabPage
            {
                Text = "📋 " + Strings.TabTaskProperties,
                BackColor = Color.FromArgb(250, 251, 253),
                Padding = new Padding(0)
            };

            pnlPropHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = Color.FromArgb(242, 246, 251),
                Padding = new Padding(12, 4, 12, 4)
            };

            lblPropJobName = new Label
            {
                Text = Strings.NoJobSelected,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(12, 35, 75),
                Dock = DockStyle.Top,
                AutoSize = true
            };

            lblPropJobBadge = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(30, 120, 50),
                Dock = DockStyle.Bottom,
                AutoSize = true
            };

            pnlPropHeader.Controls.Add(lblPropJobBadge);
            pnlPropHeader.Controls.Add(lblPropJobName);

            var pnlPropDivider = new Panel
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = Color.FromArgb(220, 228, 238)
            };

            gridProperties = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = Color.FromArgb(235, 238, 245),
                BackgroundColor = Color.White,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                MultiSelect = false,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells
            };
            gridProperties.DefaultCellStyle.SelectionBackColor = Color.FromArgb(220, 235, 252);
            gridProperties.DefaultCellStyle.SelectionForeColor = Color.Black;
            gridProperties.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            gridProperties.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(244, 246, 250);
            gridProperties.ColumnHeadersHeight = 28;
            gridProperties.EnableHeadersVisualStyles = false;

            colPropName = new DataGridViewTextBoxColumn
            {
                Name = "colPropName",
                HeaderText = Strings.ColProperty,
                Width = 200,
                ReadOnly = true
            };
            colPropName.DefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            colPropName.DefaultCellStyle.ForeColor = Color.FromArgb(20, 35, 60);
            colPropName.DefaultCellStyle.BackColor = Color.FromArgb(246, 248, 252);

            colPropValue = new DataGridViewTextBoxColumn
            {
                Name = "colPropValue",
                HeaderText = Strings.ColValue,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                ReadOnly = true
            };
            colPropValue.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            colPropValue.DefaultCellStyle.Font = new Font("Segoe UI", 9F);
            colPropValue.DefaultCellStyle.ForeColor = Color.FromArgb(20, 30, 45);

            gridProperties.Columns.Add(colPropName);
            gridProperties.Columns.Add(colPropValue);

            tabProperties.Controls.Add(gridProperties);
            tabProperties.Controls.Add(pnlPropDivider);
            tabProperties.Controls.Add(pnlPropHeader);

            // -----------------------------------------------------------------
            // Tab 2: History (Lịch sử thực thi)
            // -----------------------------------------------------------------
            tabHistory = new TabPage
            {
                Text = "⏰ " + Strings.TabHistory,
                BackColor = Color.FromArgb(250, 251, 253),
                Padding = new Padding(0)
            };

            pnlHistTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 38,
                BackColor = Color.FromArgb(242, 246, 251),
                Padding = new Padding(8, 4, 8, 4)
            };

            btnHistRefresh = new Button
            {
                Text = "↻ " + Strings.Refresh,
                Location = new Point(8, 5),
                Size = new Size(88, 28),
                BackColor = Color.White,
                FlatStyle = FlatStyle.System
            };
            btnHistRefresh.Click += (s, e) =>
            {
                var job = GetSelectedJob();
                UpdateSelectedJobHistory(job?.Id);
            };

            btnHistClear = new Button
            {
                Text = "🗑 " + Strings.ClearHistory,
                Location = new Point(102, 5),
                Size = new Size(110, 28),
                BackColor = Color.White,
                FlatStyle = FlatStyle.System
            };
            btnHistClear.Click += (s, e) => ClearSelectedJobHistory();

            btnHistDetails = new Button
            {
                Text = "🔍 " + Strings.ViewHistoryDetails,
                Location = new Point(218, 5),
                Size = new Size(120, 28),
                BackColor = Color.White,
                FlatStyle = FlatStyle.System
            };
            btnHistDetails.Click += (s, e) => ShowSelectedHistoryDetail();

            lblHistSummary = new Label
            {
                Text = Strings.HistNoRuns,
                Location = new Point(345, 10),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = Color.FromArgb(80, 90, 110)
            };

            pnlHistTop.Controls.AddRange(new Control[] { btnHistRefresh, btnHistClear, btnHistDetails, lblHistSummary });

            gridHistory = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = Color.FromArgb(235, 238, 245),
                BackgroundColor = Color.White,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false
            };
            gridHistory.DefaultCellStyle.SelectionBackColor = Color.FromArgb(220, 235, 252);
            gridHistory.DefaultCellStyle.SelectionForeColor = Color.Black;
            gridHistory.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            gridHistory.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(244, 246, 250);
            gridHistory.ColumnHeadersHeight = 28;
            gridHistory.EnableHeadersVisualStyles = false;

            gridHistory.Columns.Add(new DataGridViewTextBoxColumn { Name = "colHistStart", HeaderText = Strings.ColHistStartTime, DataPropertyName = "StartTime", Width = 135 });
            gridHistory.Columns.Add(new DataGridViewTextBoxColumn { Name = "colHistDuration", HeaderText = Strings.ColHistDuration, DataPropertyName = "Duration", Width = 80 });
            gridHistory.Columns.Add(new DataGridViewTextBoxColumn { Name = "colHistType", HeaderText = Strings.ColHistBackupType, DataPropertyName = "BackupTypeDisplay", Width = 75 });
            gridHistory.Columns.Add(new DataGridViewTextBoxColumn { Name = "colHistStatus", HeaderText = Strings.ColHistStatus, DataPropertyName = "StatusDisplay", Width = 90 });
            gridHistory.Columns.Add(new DataGridViewTextBoxColumn { Name = "colHistSize", HeaderText = Strings.ColHistSize, DataPropertyName = "SizeDisplay", Width = 90 });
            gridHistory.Columns.Add(new DataGridViewTextBoxColumn { Name = "colHistFiles", HeaderText = Strings.ColHistFiles, DataPropertyName = "FilesDisplay", Width = 70 });
            gridHistory.Columns.Add(new DataGridViewTextBoxColumn { Name = "colHistErrors", HeaderText = Strings.ColHistErrors, DataPropertyName = "ErrorsDisplay", Width = 110 });
            gridHistory.Columns.Add(new DataGridViewTextBoxColumn { Name = "colHistMessage", HeaderText = Strings.ColHistMessage, DataPropertyName = "MessageDisplay", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });

            gridHistory.CellDoubleClick += (s, e) => ShowSelectedHistoryDetail();
            gridHistory.CellClick += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= gridHistory.Rows.Count) return;
                var col = gridHistory.Columns[e.ColumnIndex];
                if (col.Name == "colHistErrors" || col.Name == "colHistStatus")
                {
                    ShowSelectedHistoryDetail(e.RowIndex);
                }
            };
            gridHistory.CellFormatting += GridHistory_CellFormatting;

            tabHistory.Controls.Add(gridHistory);
            tabHistory.Controls.Add(pnlHistTop);

            // -----------------------------------------------------------------
            // Tab 3: Operation Log (Nhật ký vận hành)
            // -----------------------------------------------------------------
            tabLog = new TabPage
            {
                Text = "📝 " + Strings.TabLog,
                BackColor = Color.FromArgb(248, 250, 252),
                Padding = new Padding(0)
            };

            operationLogControl = new OperationLogControl(_configStore, _currentConfig);
            tabLog.Controls.Add(operationLogControl);

            // Add all tabs to TabControl
            tabRight.TabPages.AddRange(new TabPage[] { tabProperties, tabHistory, tabLog });

            // SplitContainer
            splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical
            };
            splitContainer.Panel1.Controls.Add(gridJobs);
            splitContainer.Panel2.Controls.Add(tabRight);

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
            Text = $"{Strings.AppName} v{Strings.AppVersion}" + (Program.IsAdministrator() ? Strings.AdministratorBadge : "");

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
            if (itemLogs != null) itemLogs.Text = "📝 " + Strings.ViewJobLog;

            // Cobian-style Tabs
            if (tabProperties != null) tabProperties.Text = "📋 " + Strings.TabTaskProperties;
            if (tabHistory != null) tabHistory.Text = "⏰ " + Strings.TabHistory;
            if (tabLog != null) tabLog.Text = "📝 " + Strings.TabLog;

            // Property-Value column headers
            if (colPropName != null) colPropName.HeaderText = Strings.ColProperty;
            if (colPropValue != null) colPropValue.HeaderText = Strings.ColValue;

            // History toolbar & headers
            if (btnHistRefresh != null) btnHistRefresh.Text = "↻ " + Strings.Refresh;
            if (btnHistClear != null) btnHistClear.Text = "🗑 " + Strings.ClearHistory;
            if (btnHistDetails != null) btnHistDetails.Text = "🔍 " + Strings.ViewHistoryDetails;
            if (gridHistory != null)
            {
                if (gridHistory.Columns["colHistStart"] != null) gridHistory.Columns["colHistStart"].HeaderText = Strings.ColHistStartTime;
                if (gridHistory.Columns["colHistDuration"] != null) gridHistory.Columns["colHistDuration"].HeaderText = Strings.ColHistDuration;
                if (gridHistory.Columns["colHistType"] != null) gridHistory.Columns["colHistType"].HeaderText = Strings.ColHistBackupType;
                if (gridHistory.Columns["colHistStatus"] != null) gridHistory.Columns["colHistStatus"].HeaderText = Strings.ColHistStatus;
                if (gridHistory.Columns["colHistSize"] != null) gridHistory.Columns["colHistSize"].HeaderText = Strings.ColHistSize;
                if (gridHistory.Columns["colHistFiles"] != null) gridHistory.Columns["colHistFiles"].HeaderText = Strings.ColHistFiles;
                if (gridHistory.Columns["colHistErrors"] != null) gridHistory.Columns["colHistErrors"].HeaderText = Strings.ColHistErrors;
                if (gridHistory.Columns["colHistMessage"] != null) gridHistory.Columns["colHistMessage"].HeaderText = Strings.ColHistMessage;
            }

            // Embedded Operation Log
            operationLogControl?.UpdateUiLanguage();

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
            var job = GetSelectedJob();
            var vm = (gridJobs.SelectedRows.Count > 0) ? gridJobs.SelectedRows[0].DataBoundItem as JobViewModel : null;

            UpdateSelectedJobProperties(job, vm);
            UpdateSelectedJobHistory(job?.Id);
            operationLogControl?.SetCurrentJob(job);
        }

        private void AddPropertyRow(string name, string value, bool boldValue = false, Color? valueColor = null)
        {
            int rowIdx = gridProperties.Rows.Add(name, value);
            var row = gridProperties.Rows[rowIdx];
            if (boldValue)
            {
                row.Cells[1].Style.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            }
            if (valueColor.HasValue)
            {
                row.Cells[1].Style.ForeColor = valueColor.Value;
            }
        }

        private void UpdateSelectedJobProperties(BackupJob job, JobViewModel vm)
        {
            gridProperties.SuspendLayout();
            gridProperties.Rows.Clear();

            if (job == null)
            {
                lblPropJobName.Text = Strings.NoJobSelected;
                lblPropJobBadge.Text = "";
                AddPropertyRow(Strings.PropTaskName, "-");
                gridProperties.ResumeLayout();
                return;
            }

            // 1. Header
            lblPropJobName.Text = job.Name ?? string.Empty;
            string statusStr = vm != null ? vm.StatusDisplay : Strings.StatusIdle;
            lblPropJobBadge.Text = $"● {(job.Enabled ? Strings.BadgeEnabled : Strings.BadgeDisabled)}  •  {Strings.BackupMode}: {(job.BackupType == BackupType.Full ? Strings.ModeFull : Strings.ModeIncremental)}  •  {statusStr}";
            lblPropJobBadge.ForeColor = job.Enabled ? Color.FromArgb(30, 120, 50) : Color.Gray;

            // 2. Cobian Property | Value rows (with bold styling on key items)
            AddPropertyRow(Strings.PropTaskName, job.Name ?? Strings.NoneOrEmpty, boldValue: true);
            AddPropertyRow(Strings.PropTaskId, job.Id ?? "-");
            AddPropertyRow(Strings.PropEnabled, job.Enabled ? Strings.Yes : Strings.No);
            AddPropertyRow(Strings.PropBackupType, job.BackupType == BackupType.Full ? Strings.ModeFull : Strings.ModeIncremental, boldValue: true);

            string sourcesText = (job.SourceDirectories != null && job.SourceDirectories.Count > 0)
                ? string.Join(Environment.NewLine, job.SourceDirectories)
                : Strings.NoneOrEmpty;
            AddPropertyRow(Strings.PropSource, sourcesText, boldValue: true, valueColor: Color.FromArgb(10, 80, 180));

            string sourceSizeText = job.SourceSizeBytes > 0
                ? $"{JobViewModel.FormatBytes(job.SourceSizeBytes)} ({job.SourceFileCount:N0} {Strings.FilesUnit})"
                : (job.SourceDirectories != null && job.SourceDirectories.Count > 0 ? Strings.DetailNotScannedOrZero : "-");
            AddPropertyRow(Strings.PropSourceSize, sourceSizeText, boldValue: true);

            AddPropertyRow(Strings.PropDestination, vm != null ? vm.DestinationDisplay : "-", boldValue: true, valueColor: Color.FromArgb(10, 80, 180));
            AddPropertyRow(Strings.PropVss, (job.Integrity?.EnableVss == true) ? Strings.Yes : Strings.No);

            string compText = (job.Compression != null && job.Compression.Enabled)
                ? Strings.DetailZipLevel(job.Compression.Level)
                : Strings.DirectCopyUncompressed;
            AddPropertyRow(Strings.PropCompression, compText);

            string encText = (job.Compression != null && job.Compression.EncryptWithPassword)
                ? Strings.EncryptedAes256
                : Strings.NotEncrypted;
            AddPropertyRow(Strings.PropEncryption, encText);

            string schedText = (job.Schedules != null && job.Schedules.Count > 0)
                ? string.Join(Environment.NewLine, job.Schedules.Select(s => JobEditDialog.FormatScheduleDetail(s)))
                : Strings.ScheduleManualOnly;
            AddPropertyRow(Strings.PropSchedule, schedText);

            string retText = Strings.RetentionKeepAllText;
            if (job.Retention != null)
            {
                if (job.Retention.Type == RetentionType.KeepDays)
                    retText = Strings.RetentionDaysText(job.Retention.KeepDays);
                else if (job.Retention.Type == RetentionType.KeepFullCount)
                    retText = Strings.RetentionCountText(job.Retention.KeepFullCount);
            }
            AddPropertyRow(Strings.PropRetention, retText);

            string alertText = (job.Notification != null && job.Notification.Enabled && job.Notification.Recipients != null && job.Notification.Recipients.Count > 0)
                ? $"{Strings.Yes} ({string.Join(", ", job.Notification.Recipients)})"
                : Strings.AlertDisabled;
            AddPropertyRow(Strings.PropAlert, alertText);

            string retryText = (job.Integrity != null) ? Strings.RetryCountText(job.Integrity.NetworkRetryCount) : "-";
            AddPropertyRow(Strings.PropNetworkRetry, retryText);

            string lastRunText = job.LastRunUtc.HasValue
                ? $"{job.LastRunUtc.Value.ToLocalTime():yyyy-MM-dd HH:mm:ss}"
                : Strings.NeverExecuted;
            AddPropertyRow(Strings.PropLastRun, lastRunText);

            string lastResultText = !string.IsNullOrWhiteSpace(job.LastMessage)
                ? job.LastMessage
                : (job.LastRunUtc.HasValue ? statusStr : "-");
            AddPropertyRow(Strings.PropLastResult, lastResultText, boldValue: true);

            string nextRunText = job.NextRunUtc.HasValue
                ? job.NextRunUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
                : Strings.ScheduleManualOnly;
            AddPropertyRow(Strings.PropNextRun, nextRunText);

            gridProperties.ResumeLayout();
        }

        private void UpdateSelectedJobHistory(string jobId)
        {
            try
            {
                var records = _configStore.LoadHistoryRecords(jobId, 200);
                var viewModels = records.Select(r => new HistoryViewModel(r)).ToList();
                gridHistory.DataSource = viewModels;
                lblHistSummary.Text = viewModels.Count > 0 ? Strings.HistTotalRuns(viewModels.Count) : Strings.HistNoRuns;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[MainForm] Error loading history: " + ex.Message);
            }
        }

        private void GridHistory_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= gridHistory.Rows.Count) return;
            var col = gridHistory.Columns[e.ColumnIndex];
            if (col.Name == "colHistStatus" && e.Value != null)
            {
                string val = e.Value.ToString();
                if (val == Strings.StatusSuccess)
                {
                    e.CellStyle.ForeColor = Color.FromArgb(20, 140, 40);
                    e.CellStyle.Font = new Font(gridHistory.Font, FontStyle.Bold);
                }
                else if (val == Strings.StatusWarning)
                {
                    e.CellStyle.ForeColor = Color.FromArgb(200, 130, 0);
                    e.CellStyle.Font = new Font(gridHistory.Font, FontStyle.Bold);
                }
                else if (val == Strings.StatusFailed)
                {
                    e.CellStyle.ForeColor = Color.FromArgb(220, 30, 30);
                    e.CellStyle.Font = new Font(gridHistory.Font, FontStyle.Bold);
                }
            }
            else if (col.Name == "colHistErrors" && e.Value != null)
            {
                string val = e.Value.ToString();
                if (val.Contains("❌"))
                {
                    e.CellStyle.ForeColor = Color.FromArgb(220, 30, 30);
                    e.CellStyle.Font = new Font(gridHistory.Font, FontStyle.Bold);
                }
                else if (val.Contains("⚠️"))
                {
                    e.CellStyle.ForeColor = Color.FromArgb(200, 130, 0);
                    e.CellStyle.Font = new Font(gridHistory.Font, FontStyle.Bold);
                }
            }
        }

        private void ShowSelectedHistoryDetail(int rowIndex = -1)
        {
            HistoryViewModel vm = null;
            if (rowIndex >= 0 && rowIndex < gridHistory.Rows.Count)
            {
                vm = gridHistory.Rows[rowIndex].DataBoundItem as HistoryViewModel;
            }
            else if (gridHistory.SelectedRows.Count > 0)
            {
                vm = gridHistory.SelectedRows[0].DataBoundItem as HistoryViewModel;
            }
            if (vm?.Record == null) return;

            using (var dlg = new HistoryDetailDialog(vm.Record))
            {
                dlg.OnViewLogRequested = (logTime, jobId) =>
                {
                    if (tabRight != null && tabLog != null)
                    {
                        tabRight.SelectedTab = tabLog;
                    }
                    var job = _currentConfig?.Jobs?.FirstOrDefault(j => j.Id == jobId) ?? GetSelectedJob();
                    operationLogControl?.SetCurrentJob(job);
                };
                dlg.ShowDialog(this);
            }
        }

        private void ClearSelectedJobHistory()
        {
            var job = GetSelectedJob();
            if (job == null) return;

            string confirm = Strings.ConfirmClearHistory + "\n\nJob: " + (job.Name ?? job.Id);
            if (MessageBox.Show(this, confirm, Strings.AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _configStore.ClearHistory(job.Id);
                _configStore.ClearJobLog(job.Id);
                UpdateSelectedJobHistory(job.Id);
                operationLogControl?.SetCurrentJob(job);
            }
        }

        private void AdjustSplitterDistance()
        {
            try
            {
                if (splitContainer == null) return;

                int width = splitContainer.Width;
                int minPanel1 = 220;
                int minPanel2 = 500;

                if (width > (minPanel1 + minPanel2))
                {
                    splitContainer.Panel1MinSize = minPanel1;
                    splitContainer.Panel2MinSize = minPanel2;

                    int calculated = (int)(width * 0.28);
                    int maxDistance = width - minPanel2;
                    splitContainer.SplitterDistance = Math.Max(minPanel1, Math.Min(calculated, maxDistance));
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
            operationLogControl?.CleanUp();
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
                return;
            }

            operationLogControl?.CleanUp();
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

        public string SizeDisplay
        {
            get
            {
                if (Job.LastSizeBytes > 0) return FormatBytes(Job.LastSizeBytes);
                if (Job.SourceSizeBytes > 0) return FormatBytes(Job.SourceSizeBytes);
                return "-";
            }
        }

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

    public class HistoryViewModel
    {
        [System.ComponentModel.Browsable(false)]
        public BackupRunResult Record { get; }

        public HistoryViewModel(BackupRunResult record)
        {
            Record = record ?? new BackupRunResult();
        }

        public string StartTime => Record.StartTimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

        public string Duration
        {
            get
            {
                var d = Record.Duration;
                if (d <= TimeSpan.Zero) return "-";
                if (d.TotalHours >= 1) return $"{(int)d.TotalHours}h {d.Minutes}m {d.Seconds}s";
                if (d.TotalMinutes >= 1) return $"{d.Minutes}m {d.Seconds}s";
                return $"{d.Seconds}s";
            }
        }

        public string BackupTypeDisplay => Record.BackupType == BackupType.Full ? Strings.ModeFullShort : Strings.ModeIncrementalShort;

        public string StatusDisplay
        {
            get
            {
                switch (Record.Status)
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

        public string SizeDisplay => Record.BackedUpBytes > 0 ? JobViewModel.FormatBytes(Record.BackedUpBytes) : "0 B";

        public string FilesDisplay => $"{Record.BackedUpFiles:N0}";

        public string ErrorsDisplay
        {
            get
            {
                int errs = Record.ErrorCount;
                int warns = Record.WarningCount;
                if (errs > 0 && warns > 0) return $"❌ {errs} / ⚠️ {warns}";
                if (errs > 0) return $"❌ {errs} err";
                if (warns > 0) return $"⚠️ {warns} warn";
                return "0 / 0";
            }
        }

        public string MessageDisplay => !string.IsNullOrEmpty(Record.Message) ? Record.Message : "-";
    }
}
