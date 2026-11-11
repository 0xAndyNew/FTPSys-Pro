using System;
using System.Collections.Generic;

namespace FtpSysPro.Core.Models
{
    public class BackupJob
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public BackupType BackupType { get; set; } = BackupType.Incremental;

        public List<string> SourceDirectories { get; set; } = new List<string>();
        public List<string> ExcludePatterns { get; set; } = new List<string> { "*.tmp", "~$*", "Thumbs.db", "desktop.ini" };

        public DestinationConfig Destination { get; set; } = new DestinationConfig();
        public CompressionOptions Compression { get; set; } = new CompressionOptions();
        public IntegrityOptions Integrity { get; set; } = new IntegrityOptions();
        public List<ScheduleDefinition> Schedules { get; set; } = new List<ScheduleDefinition>();
        public RetentionPolicy Retention { get; set; } = new RetentionPolicy();
        public NotificationOptions Notification { get; set; } = new NotificationOptions();

        public long SourceSizeBytes { get; set; }
        public int SourceFileCount { get; set; }
        public DateTime? LastRunUtc { get; set; }
        public JobExecutionStatus LastStatus { get; set; } = JobExecutionStatus.Idle;
        public string LastMessage { get; set; } = string.Empty;
        public long LastSizeBytes { get; set; }
        public int LastFileCount { get; set; }
        public DateTime? NextRunUtc { get; set; }

        public BackupJob Clone()
        {
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(this);
            var copy = Newtonsoft.Json.JsonConvert.DeserializeObject<BackupJob>(json);
            copy.Id = Guid.NewGuid().ToString("N");
            copy.Name = Name + " - Copy";
            copy.LastRunUtc = null;
            copy.LastStatus = JobExecutionStatus.Idle;
            copy.LastMessage = string.Empty;
            copy.NextRunUtc = null;
            return copy;
        }
    }

    public class DestinationConfig
    {
        public DestinationType Type { get; set; } = DestinationType.LocalOrUnc;

        // Local / UNC
        public string LocalPath { get; set; } = string.Empty;
        public string UncUsername { get; set; } = string.Empty;
        public string UncPasswordProtected { get; set; } = string.Empty;

        // FTP / FTPS
        public string FtpHost { get; set; } = string.Empty;
        public int FtpPort { get; set; } = 21;
        public string FtpUsername { get; set; } = string.Empty;
        public string FtpPasswordProtected { get; set; } = string.Empty;
        public string FtpRemotePath { get; set; } = "/";
        public FtpEncryptionMode FtpEncryption { get; set; } = FtpEncryptionMode.None;
        public bool FtpPassiveMode { get; set; } = true;
        public bool FtpValidateCertificate { get; set; } = true;

        // SFTP
        public string SftpHost { get; set; } = string.Empty;
        public int SftpPort { get; set; } = 22;
        public string SftpUsername { get; set; } = string.Empty;
        public string SftpPasswordProtected { get; set; } = string.Empty;
        public string SftpPrivateKeyPath { get; set; } = string.Empty;
        public string SftpPrivateKeyPassphraseProtected { get; set; } = string.Empty;
        public string SftpRemotePath { get; set; } = "/";
        public string SftpTrustedFingerprint { get; set; } = string.Empty;
    }

    public class CompressionOptions
    {
        public bool Enabled { get; set; } = false; // Tùy chọn: false = đẩy nguyên thư mục sang; true = nén file .zip
        public CompressionLevel Level { get; set; } = CompressionLevel.Normal;
        public bool EncryptWithPassword { get; set; } = false;
        public string PasswordProtected { get; set; } = string.Empty;
    }

    public class IntegrityOptions
    {
        public bool EnableVss { get; set; } = true;
        public bool CheckFreeDiskSpace { get; set; } = true;
        public bool VerifySha256Checksum { get; set; } = true;
        public int NetworkRetryCount { get; set; } = 3;
    }

    public class ScheduleDefinition
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public BackupType BackupType { get; set; } = BackupType.Incremental;
        public ScheduleIntervalType IntervalType { get; set; } = ScheduleIntervalType.Daily;

        // Local time: "HH:mm" (e.g. "02:00")
        public string TimeOfDay { get; set; } = "02:00";

        // Weekly: Days of week (0=Sunday, 6=Saturday)
        public List<int> DaysOfWeek { get; set; } = new List<int> { 1, 2, 3, 4, 5 };

        // Monthly: Day of month (1-31)
        public int DayOfMonth { get; set; } = 1;

        // RepeatEvery: minutes
        public int RepeatIntervalMinutes { get; set; } = 60;
        public string RepeatStartTime { get; set; } = "08:00";
        public string RepeatEndTime { get; set; } = "18:00";

        // One-time run
        public DateTime? RunOnceLocalTime { get; set; }

        public bool MissedRunCatchUp { get; set; } = true;
    }

    public class RetentionPolicy
    {
        public RetentionType Type { get; set; } = RetentionType.KeepFullCount;
        public int KeepFullCount { get; set; } = 5;
        public int KeepDays { get; set; } = 30;
    }

    public class NotificationOptions
    {
        public bool Enabled { get; set; } = false;
        public NotificationCondition Condition { get; set; } = NotificationCondition.Always;
        public List<string> Recipients { get; set; } = new List<string>();
        public string CustomSubjectPrefix { get; set; } = "[FtpSysPro]";
    }
}
