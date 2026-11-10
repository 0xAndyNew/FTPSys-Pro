using System;
using System.Collections.Generic;
using System.Configuration.Install;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using FtpSysPro.Core.Config;
using FtpSysPro.Core.Engine;
using FtpSysPro.Core.Ipc;
using FtpSysPro.Core.Models;
using FtpSysPro.Core.Notification;
using Newtonsoft.Json;
using Serilog;

namespace FtpSysPro.Service
{
    public class FtpSysProWindowsService : ServiceBase
    {
        public const string ServiceNameConst = "FtpSysProService";
        public const string DisplayNameConst = "FtpSysPro Backup Service";

        private ConfigStore _configStore;
        private JobRunner _runner;
        private SchedulerHost _scheduler;
        private PipeServer _pipeServer;

        public FtpSysProWindowsService()
        {
            ServiceName = ServiceNameConst;
            CanStop = true;
            CanPauseAndContinue = false;
            CanShutdown = true;
        }

        public void StartConsole(string[] args)
        {
            OnStart(args);
            Console.WriteLine("FtpSysPro Service is running in console mode. Press Ctrl+C to exit.");
            var resetEvent = new ManualResetEvent(false);
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                resetEvent.Set();
            };
            resetEvent.WaitOne();
            OnStop();
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                _configStore = new ConfigStore();

                // Setup Serilog
                var logFile = Path.Combine(_configStore.LogsDirectory, "service-.log");
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Information()
                    .WriteTo.File(logFile, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
                    .WriteTo.Console()
                    .CreateLogger();

                Log.Information("Starting FtpSysPro Windows Service...");

                var cfg = _configStore.LoadConfig();
                _runner = new JobRunner(_configStore, cfg.Settings?.MaxConcurrentJobs ?? 2);
                _scheduler = new SchedulerHost(_configStore, _runner);
                _scheduler.Start();

                _pipeServer = new PipeServer(HandleIpcRequestAsync);
                _pipeServer.Start();

                Log.Information("FtpSysPro Windows Service started successfully.");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Failed to start FtpSysPro Windows Service");
                throw;
            }
        }

        protected override void OnStop()
        {
            Log.Information("Stopping FtpSysPro Windows Service...");
            try { _pipeServer?.Stop(); } catch { }
            try { _scheduler?.Stop(); } catch { }
            Log.Information("FtpSysPro Windows Service stopped.");
            Log.CloseAndFlush();
        }

