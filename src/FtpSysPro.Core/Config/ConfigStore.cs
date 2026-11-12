using System;
using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using FtpSysPro.Core.Models;
using Newtonsoft.Json;

namespace FtpSysPro.Core.Config
{
    public class ConfigStore
    {
        private readonly string _baseDirectory;
        private readonly string _configFilePath;
        private readonly string _stateFilePath;
        private readonly string _historyDirectory;
        private readonly string _catalogDirectory;
        private readonly string _logsDirectory;
        private readonly string _tempDirectory;
        private readonly object _syncLock = new object();

        public ConfigStore(string customBaseDirectory = null)
        {
            if (string.IsNullOrEmpty(customBaseDirectory))
            {
                var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                _baseDirectory = Path.Combine(programData, "FtpSysPro");
            }
            else
            {
                _baseDirectory = customBaseDirectory;
            }

            _configFilePath = Path.Combine(_baseDirectory, "config.json");
            _stateFilePath = Path.Combine(_baseDirectory, "state.json");
            _historyDirectory = Path.Combine(_baseDirectory, "history");
            _catalogDirectory = Path.Combine(_baseDirectory, "catalog");
            _logsDirectory = Path.Combine(_baseDirectory, "logs");
            _tempDirectory = Path.Combine(_baseDirectory, "temp");

            EnsureDirectories();
        }

        public string BaseDirectory => _baseDirectory;
        public string HistoryDirectory => _historyDirectory;
        public string CatalogDirectory => _catalogDirectory;
        public string LogsDirectory => _logsDirectory;
        public string TempDirectory => _tempDirectory;

        public void EnsureDirectories()
        {
            EnsureDirectoryWithAcl(_baseDirectory);
            EnsureDirectoryWithAcl(_historyDirectory);
            EnsureDirectoryWithAcl(_catalogDirectory);
            EnsureDirectoryWithAcl(_logsDirectory);
            EnsureDirectoryWithAcl(_tempDirectory);
        }

