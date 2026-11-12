using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using FtpSysPro.Core.Config;
using FtpSysPro.Core.Logging;
using FtpSysPro.Core.Models;
using FtpSysPro.Core.Resources;

namespace FtpSysPro.Manager.UI
{
    public class OperationLogControl : UserControl
    {
        private readonly ConfigStore _configStore;
        private AppConfig _config;

        private Panel pnlTop;
        private Label lblFile;
        private ComboBox cboLogFiles;
        private Label lblJob;
        private ComboBox cboJobFilter;
        private Label lblLvl;
        private ComboBox cboLevel;
        private TextBox txtSearch;

        private Button btnRefresh;
        private Button btnOpenInNotepad;
        private Button btnOpenFolder;
        private Button btnClear;
        private CheckBox chkAutoRefresh;
        private CheckBox chkAutoScroll;
        private Label lblMatchCount;

        private RichTextBox txtLog;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel lblStatusFile;
        private ToolStripStatusLabel lblStatusLines;
        private ToolStripStatusLabel lblStatusSize;
        private ToolStripStatusLabel lblStatusUpdated;

        private Timer timerAutoRefresh;
        private Timer _searchTimer;
        private string _activeFilePath;
        private long _lastLoadedLength;
        private DateTime _lastLoadedTime;
        private bool _isLoading;

        private class LogFileItem
        {
            public string FullPath { get; set; }
            public string FileName { get; set; }
            public long Length { get; set; }
            public DateTime LastWriteTime { get; set; }

            public override string ToString()
            {
                return $"{FileName} ({FormatBytes(Length)} - {LastWriteTime:dd/MM/yyyy HH:mm:ss})";
            }
        }

        private class JobFilterItem
        {
            public string JobId { get; set; }
            public string JobName { get; set; }

            public override string ToString()
            {
                return string.IsNullOrEmpty(JobId) ? Strings.AllJobs : JobName;
            }
        }

        public OperationLogControl(ConfigStore configStore = null, AppConfig config = null)
        {
            _configStore = configStore ?? new ConfigStore();
            _config = config ?? _configStore.LoadConfig();

            InitializeComponents();
            LoadJobFilterItems();
            PopulateLogFileList();
        }

        private void InitializeComponents()
        {
            Font = new Font("Segoe UI", 9F);
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(248, 250, 252);

            // Top Control Bar (2 Rows)
            pnlTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 78,
                BackColor = Color.FromArgb(242, 245, 249),
                Padding = new Padding(10, 8, 10, 6)
            };

            // Row 1: Log file dropdown + Job filter + Level filter + Search box
            lblFile = new Label
            {
                Text = Strings.SelectLogFile,
                Location = new Point(8, 11),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(50, 60, 80)
            };

            cboLogFiles = new ComboBox
            {
                Location = new Point(76, 8),
                Width = 180,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboLogFiles.SelectedIndexChanged += (s, e) =>
            {
                _lastLoadedLength = 0;
                LoadCurrentLog(appendOnly: false);
            };

            lblJob = new Label
            {
                Text = Strings.FilterJob,
                Location = new Point(264, 11),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(50, 60, 80)
            };

            cboJobFilter = new ComboBox
            {
                Location = new Point(340, 8),
                Width = 140,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboJobFilter.SelectedIndexChanged += (s, e) =>
            {
                _lastLoadedLength = 0;
                LoadCurrentLog(appendOnly: false);
            };

            lblLvl = new Label
            {
                Text = Strings.FilterLevel,
                Location = new Point(488, 11),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(50, 60, 80)
            };

            cboLevel = new ComboBox
            {
                Location = new Point(532, 8),
                Width = 98,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboLevel.Items.AddRange(new object[] {
                Strings.AllLevels,
                "ERR (" + Strings.StatusFailed + ")",
                "WRN (" + Strings.StatusWarning + ")",
                "INF (" + Strings.StatusIdle + ")"
            });
            cboLevel.SelectedIndex = 0;
            cboLevel.SelectedIndexChanged += (s, e) =>
            {
                _lastLoadedLength = 0;
                LoadCurrentLog(appendOnly: false);
            };

            txtSearch = new TextBox
            {
                Location = new Point(638, 8),
                Width = 150,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                ForeColor = Color.DarkGray,
                Text = Strings.SearchKeywords
            };
            txtSearch.GotFocus += (s, e) =>
            {
                if (txtSearch.Text == Strings.SearchKeywords)
                {
                    txtSearch.Text = "";
                    txtSearch.ForeColor = Color.Black;
                }
            };
            txtSearch.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtSearch.Text))
                {
                    txtSearch.Text = Strings.SearchKeywords;
                    txtSearch.ForeColor = Color.DarkGray;
                }
            };
            txtSearch.TextChanged += (s, e) =>
            {
                if (txtSearch.Text != Strings.SearchKeywords)
                {
                    _searchTimer?.Stop();
                    _searchTimer?.Start();
                }
            };

