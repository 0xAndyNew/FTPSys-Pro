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
        private BackupJob _currentJob;

        private Panel pnlTop;
        private Label lblJobTitle;
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

        public OperationLogControl(ConfigStore configStore = null, AppConfig config = null)
        {
            _configStore = configStore ?? new ConfigStore();
            _config = config ?? _configStore.LoadConfig();

            InitializeComponents();
        }

        private void InitializeComponents()
        {
            Font = new Font("Segoe UI", 9F);
            Dock = DockStyle.Fill;
            BackColor = Color.White;

            // Top Control Bar (Responsive 2-Tier Layout)
            pnlTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 66,
                BackColor = Color.FromArgb(246, 248, 250),
                Padding = new Padding(8, 5, 8, 3)
            };

            // Tier 1: Responsive TableLayoutPanel
            var tblRow1 = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 26,
                ColumnCount = 5,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            tblRow1.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Job Title
            tblRow1.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Filter label
            tblRow1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105F)); // Level Dropdown
            tblRow1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F)); // Search box (responsive stretch)
            tblRow1.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Match count label
            tblRow1.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            lblJobTitle = new Label
            {
                Text = Strings.IsVi ? "📌 Nhật ký: (Chưa chọn Job)" : "📌 Log: (No job selected)",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(9, 105, 218),
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 0, 10, 0)
            };

            lblLvl = new Label
            {
                Text = Strings.FilterLevel,
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(87, 96, 106),
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 0, 4, 0)
            };

            cboLevel = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 8.5F),
                Margin = new Padding(0, 0, 8, 0)
            };
            cboLevel.Items.AddRange(new object[] {
                Strings.AllLevels,
                "ERR (" + Strings.StatusFailed + ")",
                "WRN (" + Strings.StatusWarning + ")",
                "INF (" + Strings.StatusIdle + ")"
            });
            cboLevel.SelectedIndex = 0; // Default to All Levels so no lines are hidden
            cboLevel.SelectedIndexChanged += (s, e) =>
            {
                _lastLoadedLength = 0;
                LoadCurrentLog(appendOnly: false);
            };

            txtSearch = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(140, 149, 159),
                Text = Strings.SearchKeywords,
                Margin = new Padding(0, 0, 8, 0)
            };
            txtSearch.GotFocus += (s, e) =>
            {
                if (txtSearch.Text == Strings.SearchKeywords)
                {
                    txtSearch.Text = "";
                    txtSearch.ForeColor = Color.FromArgb(36, 41, 47);
                }
            };
            txtSearch.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtSearch.Text))
                {
                    txtSearch.Text = Strings.SearchKeywords;
                    txtSearch.ForeColor = Color.FromArgb(140, 149, 159);
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

            lblMatchCount = new Label
            {
                Text = Strings.IsVi ? "0 dòng" : "0 entries",
                AutoSize = true,
                Anchor = AnchorStyles.Right,
                Font = new Font("Segoe UI", 8F),
                ForeColor = Color.FromArgb(87, 96, 106),
                Margin = new Padding(0)
            };

            tblRow1.Controls.Add(lblJobTitle, 0, 0);
            tblRow1.Controls.Add(lblLvl, 1, 0);
            tblRow1.Controls.Add(cboLevel, 2, 0);
            tblRow1.Controls.Add(txtSearch, 3, 0);
            tblRow1.Controls.Add(lblMatchCount, 4, 0);

            // Tier 2: FlowLayoutPanel for actions and auto-update toggles
            var flowRow2 = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 28,
                WrapContents = false,
                AutoScroll = false,
                Margin = new Padding(0, 3, 0, 0),
                Padding = new Padding(0)
            };

            btnRefresh = new Button
            {
                Text = "↻ " + Strings.Refresh,
                Size = new Size(74, 25),
                Margin = new Padding(0, 1, 4, 0),
                BackColor = Color.White,
                FlatStyle = FlatStyle.System
            };
            btnRefresh.Click += (s, e) =>
            {
                if (_currentJob != null)
                {
                    _configStore.EnsureJobLogPopulated(_currentJob);
                }
                _lastLoadedLength = 0;
                LoadCurrentLog(appendOnly: false);
            };

            btnOpenInNotepad = new Button
            {
                Text = Strings.OpenInNotepad,
                Size = new Size(95, 25),
                Margin = new Padding(0, 1, 4, 0),
                BackColor = Color.White,
                FlatStyle = FlatStyle.System
            };
            btnOpenInNotepad.Click += (s, e) => OpenActiveLogInNotepad();

            btnOpenFolder = new Button
            {
                Text = Strings.OpenLogFolder,
                Size = new Size(95, 25),
                Margin = new Padding(0, 1, 4, 0),
                BackColor = Color.White,
                FlatStyle = FlatStyle.System
            };
            btnOpenFolder.Click += (s, e) => OpenLogDirectoryInExplorer();

            btnClear = new Button
            {
                Text = Strings.ClearScreen,
                Size = new Size(68, 25),
                Margin = new Padding(0, 1, 8, 0),
                BackColor = Color.White,
                FlatStyle = FlatStyle.System
            };
            btnClear.Click += (s, e) => txtLog.Clear();

            chkAutoRefresh = new CheckBox
            {
                Text = Strings.AutoRefresh,
                AutoSize = true,
                Checked = true,
                Margin = new Padding(0, 4, 8, 0),
                Font = new Font("Segoe UI", 8.5F)
            };

            chkAutoScroll = new CheckBox
            {
                Text = Strings.AutoScroll,
                AutoSize = true,
                Checked = true,
                Margin = new Padding(0, 4, 0, 0),
                Font = new Font("Segoe UI", 8.5F)
            };

            flowRow2.Controls.AddRange(new Control[] {
                btnRefresh, btnOpenInNotepad, btnOpenFolder, btnClear,
                chkAutoRefresh, chkAutoScroll
            });

            pnlTop.Controls.Add(flowRow2);
            pnlTop.Controls.Add(tblRow1);

            // Center: RichTextBox for high performance log rendering (Light Theme)
            txtLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(36, 41, 47),
                Font = new Font("Consolas", 9.5F),
                ReadOnly = true,
                WordWrap = false,
                ScrollBars = RichTextBoxScrollBars.Both,
                BorderStyle = BorderStyle.None
            };

            // Bottom Status Strip
            statusStrip = new StatusStrip
            {
                BackColor = Color.FromArgb(246, 248, 250),
                ForeColor = Color.FromArgb(87, 96, 106)
            };
            lblStatusFile = new ToolStripStatusLabel { Text = Strings.IsVi ? "Tệp nhật ký: (Chưa chọn Job)" : "Log file: (No job selected)", Width = 340, TextAlign = ContentAlignment.MiddleLeft };
            lblStatusLines = new ToolStripStatusLabel { Text = Strings.IsVi ? "Hiển thị: 0 dòng" : "Lines: 0", Width = 130 };
            lblStatusSize = new ToolStripStatusLabel { Text = Strings.IsVi ? "Dung lượng: 0 B" : "Size: 0 B", Width = 130 };
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
                if (chkAutoRefresh.Checked && !string.IsNullOrEmpty(_activeFilePath) && File.Exists(_activeFilePath))
                {
                    LoadCurrentLog(appendOnly: true);
                }
            };
            timerAutoRefresh.Start();
        }

        public void SetCurrentJob(BackupJob job)
        {
            _currentJob = job;
            UpdateJobHeader();

            if (_currentJob != null)
            {
                _configStore.EnsureJobLogPopulated(_currentJob);
                _activeFilePath = _configStore.GetJobLogFilePath(_currentJob.Id);
            }
            else
            {
                _activeFilePath = null;
            }

            _lastLoadedLength = 0;
            LoadCurrentLog(appendOnly: false);
        }

        private void UpdateJobHeader()
        {
            if (_currentJob != null)
            {
                lblJobTitle.Text = $"📌 Job: {_currentJob.Name}";
                lblJobTitle.ForeColor = Color.FromArgb(9, 105, 218);
            }
            else
            {
                lblJobTitle.Text = Strings.IsVi ? "📌 Nhật ký: (Chưa chọn Job)" : "📌 Log: (No job selected)";
                lblJobTitle.ForeColor = Color.FromArgb(87, 96, 106);
            }
        }

        public void LoadJobFilterItems(AppConfig config = null)
        {
            if (config != null)
                _config = config;
        }

        public void SelectJob(string jobId)
        {
            var job = _config?.Jobs?.FirstOrDefault(j => string.Equals(j.Id, jobId, StringComparison.OrdinalIgnoreCase));
            SetCurrentJob(job);
        }

        public void NavigateToDateAndJob(DateTime date, string jobId)
        {
            SelectJob(jobId);
        }

        public void PopulateLogFileList()
        {
            if (_currentJob != null)
            {
                _configStore.EnsureJobLogPopulated(_currentJob);
            }
            _lastLoadedLength = 0;
            LoadCurrentLog(appendOnly: false);
        }

        private void LoadCurrentLog(bool appendOnly)
        {
            if (_isLoading) return;

            if (_currentJob == null)
            {
                if (!appendOnly)
                {
                    txtLog.Clear();
                    AppendColorText("--------------------------------------------------------------------------------" + Environment.NewLine, Color.FromArgb(208, 215, 222));
                    AppendColorText("  NOTEPAD++ JOB LOG VIEWER - FTPSYS PRO" + Environment.NewLine, Color.FromArgb(9, 105, 218));
                    AppendColorText("--------------------------------------------------------------------------------" + Environment.NewLine, Color.FromArgb(208, 215, 222));
                    AppendColorText(Strings.IsVi
                        ? "  [INFO] Vui lòng chọn một Job ở danh sách bên trái để xem nhật ký thực thi của Job đó." + Environment.NewLine
                        : "  [INFO] Please select a Job from the list on the left to view its execution logs." + Environment.NewLine,
                        Color.FromArgb(87, 96, 106));
                    AppendColorText("--------------------------------------------------------------------------------" + Environment.NewLine, Color.FromArgb(208, 215, 222));

                    lblStatusFile.Text = Strings.IsVi ? "Tệp: (Chưa chọn Job)" : "File: (No job selected)";
                    lblStatusLines.Text = Strings.IsVi ? "Hiển thị: 0 dòng" : "Lines: 0";
                    lblStatusSize.Text = Strings.IsVi ? "Dung lượng: 0 B" : "Size: 0 B";
                    lblStatusUpdated.Text = Strings.IsVi ? "Cập nhật: -" : "Updated: -";
                    lblMatchCount.Text = Strings.IsVi ? "0 dòng" : "0 entries";
                }
                return;
            }

            string logFilePath = _activeFilePath;
            if (string.IsNullOrEmpty(logFilePath) || !File.Exists(logFilePath))
            {
                if (!appendOnly)
                {
                    txtLog.Clear();
                    AppendColorText("--------------------------------------------------------------------------------" + Environment.NewLine, Color.FromArgb(208, 215, 222));
                    AppendColorText($"  NOTEPAD++ JOB LOG VIEWER - FTPSYS PRO" + Environment.NewLine, Color.FromArgb(9, 105, 218));
                    AppendColorText($"  Job: {_currentJob.Name} ({_currentJob.Id})" + Environment.NewLine, Color.FromArgb(36, 41, 47));
                    AppendColorText("--------------------------------------------------------------------------------" + Environment.NewLine, Color.FromArgb(208, 215, 222));
                    AppendColorText(Strings.IsVi
                        ? $"  [INFO] Job '{_currentJob.Name}' chưa có dữ liệu nhật ký nào." + Environment.NewLine
                        : $"  [INFO] Job '{_currentJob.Name}' has no log entries yet." + Environment.NewLine,
                        Color.FromArgb(154, 103, 0));
                    AppendColorText(Strings.IsVi
                        ? "  💡 Gợi ý: Bấm nút '▶ Chạy ngay' trên thanh công cụ để thực thi sao lưu lần đầu." + Environment.NewLine
                        : "  💡 Tip: Click '▶ Run' on the toolbar to execute the first backup." + Environment.NewLine,
                        Color.FromArgb(26, 127, 55));
                    AppendColorText("--------------------------------------------------------------------------------" + Environment.NewLine, Color.FromArgb(208, 215, 222));

                    lblStatusFile.Text = $"Job: {_currentJob.Name}";
                    lblStatusLines.Text = Strings.IsVi ? "Hiển thị: 0 dòng" : "Lines: 0";
                    lblStatusSize.Text = Strings.IsVi ? "Dung lượng: 0 B" : "Size: 0 B";
                    lblStatusUpdated.Text = Strings.IsVi ? $"Cập nhật: {DateTime.Now:HH:mm:ss}" : $"Updated: {DateTime.Now:HH:mm:ss}";
                    lblMatchCount.Text = Strings.IsVi ? "0 dòng" : "0 entries";
                }
                return;
            }

            _isLoading = true;

            try
            {
                var fileInfo = new FileInfo(logFilePath);
                long currentLength = fileInfo.Length;

                if (appendOnly && currentLength <= _lastLoadedLength)
                {
                    _isLoading = false;
                    return;
                }

                int filterLevelIndex = cboLevel.SelectedIndex;
                string keyword = (txtSearch.Text != Strings.SearchKeywords) ? txtSearch.Text.Trim() : null;

                using (var fs = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
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
                            AppendColorText("--------------------------------------------------------------------------------" + Environment.NewLine, Color.FromArgb(208, 215, 222));
                            AppendColorText($"  NOTEPAD++ JOB LOG VIEWER - FTPSYS PRO" + Environment.NewLine, Color.FromArgb(9, 105, 218));
                            AppendColorText($"  Job: {_currentJob.Name}  |  File: {Path.GetFileName(logFilePath)}" + Environment.NewLine, Color.FromArgb(36, 41, 47));
                            AppendColorText("--------------------------------------------------------------------------------" + Environment.NewLine, Color.FromArgb(208, 215, 222));
                            if (rawLineNumber > 0)
                            {
                                AppendColorText(Strings.IsVi
                                    ? $"  [INFO] Tệp có {rawLineNumber} dòng nhật ký, nhưng không có dòng nào khớp với mức độ hoặc từ khóa tìm kiếm." + Environment.NewLine
                                    : $"  [INFO] File has {rawLineNumber} lines, but none matched the level filter or search keyword." + Environment.NewLine,
                                    Color.FromArgb(154, 103, 0));
                                AppendColorText(Strings.IsVi
                                    ? "  💡 Gợi ý: Đặt 'Mức độ' về '-- Tất cả --' và xóa ô tìm kiếm để xem đầy đủ nội dung." + Environment.NewLine
                                    : "  💡 Tip: Set 'Level' to '-- All --' and clear search to view full logs." + Environment.NewLine,
                                    Color.FromArgb(26, 127, 55));
                            }
                            else
                            {
                                AppendColorText($"  [INFO] {Strings.NoLogsFound}" + Environment.NewLine, Color.FromArgb(87, 96, 106));
                            }
                            AppendColorText("--------------------------------------------------------------------------------" + Environment.NewLine, Color.FromArgb(208, 215, 222));
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
                        lblStatusFile.Text = $"Job: {_currentJob.Name} | {Path.GetFileName(logFilePath)}";
                        lblStatusLines.Text = Strings.IsVi ? $"Hiển thị: {totalDisplayedLines:N0} dòng" : $"Lines: {totalDisplayedLines:N0}";
                        lblStatusSize.Text = Strings.IsVi ? $"Dung lượng: {FormatBytes(currentLength)}" : $"Size: {FormatBytes(currentLength)}";
                        lblStatusUpdated.Text = Strings.IsVi ? $"Cập nhật: {_lastLoadedTime:HH:mm:ss}" : $"Updated: {_lastLoadedTime:HH:mm:ss}";
                        lblMatchCount.Text = Strings.IsVi ? $"{totalDisplayedLines:N0} dòng" : $"{totalDisplayedLines:N0} entries";
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

        private void AppendNotepadPlusPlusLine(int lineNumber, string line)
        {
            // 1. Notepad++ gutter line numbers: 0001 | (Steel Gray)
            AppendColorText($"{lineNumber:D4} | ", Color.FromArgb(110, 119, 129));

            if (string.IsNullOrWhiteSpace(line))
            {
                AppendColorText(Environment.NewLine, Color.FromArgb(36, 41, 47));
                return;
            }

            // 2. Notepad++ Serilog pattern coloring: "yyyy-MM-dd HH:mm:ss.fff +zz:zz [LVL] message"
            int bracketIdx = line.IndexOf('[');
            int bracketEnd = (bracketIdx >= 0) ? line.IndexOf(']', bracketIdx) : -1;

            if (bracketIdx > 0 && bracketEnd > bracketIdx && bracketEnd - bracketIdx <= 6)
            {
                // Timestamp (Slate Gray)
                string timestamp = line.Substring(0, bracketIdx);
                AppendColorText(timestamp, Color.FromArgb(87, 96, 106));

                // Level tag [INF], [WRN], [ERR]
                string levelTag = line.Substring(bracketIdx, bracketEnd - bracketIdx + 1);
                Color levelCol = Color.FromArgb(9, 105, 218); // Royal Blue
                if (levelTag.Contains("ERR") || levelTag.Contains("FTL"))
                    levelCol = Color.FromArgb(207, 34, 46); // Bold Crimson Red
                else if (levelTag.Contains("WRN"))
                    levelCol = Color.FromArgb(154, 103, 0); // Deep Amber
                else if (levelTag.Contains("DBG") || levelTag.Contains("VRB"))
                    levelCol = Color.FromArgb(110, 119, 129); // Slate Gray

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
                return Color.FromArgb(87, 96, 106);

            if (line.Contains("[ERR]") || line.Contains("[FTL]") ||
                line.IndexOf("Exception", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("Error", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("Thất bại", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("Failed", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Color.FromArgb(207, 34, 46); // Crimson Red
            }

            if (line.Contains("[WRN]") ||
                line.IndexOf("Warning", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("Cảnh báo", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Color.FromArgb(154, 103, 0); // Deep Amber
            }

            if (line.IndexOf("Thành công", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("Success", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("Completed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("Hoàn tất", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Color.FromArgb(26, 127, 55); // Forest Green
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
            string path = _activeFilePath;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                try
                {
                    Process.Start("notepad.exe", $"\"{path}\"");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Không thể mở Notepad: " + ex.Message, Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            else
            {
                MessageBox.Show(this, Strings.IsVi ? "Chưa có tệp nhật ký cho Job này." : "No log file found for this Job.", Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void OpenLogDirectoryInExplorer()
        {
            try
            {
                var jobsLogDir = _configStore.JobLogsDirectory;
                if (!Directory.Exists(jobsLogDir))
                    _configStore.EnsureDirectories();

                if (!string.IsNullOrEmpty(_activeFilePath) && File.Exists(_activeFilePath))
                {
                    Process.Start("explorer.exe", $"/select,\"{_activeFilePath}\"");
                }
                else
                {
                    Process.Start("explorer.exe", $"\"{jobsLogDir}\"");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Không thể mở thư mục Log: " + ex.Message, Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        public void UpdateUiLanguage()
        {
            UpdateJobHeader();
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

            if (_currentJob != null)
            {
                _configStore.EnsureJobLogPopulated(_currentJob);
            }
            _lastLoadedLength = 0;
            LoadCurrentLog(appendOnly: false);
        }

        public void CleanUp()
        {
            try
            {
                timerAutoRefresh?.Stop();
                timerAutoRefresh?.Dispose();
                timerAutoRefresh = null;

                _searchTimer?.Stop();
                _searchTimer?.Dispose();
                _searchTimer = null;
            }
            catch { }
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
