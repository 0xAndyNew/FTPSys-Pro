using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using FtpSysPro.Core.Models;
using FtpSysPro.Core.Resources;

namespace FtpSysPro.Manager.UI
{
    public class HistoryDetailDialog : Form
    {
        private readonly BackupRunResult _record;

        public HistoryDetailDialog(BackupRunResult record)
        {
            _record = record ?? new BackupRunResult();
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = Strings.HistoryDetailTitle + " - " + (_record.JobName ?? _record.JobId ?? "");
            Size = new Size(680, 520);
            MinimumSize = new Size(540, 400);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(248, 250, 252);
            Font = new Font("Segoe UI", 9F);
            ShowIcon = false;
            ShowInTaskbar = false;

            var pnlTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.FromArgb(242, 246, 251),
                Padding = new Padding(16, 10, 16, 10)
            };

            var lblHeader = new Label
            {
                Text = "⏰ " + (_record.JobName ?? Strings.DefaultNewJobName(1)) + " (" + (_record.BackupType == BackupType.Full ? Strings.ModeFull : Strings.ModeIncremental) + ")",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(12, 35, 75),
                Dock = DockStyle.Top,
                AutoSize = true
            };

            string statusText = _record.Status == JobExecutionStatus.Success ? Strings.StatusSuccess
                : _record.Status == JobExecutionStatus.Warning ? Strings.StatusWarning
                : _record.Status == JobExecutionStatus.Failed ? Strings.StatusFailed
                : _record.Status.ToString();

            Color statusColor = _record.Status == JobExecutionStatus.Success ? Color.FromArgb(30, 120, 50)
                : _record.Status == JobExecutionStatus.Warning ? Color.FromArgb(190, 120, 20)
                : Color.FromArgb(190, 30, 30);

            var lblSub = new Label
            {
                Text = $"{Strings.ColStatus}: {statusText}  •  {Strings.ColHistStartTime}: {_record.StartTimeUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}  •  {Strings.ColHistDuration}: {FormatDuration(_record.Duration)}",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = statusColor,
                Dock = DockStyle.Bottom,
                AutoSize = true
            };

            pnlTop.Controls.Add(lblSub);
            pnlTop.Controls.Add(lblHeader);

            var txtContent = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(18, 22, 30),
                ForeColor = Color.FromArgb(220, 225, 235),
                Font = new Font("Consolas", 9.5F),
                BorderStyle = BorderStyle.None
            };

            var sb = new StringBuilder();
            sb.AppendLine("================================================================================");
            sb.AppendLine($"  {Strings.HistoryDetailTitle.ToUpper()}");
            sb.AppendLine("================================================================================");
            sb.AppendLine($"  {Strings.PropTaskName,-28}: {_record.JobName ?? "-"}");
            sb.AppendLine($"  {Strings.PropTaskId,-28}: {_record.JobId ?? "-"}");
            sb.AppendLine($"  {"Run ID",-28}: {_record.RunId ?? "-"}");
            sb.AppendLine($"  {Strings.PropBackupType,-28}: {(_record.BackupType == BackupType.Full ? Strings.ModeFull : Strings.ModeIncremental)}");
            sb.AppendLine($"  {Strings.ColStatus,-28}: {statusText}");
            sb.AppendLine($"  {Strings.ColHistStartTime,-28}: {_record.StartTimeUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"  {"End Time",-28}: {_record.EndTimeUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"  {Strings.ColHistDuration,-28}: {FormatDuration(_record.Duration)}");
            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine($"  {"Scanned Files",-28}: {_record.ScannedFiles:N0} ({JobViewModel.FormatBytes(_record.ScannedBytes)})");
            sb.AppendLine($"  {Strings.ColHistFiles,-28}: {_record.BackedUpFiles:N0} ({JobViewModel.FormatBytes(_record.BackedUpBytes)})");
            sb.AppendLine($"  {"Deleted Files",-28}: {_record.DeletedFiles:N0}");
            sb.AppendLine($"  {Strings.ColHistErrors,-28}: {_record.ErrorCount} err / {_record.WarningCount} warn");
            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine($"  {Strings.ColHistMessage}:");
            sb.AppendLine($"  {_record.Message ?? "-"}");

            if (_record.Warnings != null && _record.Warnings.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("--------------------------------------------------------------------------------");
                sb.AppendLine($"  [WARNINGS ({_record.Warnings.Count})]:");
                foreach (var w in _record.Warnings)
                {
                    sb.AppendLine($"    • {w}");
                }
            }

            if (_record.Errors != null && _record.Errors.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("--------------------------------------------------------------------------------");
                sb.AppendLine($"  [ERRORS ({_record.Errors.Count})]:");
                foreach (var err in _record.Errors)
                {
                    sb.AppendLine($"    • {err}");
                }
            }

            sb.AppendLine("================================================================================");
            txtContent.Text = sb.ToString();

            var pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                BackColor = Color.FromArgb(242, 246, 251),
                Padding = new Padding(12, 8, 12, 8)
            };

            var btnCopy = new Button
            {
                Text = "📋 " + Strings.CopyToClipboard,
                Size = new Size(160, 30),
                Location = new Point(12, 9),
                BackColor = Color.White,
                FlatStyle = FlatStyle.System
            };
            btnCopy.Click += (s, e) =>
            {
                try
                {
                    Clipboard.SetText(txtContent.Text);
                    MessageBox.Show(this, Strings.CopiedToClipboard, Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch { }
            };

            var btnClose = new Button
            {
                Text = Strings.Close,
                Size = new Size(90, 30),
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                Location = new Point(pnlBottom.Width - 105, 9),
                BackColor = Color.White,
                FlatStyle = FlatStyle.System,
                DialogResult = DialogResult.OK
            };

            pnlBottom.Controls.Add(btnCopy);
            pnlBottom.Controls.Add(btnClose);
            AcceptButton = btnClose;

            Controls.Add(pnlTop);
            Controls.Add(pnlBottom);
            Controls.Add(txtContent);
        }

        private static string FormatDuration(TimeSpan d)
        {
            if (d <= TimeSpan.Zero) return "-";
            if (d.TotalHours >= 1) return $"{(int)d.TotalHours}h {d.Minutes}m {d.Seconds}s";
            if (d.TotalMinutes >= 1) return $"{d.Minutes}m {d.Seconds}s";
            return $"{d.Seconds}s";
        }
    }
}
