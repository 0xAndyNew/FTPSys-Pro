using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FtpSysPro.Core.Config;
using FtpSysPro.Core.Engine;
using FtpSysPro.Core.Models;
using FtpSysPro.Core.Notification;
using FtpSysPro.Core.Scheduling;
using Serilog;

namespace FtpSysPro.Service
{
    public class JobRunner
    {
        private readonly ConfigStore _configStore;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _runningJobs = new ConcurrentDictionary<string, CancellationTokenSource>();
        private readonly ConcurrentDictionary<string, JobProgressInfo> _jobProgress = new ConcurrentDictionary<string, JobProgressInfo>();
        private readonly SemaphoreSlim _concurrencySemaphore;

        public JobRunner(ConfigStore configStore, int maxConcurrent = 2)
        {
            _configStore = configStore;
            _concurrencySemaphore = new SemaphoreSlim(Math.Max(1, maxConcurrent), Math.Max(1, maxConcurrent));
        }

        public bool IsJobRunning(string jobId) => _runningJobs.ContainsKey(jobId);

        public List<string> GetRunningJobIds() => _runningJobs.Keys.ToList();

        public JobProgressInfo GetProgress(string jobId)
        {
            _jobProgress.TryGetValue(jobId, out var p);
            return p;
        }

        public bool CancelJob(string jobId)
        {
            if (_runningJobs.TryGetValue(jobId, out var cts))
            {
                cts.Cancel();
                return true;
            }
            return false;
        }

        public async Task<BackupRunResult> RunJobAsync(BackupJob job, BackupType backupType)
        {
            if (job == null) throw new ArgumentNullException(nameof(job));

            var cts = new CancellationTokenSource();
            if (!_runningJobs.TryAdd(job.Id, cts))
            {
                return new BackupRunResult
                {
                    JobId = job.Id,
                    JobName = job.Name,
                    Status = JobExecutionStatus.Failed,
                    Message = "Job is already running."
                };
            }

            await _concurrencySemaphore.WaitAsync(cts.Token);
            try
            {
                Log.Information("[Job '{JobName}'] Bắt đầu sao lưu ({Type})...", job.Name, backupType);
                _configStore.AppendJobLog(job.Id, job.Name, "INF", $"Bắt đầu sao lưu ({backupType})...");

                var progress = new Progress<JobProgressInfo>(p =>
                {
                    _jobProgress[job.Id] = p;
                });

                var engine = new BackupEngine(_configStore);
                var result = await engine.ExecuteBackupAsync(job, backupType, progress, cts.Token);

                // Save config to persist updated LastRun / status
                var cfg = _configStore.LoadConfig();
                var existingJob = cfg.Jobs.FirstOrDefault(j => j.Id == job.Id);
                if (existingJob != null)
                {
                    existingJob.LastRunUtc = job.LastRunUtc;
                    existingJob.LastStatus = job.LastStatus;
                    existingJob.LastMessage = job.LastMessage;
                    existingJob.LastSizeBytes = job.LastSizeBytes;
                    existingJob.LastFileCount = job.LastFileCount;
                    existingJob.NextRunUtc = ScheduleCalculator.GetNextJobRun(existingJob, DateTime.Now);
                    _configStore.SaveConfig(cfg);
                }

                // Log warnings if any
                if (result.Warnings != null && result.Warnings.Count > 0)
                {
                    foreach (var w in result.Warnings)
                    {
                        Log.Warning("[Job '{JobName}'] Cảnh báo: {Warning}", job.Name, w);
                        _configStore.AppendJobLog(job.Id, job.Name, "WRN", $"Cảnh báo: {w}");
                    }
                }

                // Send email notification
                try
                {
                    if (cfg.Settings.Smtp != null && cfg.Settings.Smtp.Enabled && job.Notification.Enabled)
                    {
                        var notifier = new EmailNotifier(cfg.Settings.Smtp);
                        await notifier.SendJobNotificationAsync(job, result, CancellationToken.None);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[Job '{JobName}'] Không thể gửi email thông báo", job.Name);
                }

                if (result.Status == JobExecutionStatus.Failed)
                {
                    Log.Error("[Job '{JobName}'] Thất bại: {Message}", job.Name, result.Message);
                    _configStore.AppendJobLog(job.Id, job.Name, "ERR", $"Thất bại: {result.Message}");
                }
                else
                {
                    Log.Information("[Job '{JobName}'] Hoàn tất với trạng thái {Status}: {Message} (Sao lưu: {Files} tệp, {Size} bytes)",
                        job.Name, result.Status, result.Message, result.BackedUpFiles, result.BackedUpBytes);
                    string statusTag = result.Status == JobExecutionStatus.Warning ? "WRN" : "INF";
                    _configStore.AppendJobLog(job.Id, job.Name, statusTag,
                        $"Hoàn tất với trạng thái {result.Status}: {result.Message} (Sao lưu: {result.BackedUpFiles} tệp, {result.BackedUpBytes:N0} bytes)");
                }
                return result;
            }
            finally
            {
                _concurrencySemaphore.Release();
                _runningJobs.TryRemove(job.Id, out _);
                _jobProgress.TryRemove(job.Id, out _);
                cts.Dispose();
            }
        }
    }

    public class SchedulerHost : IDisposable
    {
        private readonly ConfigStore _configStore;
        private readonly JobRunner _runner;
        private System.Threading.Timer _timer;
        private bool _isChecking;
        private readonly object _lock = new object();

        public SchedulerHost(ConfigStore configStore, JobRunner runner)
        {
            _configStore = configStore;
            _runner = runner;
        }

        public void Start()
        {
            // Update initial NextRunUtc for all jobs
            RefreshNextRunTimes();

            // Check every 30 seconds
            _timer = new System.Threading.Timer(OnTimerTick, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30));
            Log.Information("Scheduler host started");
        }

        public void Stop()
        {
            _timer?.Change(Timeout.Infinite, 0);
            Log.Information("Scheduler host stopped");
        }

        public void RefreshNextRunTimes()
        {
            lock (_lock)
            {
                var cfg = _configStore.LoadConfig();
                bool changed = false;
                var now = DateTime.Now;

                foreach (var job in cfg.Jobs)
                {
                    var next = ScheduleCalculator.GetNextJobRun(job, now);
                    if (job.NextRunUtc != next)
                    {
                        job.NextRunUtc = next;
                        changed = true;
                    }
                }

                if (changed)
                {
                    _configStore.SaveConfig(cfg);
                }
            }
        }

        private async void OnTimerTick(object state)
        {
            if (_isChecking) return;
            _isChecking = true;

            try
            {
                var cfg = _configStore.LoadConfig();
                var now = DateTime.Now;

                foreach (var job in cfg.Jobs.Where(j => j.Enabled))
                {
                    if (_runner.IsJobRunning(job.Id))
                        continue;

                    foreach (var s in job.Schedules.Where(s => s.Enabled))
                    {
                        var next = ScheduleCalculator.GetNextRun(s, now.AddSeconds(-35));
                        if (!next.HasValue) continue;

                        // If scheduled time has arrived within the last 40 seconds window
                        var diff = (now - next.Value).TotalSeconds;
                        if (diff >= 0 && diff <= 40)
                        {
                            Log.Information("[Job '{JobName}'] Lịch chạy '{ScheduleName}' được kích hoạt (Chế độ: {BackupType})", job.Name, s.Name, job.BackupType);
                            _ = Task.Run(() => _runner.RunJobAsync(job, job.BackupType));
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during scheduler tick");
            }
            finally
            {
                _isChecking = false;
            }
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
