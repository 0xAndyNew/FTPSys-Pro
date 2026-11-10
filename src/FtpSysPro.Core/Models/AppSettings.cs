using System;
using System.Collections.Generic;

namespace FtpSysPro.Core.Models
{
    public class AppSettings
    {
        public string Language { get; set; } = "vi"; // "vi" or "en"
        public int MaxConcurrentJobs { get; set; } = 2;
        public string TempDirectory { get; set; } = string.Empty; // empty = ProgramData/FtpSysPro/temp
        public int KeepLogsDays { get; set; } = 30;
        public int KeepHistoryRecords { get; set; } = 500;
        public SmtpSettings Smtp { get; set; } = new SmtpSettings();
        public bool StartMinimizedToTray { get; set; } = true;
        public bool MinimizeToTrayOnClose { get; set; } = true;
        public bool ShowNotificationsOnSuccess { get; set; } = true;
        public bool ShowNotificationsOnError { get; set; } = true;
    }

    public class SmtpSettings
    {
        public bool Enabled { get; set; } = false;
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 587;
        public bool UseSsl { get; set; } = false;
        public bool UseStartTls { get; set; } = true;
        public string Username { get; set; } = string.Empty;
        public string PasswordProtected { get; set; } = string.Empty;
        public string FromAddress { get; set; } = string.Empty;
        public string FromDisplayName { get; set; } = "FtpSysPro Backup";
    }

    public class AppConfig
    {
        public int Version { get; set; } = 1;
        public AppSettings Settings { get; set; } = new AppSettings();
        public List<BackupJob> Jobs { get; set; } = new List<BackupJob>();
    }
}
