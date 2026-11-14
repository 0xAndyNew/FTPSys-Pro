using System;
using System.Collections.Generic;

namespace FtpSysPro.Core.Models
{
    public class BackupRunResult
    {
        public string RunId { get; set; } = Guid.NewGuid().ToString("N");
        public string JobId { get; set; } = string.Empty;
        public string JobName { get; set; } = string.Empty;
        public BackupType BackupType { get; set; }
        public DateTime StartTimeUtc { get; set; }
        public DateTime EndTimeUtc { get; set; }
        public JobExecutionStatus Status { get; set; }
        public string Message { get; set; } = string.Empty;

        public int ScannedFiles { get; set; }
        public long ScannedBytes { get; set; }
        public int BackedUpFiles { get; set; }
        public long BackedUpBytes { get; set; }
        public int DeletedFiles { get; set; }

        private int _warningCount;
        private int _errorCount;

        public int WarningCount
        {
            get => Math.Max(_warningCount, Warnings?.Count ?? 0);
            set => _warningCount = value;
        }

        public int ErrorCount
        {
            get => Math.Max(_errorCount, Errors?.Count ?? 0);
            set => _errorCount = value;
        }

        public string BackupSetName { get; set; } = string.Empty;
        public string ManifestPathOnDestination { get; set; } = string.Empty;
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> Errors { get; set; } = new List<string>();

        public TimeSpan Duration => EndTimeUtc > StartTimeUtc ? EndTimeUtc - StartTimeUtc : TimeSpan.Zero;
    }

    public class BackupSetManifest
    {
        public int ManifestVersion { get; set; } = 1;
        public string BackupSetId { get; set; } = string.Empty;
        public string JobId { get; set; } = string.Empty;
        public string JobName { get; set; } = string.Empty;
        public BackupType BackupType { get; set; }
        public DateTime CreatedUtc { get; set; }
        public string BaseFullBackupSetId { get; set; } = string.Empty;
        public string PreviousBackupSetId { get; set; } = string.Empty;

        public bool IsCompressed { get; set; }
        public string ArchiveFileName { get; set; } = string.Empty;
        public bool IsEncrypted { get; set; }
        public string RelativeLocation { get; set; } = string.Empty;

        public List<ManifestFileEntry> Files { get; set; } = new List<ManifestFileEntry>();
        public List<string> DeletedRelativePaths { get; set; } = new List<string>();

        public long TotalSizeBytes { get; set; }
        public int FileCount { get; set; }
    }

    public class ManifestFileEntry
    {
        public string SourceRoot { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public DateTime LastWriteTimeUtc { get; set; }
        public string StoredRelativePath { get; set; } = string.Empty;
        public string Sha256 { get; set; } = string.Empty;
    }

    public class JobProgressInfo
    {
        public string JobId { get; set; } = string.Empty;
        public string JobName { get; set; } = string.Empty;
        public BackupType BackupType { get; set; }
        public string CurrentPhase { get; set; } = string.Empty; // Scanning, Compressing, Uploading, Finalizing
        public string CurrentFile { get; set; } = string.Empty;
        public int ProcessedFiles { get; set; }
        public int TotalFiles { get; set; }
        public long ProcessedBytes { get; set; }
        public long TotalBytes { get; set; }
        public double PercentComplete { get; set; }
        public double TransferSpeedBytesPerSec { get; set; }
        public TimeSpan Elapsed { get; set; }
        public TimeSpan? EstimatedRemaining { get; set; }
    }
}