        private async Task<IpcResponse> HandleIpcRequestAsync(IpcMessage msg)
        {
            if (msg == null) return IpcResponse.Fail("", "Null message");

            switch (msg.Action)
            {
                case IpcActions.Ping:
                    return IpcResponse.Ok(msg.MessageId, "Pong");

                case IpcActions.GetConfig:
                    var cfg = _configStore.LoadConfig();
                    return IpcResponse.Ok(msg.MessageId, cfg);

                case IpcActions.SaveConfig:
                    var newCfg = JsonConvert.DeserializeObject<AppConfig>(msg.PayloadJson);
                    _configStore.SaveConfig(newCfg);
                    _scheduler?.RefreshNextRunTimes();
                    return IpcResponse.Ok(msg.MessageId, true);

                case IpcActions.RunJob:
                    var runReq = JsonConvert.DeserializeObject<RunJobRequest>(msg.PayloadJson);
                    var currentCfg = _configStore.LoadConfig();
                    var jobToRun = currentCfg.Jobs.FirstOrDefault(j => j.Id == runReq.JobId);
                    if (jobToRun == null)
                        return IpcResponse.Fail(msg.MessageId, "Job not found: " + runReq.JobId);

                    // Fire and forget, or wait? Run in background and return accepted
                    _ = Task.Run(() => _runner.RunJobAsync(jobToRun, runReq.BackupType));
                    return IpcResponse.Ok(msg.MessageId, "Job started");

                case IpcActions.StopJob:
                    var jobIdToStop = msg.PayloadJson.Trim('"', ' ');
                    var cancelled = _runner.CancelJob(jobIdToStop);
                    return IpcResponse.Ok(msg.MessageId, cancelled);

                case IpcActions.TestConnection:
                    var destCfg = JsonConvert.DeserializeObject<DestinationConfig>(msg.PayloadJson);
                    using (var dest = BackupEngine.CreateDestination(destCfg))
                    {
                        var ok = await dest.TestConnectionAsync();
                        return IpcResponse.Ok(msg.MessageId, ok);
                    }

                case IpcActions.SendTestEmail:
                    var emailReq = JsonConvert.DeserializeObject<TestEmailRequest>(msg.PayloadJson);
                    var notifier = new EmailNotifier(emailReq.Settings);
                    var sent = await notifier.SendTestEmailAsync(emailReq.Recipient);
                    return IpcResponse.Ok(msg.MessageId, sent);

                case IpcActions.ListBackupSets:
                    var jobId = msg.PayloadJson.Trim('"', ' ');
                    var configForList = _configStore.LoadConfig();
                    var jobForList = configForList.Jobs.FirstOrDefault(j => j.Id == jobId);
                    if (jobForList == null)
                        return IpcResponse.Fail(msg.MessageId, "Job not found");

                    var restoreEngine = new RestoreEngine(_configStore);
                    var sets = await restoreEngine.ListAvailableBackupSetsAsync(jobForList);
                    return IpcResponse.Ok(msg.MessageId, sets);

                case IpcActions.Restore:
                    var restoreReq = JsonConvert.DeserializeObject<RestoreRequest>(msg.PayloadJson);
                    var configForRestore = _configStore.LoadConfig();
                    var jobForRestore = configForRestore.Jobs.FirstOrDefault(j => j.Id == restoreReq.JobId);
                    if (jobForRestore == null)
                        return IpcResponse.Fail(msg.MessageId, "Job not found");

                    var restoreEng = new RestoreEngine(_configStore);
                    var restoreOpts = new RestoreOptions
                    {
                        TargetDirectory = restoreReq.TargetDirectory,
                        OverwriteExisting = restoreReq.OverwriteExisting,
                        ZipPassword = restoreReq.ZipPassword
                    };

                    var restoreResult = await restoreEng.RestoreToPointInTimeAsync(jobForRestore, restoreReq.BackupSetId, restoreOpts);
                    return IpcResponse.Ok(msg.MessageId, restoreResult);

                case IpcActions.GetRunningJobs:
                    var running = _runner.GetRunningJobIds();
                    var progressList = new List<JobProgressInfo>();
                    foreach (var id in running)
                    {
                        var p = _runner.GetProgress(id);
                        if (p != null) progressList.Add(p);
                    }
                    return IpcResponse.Ok(msg.MessageId, progressList);

                default:
                    return IpcResponse.Fail(msg.MessageId, "Unknown action: " + msg.Action);
            }
        }
    }

    internal static class Program
    {
        private static void Main(string[] args)
        {
            if (args != null && args.Length > 0)
            {
                var arg = args[0].ToLowerInvariant();
                if (arg == "--console" || arg == "-c")
                {
                    var service = new FtpSysProWindowsService();
                    service.StartConsole(args);
                    return;
                }
                if (arg == "--install" || arg == "-i")
                {
                    ManagedInstallerClass.InstallHelper(new[] { Assembly.GetExecutingAssembly().Location });
                    return;
                }
                if (arg == "--uninstall" || arg == "-u")
                {
                    ManagedInstallerClass.InstallHelper(new[] { "/u", Assembly.GetExecutingAssembly().Location });
                    return;
                }
            }

            ServiceBase.Run(new FtpSysProWindowsService());
        }
    }

    [System.ComponentModel.RunInstaller(true)]
    public class ProjectInstaller : Installer
    {
        public ProjectInstaller()
        {
            var processInstaller = new ServiceProcessInstaller
            {
                Account = ServiceAccount.LocalSystem
            };

            var serviceInstaller = new ServiceInstaller
            {
                ServiceName = FtpSysProWindowsService.ServiceNameConst,
                DisplayName = FtpSysProWindowsService.DisplayNameConst,
                Description = "FtpSysPro automated folder backup service with FTP, SFTP and Local support.",
                StartType = ServiceStartMode.Automatic,
                DelayedAutoStart = false
            };

            Installers.Add(processInstaller);
            Installers.Add(serviceInstaller);
        }
    }
}
