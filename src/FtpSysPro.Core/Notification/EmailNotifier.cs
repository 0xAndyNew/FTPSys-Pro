using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FtpSysPro.Core.Config;
using FtpSysPro.Core.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace FtpSysPro.Core.Notification
{
    public class EmailNotifier
    {
        private readonly SmtpSettings _settings;

        public EmailNotifier(SmtpSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public async Task<bool> SendTestEmailAsync(string recipientEmail, CancellationToken cancellationToken = default)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.FromDisplayName, _settings.FromAddress));
            message.To.Add(MailboxAddress.Parse(recipientEmail));
            message.Subject = "[FtpSysPro] Test Email / Email thử nghiệm";

            var body = new BodyBuilder
            {
                HtmlBody = @"
<h2>FtpSysPro - Kiểm tra cấu hình Email thành công</h2>
<p>Email này được gửi từ ứng dụng <b>FtpSysPro</b> để xác nhận cấu hình máy chủ SMTP hoạt động chính xác.</p>
<hr/>
<h2>FtpSysPro - Email Configuration Test Succeeded</h2>
<p>This email was sent from <b>FtpSysPro</b> to confirm your SMTP server configuration is working properly.</p>"
            };

            message.Body = body.ToMessageBody();

            return await SendMessageAsync(message, cancellationToken);
        }

        public async Task<bool> SendJobNotificationAsync(
            BackupJob job,
            BackupRunResult result,
            CancellationToken cancellationToken = default)
        {
            if (!_settings.Enabled) return false;
            if (job.Notification == null || !job.Notification.Enabled) return false;

            // Check condition
            var cond = job.Notification.Condition;
            if (cond == NotificationCondition.Never) return false;
            if (cond == NotificationCondition.OnFailureOnly && result.Status != JobExecutionStatus.Failed) return false;
            if (cond == NotificationCondition.OnSuccessOnly && result.Status != JobExecutionStatus.Success && result.Status != JobExecutionStatus.Warning) return false;

            if (job.Notification.Recipients == null || job.Notification.Recipients.Count == 0)
                return false;

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.FromDisplayName, _settings.FromAddress));

            foreach (var r in job.Notification.Recipients)
            {
                if (!string.IsNullOrWhiteSpace(r))
                {
                    try { message.To.Add(MailboxAddress.Parse(r.Trim())); } catch { }
                }
            }

            if (message.To.Count == 0) return false;

            var prefix = string.IsNullOrEmpty(job.Notification.CustomSubjectPrefix)
                ? "[FtpSysPro]"
                : job.Notification.CustomSubjectPrefix.Trim();

            var statusText = result.Status == JobExecutionStatus.Success ? "SUCCESS / THÀNH CÔNG"
                : result.Status == JobExecutionStatus.Warning ? "WARNING / CẢNH BÁO"
                : "FAILED / THẤT BÀI";

            message.Subject = $"{prefix} [{statusText}] {job.Name} ({result.BackupType})";

            var html = BuildHtmlReport(job, result);
            var body = new BodyBuilder { HtmlBody = html };
            message.Body = body.ToMessageBody();

            return await SendMessageAsync(message, cancellationToken);
        }

        private async Task<bool> SendMessageAsync(MimeMessage message, CancellationToken cancellationToken)
        {
            using (var client = new SmtpClient())
            {
                SecureSocketOptions socketOptions;
                if (_settings.UseSsl)
                {
                    socketOptions = SecureSocketOptions.SslOnConnect;
                }
                else if (_settings.UseStartTls)
                {
                    socketOptions = SecureSocketOptions.StartTlsWhenAvailable;
                }
                else
                {
                    socketOptions = SecureSocketOptions.None;
                }

                await client.ConnectAsync(_settings.Host, _settings.Port, socketOptions, cancellationToken);

                if (!string.IsNullOrEmpty(_settings.Username))
                {
                    var plainPass = SecretProtector.Unprotect(_settings.PasswordProtected);
                    await client.AuthenticateAsync(_settings.Username, plainPass, cancellationToken);
                }

                await client.SendAsync(message, cancellationToken);
                await client.DisconnectAsync(true, cancellationToken);
                return true;
            }
        }

        private static string BuildHtmlReport(BackupJob job, BackupRunResult result)
        {
            var statusColor = result.Status == JobExecutionStatus.Success ? "#28a745"
                : result.Status == JobExecutionStatus.Warning ? "#ffc107"
                : "#dc3545";

            var sb = new StringBuilder();
            sb.Append("<html><body style='font-family: Arial, sans-serif; color: #333; line-height: 1.5;'>");
            sb.Append($"<div style='background-color: {statusColor}; color: white; padding: 15px; border-radius: 4px;'>");
            sb.Append($"<h2 style='margin: 0;'>FtpSysPro Backup Report: {job.Name}</h2>");
            sb.Append($"<p style='margin: 5px 0 0 0;'>Trạng thái / Status: <b>{result.Status}</b></p>");
            sb.Append("</div>");

            sb.Append("<table style='width: 100%; border-collapse: collapse; margin-top: 20px;'>");
            AddRow(sb, "Tác vụ / Job", job.Name);
            AddRow(sb, "Loại sao lưu / Type", result.BackupType.ToString());
            AddRow(sb, "Bắt đầu / Start Time (UTC)", result.StartTimeUtc.ToString("yyyy-MM-dd HH:mm:ss"));
            AddRow(sb, "Kết thúc / End Time (UTC)", result.EndTimeUtc.ToString("yyyy-MM-dd HH:mm:ss"));
            AddRow(sb, "Thời lượng / Duration", result.Duration.ToString(@"hh\:mm\:ss"));
            AddRow(sb, "Số file quét / Scanned Files", $"{result.ScannedFiles:N0} ({FormatBytes(result.ScannedBytes)})");
            AddRow(sb, "Số file sao lưu / Backed Up Files", $"{result.BackedUpFiles:N0} ({FormatBytes(result.BackedUpBytes)})");
            AddRow(sb, "Bộ sao lưu / Backup Set", result.BackupSetName);
            AddRow(sb, "Thông điệp / Message", result.Message);
            sb.Append("</table>");

            if (result.Warnings.Count > 0)
            {
                sb.Append("<h3 style='color: #856404; margin-top: 20px;'>Cảnh báo / Warnings:</h3><ul>");
                foreach (var w in result.Warnings)
                {
                    sb.Append($"<li>{System.Net.WebUtility.HtmlEncode(w)}</li>");
                }
                sb.Append("</ul>");
            }

            if (result.Errors.Count > 0)
            {
                sb.Append("<h3 style='color: #721c24; margin-top: 20px;'>Lỗi / Errors:</h3><ul>");
                foreach (var e in result.Errors)
                {
                    sb.Append($"<li><pre style='background:#f8d7da; padding: 5px;'>{System.Net.WebUtility.HtmlEncode(e)}</pre></li>");
                }
                sb.Append("</ul>");
            }

            sb.Append("<hr style='margin-top: 30px;'/>");
            sb.Append("<p style='font-size: 12px; color: #777;'>Email được gửi tự động bởi FtpSysPro Windows Backup Service.</p>");
            sb.Append("</body></html>");

            return sb.ToString();
        }

        private static void AddRow(StringBuilder sb, string label, string value)
        {
            sb.Append("<tr style='border-bottom: 1px solid #ddd;'>");
            sb.Append($"<td style='padding: 8px; font-weight: bold; width: 35%; background: #f8f9fa;'>{label}</td>");
            sb.Append($"<td style='padding: 8px;'>{System.Net.WebUtility.HtmlEncode(value ?? string.Empty)}</td>");
            sb.Append("</tr>");
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