        private static void EnsureDirectoryWithAcl(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                var commonData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                bool isProgramData = !string.IsNullOrEmpty(commonData) && path.StartsWith(commonData, StringComparison.OrdinalIgnoreCase);

                if (isProgramData)
                {
                    var dInfo = new DirectoryInfo(path);
                    var sec = dInfo.GetAccessControl();

                    // LocalSystem: Full control
                    sec.AddAccessRule(new FileSystemAccessRule(
                        new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                        FileSystemRights.FullControl,
                        InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                        PropagationFlags.None,
                        AccessControlType.Allow));

                    // Built-in Administrators: Full control
                    sec.AddAccessRule(new FileSystemAccessRule(
                        new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                        FileSystemRights.FullControl,
                        InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                        PropagationFlags.None,
                        AccessControlType.Allow));

                    // Authenticated Users: Read and Execute, List, Read
                    sec.AddAccessRule(new FileSystemAccessRule(
                        new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
                        FileSystemRights.ReadAndExecute | FileSystemRights.ListDirectory | FileSystemRights.Read,
                        InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                        PropagationFlags.None,
                        AccessControlType.Allow));

                    // Built-in Users: Read and Execute, List, Read
                    sec.AddAccessRule(new FileSystemAccessRule(
                        new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
                        FileSystemRights.ReadAndExecute | FileSystemRights.ListDirectory | FileSystemRights.Read,
                        InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                        PropagationFlags.None,
                        AccessControlType.Allow));

                    dInfo.SetAccessControl(sec);
                }
            }
            catch
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
            }
        }

        public AppConfig LoadConfig()
        {
            lock (_syncLock)
            {
                if (!File.Exists(_configFilePath))
                {
                    var fresh = new AppConfig();
                    SaveConfig(fresh);
                    return fresh;
                }

                try
                {
                    var json = File.ReadAllText(_configFilePath);
                    var cfg = JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
                    return cfg;
                }
                catch
                {
                    // If corrupted, try to restore from backup
                    var backup = _configFilePath + ".bak";
                    if (File.Exists(backup))
                    {
                        var json = File.ReadAllText(backup);
                        return JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
                    }
                    return new AppConfig();
                }
            }
        }

        public void SaveConfig(AppConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            lock (_syncLock)
            {
                EnsureDirectories();
                var json = JsonConvert.SerializeObject(config, Formatting.Indented);
                WriteAtomic(_configFilePath, json);
            }
        }

        public void SaveHistoryRecord(BackupRunResult result)
        {
            if (result == null) return;

            lock (_syncLock)
            {
                EnsureDirectories();
                var jobHistDir = Path.Combine(_historyDirectory, Sanitize(result.JobId));
                if (!Directory.Exists(jobHistDir))
                    Directory.CreateDirectory(jobHistDir);

                var file = Path.Combine(jobHistDir, $"{result.StartTimeUtc:yyyyMMdd_HHmmss}_{result.RunId}.json");
                var json = JsonConvert.SerializeObject(result, Formatting.Indented);
                File.WriteAllText(file, json);
            }
        }

        public List<BackupRunResult> LoadHistoryRecords(string jobId = null, int maxCount = 200)
        {
            var results = new List<BackupRunResult>();
            lock (_syncLock)
            {
                if (!Directory.Exists(_historyDirectory))
                    return results;

                string[] files;
                if (!string.IsNullOrEmpty(jobId))
                {
                    var jobHistDir = Path.Combine(_historyDirectory, Sanitize(jobId));
                    if (!Directory.Exists(jobHistDir))
                        return results;
                    files = Directory.GetFiles(jobHistDir, "*.json");
                }
                else
                {
                    files = Directory.GetFiles(_historyDirectory, "*.json", SearchOption.AllDirectories);
                }

                Array.Sort(files);
                Array.Reverse(files); // Newest files first

                foreach (var f in files)
                {
                    try
                    {
                        var json = File.ReadAllText(f);
                        var r = JsonConvert.DeserializeObject<BackupRunResult>(json);
                        if (r != null)
                        {
                            results.Add(r);
                            if (results.Count >= maxCount)
                                break;
                        }
                    }
                    catch
                    {
                        // Ignore corrupt history files
                    }
                }
            }

            return results;
        }

        public void ClearHistory(string jobId = null)
        {
            lock (_syncLock)
            {
                if (!Directory.Exists(_historyDirectory))
                    return;

                if (!string.IsNullOrEmpty(jobId))
                {
                    var jobHistDir = Path.Combine(_historyDirectory, Sanitize(jobId));
                    if (Directory.Exists(jobHistDir))
                    {
                        try { Directory.Delete(jobHistDir, true); } catch { }
                    }
                }
                else
                {
                    try
                    {
                        var di = new DirectoryInfo(_historyDirectory);
                        foreach (var file in di.GetFiles()) { try { file.Delete(); } catch { } }
                        foreach (var dir in di.GetDirectories()) { try { dir.Delete(true); } catch { } }
                    }
                    catch { }
                }
            }
        }

        public void SaveLocalManifest(BackupSetManifest manifest)
        {
            if (manifest == null) return;

            lock (_syncLock)
            {
                var jobCatDir = Path.Combine(_catalogDirectory, Sanitize(manifest.JobId));
                if (!Directory.Exists(jobCatDir))
                    Directory.CreateDirectory(jobCatDir);

                var file = Path.Combine(jobCatDir, $"{manifest.BackupSetId}.manifest.json");
                var json = JsonConvert.SerializeObject(manifest, Formatting.Indented);
                File.WriteAllText(file, json);
            }
        }

        public BackupSetManifest LoadLatestLocalManifest(string jobId, BackupType? typeFilter = null)
        {
            lock (_syncLock)
            {
                var jobCatDir = Path.Combine(_catalogDirectory, Sanitize(jobId));
                if (!Directory.Exists(jobCatDir))
                    return null;

                var files = Directory.GetFiles(jobCatDir, "*.manifest.json");
                Array.Sort(files);
                Array.Reverse(files);

                foreach (var f in files)
                {
                    try
                    {
                        var json = File.ReadAllText(f);
                        var m = JsonConvert.DeserializeObject<BackupSetManifest>(json);
                        if (m == null) continue;
                        if (typeFilter == null || m.BackupType == typeFilter.Value)
                            return m;
                    }
                    catch
                    {
                        // Ignore corrupt manifest files
                    }
                }
                return null;
            }
        }

        private static void WriteAtomic(string path, string content)
        {
            var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            var backup = path + ".bak";

            File.WriteAllText(temp, content);

            if (File.Exists(path))
            {
                try
                {
                    File.Copy(path, backup, true);
                }
                catch { }

                try
                {
                    File.Replace(temp, path, null);
                }
                catch
                {
                    // Fallback for filesystems that do not support replace
                    File.Delete(path);
                    File.Move(temp, path);
                }
            }
            else
            {
                File.Move(temp, path);
            }
        }

        private static string Sanitize(string input)
        {
            if (string.IsNullOrEmpty(input)) return "default";
            foreach (var c in Path.GetInvalidFileNameChars())
                input = input.Replace(c, '_');
            return input;
        }
    }
}
