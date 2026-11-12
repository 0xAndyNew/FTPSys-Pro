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
        private TableLayoutPanel tblRow1;
        private FlowLayoutPanel flowRow2;
        private Label lblJobTitle;
        private Label lblLvl;
        private ComboBox cboLevel;
        private TextBox txtSearch;

        private Button btnRefresh;
        private Button btnOpenInNotepad;
        private Button btnOpenFolder;
        private Button btnClear;
        private CheckBox chkWordWrap;
        private CheckBox chkAutoRefresh;
        private CheckBox chkAutoScroll;
        private Label lblMatchCount;

        private RichTextBox txtLog;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel lblStatusFile;
        private ToolStripStatusLabel lblStatusLines;
        private ToolStripStatusLabel lblStatusSize;
        private ToolStripStatusLabel lblStatusEncoding;
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
                Height = 64,
                BackColor = Color.FromArgb(248, 250, 252),
                Padding = new Padding(8, 6, 8, 4)
            };
            pnlTop.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(226, 232, 240), 1))
                {
                    e.Graphics.DrawLine(pen, 0, pnlTop.Height - 1, pnlTop.Width, pnlTop.Height - 1);
                }
            };

            // Tier 1: Responsive TableLayoutPanel
            tblRow1 = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 26,
                ColumnCount = 5,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            tblRow1.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Job Title Pill Badge
            tblRow1.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Filter label
            tblRow1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95F)); // Level Dropdown
            tblRow1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F)); // Search box (responsive stretch)
            tblRow1.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Match count label
            tblRow1.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            lblJobTitle = new Label
            {
                Text = Strings.IsVi ? "📌 Chưa chọn Job" : "📌 No job selected",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 116, 139),
                BackColor = Color.FromArgb(241, 245, 249),
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Padding = new Padding(6, 3, 6, 3),
                Margin = new Padding(0, 0, 6, 0)
            };

            lblLvl = new Label
            {
                Text = Strings.FilterLevel,
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 116, 139),
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 0, 4, 0)
            };

            cboLevel = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 8.5F),
                BackColor = Color.White,
                Margin = new Padding(0, 0, 6, 0)
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
                ForeColor = Color.FromArgb(148, 163, 184),
                Text = "🔍 " + Strings.SearchKeywords,
                Margin = new Padding(0, 0, 6, 0),
                BorderStyle = BorderStyle.FixedSingle
            };
            txtSearch.GotFocus += (s, e) =>
            {
                if (txtSearch.Text == "🔍 " + Strings.SearchKeywords || txtSearch.Text == Strings.SearchKeywords)
                {
                    txtSearch.Text = "";
                    txtSearch.ForeColor = Color.FromArgb(30, 41, 59);
                }
            };
            txtSearch.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtSearch.Text))
                {
                    txtSearch.Text = "🔍 " + Strings.SearchKeywords;
                    txtSearch.ForeColor = Color.FromArgb(148, 163, 184);
                }
            };
            txtSearch.TextChanged += (s, e) =>
            {
                if (txtSearch.Text != "🔍 " + Strings.SearchKeywords && txtSearch.Text != Strings.SearchKeywords)
                {
                    _searchTimer?.Stop();
                    _searchTimer?.Start();
                }
            };

            lblMatchCount = new Label
            {
                Text = Strings.IsVi ? "0 dòng" : "0 lines",
                AutoSize = true,
                Anchor = AnchorStyles.Right,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 116, 139),
                BackColor = Color.FromArgb(241, 245, 249),
                Padding = new Padding(6, 3, 6, 3),
                Margin = new Padding(0)
            };

            tblRow1.Controls.Add(lblJobTitle, 0, 0);
            tblRow1.Controls.Add(lblLvl, 1, 0);
            tblRow1.Controls.Add(cboLevel, 2, 0);
            tblRow1.Controls.Add(txtSearch, 3, 0);
            tblRow1.Controls.Add(lblMatchCount, 4, 0);

            // Tier 2: FlowLayoutPanel for actions and auto-update toggles (WrapContents enabled for responsive flow)
            flowRow2 = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 26,
                WrapContents = true,
                AutoScroll = false,
                Margin = new Padding(0, 3, 0, 0),
                Padding = new Padding(0)
            };

            btnRefresh = CreateModernButton("↻ " + (Strings.IsVi ? "Làm mới" : "Refresh"), 72, (s, e) =>
            {
                if (_currentJob != null)
                {
                    _configStore.EnsureJobLogPopulated(_currentJob);
                }
                _lastLoadedLength = 0;
                LoadCurrentLog(appendOnly: false);
            });

            btnOpenInNotepad = CreateModernButton("📝 Notepad", 76, (s, e) => OpenActiveLogInNotepad());

            btnOpenFolder = CreateModernButton("📁 " + (Strings.IsVi ? "Thư mục" : "Folder"), 72, (s, e) => OpenLogDirectoryInExplorer());

            btnClear = CreateModernButton("🗑 " + (Strings.IsVi ? "Xóa" : "Clear"), 54, (s, e) => txtLog.Clear());

            chkWordWrap = CreateModernCheckBox("↵ " + (Strings.IsVi ? "Xuống dòng" : "Word wrap"), true, (s, e) =>
            {
                txtLog.WordWrap = chkWordWrap.Checked;
                txtLog.ScrollBars = chkWordWrap.Checked ? RichTextBoxScrollBars.Vertical : RichTextBoxScrollBars.Both;
            });

            chkAutoScroll = CreateModernCheckBox("↓ " + (Strings.IsVi ? "Cuộn" : "Scroll"), true);

            chkAutoRefresh = CreateModernCheckBox("⚡ " + (Strings.IsVi ? "Tự làm mới (3s)" : "Auto (3s)"), true);

            flowRow2.Controls.AddRange(new Control[] {
                btnRefresh, btnOpenInNotepad, btnOpenFolder, btnClear,
                chkWordWrap, chkAutoScroll, chkAutoRefresh
            });

            pnlTop.Controls.Add(flowRow2);
            pnlTop.Controls.Add(tblRow1);

            // Center: RichTextBox for authentic Notepad++ log rendering (Light Theme with WordWrap enabled)
            txtLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(30, 41, 59),
                Font = new Font("Consolas", 9.75F),
                ReadOnly = true,
                WordWrap = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                BorderStyle = BorderStyle.None
            };
            txtLog.HandleCreated += (s, e) => SetRichTextBoxMargins(txtLog, 6, 6);

            // Bottom Status Strip (Notepad++ Status Style)
            statusStrip = new StatusStrip
            {
                BackColor = Color.FromArgb(248, 250, 252),
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = new Font("Segoe UI", 8.5F),
                SizingGrip = false
            };
            lblStatusFile = new ToolStripStatusLabel { Text = Strings.IsVi ? "Tệp: (Chưa chọn Job)" : "File: (No job selected)", Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            lblStatusLines = new ToolStripStatusLabel { Text = Strings.IsVi ? "0 dòng" : "0 lines", AutoSize = true };
            lblStatusSize = new ToolStripStatusLabel { Text = "0 B", AutoSize = true };
            lblStatusEncoding = new ToolStripStatusLabel { Text = "UTF-8", AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139) };
            lblStatusUpdated = new ToolStripStatusLabel { Text = Strings.IsVi ? "Cập nhật: -" : "Updated: -", AutoSize = true };

            statusStrip.Items.AddRange(new ToolStripItem[] {
                lblStatusFile, lblStatusLines, lblStatusSize, lblStatusEncoding, lblStatusUpdated
            });

            Controls.Add(pnlTop);
            Controls.Add(statusStrip);
            Controls.Add(txtLog);
            txtLog.BringToFront();

            // Responsive auto-fit listeners for all window and panel dimensions
            pnlTop.SizeChanged += (s, e) => UpdateTopPanelLayout();
            this.SizeChanged += (s, e) => UpdateTopPanelLayout();
            this.Layout += (s, e) => UpdateTopPanelLayout();

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

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp);
        private const int EM_SETMARGINS = 0xd3;
        private const int EC_LEFTMARGIN = 0x1;
        private const int EC_RIGHTMARGIN = 0x2;

        private static void SetRichTextBoxMargins(RichTextBox rtb, int left, int right)
        {
            try
            {
                if (rtb.IsHandleCreated)
                {
                    SendMessage(rtb.Handle, EM_SETMARGINS, (IntPtr)(EC_LEFTMARGIN | EC_RIGHTMARGIN), (IntPtr)((right << 16) | left));
                }
            }
            catch { }
        }

        private void UpdateTopPanelLayout()
        {
            if (pnlTop == null || tblRow1 == null || flowRow2 == null) return;

            pnlTop.SuspendLayout();

            int availableWidth = pnlTop.ClientSize.Width - pnlTop.Padding.Horizontal;

            // 1. Compact / adapt Tier 1 controls based on width
            if (availableWidth < 460)
            {
                lblLvl.Visible = false;
                cboLevel.Width = 78;
                lblMatchCount.Visible = availableWidth > 320;
            }
            else
            {
                lblLvl.Visible = true;
                cboLevel.Width = 95;
                lblMatchCount.Visible = true;
            }

            // Compact job badge if very tight
            if (_currentJob != null)
            {
                int maxLen = availableWidth < 420 ? 14 : (availableWidth < 600 ? 22 : 40);
                string jName = _currentJob.Name ?? _currentJob.Id ?? "Job";
                if (jName.Length > maxLen)
                    jName = jName.Substring(0, maxLen - 2) + "..";
                lblJobTitle.Text = $"📌 {jName}";
            }

            // 2. Responsive wrapping of Tier 2 (FlowLayoutPanel)
            int flowWidth = Math.Max(120, availableWidth);
            Size preferred = flowRow2.GetPreferredSize(new Size(flowWidth, 0));
            int flowHeight = Math.Max(26, preferred.Height);
            flowRow2.Height = flowHeight;

            int neededHeight = tblRow1.Height + flowHeight + pnlTop.Padding.Vertical + 6;
            if (pnlTop.Height != neededHeight && neededHeight >= 56)
            {
                pnlTop.Height = neededHeight;
            }

            pnlTop.ResumeLayout(true);
        }

        private static Button CreateModernButton(string text, int width, EventHandler onClick)
        {
            var btn = new Button
            {
                Text = text,
                Width = width,
                Height = 24,
                Margin = new Padding(0, 1, 6, 1),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(30, 41, 59),
                Font = new Font("Segoe UI", 8.5F),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(241, 245, 249);
            btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(226, 232, 240);
            if (onClick != null) btn.Click += onClick;
            return btn;
        }

        private static CheckBox CreateModernCheckBox(string text, bool isChecked, EventHandler onCheckChanged = null)
        {
            var chk = new CheckBox
            {
                Text = text,
                AutoSize = true,
                Checked = isChecked,
                Margin = new Padding(0, 3, 10, 0),
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(51, 65, 85),
                Cursor = Cursors.Hand
            };
            if (onCheckChanged != null) chk.CheckedChanged += onCheckChanged;
            return chk;
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
                lblJobTitle.Text = $"📌 {_currentJob.Name}";
                lblJobTitle.ForeColor = Color.FromArgb(9, 105, 218);
                lblJobTitle.BackColor = Color.FromArgb(221, 244, 255);
            }
            else
            {
                lblJobTitle.Text = Strings.IsVi ? "📌 Chưa chọn Job" : "📌 No job selected";
                lblJobTitle.ForeColor = Color.FromArgb(100, 116, 139);
                lblJobTitle.BackColor = Color.FromArgb(241, 245, 249);
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
                string rawKeyword = txtSearch.Text?.Trim();
                string keyword = (!string.IsNullOrWhiteSpace(rawKeyword) &&
                                  rawKeyword != Strings.SearchKeywords &&
                                  rawKeyword != "🔍 " + Strings.SearchKeywords &&
                                  rawKeyword != "🔍") ? rawKeyword : null;

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
                            AppendNotepadPlusPlusLine(matchedLinesCount, line, keyword);
                        }

                        if (matchedLinesCount == 0 && !appendOnly)
                        {
                            txtLog.Clear();
                            AppendColorText("--------------------------------------------------------------------------------" + Environment.NewLine, Color.FromArgb(208, 215, 222));
                            AppendColorText($"  NOTEPAD++ JOB LOG VIEWER - FTPSYS PRO" + Environment.NewLine, Color.FromArgb(9, 105, 218));
                            AppendColorText($"  Job: {_currentJob.Name}  |  File: {Path.GetFileName(logFilePath)}  |  Format: UTF-8" + Environment.NewLine, Color.FromArgb(36, 41, 47));
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

        private void AppendNotepadPlusPlusLine(int lineNumber, string line, string keyword = null)
        {
            // 1. Notepad++ gutter line numbers: Right-aligned padded number (128,128,128) + separator (203,213,225)
            AppendColorText($"{lineNumber,4} ", Color.FromArgb(128, 128, 128));
            AppendColorText("| ", Color.FromArgb(203, 213, 225));

            if (string.IsNullOrWhiteSpace(line))
            {
                AppendColorText(Environment.NewLine, Color.FromArgb(30, 41, 59));
                return;
            }

            // 2. Notepad++ Serilog pattern coloring: "yyyy-MM-dd HH:mm:ss.fff +zz:zz [LVL] message"
            int bracketIdx = line.IndexOf('[');
            int bracketEnd = (bracketIdx >= 0) ? line.IndexOf(']', bracketIdx) : -1;

            if (bracketIdx > 0 && bracketEnd > bracketIdx && bracketEnd - bracketIdx <= 6)
            {
                // Compact timestamp: strip .fff milliseconds and +zz:zz timezone clutter
                string rawTimestamp = line.Substring(0, bracketIdx).Trim();
                string cleanTimestamp = rawTimestamp;
                if (rawTimestamp.Length >= 19 && DateTime.TryParse(rawTimestamp.Substring(0, 19), out var dt))
                {
                    cleanTimestamp = dt.ToString("yyyy-MM-dd HH:mm:ss");
                }
                AppendColorText(cleanTimestamp + " ", Color.FromArgb(92, 99, 112));

                // Level tag [INF], [WRN], [ERR]
                string levelTag = line.Substring(bracketIdx, bracketEnd - bracketIdx + 1);
                Color levelCol = Color.FromArgb(9, 105, 218); // Royal Blue
                if (levelTag.Contains("ERR") || levelTag.Contains("FTL"))
                    levelCol = Color.FromArgb(207, 34, 46); // Bold Crimson Red
                else if (levelTag.Contains("WRN"))
                    levelCol = Color.FromArgb(180, 83, 9); // Amber / Warning
                else if (levelTag.Contains("DBG") || levelTag.Contains("VRB"))
                    levelCol = Color.FromArgb(100, 116, 139); // Slate Gray

                AppendColorText(levelTag + " ", levelCol);

                // Remainder message
                string message = line.Substring(bracketEnd + 1).TrimStart();

                // Strip redundant [Job 'Name'] prefix if this log is already dedicated to the selected job
                if (_currentJob != null)
                {
                    string jobPrefix = $"[Job '{_currentJob.Name}']";
                    if (message.StartsWith(jobPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        message = message.Substring(jobPrefix.Length).TrimStart();
                    }
                }

                Color msgCol = DetermineLineColor(line);
                AppendMessageWithHighlight(message, msgCol, keyword);
            }
            else
            {
                Color col = DetermineLineColor(line);
                AppendMessageWithHighlight(line, col, keyword);
            }
        }

        private void AppendMessageWithHighlight(string message, Color defaultColor, string keyword)
        {
            if (string.IsNullOrEmpty(keyword))
            {
                AppendColorText(message + Environment.NewLine, defaultColor);
                return;
            }

            int startIdx = 0;
            while (startIdx < message.Length)
            {
                int matchIdx = message.IndexOf(keyword, startIdx, StringComparison.OrdinalIgnoreCase);
                if (matchIdx < 0)
                {
                    AppendColorText(message.Substring(startIdx), defaultColor);
                    break;
                }

                if (matchIdx > startIdx)
                {
                    AppendColorText(message.Substring(startIdx, matchIdx - startIdx), defaultColor);
                }

                // Highlight match with authentic Notepad++ yellow marker
                string matchedWord = message.Substring(matchIdx, keyword.Length);
                AppendHighlightText(matchedWord, Color.FromArgb(15, 23, 42), Color.FromArgb(254, 240, 138));

                startIdx = matchIdx + keyword.Length;
            }

            AppendColorText(Environment.NewLine, defaultColor);
        }

        private void AppendHighlightText(string text, Color foreColor, Color backColor)
        {
            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.SelectionLength = 0;
            txtLog.SelectionColor = foreColor;
            txtLog.SelectionBackColor = backColor;
            txtLog.AppendText(text);
            txtLog.SelectionBackColor = Color.White;
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
            txtLog.SelectionBackColor = Color.White;
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

            btnRefresh.Text = "↻ " + (Strings.IsVi ? "Làm mới" : "Refresh");
            btnOpenInNotepad.Text = "📝 Notepad";
            btnOpenFolder.Text = "📁 " + (Strings.IsVi ? "Thư mục" : "Folder");
            btnClear.Text = "🗑 " + (Strings.IsVi ? "Xóa" : "Clear");
            chkWordWrap.Text = "↵ " + (Strings.IsVi ? "Xuống dòng" : "Word wrap");
            chkAutoScroll.Text = "↓ " + (Strings.IsVi ? "Cuộn" : "Scroll");
            chkAutoRefresh.Text = "⚡ " + (Strings.IsVi ? "Tự làm mới (3s)" : "Auto (3s)");

            UpdateTopPanelLayout();

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
