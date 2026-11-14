using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
        public string SelectedSubFolder { get; set; } = string.Empty;
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
            if (job == null) throw new ArgumentNullException(nameof(job));

            using (var destination = BackupEngine.CreateDestination(job.Destination))
            {
                var jobRemoteRoot = SanitizeName(job.Name);
                return await ListAvailableBackupSetsInternalAsync(destination, jobRemoteRoot, job, cancellationToken);
            }
        }

        public async Task<List<BackupSetManifest>> ListAvailableBackupSetsFromDestinationAsync(
            DestinationConfig destinationConfig,
            string remoteRoot = "",
            CancellationToken cancellationToken = default)
        {
            if (destinationConfig == null) throw new ArgumentNullException(nameof(destinationConfig));

            using (var destination = BackupEngine.CreateDestination(destinationConfig))
            {
                return await ListAvailableBackupSetsInternalAsync(destination, remoteRoot ?? "", null, cancellationToken);
            }
        }

        private async Task<List<BackupSetManifest>> ListAvailableBackupSetsInternalAsync(
            IDestination destination,
            string remoteRoot,
            BackupJob job,
            CancellationToken cancellationToken)
        {
            var list = new List<BackupSetManifest>();

            // 1. Load from local catalog if available
            if (_configStore != null && job != null && !string.IsNullOrEmpty(job.Id))
            {
                var localSets = _configStore.LoadLocalManifests(job.Id);
                foreach (var s in localSets)
                {
                    if (!list.Any(x => x.BackupSetId == s.BackupSetId))
                        list.Add(s);
                }
            }

            // 2. Discover sets on destination
            await ScanDestinationForBackupSetsAsync(destination, remoteRoot ?? "", job, list, cancellationToken);

            return list.OrderByDescending(m => m.CreatedUtc).ToList();
        }

        private static async Task ScanDestinationForBackupSetsAsync(
            IDestination destination,
            string remotePath,
            BackupJob job,
            List<BackupSetManifest> list,
            CancellationToken cancellationToken)
        {
            var items = await destination.ListAsync(remotePath, cancellationToken);

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
                            {
                                m.RelativeLocation = remotePath ?? string.Empty;
                                if (string.IsNullOrEmpty(m.ArchiveFileName) && m.IsCompressed)
                                    m.ArchiveFileName = $"{m.BackupSetId}.zip";
                                list.Add(m);
                            }
                        }
                    }
                    catch { }
                }
                else if (item.IsDirectory)
                {
                    var subManifestPath = string.IsNullOrEmpty(remotePath)
                        ? $"{item.Name}/manifest.json"
                        : $"{remotePath}/{item.Name}/manifest.json";

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
                                    m.RelativeLocation = remotePath ?? string.Empty;
                                    list.Add(m);
                                    loaded = true;
                                }
                            }
                        }
                        catch { }
                    }

                    if (!loaded && !list.Any(x => x.BackupSetId == item.Name))
                    {
                        var match = Regex.Match(item.Name, @"(?:_|^)(\d{8}_\d{6})_(FULL|INCR)$", RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            string dateStr = match.Groups[1].Value;
                            string typeStr = match.Groups[2].Value.ToUpperInvariant();
                            if (DateTime.TryParseExact(dateStr, "yyyyMMdd_HHmmss", null, System.Globalization.DateTimeStyles.AssumeUniversal, out var dt))
                            {
                                list.Add(new BackupSetManifest
                                {
                                    BackupSetId = item.Name,
                                    JobId = job?.Id ?? string.Empty,
                                    JobName = job?.Name ?? item.Name,
                                    CreatedUtc = dt.ToUniversalTime(),
                                    BackupType = typeStr == "FULL" ? BackupType.Full : BackupType.Incremental,
                                    BaseFullBackupSetId = typeStr == "FULL" ? item.Name : string.Empty,
                                    IsCompressed = false,
                                    RelativeLocation = remotePath ?? string.Empty
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
                        var match = Regex.Match(item.Name, @"(?:_|^)(\d{8}_\d{6})_(FULL|INCR)\.zip$", RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            string dateStr = match.Groups[1].Value;
                            string typeStr = match.Groups[2].Value.ToUpperInvariant();
                            if (DateTime.TryParseExact(dateStr, "yyyyMMdd_HHmmss", null, System.Globalization.DateTimeStyles.AssumeUniversal, out var dt))
                            {
                                list.Add(new BackupSetManifest
                                {
                                    BackupSetId = setId,
                                    JobId = job?.Id ?? string.Empty,
                                    JobName = job?.Name ?? setId,
                                    CreatedUtc = dt.ToUniversalTime(),
                                    BackupType = typeStr == "FULL" ? BackupType.Full : BackupType.Incremental,
                                    BaseFullBackupSetId = typeStr == "FULL" ? setId : string.Empty,
                                    IsCompressed = true,
                                    ArchiveFileName = item.Name,
                                    TotalSizeBytes = item.SizeBytes,
                                    RelativeLocation = remotePath ?? string.Empty
                                });
                            }
                        }
                    }
                }
            }

            // Also check one level of subdirectories if this is the root directory
            if (string.IsNullOrEmpty(remotePath))
            {
                foreach (var dir in items.Where(i => i.IsDirectory && !Regex.IsMatch(i.Name, @"(?:_|^)(\d{8}_\d{6})_(FULL|INCR)$", RegexOptions.IgnoreCase)))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await ScanDestinationForBackupSetsAsync(destination, dir.Name, job, list, cancellationToken);
                }
            }
        }

        public async Task<RestoreResult> RestoreToPointInTimeAsync(
            BackupJob job,
            string targetBackupSetId,
            RestoreOptions options,
            IProgress<JobProgressInfo> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (job == null) throw new ArgumentNullException(nameof(job));

            using (var destination = BackupEngine.CreateDestination(job.Destination))
            {
                var jobRemoteRoot = SanitizeName(job.Name);
                var defaultPassword = job.Compression?.EncryptWithPassword == true
                    ? SecretProtector.Unprotect(job.Compression.PasswordProtected)
                    : null;

                return await RestoreFromDestinationInternalAsync(
                    destination,
                    jobRemoteRoot,
                    targetBackupSetId,
                    options,
                    defaultPassword,
                    job,
                    progress,
                    cancellationToken);
            }
        }

        public async Task<RestoreResult> RestoreFromDestinationToPointInTimeAsync(
            DestinationConfig destinationConfig,
            string remoteRoot,
            string targetBackupSetId,
            RestoreOptions options,
            string defaultPassword = null,
            IProgress<JobProgressInfo> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (destinationConfig == null) throw new ArgumentNullException(nameof(destinationConfig));

            using (var destination = BackupEngine.CreateDestination(destinationConfig))
            {
                return await RestoreFromDestinationInternalAsync(
                    destination,
                    remoteRoot ?? "",
                    targetBackupSetId,
                    options,
                    defaultPassword,
                    null,
                    progress,
                    cancellationToken);
            }
        }

        private async Task<RestoreResult> RestoreFromDestinationInternalAsync(
            IDestination destination,
            string remoteRoot,
            string targetBackupSetId,
            RestoreOptions options,
            string defaultPassword,
            BackupJob job,
            IProgress<JobProgressInfo> progress,
            CancellationToken cancellationToken)
        {
            var result = new RestoreResult();

            if (string.IsNullOrWhiteSpace(options.TargetDirectory))
            {
                result.Success = false;
                result.Message = "Target restore directory must be specified";
                result.Errors.Add(result.Message);
                return result;
            }

            if (!Directory.Exists(options.TargetDirectory))
            {
                Directory.CreateDirectory(options.TargetDirectory);
            }

            var baseTemp = _configStore?.TempDirectory ?? Path.Combine(_configStore?.BaseDirectory ?? AppDomain.CurrentDomain.BaseDirectory, "temp");
            var tempDir = Path.Combine(baseTemp, "restore_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                // 1. Get chain of backup sets from Full -> target
                var allSets = await ListAvailableBackupSetsInternalAsync(destination, remoteRoot, job, cancellationToken);
                var targetSet = allSets.FirstOrDefault(s => s.BackupSetId == targetBackupSetId);
                if (targetSet == null)
                {
                    result.Success = false;
                    result.Message = $"Backup set '{targetBackupSetId}' not found";
                    result.Errors.Add(result.Message);
                    return result;
                }

                List<BackupSetManifest> chain;
                if (targetSet.BackupType == BackupType.Full)
                {
                    chain = new List<BackupSetManifest> { targetSet };
                }
                else
                {
                    var baseFullId = targetSet.BaseFullBackupSetId;

                    if (!string.IsNullOrEmpty(baseFullId))
                    {
                        chain = allSets
                            .Where(s => (s.BackupSetId == baseFullId || s.BaseFullBackupSetId == baseFullId)
                                        && s.CreatedUtc <= targetSet.CreatedUtc)
                            .OrderBy(s => s.CreatedUtc)
                            .ToList();
                    }
                    else
                    {
                        // Discovered from folder/zip without manifest: find most recent Full set created before or at target
                        var baseFullSet = allSets
                            .Where(s => s.BackupType == BackupType.Full && s.CreatedUtc <= targetSet.CreatedUtc)
                            .OrderByDescending(s => s.CreatedUtc)
                            .FirstOrDefault();

                        if (baseFullSet != null)
                        {
                            chain = allSets
                                .Where(s => s.CreatedUtc >= baseFullSet.CreatedUtc && s.CreatedUtc <= targetSet.CreatedUtc)
                                .OrderBy(s => s.CreatedUtc)
                                .ToList();
                        }
                        else
                        {
                            chain = allSets
                                .Where(s => s.CreatedUtc <= targetSet.CreatedUtc)
                                .OrderBy(s => s.CreatedUtc)
                                .ToList();
                        }
                    }
                }

                // 2. Apply sets in chronological order
                foreach (var set in chain)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var effectivePassword = string.IsNullOrEmpty(options.ZipPassword)
                        ? defaultPassword
                        : options.ZipPassword;

                    if (set.IsCompressed)
                    {
                        var candidatePaths = new List<string>();
                        if (!string.IsNullOrEmpty(set.RelativeLocation))
                        {
                            candidatePaths.Add(string.IsNullOrEmpty(remoteRoot) 
                                ? $"{set.RelativeLocation}/{set.ArchiveFileName}" 
                                : $"{remoteRoot}/{set.RelativeLocation}/{set.ArchiveFileName}");
                        }
                        if (!string.IsNullOrEmpty(remoteRoot))
                        {
                            candidatePaths.Add($"{remoteRoot}/{set.ArchiveFileName}");
                        }
                        candidatePaths.Add(set.ArchiveFileName);

                        string remoteZip = null;
                        foreach (var path in candidatePaths)
                        {
                            if (await destination.FileExistsAsync(path, cancellationToken))
                            {
                                remoteZip = path;
                                break;
                            }
                        }
                        if (remoteZip == null) remoteZip = candidatePaths[0];

                        var localZip = Path.Combine(tempDir, Path.GetFileName(set.ArchiveFileName));

                        await destination.DownloadFileAsync(remoteZip, localZip, null, cancellationToken);

                        ZipArchiveHelper.ExtractZipArchive(
                            localZip,
                            options.TargetDirectory,
                            effectivePassword,
                            options.OverwriteExisting,
                            null,
                            cancellationToken,
                            options.SelectedSubFolder);

                        try { File.Delete(localZip); } catch { }
                    }
                    else
                    {
                        // Download uncompressed files
                        var candidateDirs = new List<string>();
                        if (!string.IsNullOrEmpty(set.RelativeLocation))
                        {
                            candidateDirs.Add(string.IsNullOrEmpty(remoteRoot)
                                ? $"{set.RelativeLocation}/{set.BackupSetId}"
                                : $"{remoteRoot}/{set.RelativeLocation}/{set.BackupSetId}");
                        }
                        if (!string.IsNullOrEmpty(remoteRoot))
                        {
                            candidateDirs.Add($"{remoteRoot}/{set.BackupSetId}");
                        }
                        candidateDirs.Add(set.BackupSetId);

                        string remoteDir = null;
                        foreach (var dir in candidateDirs)
                        {
                            if (await destination.DirectoryExistsAsync(dir, cancellationToken))
                            {
                                remoteDir = dir;
                                break;
                            }
                        }
                        if (remoteDir == null) remoteDir = candidateDirs[0];

                        if (set.Files != null && set.Files.Count > 0)
                        {
                            foreach (var f in set.Files)
                            {
                                cancellationToken.ThrowIfCancellationRequested();

                                if (!MatchesSubFolder(f.RelativePath, options.SelectedSubFolder))
                                    continue;

                                var remoteFile = $"{remoteDir}/{f.RelativePath.Replace('\\', '/')}";
                                var localFile = Path.Combine(options.TargetDirectory, f.RelativePath);

                                if (File.Exists(localFile) && !options.OverwriteExisting)
                                    continue;

                                await destination.DownloadFileAsync(remoteFile, localFile, null, cancellationToken);
                                result.RestoredFiles++;
                                result.RestoredBytes += f.SizeBytes;
                            }
                        }
                        else
                        {
                            await DownloadRemoteDirectoryRecursiveAsync(
                                destination,
                                remoteDir,
                                "",
                                options.TargetDirectory,
                                options.OverwriteExisting,
                                cancellationToken,
                                options.SelectedSubFolder,
                                result);
                        }
                    }

                    // Apply file deletions recorded in incremental manifest
                    if (set.DeletedRelativePaths != null)
                    {
                        foreach (var deletedRel in set.DeletedRelativePaths)
                        {
                            if (!MatchesSubFolder(deletedRel, options.SelectedSubFolder))
                                continue;

                            var deletedFull = Path.Combine(options.TargetDirectory, deletedRel);
                            if (File.Exists(deletedFull))
                            {
                                try { File.Delete(deletedFull); } catch { }
                            }
                        }
                    }
                }

                result.Success = true;
                var subFolderNote = string.IsNullOrWhiteSpace(options.SelectedSubFolder)
                    ? ""
                    : $" [Thư mục con: '{options.SelectedSubFolder}']";
                result.Message = $"Phục hồi dữ liệu thành công từ {chain.Count} điểm sao lưu (chuỗi Full + Incremental){subFolderNote}.";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = "Lỗi khi phục hồi dữ liệu: " + ex.Message;
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

        private static bool MatchesSubFolder(string relativePath, string subFolderFilter)
        {
            if (string.IsNullOrWhiteSpace(subFolderFilter))
                return true;

            var normFilter = subFolderFilter.Replace('\\', '/').Trim('/');
            var normRel = relativePath.Replace('\\', '/').Trim('/');

            return normRel.StartsWith(normFilter + "/", StringComparison.OrdinalIgnoreCase) ||
                   normRel.Equals(normFilter, StringComparison.OrdinalIgnoreCase);
        }

        private static async Task DownloadRemoteDirectoryRecursiveAsync(
            IDestination destination,
            string remoteBaseDir,
            string relativePath,
            string targetDir,
            bool overwriteExisting,
            CancellationToken cancellationToken,
            string subFolderFilter,
            RestoreResult result)
        {
            var currentRemote = string.IsNullOrEmpty(relativePath) ? remoteBaseDir : $"{remoteBaseDir}/{relativePath}";
            var items = await destination.ListAsync(currentRemote, cancellationToken);

            var normFilter = string.IsNullOrWhiteSpace(subFolderFilter)
                ? null
                : subFolderFilter.Replace('\\', '/').Trim('/');

            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var childRel = string.IsNullOrEmpty(relativePath) ? item.Name : $"{relativePath}/{item.Name}";
                var normChild = childRel.Replace('\\', '/').Trim('/');

                if (item.IsDirectory)
                {
                    if (normFilter != null)
                    {
                        bool isAncestor = normFilter.StartsWith(normChild + "/", StringComparison.OrdinalIgnoreCase);
                        bool isDescendantOrSelf = normChild.StartsWith(normFilter, StringComparison.OrdinalIgnoreCase);
                        if (!isAncestor && !isDescendantOrSelf)
                            continue;
                    }

                    await DownloadRemoteDirectoryRecursiveAsync(
                        destination,
                        remoteBaseDir,
                        childRel,
                        targetDir,
                        overwriteExisting,
                        cancellationToken,
                        subFolderFilter,
                        result);
                }
                else
                {
                    if (normFilter != null)
                    {
                        if (!normChild.StartsWith(normFilter + "/", StringComparison.OrdinalIgnoreCase) &&
                            !normChild.Equals(normFilter, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                    }

                    var localTarget = Path.Combine(targetDir, childRel.Replace('/', '\\'));
                    if (File.Exists(localTarget) && !overwriteExisting)
                        continue;

                    await destination.DownloadFileAsync(item.FullPath, localTarget, null, cancellationToken);
                    if (result != null)
                    {
                        result.RestoredFiles++;
                        result.RestoredBytes += item.SizeBytes;
                    }
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
