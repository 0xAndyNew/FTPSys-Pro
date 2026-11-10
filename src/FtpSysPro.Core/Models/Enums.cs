using System;

namespace FtpSysPro.Core.Models
{
    public enum BackupType
    {
        Full = 0,
        Incremental = 1
    }

    public enum DestinationType
    {
        LocalOrUnc = 0,
        Ftp = 1,
        Sftp = 2
    }

    public enum FtpEncryptionMode
    {
        None = 0,
        Explicit = 1,
        Implicit = 2
    }

    public enum CompressionLevel
    {
        None = 0,
        Fastest = 1,
        Normal = 5,
        Maximum = 9
    }

    public enum RetentionType
    {
        KeepFullCount = 0,
        KeepDays = 1,
        KeepAll = 2
    }

    public enum NotificationCondition
    {
        Always = 0,
        OnFailureOnly = 1,
        OnSuccessOnly = 2,
        Never = 3
    }

    public enum JobExecutionStatus
    {
        Idle = 0,
        Running = 1,
        Success = 2,
        Warning = 3,
        Failed = 4,
        Cancelled = 5
    }

    public enum ScheduleIntervalType
    {
        Once = 0,
        Daily = 1,
        Weekly = 2,
        Monthly = 3,
        RepeatEvery = 4
    }
}