            // Row 2: Action Buttons & Checkboxes
            btnRefresh = new Button
            {
                Text = "↻ " + Strings.Refresh,
                Location = new Point(8, 42),
                Size = new Size(76, 27),
                BackColor = Color.White,
                FlatStyle = FlatStyle.System
            };
            btnRefresh.Click += (s, e) =>
            {
                PopulateLogFileList();
                _lastLoadedLength = 0;
                LoadCurrentLog(appendOnly: false);
            };

            btnOpenInNotepad = new Button
            {
                Text = Strings.OpenInNotepad,
                Location = new Point(88, 42),
                Size = new Size(120, 27),
                BackColor = Color.White,
                FlatStyle = FlatStyle.System
            };
            btnOpenInNotepad.Click += (s, e) => OpenActiveLogInNotepad();

            btnOpenFolder = new Button
            {
                Text = Strings.OpenLogFolder,
                Location = new Point(212, 42),
                Size = new Size(120, 27),
                BackColor = Color.White,
                FlatStyle = FlatStyle.System
            };
            btnOpenFolder.Click += (s, e) => OpenLogDirectoryInExplorer();

            btnClear = new Button
            {
                Text = Strings.ClearScreen,
                Location = new Point(336, 42),
                Size = new Size(88, 27),
                BackColor = Color.White,
                FlatStyle = FlatStyle.System
            };
            btnClear.Click += (s, e) => txtLog.Clear();

            chkAutoRefresh = new CheckBox
            {
                Text = Strings.AutoRefresh,
                Location = new Point(430, 46),
                AutoSize = true,
                Checked = true,
                Font = new Font("Segoe UI", 8.5F)
            };

            chkAutoScroll = new CheckBox
            {
                Text = Strings.AutoScroll,
                Location = new Point(545, 46),
                AutoSize = true,
                Checked = true,
                Font = new Font("Segoe UI", 8.5F)
            };

            lblMatchCount = new Label
            {
                Text = Strings.IsVi ? "0 dòng nhật ký" : "0 log entries",
                Location = new Point(640, 47),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = Color.FromArgb(70, 80, 100)
            };

            pnlTop.Controls.AddRange(new Control[] {
                lblFile, cboLogFiles,
                lblJob, cboJobFilter,
                lblLvl, cboLevel,
                txtSearch,
                btnRefresh, btnOpenInNotepad, btnOpenFolder, btnClear,
                chkAutoRefresh, chkAutoScroll, lblMatchCount
            });

            // Center: RichTextBox for high performance log rendering
            txtLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(18, 22, 30),
                ForeColor = Color.FromArgb(220, 225, 235),
                Font = new Font("Consolas", 9.5F),
                ReadOnly = true,
                WordWrap = false,
                ScrollBars = RichTextBoxScrollBars.Both,
                BorderStyle = BorderStyle.None
            };

            // Bottom Status Strip
            statusStrip = new StatusStrip();
            lblStatusFile = new ToolStripStatusLabel { Text = Strings.IsVi ? "Tệp nhật ký: (Chưa chọn)" : "Log file: (None selected)", Width = 340, TextAlign = ContentAlignment.MiddleLeft };
            lblStatusLines = new ToolStripStatusLabel { Text = Strings.IsVi ? "Hiển thị: 0 dòng" : "Lines: 0", Width = 140 };
            lblStatusSize = new ToolStripStatusLabel { Text = Strings.IsVi ? "Dung lượng: 0 B" : "Size: 0 B", Width = 140 };
            lblStatusUpdated = new ToolStripStatusLabel { Text = Strings.IsVi ? "Cập nhật: -" : "Updated: -", Spring = true, TextAlign = ContentAlignment.MiddleRight };

