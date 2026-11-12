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
    }
}
