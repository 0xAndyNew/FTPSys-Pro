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
    public class LogViewerDialog : Form
    {
        private readonly ConfigStore _configStore;
        private readonly AppConfig _config;
        private readonly string _initialJobFilter;

        private ComboBox cboLogFiles;
        private ComboBox cboJobFilter;
        private ComboBox cboLevel;
        private TextBox txtSearch;
        private Button btnRefresh;
        private Button btnOpenInNotepad;
        private Button btnOpenFolder;
        private Button btnClear;
        private CheckBox chkAutoRefresh;
        private CheckBox chkAutoScroll;
        private CheckBox chkWordWrap;
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

        public LogViewerDialog(ConfigStore configStore, AppConfig config, string initialJobFilter = null)
        {
            _configStore = configStore ?? new ConfigStore();
            _config = config ?? _configStore.LoadConfig();
            _initialJobFilter = initialJobFilter;

            InitializeComponents();
            LoadJobFilterItems();
            PopulateLogFileList();
        }

        private void InitializeComponents()
        {
            Text = Strings.LogViewerTitle;
            Size = new Size(1180, 750);
            MinimumSize = new Size(920, 560);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9F);
            Icon = SystemIcons.Information;
            MaximizeBox = true;
            MinimizeBox = true;

            // Top Control Bar (2 Rows)
            var pnlTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 78,
                BackColor = Color.FromArgb(242, 245, 249),
                Padding = new Padding(10, 8, 10, 6)
            };

            // Row 1: File selection, Job filter, Level filter, Search
            var lblFile = new Label
            {
                Text = Strings.SelectLogFile,
                Location = new Point(10, 11),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 50, 70)
            };

            cboLogFiles = new ComboBox
            {
                Location = new Point(90, 8),
                Width = 320,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 8.5F)
            };
            cboLogFiles.SelectedIndexChanged += (s, e) =>
            {
                if (cboLogFiles.SelectedItem is LogFileItem item)
                {
                    _activeFilePath = item.FullPath;
                    _lastLoadedLength = 0;
                    LoadCurrentLog(appendOnly: false);
                }
            };

            var lblJob = new Label
            {
                Text = Strings.FilterJob,
                Location = new Point(425, 11),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 50, 70)
            };

            cboJobFilter = new ComboBox
            {
                Location = new Point(530, 8),
                Width = 220,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 8.5F)
            };
            cboJobFilter.SelectedIndexChanged += (s, e) =>
            {
                _lastLoadedLength = 0;
                LoadCurrentLog(appendOnly: false);
            };

            var lblLvl = new Label
            {
                Text = Strings.FilterLevel,
                Location = new Point(765, 11),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 50, 70)
            };

            cboLevel = new ComboBox
            {
                Location = new Point(830, 8),
                Width = 140,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 8.5F)
            };
            cboLevel.Items.Add(Strings.AllLevels);
            if (Strings.IsVi)
            {
                cboLevel.Items.AddRange(new object[] {
                    "ERR (Lỗi)",
                    "WRN (Cảnh báo)",
                    "INF (Thông tin)"
                });
            }
            else
            {
                cboLevel.Items.AddRange(new object[] {
                    "ERR (Error)",
                    "WRN (Warning)",
                    "INF (Information)"
                });
            }
            cboLevel.SelectedIndex = 0;
            cboLevel.SelectedIndexChanged += (s, e) =>
            {
                _lastLoadedLength = 0;
                LoadCurrentLog(appendOnly: false);
            };

            txtSearch = new TextBox
            {
                Location = new Point(980, 8),
                Width = 170,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(140, 149, 159),
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
                    txtSearch.ForeColor = Color.Gray;
                }
            };
            txtSearch.TextChanged += (s, e) =>
            {
                if (_isLoading) return;
                if (txtSearch.Text == Strings.SearchKeywords) return;
                _searchTimer.Stop();
                _searchTimer.Start();
            };
            txtSearch.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    _searchTimer.Stop();
                    _lastLoadedLength = 0;
                    LoadCurrentLog(appendOnly: false);
                }
            };

            // Row 2: Actions & Options
            btnRefresh = new Button
            {
                Text = "↻ " + Strings.Refresh,
                Location = new Point(10, 42),
                Size = new Size(90, 28),
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
                Location = new Point(108, 42),
                Size = new Size(140, 28),
                FlatStyle = FlatStyle.System
            };
            btnOpenInNotepad.Click += (s, e) => OpenActiveLogInNotepad();

            btnOpenFolder = new Button
            {
                Text = Strings.OpenLogFolder,
                Location = new Point(256, 42),
                Size = new Size(150, 28),
                FlatStyle = FlatStyle.System
            };
            btnOpenFolder.Click += (s, e) => OpenLogDirectoryInExplorer();

            btnClear = new Button
            {
                Text = Strings.ClearScreen,
                Location = new Point(414, 42),
                Size = new Size(100, 28),
                FlatStyle = FlatStyle.System
            };
            btnClear.Click += (s, e) => txtLog.Clear();

            chkAutoRefresh = new CheckBox
            {
                Text = Strings.AutoRefresh,
                Location = new Point(530, 47),
                AutoSize = true,
                Checked = true,
                Font = new Font("Segoe UI", 8.5F)
            };

            chkAutoScroll = new CheckBox
            {
                Text = Strings.AutoScroll,
                Location = new Point(680, 47),
                AutoSize = true,
                Checked = true,
                Font = new Font("Segoe UI", 8.5F)
            };

            chkWordWrap = new CheckBox
            {
                Text = Strings.IsVi ? "Xuống dòng" : "Word wrap",
                Location = new Point(780, 47),
                AutoSize = true,
                Checked = true,
                Font = new Font("Segoe UI", 8.5F)
            };
            chkWordWrap.CheckedChanged += (s, e) =>
            {
                txtLog.WordWrap = chkWordWrap.Checked;
                txtLog.ScrollBars = chkWordWrap.Checked ? RichTextBoxScrollBars.Vertical : RichTextBoxScrollBars.Both;
            };

            lblMatchCount = new Label
            {
                Text = Strings.IsVi ? "0 dòng nhật ký" : "0 log entries",
                Location = new Point(885, 48),
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
                chkAutoRefresh, chkAutoScroll, chkWordWrap, lblMatchCount
            });

            // Center: RichTextBox for high performance log rendering (Light Theme with WordWrap)
            txtLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(36, 41, 47),
                Font = new Font("Consolas", 9.5F),
                ReadOnly = true,
                WordWrap = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                BorderStyle = BorderStyle.None
            };

            // Bottom Status Strip
            statusStrip = new StatusStrip
            {
                BackColor = Color.FromArgb(246, 248, 250),
                ForeColor = Color.FromArgb(87, 96, 106)
            };
            lblStatusFile = new ToolStripStatusLabel { Text = Strings.IsVi ? "Tệp nhật ký: (Chưa chọn)" : "Log file: (None selected)", Width = 380, TextAlign = ContentAlignment.MiddleLeft };
            lblStatusLines = new ToolStripStatusLabel { Text = Strings.IsVi ? "Hiển thị: 0 dòng" : "Lines: 0", Width = 150 };
            lblStatusSize = new ToolStripStatusLabel { Text = Strings.IsVi ? "Dung lượng: 0 B" : "Size: 0 B", Width = 150 };
            lblStatusUpdated = new ToolStripStatusLabel { Text = Strings.IsVi ? "Cập nhật: -" : "Updated: -", Spring = true, TextAlign = ContentAlignment.MiddleRight };

            statusStrip.Items.AddRange(new ToolStripItem[] {
                lblStatusFile, lblStatusLines, lblStatusSize, lblStatusUpdated
            });

            Controls.Add(txtLog);
            Controls.Add(pnlTop);
            Controls.Add(statusStrip);

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
                    // Only auto-poll if active file is the latest one
                    if (cboLogFiles.SelectedIndex == 0)
                    {
                        LoadCurrentLog(appendOnly: true);
                    }
                }
            };
            timerAutoRefresh.Start();

            FormClosing += (s, e) =>
            {
                _searchTimer?.Stop();
                _searchTimer?.Dispose();
                timerAutoRefresh?.Stop();
                timerAutoRefresh?.Dispose();
            };
        }

        private void LoadJobFilterItems()
        {
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

                    if (!string.IsNullOrEmpty(_initialJobFilter) &&
                        (string.Equals(job.Id, _initialJobFilter, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(job.Name, _initialJobFilter, StringComparison.OrdinalIgnoreCase)))
                    {
                        selectIndex = i + 1;
                    }
                }
            }

            cboJobFilter.SelectedIndex = selectIndex;
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
                lblStatusFile.Text = (Strings.IsVi ? "Lỗi khi quét thư mục log: " : "Error scanning log folder: ") + ex.Message;
            }
        }

        private void LoadCurrentLog(bool appendOnly)
        {
            if (_isLoading) return;
            if (string.IsNullOrEmpty(_activeFilePath) || !File.Exists(_activeFilePath))
                return;

            _isLoading = true;
            try
            {
                var fi = new FileInfo(_activeFilePath);
                if (appendOnly && fi.Length == _lastLoadedLength)
                {
                    return;
                }

                var selectedJobItem = cboJobFilter.SelectedItem as JobFilterItem;
                string filterJobName = selectedJobItem?.JobName;
                string filterJobId = selectedJobItem?.JobId;
                bool filterByJob = !string.IsNullOrEmpty(filterJobId);

                int levelIndex = cboLevel.SelectedIndex;
                string searchKeyword = txtSearch.Text;
                if (searchKeyword == Strings.SearchKeywords) searchKeyword = "";

                if (!appendOnly)
                {
                    txtLog.SuspendLayout();
                    txtLog.Clear();
                    _lastLoadedLength = 0;
                }

                int matchingLines = 0;
                int totalLines = 0;

                using (var fs = new FileStream(_activeFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    long totalFileBytes = fs.Length;

                    if (appendOnly && _lastLoadedLength > 0 && totalFileBytes > _lastLoadedLength)
                    {
                        fs.Seek(_lastLoadedLength, SeekOrigin.Begin);
                    }
                    else if (!appendOnly)
                    {
                        // If file is larger than 2MB and no filter, load the last 512KB for smooth UI responsiveness
                        if (totalFileBytes > 2 * 1024 * 1024 && !filterByJob && levelIndex == 0 && string.IsNullOrEmpty(searchKeyword))
                        {
                            fs.Seek(-524288, SeekOrigin.End);
                            // Read past the partial first line
                            while (fs.Position < totalFileBytes)
                            {
                                int b = fs.ReadByte();
                                if (b == '\n') break;
                            }
                        }
                    }

                    // leaveOpen: true ensures reader.Dispose() does NOT close the underlying FileStream fs!
                    using (var reader = new StreamReader(fs, Encoding.UTF8, true, 4096, leaveOpen: true))
                    {
                        bool lastJobMatched = false;
                        bool lastLevelMatched = true;
                        bool lastSearchMatched = false;
                        string line;

                        while ((line = reader.ReadLine()) != null)
                        {
                            totalLines++;
                            bool isLogHeader = LogFilterHelper.IsLogEntryHeader(line);

                            bool isMatch = true;
                            if (filterByJob)
                            {
                                if (isLogHeader)
                                {
                                    isMatch = LogFilterHelper.LineMatchesJob(line, filterJobName, filterJobId);
                                    lastJobMatched = isMatch;
                                }
                                else
                                {
                                    // Continuation line (e.g. exception stack trace) inherits previous job match
                                    isMatch = lastJobMatched;
                                }
                            }

                            if (isMatch && levelIndex > 0)
                            {
                                if (isLogHeader)
                                {
                                    bool levelMatch = LogFilterHelper.LineMatchesLevel(line, levelIndex);
                                    lastLevelMatched = levelMatch;
                                    isMatch = levelMatch;
                                }
                                else
                                {
                                    // Stack trace / continuation lines inherit level match
                                    isMatch = lastLevelMatched;
                                }
                            }

                            if (isMatch && !string.IsNullOrWhiteSpace(searchKeyword))
                            {
                                if (line.IndexOf(searchKeyword, StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    lastSearchMatched = true;
                                }
                                else if (isLogHeader || !lastSearchMatched)
                                {
                                    isMatch = false;
                                    lastSearchMatched = false;
                                }
                            }

                            if (isMatch)
                            {
                                matchingLines++;
                                Color color = DetermineLineColor(line);
                                AppendColorText($"{matchingLines,4} ", Color.FromArgb(128, 128, 128));
                                AppendColorText("| ", Color.FromArgb(203, 213, 225));
                                AppendColorText(line + Environment.NewLine, color);
                            }
                        }
                    }

                    _lastLoadedLength = totalFileBytes;
                    _lastLoadedTime = DateTime.Now;
                }

                if (!appendOnly && matchingLines == 0)
                {
                    if (filterByJob || levelIndex > 0 || !string.IsNullOrWhiteSpace(searchKeyword))
                    {
                        AppendColorText((Strings.IsVi ? "(Không có dòng nhật ký vận hành nào phù hợp với bộ lọc đã chọn)" : "(No log entries match the selected filter)") + Environment.NewLine, Color.FromArgb(87, 96, 106));
                    }
                }

                if (chkAutoScroll.Checked && matchingLines > 0)
                {
                    txtLog.SelectionStart = txtLog.TextLength;
                    txtLog.ScrollToCaret();
                }

                lblMatchCount.Text = Strings.IsVi ? $"{matchingLines:N0} dòng hiển thị" : $"{matchingLines:N0} lines shown";
                lblStatusFile.Text = (Strings.IsVi ? "Tệp nhật ký: " : "Log file: ") + Path.GetFileName(_activeFilePath);
                lblStatusLines.Text = (Strings.IsVi ? "Dòng hiển thị: " : "Lines: ") + $"{matchingLines:N0}";
                lblStatusSize.Text = (Strings.IsVi ? "Dung lượng: " : "Size: ") + FormatBytes(fi.Length);
                lblStatusUpdated.Text = (Strings.IsVi ? "Cập nhật: " : "Updated: ") + $"{_lastLoadedTime:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                lblStatusFile.Text = (Strings.IsVi ? "Lỗi khi đọc tệp nhật ký: " : "Error reading log file: ") + ex.Message;
            }
            finally
            {
                if (!appendOnly)
                {
                    txtLog.ResumeLayout();
                }
                _isLoading = false;
            }
        }

        private static Color DetermineLineColor(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return Color.FromArgb(87, 96, 106);

            if (line.Contains("[ERR]") || line.Contains("[FTL]") || line.IndexOf("Exception", StringComparison.OrdinalIgnoreCase) >= 0 || line.Contains("Thất bại") || line.IndexOf("Failed", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Color.FromArgb(207, 34, 46); // Crimson Red
            }
            if (line.Contains("[WRN]") || line.Contains("Cảnh báo") || line.IndexOf("Warning", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Color.FromArgb(154, 103, 0); // Deep Amber / Orange
            }
            if (line.Contains("Hoàn tất") || line.Contains("Thành công") || line.Contains("Success"))
            {
                return Color.FromArgb(26, 127, 55); // Forest Green
            }
            if (line.Contains("[INF]"))
            {
                return Color.FromArgb(9, 105, 218); // Royal Blue
            }
            return Color.FromArgb(36, 41, 47); // Dark Charcoal Text
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
            try
            {
                if (string.IsNullOrEmpty(_activeFilePath) || !File.Exists(_activeFilePath))
                {
                    MessageBox.Show(Strings.IsVi ? "Chưa chọn tệp nhật ký hoặc tệp không tồn tại." : "No log file selected or file does not exist.", Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Process.Start("notepad.exe", $"\"{_activeFilePath}\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show((Strings.IsVi ? "Không thể mở Notepad: " : "Cannot open Notepad: ") + ex.Message, Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OpenLogDirectoryInExplorer()
        {
            try
            {
                var dir = _configStore.LogsDirectory;
                if (!Directory.Exists(dir))
                {
                    _configStore.EnsureDirectories();
                }

                if (Directory.Exists(dir))
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
                }
                else
                {
                    MessageBox.Show((Strings.IsVi ? "Thư mục nhật ký không tồn tại: " : "Log directory does not exist: ") + dir, Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show((Strings.IsVi ? "Không thể mở thư mục nhật ký: " : "Cannot open log directory: ") + ex.Message, Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("N1") + " KB";
            if (bytes < 1024 * 1024 * 1024) return (bytes / (1024.0 * 1024.0)).ToString("N1") + " MB";
            return (bytes / (1024.0 * 1024.0 * 1024.0)).ToString("N2") + " GB";
        }
    }
}