            statusStrip.Items.AddRange(new ToolStripItem[] {
                lblStatusFile, lblStatusLines, lblStatusSize, lblStatusUpdated
            });

            Controls.Add(pnlTop);
            Controls.Add(statusStrip);
            Controls.Add(txtLog);

            // Debounce timer for search input (350 ms)
            _searchTimer = new Timer { Interval = 350 };
            _searchTimer.Tick += (s, e) =>
            {
                _searchTimer.Stop();
                _lastLoadedLength = 0;
                LoadCurrentLog(appendOnly: false);
            };

            // Auto-refresh timer (every 3 seconds)
            timerAutoRefresh = new Timer { Interval = 3000 };
            timerAutoRefresh.Tick += (s, e) =>
            {
                if (chkAutoRefresh.Checked && !string.IsNullOrEmpty(_activeFilePath))
                {
                    if (cboLogFiles.SelectedIndex == 0)
                    {
                        LoadCurrentLog(appendOnly: true);
                    }
                }
            };
            timerAutoRefresh.Start();
        }

        public void LoadJobFilterItems(AppConfig config = null)
        {
            if (config != null)
                _config = config;

            var currentJobId = (cboJobFilter.SelectedItem as JobFilterItem)?.JobId;

            cboJobFilter.Items.Clear();
            cboJobFilter.Items.Add(new JobFilterItem { JobId = null, JobName = Strings.AllJobs });

            int selectIndex = 0;
            if (_config?.Jobs != null)
            {
                for (int i = 0; i < _config.Jobs.Count; i++)
                {
                    var job = _config.Jobs[i];
                    var item = new JobFilterItem { JobId = job.Id, JobName = job.Name };
                    cboJobFilter.Items.Add(item);

                    if (!string.IsNullOrEmpty(currentJobId) &&
                        string.Equals(job.Id, currentJobId, StringComparison.OrdinalIgnoreCase))
                    {
                        selectIndex = i + 1;
                    }
                }
            }

            // Default to All Jobs so logs are immediately visible
            if (cboJobFilter.Items.Count > 0)
                cboJobFilter.SelectedIndex = 0;
        }

        public void SelectJob(string jobId)
        {
            if (cboJobFilter == null || cboJobFilter.Items.Count == 0) return;

            if (string.IsNullOrEmpty(jobId))
            {
                cboJobFilter.SelectedIndex = 0;
                return;
            }

            for (int i = 0; i < cboJobFilter.Items.Count; i++)
            {
                if (cboJobFilter.Items[i] is JobFilterItem item &&
                    string.Equals(item.JobId, jobId, StringComparison.OrdinalIgnoreCase))
                {
                    cboJobFilter.SelectedIndex = i;
                    return;
                }
            }
        }

        public void NavigateToDateAndJob(DateTime date, string jobId)
        {
            string targetFileName = $"service-{date:yyyyMMdd}.log";
            for (int i = 0; i < cboLogFiles.Items.Count; i++)
            {
                if (cboLogFiles.Items[i] is LogFileItem item &&
                    string.Equals(item.FileName, targetFileName, StringComparison.OrdinalIgnoreCase))
                {
                    cboLogFiles.SelectedIndex = i;
                    break;
                }
            }

            if (!string.IsNullOrEmpty(jobId))
            {
                SelectJob(jobId);
            }
        }

        public void PopulateLogFileList()
        {
            try
            {
                var logsDir = _configStore.LogsDirectory;
                if (!Directory.Exists(logsDir))
                {
                    _configStore.EnsureDirectories();
                }

                if (!Directory.Exists(logsDir))
                {
                    cboLogFiles.Items.Clear();
                    lblStatusFile.Text = Strings.NoLogsFound;
                    return;
                }

                var dir = new DirectoryInfo(logsDir);
                var files = dir.GetFiles("service-*.log")
                               .OrderByDescending(f => f.LastWriteTimeUtc)
                               .ToList();

                var currentSelection = (cboLogFiles.SelectedItem as LogFileItem)?.FullPath;

                cboLogFiles.Items.Clear();
                if (files.Count == 0)
                {
                    lblStatusFile.Text = Strings.NoLogsFound;
                    txtLog.Clear();
                    AppendColorText(Strings.NoLogsFound + Environment.NewLine, Color.DarkGray);
                    return;
                }

                int selectIdx = 0;
                for (int i = 0; i < files.Count; i++)
                {
                    var f = files[i];
                    var item = new LogFileItem
                    {
                        FullPath = f.FullName,
                        FileName = f.Name,
                        Length = f.Length,
                        LastWriteTime = f.LastWriteTime
                    };
                    cboLogFiles.Items.Add(item);

                    if (currentSelection != null && string.Equals(f.FullName, currentSelection, StringComparison.OrdinalIgnoreCase))
                    {
                        selectIdx = i;
                    }
                }

                cboLogFiles.SelectedIndex = selectIdx;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[OperationLogControl] Error populating log files: " + ex.Message);
            }
        }

        private void LoadCurrentLog(bool appendOnly)
        {
            if (_isLoading) return;
            var item = cboLogFiles.SelectedItem as LogFileItem;
            if (item == null || string.IsNullOrEmpty(item.FullPath) || !File.Exists(item.FullPath))
            {
                if (!appendOnly)
                {
                    txtLog.Clear();
                    lblStatusFile.Text = Strings.NoLogsFound;
                }
                return;
            }

            _isLoading = true;
            _activeFilePath = item.FullPath;

            try
            {
                var fileInfo = new FileInfo(_activeFilePath);
                long currentLength = fileInfo.Length;

                if (appendOnly && currentLength <= _lastLoadedLength)
                {
                    _isLoading = false;
                    return;
                }

                var selectedJob = cboJobFilter.SelectedItem as JobFilterItem;
                string filterJobId = selectedJob?.JobId;
                string filterJobName = selectedJob?.JobName;
                int filterLevelIndex = cboLevel.SelectedIndex;
                string keyword = (txtSearch.Text != Strings.SearchKeywords) ? txtSearch.Text.Trim() : null;

                using (var fs = new FileStream(_activeFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (appendOnly && _lastLoadedLength > 0 && _lastLoadedLength < currentLength)
                    {
                        fs.Seek(_lastLoadedLength, SeekOrigin.Begin);
                    }
                    else
                    {
                        txtLog.Clear();
                        _lastLoadedLength = 0;
                    }

                    using (var reader = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 8192, leaveOpen: true))
                    {
                        string line;
                        int rawLineNumber = 0;
                        int matchedLinesCount = 0;
                        txtLog.SuspendLayout();

                        while ((line = reader.ReadLine()) != null)
                        {
                            rawLineNumber++;

                            if (!string.IsNullOrEmpty(filterJobId) || !string.IsNullOrEmpty(filterJobName))
                            {
                                if (!LogFilterHelper.LineMatchesJob(line, filterJobName, filterJobId))
                                    continue;
                            }

                            if (filterLevelIndex > 0)
                            {
                                if (!LogFilterHelper.LineMatchesLevel(line, filterLevelIndex))
                                    continue;
                            }

                            if (!string.IsNullOrEmpty(keyword))
                            {
                                if (line.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0)
                                    continue;
                            }

                            matchedLinesCount++;
                            AppendNotepadPlusPlusLine(matchedLinesCount, line);
                        }

                        if (matchedLinesCount == 0 && !appendOnly)
                        {
                            txtLog.Clear();
                            AppendColorText("--------------------------------------------------------------------------------" + Environment.NewLine, Color.FromArgb(100, 115, 135));
                            AppendColorText($"  NOTEPAD++ LOG VIEWER - FTPSYS PRO" + Environment.NewLine, Color.FromArgb(88, 166, 255));
                            AppendColorText($"  File: {item.FileName}  |  Job Filter: {filterJobName ?? Strings.AllJobs}" + Environment.NewLine, Color.FromArgb(135, 145, 160));
                            AppendColorText("--------------------------------------------------------------------------------" + Environment.NewLine, Color.FromArgb(100, 115, 135));
                            if (rawLineNumber > 0)
                            {
                                AppendColorText($"  [INFO] Tệp có {rawLineNumber} dòng nhật ký, nhưng không có dòng nào khớp với bộ lọc Job '{(filterJobName ?? filterJobId)}'." + Environment.NewLine, Color.FromArgb(255, 195, 85));
                                AppendColorText($"  💡 Gợi ý: Chuyển 'Lọc theo Job' về '-- Tất cả Job --' để xem toàn bộ nội dung nhật ký hệ thống." + Environment.NewLine, Color.FromArgb(126, 231, 135));
                            }
                            else
                            {
                                AppendColorText($"  [INFO] {Strings.NoLogsFound}" + Environment.NewLine, Color.FromArgb(160, 170, 185));
                            }
                            AppendColorText("--------------------------------------------------------------------------------" + Environment.NewLine, Color.FromArgb(100, 115, 135));
                        }

                        _lastLoadedLength = fs.Position;
                        _lastLoadedTime = DateTime.Now;

                        if (chkAutoScroll.Checked)
                        {
                            txtLog.SelectionStart = txtLog.TextLength;
                            txtLog.ScrollToCaret();
                        }

                        txtLog.ResumeLayout();

                        int totalDisplayedLines = matchedLinesCount;
                        lblStatusFile.Text = $"{Strings.SelectLogFile} {item.FileName}";
                        lblStatusLines.Text = Strings.IsVi ? $"Hiển thị: {totalDisplayedLines:N0} dòng" : $"Lines: {totalDisplayedLines:N0}";
                        lblStatusSize.Text = Strings.IsVi ? $"Dung lượng: {FormatBytes(currentLength)}" : $"Size: {FormatBytes(currentLength)}";
                        lblStatusUpdated.Text = Strings.IsVi ? $"Cập nhật: {_lastLoadedTime:HH:mm:ss}" : $"Updated: {_lastLoadedTime:HH:mm:ss}";
                        lblMatchCount.Text = Strings.IsVi ? $"{totalDisplayedLines:N0} dòng nhật ký" : $"{totalDisplayedLines:N0} log entries";
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[OperationLogControl] Error reading log: " + ex.Message);
            }
            finally
            {
                _isLoading = false;
            }
        }

        private string GetSelectedLogLevel()
        {
            if (cboLevel.SelectedIndex <= 0) return null;
            if (cboLevel.SelectedIndex == 1) return "ERR";
            if (cboLevel.SelectedIndex == 2) return "WRN";
            if (cboLevel.SelectedIndex == 3) return "INF";
            return null;
        }

        private void AppendNotepadPlusPlusLine(int lineNumber, string line)
        {
            // 1. Notepad++ gutter line numbers: 0001 |
            AppendColorText($"{lineNumber:D4} | ", Color.FromArgb(105, 120, 140));

            if (string.IsNullOrWhiteSpace(line))
            {
                AppendColorText(Environment.NewLine, Color.White);
                return;
            }

            // 2. Notepad++ Serilog pattern coloring: "yyyy-MM-dd HH:mm:ss.fff +zz:zz [LVL] message"
            int bracketIdx = line.IndexOf('[');
            int bracketEnd = (bracketIdx >= 0) ? line.IndexOf(']', bracketIdx) : -1;

            if (bracketIdx > 0 && bracketEnd > bracketIdx && bracketEnd - bracketIdx <= 6)
            {
                // Timestamp
                string timestamp = line.Substring(0, bracketIdx);
                AppendColorText(timestamp, Color.FromArgb(135, 145, 160));

                // Level tag [INF], [WRN], [ERR]
                string levelTag = line.Substring(bracketIdx, bracketEnd - bracketIdx + 1);
                Color levelCol = Color.FromArgb(88, 166, 255); // Notepad++ Vivid Blue
                if (levelTag.Contains("ERR") || levelTag.Contains("FTL"))
                    levelCol = Color.FromArgb(255, 95, 95); // Notepad++ Vivid Red
                else if (levelTag.Contains("WRN"))
                    levelCol = Color.FromArgb(255, 175, 65); // Notepad++ Amber Orange
                else if (levelTag.Contains("DBG") || levelTag.Contains("VRB"))
                    levelCol = Color.FromArgb(160, 170, 185);

                AppendColorText(levelTag + " ", levelCol);

                // Remainder message
                string message = line.Substring(bracketEnd + 1).TrimStart();
                Color msgCol = DetermineLineColor(line);
                AppendColorText(message + Environment.NewLine, msgCol);
            }
            else
            {
                Color col = DetermineLineColor(line);
                AppendColorText(line + Environment.NewLine, col);
            }
        }

        private static Color DetermineLineColor(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return Color.FromArgb(160, 170, 185);

            if (line.Contains("[ERR]") || line.IndexOf("Exception", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("Error", StringComparison.OrdinalIgnoreCase) >= 0 || line.IndexOf("Thất bại", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Color.FromArgb(255, 115, 115); // Red
            }

            if (line.Contains("[WRN]") || line.IndexOf("Warning", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("Cảnh báo", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Color.FromArgb(255, 200, 80); // Orange / Yellow
            }

            if (line.IndexOf("Thành công", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("Success", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("Completed", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Color.FromArgb(130, 230, 140); // Soft Green
            }

            if (line.Contains("[INF]"))
            {
                return Color.FromArgb(215, 235, 255); // Soft Light Blue
            }

            return Color.FromArgb(220, 225, 235);
        }

        private void AppendColorText(string text, Color color)
        {
            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.SelectionLength = 0;
            txtLog.SelectionColor = color;
            txtLog.AppendText(text);
            txtLog.SelectionColor = txtLog.ForeColor;
        }

        private void OpenActiveLogInNotepad()
        {
            var item = cboLogFiles.SelectedItem as LogFileItem;
            if (item != null && File.Exists(item.FullPath))
            {
                try
                {
                    Process.Start("notepad.exe", $"\"{item.FullPath}\"");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Không thể mở Notepad: " + ex.Message, Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void OpenLogDirectoryInExplorer()
        {
            try
            {
                var logsDir = _configStore.LogsDirectory;
                if (!Directory.Exists(logsDir))
                    _configStore.EnsureDirectories();

                Process.Start("explorer.exe", $"\"{logsDir}\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Không thể mở thư mục Log: " + ex.Message, Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        public void UpdateUiLanguage()
        {
            lblFile.Text = Strings.SelectLogFile;
            lblJob.Text = Strings.FilterJob;
            lblLvl.Text = Strings.FilterLevel;

            int lvlIdx = cboLevel.SelectedIndex;
            cboLevel.Items.Clear();
            cboLevel.Items.AddRange(new object[] {
                Strings.AllLevels,
                "ERR (" + Strings.StatusFailed + ")",
                "WRN (" + Strings.StatusWarning + ")",
                "INF (" + Strings.StatusIdle + ")"
            });
            cboLevel.SelectedIndex = Math.Max(0, lvlIdx);

            if (txtSearch.Text == "Search keywords..." || txtSearch.Text == "Tìm từ khóa trong log...")
            {
                txtSearch.Text = Strings.SearchKeywords;
            }

            btnRefresh.Text = "↻ " + Strings.Refresh;
            btnOpenInNotepad.Text = Strings.OpenInNotepad;
            btnOpenFolder.Text = Strings.OpenLogFolder;
            btnClear.Text = Strings.ClearScreen;
            chkAutoRefresh.Text = Strings.AutoRefresh;
            chkAutoScroll.Text = Strings.AutoScroll;

            LoadJobFilterItems();
            PopulateLogFileList();
        }

        public void CleanUp()
        {
            try
            {
                _searchTimer?.Stop();
                _searchTimer?.Dispose();
                timerAutoRefresh?.Stop();
                timerAutoRefresh?.Dispose();
            }
            catch { }
        }

        private static string FormatBytes(long bytes)
        {
            return JobViewModel.FormatBytes(bytes);
        }
    }
}
