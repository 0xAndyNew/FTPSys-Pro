using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FtpSysPro.Core.Compression;
using FtpSysPro.Core.Config;
using FtpSysPro.Core.Destinations;
using FtpSysPro.Core.Models;
using Newtonsoft.Json;

namespace FtpSysPro.Core.Engine
{
    public class RestoreOptions
    {
        public string TargetDirectory { get; set; } = string.Empty;
        public bool OverwriteExisting { get; set; } = true;
        public string ZipPassword { get; set; } = string.Empty;
    }

    public class RestoreResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int RestoredFiles { get; set; }
        public long RestoredBytes { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> Errors { get; set; } = new List<string>();
    }

    public class RestoreEngine
    {
        private readonly ConfigStore _configStore;

        public RestoreEngine(ConfigStore configStore)
        {
            _configStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
        }

        public async Task<List<BackupSetManifest>> ListAvailableBackupSetsAsync(
            BackupJob job,
            CancellationToken cancellationToken = default)
        {
            var list = new List<BackupSetManifest>();

            // 1. Load from local catalog if available
            if (_configStore != null && !string.IsNullOrEmpty(job.Id))
            {
                var localSets = _configStore.LoadLocalManifests(job.Id);
                foreach (var s in localSets)
                {
                    if (!list.Any(x => x.BackupSetId == s.BackupSetId))
                        list.Add(s);
                }
            }

            // 2. Discover sets on destination
            using (var destination = BackupEngine.CreateDestination(job.Destination))
            {
                var jobRemoteRoot = SanitizeName(job.Name);
                var items = await destination.ListAsync(jobRemoteRoot, cancellationToken);

                foreach (var item in items)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (item.Name.EndsWith(".manifest.json", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            using (var stream = await destination.OpenReadStreamAsync(item.FullPath, cancellationToken))
                            using (var reader = new StreamReader(stream))
                            {
                                var json = await reader.ReadToEndAsync();
                                var m = JsonConvert.DeserializeObject<BackupSetManifest>(json);
                                if (m != null && !list.Any(x => x.BackupSetId == m.BackupSetId))
                                    list.Add(m);
                            }
                        }
                        catch { }
                    }
                    else if (item.IsDirectory)
                    {
                        var subManifestPath = $"{item.FullPath}/manifest.json";
                        bool loaded = false;
                        if (await destination.FileExistsAsync(subManifestPath, cancellationToken))
                        {
                            try
                            {
                                using (var stream = await destination.OpenReadStreamAsync(subManifestPath, cancellationToken))
                                using (var reader = new StreamReader(stream))
                                {
                                    var json = await reader.ReadToEndAsync();
                                    var m = JsonConvert.DeserializeObject<BackupSetManifest>(json);
                                    if (m != null && !list.Any(x => x.BackupSetId == m.BackupSetId))
                                    {
                                        list.Add(m);
                                        loaded = true;
                                    }
                                }
                            }
                            catch { }
                        }

                        if (!loaded && !list.Any(x => x.BackupSetId == item.Name))
                        {
                            var match = System.Text.RegularExpressions.Regex.Match(item.Name, @"_(\d{8}_\d{6})_(FULL|INCR)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            if (match.Success)
                            {
                                string dateStr = match.Groups[1].Value;
                                string typeStr = match.Groups[2].Value.ToUpperInvariant();
                                if (DateTime.TryParseExact(dateStr, "yyyyMMdd_HHmmss", null, System.Globalization.DateTimeStyles.AssumeUniversal, out var dt))
                                {
                                    list.Add(new BackupSetManifest
                                    {
                                        BackupSetId = item.Name,
                                        JobId = job.Id,
                                        JobName = job.Name,
                                        CreatedUtc = dt.ToUniversalTime(),
                                        BackupType = typeStr == "FULL" ? BackupType.Full : BackupType.Incremental,
                                        BaseFullBackupSetId = typeStr == "FULL" ? item.Name : string.Empty,
                                        IsCompressed = false
                                    });
                                }
                            }
                        }
                    }
                    else if (item.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        var setId = Path.GetFileNameWithoutExtension(item.Name);
                        if (!list.Any(x => x.BackupSetId == setId))
                        {
                            var match = System.Text.RegularExpressions.Regex.Match(item.Name, @"_(\d{8}_\d{6})_(FULL|INCR)\.zip$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            if (match.Success)
                            {
                                string dateStr = match.Groups[1].Value;
                                string typeStr = match.Groups[2].Value.ToUpperInvariant();
                                if (DateTime.TryParseExact(dateStr, "yyyyMMdd_HHmmss", null, System.Globalization.DateTimeStyles.AssumeUniversal, out var dt))
                                {
                                    list.Add(new BackupSetManifest
                                    {
                                        BackupSetId = setId,
                                        JobId = job.Id,
                                        JobName = job.Name,
                                        CreatedUtc = dt.ToUniversalTime(),
                                        BackupType = typeStr == "FULL" ? BackupType.Full : BackupType.Incremental,
                                        BaseFullBackupSetId = typeStr == "FULL" ? setId : string.Empty,
                                        IsCompressed = true,
                                        ArchiveFileName = item.Name
                                    });
                                }
                            }
                        }
                    }
                }
            }

            return list.OrderByDescending(m => m.CreatedUtc).ToList();
        }

        public async Task<RestoreResult> RestoreToPointInTimeAsync(
            BackupJob job,
            string targetBackupSetId,
            RestoreOptions options,
            IProgress<JobProgressInfo> progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new RestoreResult();

            if (string.IsNullOrWhiteSpace(options.TargetDirectory))
            {
                result.Errors.Add("Target restore directory must be specified");
                return result;
            }

            if (!Directory.Exists(options.TargetDirectory))
            {
                Directory.CreateDirectory(options.TargetDirectory);
            }

            var tempDir = Path.Combine(_configStore.TempDirectory ?? Path.Combine(_configStore.BaseDirectory, "temp"),
                "restore_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                using (var destination = BackupEngine.CreateDestination(job.Destination))
                {
                    // 1. Get chain of backup sets from Full -> target
                    var allSets = await ListAvailableBackupSetsAsync(job, cancellationToken);
                    var targetSet = allSets.FirstOrDefault(s => s.BackupSetId == targetBackupSetId);
                    if (targetSet == null)
                    {
                        result.Errors.Add($"Backup set '{targetBackupSetId}' not found");
                        return result;
                    }

                    var baseFullId = string.IsNullOrEmpty(targetSet.BaseFullBackupSetId)
                        ? targetSet.BackupSetId
                        : targetSet.BaseFullBackupSetId;

                    // Build ordered chain from Full -> Incremental -> ... -> targetSet
                    var chain = allSets
                        .Where(s => (s.BackupSetId == baseFullId || s.BaseFullBackupSetId == baseFullId)
                                    && s.CreatedUtc <= targetSet.CreatedUtc)
                        .OrderBy(s => s.CreatedUtc)
                        .ToList();

                    var jobRemoteRoot = SanitizeName(job.Name);

                    // 2. Apply sets in chronological order
                    foreach (var set in chain)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (set.IsCompressed)
                        {
                            var remoteZip = $"{jobRemoteRoot}/{set.ArchiveFileName}";
                            var localZip = Path.Combine(tempDir, set.ArchiveFileName);

                            await destination.DownloadFileAsync(remoteZip, localZip, null, cancellationToken);

                            ZipArchiveHelper.ExtractZipArchive(
                                localZip,
                                options.TargetDirectory,
                                string.IsNullOrEmpty(options.ZipPassword)
                                    ? SecretProtector.Unprotect(job.Compression.PasswordProtected)
                                    : options.ZipPassword,
                                options.OverwriteExisting,
                                null,
                                cancellationToken);

                            try { File.Delete(localZip); } catch { }
                        }
                        else
                        {
                            // Download uncompressed files
                            var remoteDir = $"{jobRemoteRoot}/{set.BackupSetId}";
                            if (set.Files != null && set.Files.Count > 0)
                            {
                                foreach (var f in set.Files)
                                {
                                    cancellationToken.ThrowIfCancellationRequested();
                                    var remoteFile = $"{remoteDir}/{f.RelativePath.Replace('\\', '/')}";
                                    var localFile = Path.Combine(options.TargetDirectory, f.RelativePath);

                                    if (File.Exists(localFile) && !options.OverwriteExisting)
                                        continue;

                                    await destination.DownloadFileAsync(remoteFile, localFile, null, cancellationToken);
                                }
                            }
                            else
                            {
                                await DownloadRemoteDirectoryRecursiveAsync(destination, remoteDir, "", options.TargetDirectory, options.OverwriteExisting, cancellationToken);
                            }
                        }

                        // Apply file deletions recorded in incremental manifest
                        if (set.DeletedRelativePaths != null)
                        {
                            foreach (var deletedRel in set.DeletedRelativePaths)
                            {
                                var deletedFull = Path.Combine(options.TargetDirectory, deletedRel);
                                if (File.Exists(deletedFull))
                                {
                                    try { File.Delete(deletedFull); } catch { }
                                }
                            }
                        }
                    }

                    result.Success = true;
                    result.Message = $"Restore completed successfully from {chain.Count} backup set(s).";
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = "Restore failed: " + ex.Message;
                result.Errors.Add(ex.ToString());
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, true);
                }
                catch { }
            }

            return result;
        }

        private static async Task DownloadRemoteDirectoryRecursiveAsync(
            IDestination destination,
            string remoteBaseDir,
            string relativePath,
            string targetDir,
            bool overwriteExisting,
            CancellationToken cancellationToken)
        {
            var currentRemote = string.IsNullOrEmpty(relativePath) ? remoteBaseDir : $"{remoteBaseDir}/{relativePath}";
            var items = await destination.ListAsync(currentRemote, cancellationToken);

            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var childRel = string.IsNullOrEmpty(relativePath) ? item.Name : $"{relativePath}/{item.Name}";

                if (item.IsDirectory)
                {
                    await DownloadRemoteDirectoryRecursiveAsync(destination, remoteBaseDir, childRel, targetDir, overwriteExisting, cancellationToken);
                }
                else
                {
                    var localTarget = Path.Combine(targetDir, childRel.Replace('/', '\\'));
                    if (File.Exists(localTarget) && !overwriteExisting)
                        continue;

                    await destination.DownloadFileAsync(item.FullPath, localTarget, null, cancellationToken);
                }
            }
        }

        private static string SanitizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Job";
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim();
        }
    }
}
