using System;
using FtpSysPro.Core.Logging;
using Xunit;

namespace FtpSysPro.Core.Tests
{
    public class LogFilterTests
    {
        [Theory]
        [InlineData("2026-11-11 15:53:32.982 +07:00 [INF] Starting job 'Backup1' (\"Incremental\")", true)]
        [InlineData("2026-11-11 15:55:21.062 +07:00 [WRN] Finished job 'Backup1' with status \"Warning\"", true)]
        [InlineData("[Job 'Backup1'] Bắt đầu sao lưu (Incremental)...", true)]
        [InlineData("[INF] Starting FtpSysPro Windows Service...", true)]
        [InlineData("   at System.IO.FileStream.Init(String path...)", false)]
        [InlineData("      InnerException: Access Denied", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsLogEntryHeader_DetectsHeadersCorrectly(string line, bool expected)
        {
            var result = LogFilterHelper.IsLogEntryHeader(line);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("2026-11-11 15:53:32.982 +07:00 [INF] Starting job 'Backup1' (\"Incremental\")", "Backup1", "job-guid-1", true)]
        [InlineData("2026-11-11 15:55:21.062 +07:00 [INF] Finished job 'Backup1' with status \"Warning\"", "Backup1", "job-guid-1", true)]
        [InlineData("[Job 'Backup1'] Bắt đầu sao lưu (Incremental)...", "Backup1", "job-guid-1", true)]
        [InlineData("[Job 'DatabaseDaily'] Sao lưu thành công", "Backup1", "job-guid-1", false)]
        [InlineData("2026-11-11 15:13:44.785 +07:00 [INF] Starting FtpSysPro Windows Service...", "Backup1", "job-guid-1", false)]
        [InlineData("2026-11-11 15:13:44.785 +07:00 [INF] Starting FtpSysPro Windows Service...", null, null, true)]
        [InlineData("2026-11-11 15:13:44.785 +07:00 [INF] Run with jobId: job-guid-1", "OtherJob", "job-guid-1", true)]
        public void LineMatchesJob_MatchesExpectedJobs(string line, string jobName, string jobId, bool expected)
        {
            var result = LogFilterHelper.LineMatchesJob(line, jobName, jobId);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("2026-11-11 15:53:32.982 +07:00 [INF] Starting job", 0, true)]
        [InlineData("2026-11-11 15:53:32.982 +07:00 [INF] Starting job", 3, true)]
        [InlineData("2026-11-11 15:53:32.982 +07:00 [INF] Starting job", 1, false)]
        [InlineData("2026-11-11 15:53:32.982 +07:00 [ERR] Thất bại khi kết nối FTP", 1, true)]
        [InlineData("2026-11-11 15:53:32.982 +07:00 [WRN] Cảnh báo dung lượng ổ đĩa", 2, true)]
        public void LineMatchesLevel_FiltersLevelsCorrectly(string line, int levelIndex, bool expected)
        {
            var result = LogFilterHelper.LineMatchesLevel(line, levelIndex);
            Assert.Equal(expected, result);
        }
    }
}
