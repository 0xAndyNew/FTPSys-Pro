using System;
using System.IO;
using FtpSysPro.Core.Config;
using FtpSysPro.Core.Models;
using Xunit;

namespace FtpSysPro.Core.Tests
{
    public class HistoryTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly ConfigStore _store;

        public HistoryTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "FtpSysPro_HistTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            _store = new ConfigStore(_tempDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDir))
                    Directory.Delete(_tempDir, true);
            }
            catch { }
        }

        [Fact]
        public void SaveAndLoadHistoryRecords_LoadsNewestFirst_AndFiltersByJob()
        {
            var job1Id = "job-alpha";
            var job2Id = "job-beta";

            var run1 = new BackupRunResult
            {
                RunId = "run-1",
                JobId = job1Id,
                JobName = "Job Alpha",
                BackupType = BackupType.Full,
                StartTimeUtc = DateTime.UtcNow.AddMinutes(-30),
                EndTimeUtc = DateTime.UtcNow.AddMinutes(-25),
                Status = JobExecutionStatus.Success,
                BackedUpFiles = 10,
                BackedUpBytes = 10240,
                Message = "Success full"
            };

            var run2 = new BackupRunResult
            {
                RunId = "run-2",
                JobId = job1Id,
                JobName = "Job Alpha",
                BackupType = BackupType.Incremental,
                StartTimeUtc = DateTime.UtcNow.AddMinutes(-10),
                EndTimeUtc = DateTime.UtcNow.AddMinutes(-8),
                Status = JobExecutionStatus.Success,
                BackedUpFiles = 2,
                BackedUpBytes = 512,
                Message = "Success incr"
            };

            var run3 = new BackupRunResult
            {
                RunId = "run-3",
                JobId = job2Id,
                JobName = "Job Beta",
                BackupType = BackupType.Full,
                StartTimeUtc = DateTime.UtcNow.AddMinutes(-5),
                EndTimeUtc = DateTime.UtcNow.AddMinutes(-3),
                Status = JobExecutionStatus.Failed,
                Message = "Disk full"
            };

            _store.SaveHistoryRecord(run1);
            _store.SaveHistoryRecord(run2);
            _store.SaveHistoryRecord(run3);

            // Filter by job1
            var job1History = _store.LoadHistoryRecords(job1Id);
            Assert.Equal(2, job1History.Count);
            Assert.Equal("run-2", job1History[0].RunId); // Newer run first
            Assert.Equal("run-1", job1History[1].RunId);

            // Filter by job2
            var job2History = _store.LoadHistoryRecords(job2Id);
            Assert.Single(job2History);
            Assert.Equal("run-3", job2History[0].RunId);

            // Load all
            var allHistory = _store.LoadHistoryRecords(null);
            Assert.Equal(3, allHistory.Count);

            // Clear job1 history
            _store.ClearHistory(job1Id);
            Assert.Empty(_store.LoadHistoryRecords(job1Id));
            Assert.Single(_store.LoadHistoryRecords(job2Id));

            // Clear all
            _store.ClearHistory(null);
            Assert.Empty(_store.LoadHistoryRecords(null));
        }

        [Fact]
        public void JobLog_Appends_And_PopulatesFromHistorySuccessfully()
        {
            var job = new BackupJob
            {
                Id = "job-log-test",
                Name = "DailyBackup"
            };

            // 1. Path check
            var logPath = _store.GetJobLogFilePath(job.Id);
            Assert.NotNull(logPath);
            Assert.Contains("job-log-test.log", logPath);

            // 2. Append job log directly
            _store.AppendJobLog(job.Id, job.Name, "INF", "Starting manual backup...");
            _store.AppendJobLog(job.Id, job.Name, "WRN", "VSS skipped, direct read");
            _store.AppendJobLog(job.Id, job.Name, "INF", "Completed successfully");

            Assert.True(File.Exists(logPath));
            var lines = File.ReadAllLines(logPath);
            Assert.Equal(3, lines.Length);
            Assert.Contains("[INF] [Job 'DailyBackup'] Starting manual backup...", lines[0]);
            Assert.Contains("[WRN] [Job 'DailyBackup'] VSS skipped, direct read", lines[1]);
            Assert.Contains("[INF] [Job 'DailyBackup'] Completed successfully", lines[2]);

            // 3. Clear job log
            _store.ClearJobLog(job.Id);
            Assert.False(File.Exists(logPath));

            // 4. EnsureJobLogPopulated synthesizes from history
            var runRec = new BackupRunResult
            {
                RunId = "run-synth-1",
                JobId = job.Id,
                JobName = job.Name,
                BackupType = BackupType.Full,
                StartTimeUtc = DateTime.UtcNow.AddMinutes(-10),
                EndTimeUtc = DateTime.UtcNow.AddMinutes(-8),
                Status = JobExecutionStatus.Success,
                BackedUpFiles = 150,
                BackedUpBytes = 1048576,
                Message = "Backup done 150 files.",
                BackupSetName = "DailyBackup_20261112_FULL",
                Warnings = new System.Collections.Generic.List<string> { "Warning test sample" }
            };
            _store.SaveHistoryRecord(runRec);

            // Now call EnsureJobLogPopulated
            _store.EnsureJobLogPopulated(job);
            Assert.True(File.Exists(logPath));
            var synthLines = File.ReadAllLines(logPath);
            Assert.True(synthLines.Length >= 3);
            Assert.Contains("Bộ sao lưu: DailyBackup_20261112_FULL", string.Join(" ", synthLines));
            Assert.Contains("Cảnh báo: Warning test sample", string.Join(" ", synthLines));
            Assert.Contains("Hoàn tất với trạng thái Success", string.Join(" ", synthLines));
        }

        [Fact]
        public void DeleteJobData_RemovesHistoryLogAndCatalogFiles()
        {
            var jobId = "job-to-delete";
            var runRec = new BackupRunResult
            {
                RunId = "run-del-1",
                JobId = jobId,
                JobName = "DeletedJob",
                BackupType = BackupType.Full,
                StartTimeUtc = DateTime.UtcNow,
                EndTimeUtc = DateTime.UtcNow,
                Status = JobExecutionStatus.Success
            };
            _store.SaveHistoryRecord(runRec);
            _store.AppendJobLog(jobId, "DeletedJob", "INF", "Test message");

            var manifest = new BackupSetManifest
            {
                BackupSetId = "DeletedJob_20261113_FULL",
                JobId = jobId,
                JobName = "DeletedJob",
                CreatedUtc = DateTime.UtcNow
            };
            _store.SaveLocalManifest(manifest);

            var histRecords = _store.LoadHistoryRecords(jobId);
            Assert.Single(histRecords);
            Assert.True(File.Exists(_store.GetJobLogFilePath(jobId)));
            Assert.NotNull(_store.LoadLatestLocalManifest(jobId));

            // Execute DeleteJobData
            _store.DeleteJobData(jobId);

            // Verify everything is deleted
            Assert.Empty(_store.LoadHistoryRecords(jobId));
            Assert.False(File.Exists(_store.GetJobLogFilePath(jobId)));
            Assert.Null(_store.LoadLatestLocalManifest(jobId));
        }

        [Fact]
        public void CleanOrphanedJobData_RemovesDataForNonExistentJobs()
        {
            var activeJobId = "job-active";
            var deletedJobId = "job-orphan";

            _store.SaveHistoryRecord(new BackupRunResult { RunId = "r1", JobId = activeJobId, JobName = "Active", StartTimeUtc = DateTime.UtcNow });
            _store.SaveHistoryRecord(new BackupRunResult { RunId = "r2", JobId = deletedJobId, JobName = "Orphan", StartTimeUtc = DateTime.UtcNow });

            _store.AppendJobLog(activeJobId, "Active", "INF", "Active log");
            _store.AppendJobLog(deletedJobId, "Orphan", "INF", "Orphan log");

            _store.SaveLocalManifest(new BackupSetManifest { BackupSetId = "set1", JobId = activeJobId });
            _store.SaveLocalManifest(new BackupSetManifest { BackupSetId = "set2", JobId = deletedJobId });

            // Active list only contains activeJobId
            _store.CleanOrphanedJobData(new[] { activeJobId });

            // Active job data remains
            Assert.Single(_store.LoadHistoryRecords(activeJobId));
            Assert.True(File.Exists(_store.GetJobLogFilePath(activeJobId)));
            Assert.NotNull(_store.LoadLatestLocalManifest(activeJobId));

            // Orphaned job data is completely removed
            Assert.Empty(_store.LoadHistoryRecords(deletedJobId));
            Assert.False(File.Exists(_store.GetJobLogFilePath(deletedJobId)));
            Assert.Null(_store.LoadLatestLocalManifest(deletedJobId));
        }

        [Fact]
        public void RestoreLog_AppendsAndReadsEntriesCorrectly()
        {
            var jobId = "job-restore-test";
            var jobName = "FinanceRestore";
            var setId = "Finance_20261114_FULL";
            var target = @"C:\RestoreTarget";

            _store.AppendRestoreLog("INF", "Bắt đầu phục hồi bản sao lưu", jobId, jobName, setId, target);
            _store.AppendRestoreLog("INF", "Phục hồi thành công 25 tệp", jobId, jobName, setId, target);

            var logContent = _store.ReadRestoreLog();
            Assert.Contains("[RESTORE]", logContent);
            Assert.Contains(setId, logContent);
            Assert.Contains(target, logContent);
            Assert.Contains("Phục hồi thành công 25 tệp", logContent);

            // Verify it also appends to the Job's log file
            var jobLog = File.ReadAllText(_store.GetJobLogFilePath(jobId));
            Assert.Contains("[RESTORE]", jobLog);
            Assert.Contains("Phục hồi thành công 25 tệp", jobLog);
        }
    }
}
