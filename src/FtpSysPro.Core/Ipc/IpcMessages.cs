using System;
using System.Collections.Generic;
using FtpSysPro.Core.Models;

namespace FtpSysPro.Core.Ipc
{
    public static class IpcConstants
    {
        public const string PipeName = "FtpSysPro.Service.Pipe";
        public const int DefaultTimeoutMs = 15000;
    }

    public class IpcMessage
    {
        public string MessageId { get; set; } = Guid.NewGuid().ToString("N");
        public string Action { get; set; } = string.Empty;
        public string PayloadJson { get; set; } = string.Empty;
    }

    public class IpcResponse
    {
        public string MessageId { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string PayloadJson { get; set; } = string.Empty;

        public static IpcResponse Ok(string messageId, object payload = null)
        {
            return new IpcResponse
            {
                MessageId = messageId,
                Success = true,
                PayloadJson = payload != null ? Newtonsoft.Json.JsonConvert.SerializeObject(payload) : null
            };
        }

        public static IpcResponse Fail(string messageId, string error)
        {
            return new IpcResponse
            {
                MessageId = messageId,
                Success = false,
                ErrorMessage = error
            };
        }
    }

    public static class IpcActions
    {
        public const string Ping = "Ping";
        public const string GetConfig = "GetConfig";
        public const string SaveConfig = "SaveConfig";
        public const string RunJob = "RunJob"; // Payload: RunJobRequest
        public const string StopJob = "StopJob"; // Payload: JobId
        public const string TestConnection = "TestConnection"; // Payload: DestinationConfig
        public const string SendTestEmail = "SendTestEmail"; // Payload: TestEmailRequest
        public const string GetHistory = "GetHistory"; // Payload: JobId
        public const string ListBackupSets = "ListBackupSets"; // Payload: JobId
        public const string Restore = "Restore"; // Payload: RestoreRequest
        public const string GetRunningJobs = "GetRunningJobs";
    }

    public class RunJobRequest
    {
        public string JobId { get; set; } = string.Empty;
        public BackupType BackupType { get; set; } = BackupType.Incremental;
    }

    public class TestEmailRequest
    {
        public SmtpSettings Settings { get; set; }
        public string Recipient { get; set; }
    }

    public class RestoreRequest
    {
        public string JobId { get; set; } = string.Empty;
        public string BackupSetId { get; set; } = string.Empty;
        public string TargetDirectory { get; set; } = string.Empty;
        public bool OverwriteExisting { get; set; } = true;
        public string ZipPassword { get; set; } = string.Empty;
    }
}
