using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    public class BackupEngine
    {
        private readonly ConfigStore _configStore;
        private readonly Func<DestinationConfig, IDestination> _destinationFactory;

        public BackupEngine(ConfigStore configStore, Func<DestinationConfig, IDestination> destinationFactory = null)
        {
            _configStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
            _destinationFactory = destinationFactory ?? CreateDestination;
        }

        public static IDestination CreateDestination(DestinationConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            switch (config.Type)
            {
                case DestinationType.LocalOrUnc:
                    var uncPass = SecretProtector.Unprotect(config.UncPasswordProtected);
                    return new LocalDestination(config.LocalPath, config.UncUsername, uncPass);

                case DestinationType.Ftp:
                    var ftpPass = SecretProtector.Unprotect(config.FtpPasswordProtected);
                    return new FtpDestination(config, ftpPass);

                case DestinationType.Sftp:
                    var sftpPass = SecretProtector.Unprotect(config.SftpPasswordProtected);
                    var keyPass = SecretProtector.Unprotect(config.SftpPrivateKeyPassphraseProtected);
                    return new SftpDestination(config, sftpPass, keyPass);

                default:
                    throw new NotSupportedException($"Destination type {config.Type} is not supported");
            }
        }

        public async Task<BackupRunResult> ExecuteBackupAsync(
            BackupJob job,
            BackupType requestedType,
            IProgress<JobProgressInfo> progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new BackupRunResult
            {
                JobId = job.Id,
                JobName = job.Name,
                BackupType = requestedType,
                StartTimeUtc = DateTime.UtcNow,
                Status = JobExecutionStatus.Running
            };

            var progressInfo = new JobProgressInfo
            {
                JobId = job.Id,
                JobName = job.Name,
                BackupType = requestedType,
                CurrentPhase = "Initializing"
            };
            progress?.Report(progressInfo);

            IDestination destination = null;
            string tempDirForRun = null;
            VssSnapshotContext vssContext = null;

            try
            {
                // Setup temp directory for this run
                var baseTemp = string.IsNullOrEmpty(_configStore.TempDirectory)
                    ? Path.Combine(_configStore.BaseDirectory, "temp")
                    : _configStore.TempDirectory;
                tempDirForRun = Path.Combine(baseTemp, $"run_{result.RunId}");
                Directory.CreateDirectory(tempDirForRun);

                // Initialize destination
                destination = _destinationFactory(job.Destination);

                // Step 0: VSS Snapshot (if enabled)
                if (job.Integrity != null && job.Integrity.EnableVss)
                {
                    progressInfo.CurrentPhase = "VSS Snapshot";
                    progress?.Report(progressInfo);
                    try
                    {
                        vssContext = new VssSnapshotContext(baseTemp);
                        vssContext.TryInitializeForDirectories(job.SourceDirectories);
                        result.Warnings.AddRange(vssContext.Warnings);
                    }
                    catch (Exception ex)
                    {
                        result.Warnings.Add("VSS: Không thể khởi tạo snapshot (" + ex.Message + "). Sẽ đọc file trực tiếp.");
                    }
                }

                // Step 1: Scan sources
                progressInfo.CurrentPhase = "Scanning";
                progress?.Report(progressInfo);

                var scanResult = FileScanner.Scan(job.SourceDirectories, job.ExcludePatterns, cancellationToken);
                result.ScannedFiles = scanResult.Files.Count;
                result.ScannedBytes = scanResult.TotalSizeBytes;
                result.Warnings.AddRange(scanResult.Warnings);

                job.SourceSizeBytes = scanResult.TotalSizeBytes;
                job.SourceFileCount = scanResult.Files.Count;

                // Remap file paths to VSS shadow copy if active
                if (vssContext != null && vssContext.IsActive)
                {
                    foreach (var f in scanResult.Files)
                    {
                        f.FullPath = vssContext.MapPathToShadow(f.FullPath);
                    }
                }

                // Step 2: Determine actual backup type (Incremental -> Full if no previous full exists)
                var effectiveType = requestedType;
                BackupSetManifest previousManifest = null;
                string baseFullSetId = null;

                if (effectiveType == BackupType.Incremental)
                {
                    previousManifest = _configStore.LoadLatestLocalManifest(job.Id);
                    if (previousManifest == null)
                    {
                        effectiveType = BackupType.Full;
                        result.Warnings.Add("No previous backup manifest found. Converting Incremental backup to Full backup.");
                    }
                    else
                    {
                        baseFullSetId = string.IsNullOrEmpty(previousManifest.BaseFullBackupSetId)
                            ? previousManifest.BackupSetId
                            : previousManifest.BaseFullBackupSetId;
                    }
                }

                result.BackupType = effectiveType;
                progressInfo.BackupType = effectiveType;

                // Step 3: Change detection
                var changes = ChangeDetector.Detect(scanResult.Files, effectiveType == BackupType.Full ? null : previousManifest);
                var filesToBackup = changes.FilesToBackup;

                result.BackedUpFiles = filesToBackup.Count;
                result.BackedUpBytes = changes.TotalBytesToBackup;
                result.DeletedFiles = changes.DeletedRelativePaths.Count;

                progressInfo.TotalFiles = filesToBackup.Count;
                progressInfo.TotalBytes = changes.TotalBytesToBackup;
                progress?.Report(progressInfo);

                // Step 3.5: Pre-flight Disk Space Check (if enabled)
                if (job.Integrity != null && job.Integrity.CheckFreeDiskSpace && filesToBackup.Count > 0)
                {
                    progressInfo.CurrentPhase = "Checking Disk Space";
                    progress?.Report(progressInfo);

                    // Check Temp directory drive space
                    var tempSpaceCheck = DiskSpaceChecker.CheckFreeSpace(tempDirForRun, changes.TotalBytesToBackup);
                    if (!tempSpaceCheck.IsSufficient)
                    {
                        throw new InvalidOperationException($"Không đủ dung lượng trống trong thư mục Temp ({tempDirForRun}): {tempSpaceCheck.ErrorMessage}");
                    }

                    // Check Destination drive space if local/UNC
                    if (job.Destination.Type == DestinationType.LocalOrUnc && !string.IsNullOrEmpty(job.Destination.LocalPath))
                    {
                        var destSpaceCheck = DiskSpaceChecker.CheckFreeSpace(job.Destination.LocalPath, changes.TotalBytesToBackup);
                        if (!destSpaceCheck.IsSufficient)
                        {
                            throw new InvalidOperationException($"Không đủ dung lượng trống tại thư mục đích ({job.Destination.LocalPath}): {destSpaceCheck.ErrorMessage}");
                        }
                    }
                }

                // Step 4: Prepare backup set name and remote folders
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                var typeSuffix = effectiveType == BackupType.Full ? "FULL" : "INCR";
                var backupSetName = $"{SanitizeName(job.Name)}_{timestamp}_{typeSuffix}";
                result.BackupSetName = backupSetName;

                var jobRemoteRoot = SanitizeName(job.Name);
                var setRemoteDir = $"{jobRemoteRoot}/{backupSetName}";
                await destination.EnsureDirectoryAsync(jobRemoteRoot, cancellationToken);

                // Build manifest
                var manifest = new BackupSetManifest
                {
                    BackupSetId = backupSetName,
                    JobId = job.Id,
                    JobName = job.Name,
                    BackupType = effectiveType,
                    CreatedUtc = DateTime.UtcNow,
                    BaseFullBackupSetId = effectiveType == BackupType.Full ? backupSetName : baseFullSetId,
                    PreviousBackupSetId = previousManifest?.BackupSetId ?? string.Empty,
                    IsCompressed = job.Compression.Enabled,
                    IsEncrypted = job.Compression.Enabled && job.Compression.EncryptWithPassword,
                    DeletedRelativePaths = changes.DeletedRelativePaths,
                    TotalSizeBytes = changes.TotalBytesToBackup,
                    FileCount = filesToBackup.Count
                };

                // Step 4.5: Configure integrity parameters
                int retryCount = job.Integrity?.NetworkRetryCount ?? 3;
                bool verifySha = job.Integrity?.VerifySha256Checksum ?? true;

                foreach (var f in filesToBackup)
                {
                    string sha = string.Empty;
                    if (verifySha && !job.Compression.Enabled)
                    {
                        try { sha = ChecksumVerifier.ComputeSha256(f.FullPath, cancellationToken); } catch { }
                    }

                    manifest.Files.Add(new ManifestFileEntry
                    {
                        SourceRoot = f.SourceRoot,
                        RelativePath = f.RelativePath,
                        SizeBytes = f.SizeBytes,
                        LastWriteTimeUtc = f.LastWriteTimeUtc,
                        StoredRelativePath = f.RelativePath,
                        Sha256 = sha
                    });
                }

                // If nothing to backup and incremental with no deleted files
                if (filesToBackup.Count == 0 && changes.DeletedRelativePaths.Count == 0 && effectiveType == BackupType.Incremental)
                {
                    result.Status = JobExecutionStatus.Success;
                    result.Message = "No changes detected since previous backup.";
                    result.EndTimeUtc = DateTime.UtcNow;
                    return result;
                }

                // Step 5: Transfer / Compress with Retry and Checksum
                if (job.Compression.Enabled)
                {
                    await ExecuteCompressedBackupAsync(
                        job,
                        filesToBackup,
                        tempDirForRun,
                        destination,
                        jobRemoteRoot,
                        backupSetName,
                        manifest,
                        progressInfo,
                        progress,
                        retryCount,
                        verifySha,
                        cancellationToken);
                }
                else
                {
                    await ExecuteUncompressedBackupAsync(
                        filesToBackup,
                        destination,
                        setRemoteDir,
                        manifest,
                        progressInfo,
                        progress,
                        retryCount,
                        verifySha,
                        cancellationToken);
                }

                // Step 6: Upload manifest to destination (this commits the backup set)
                progressInfo.CurrentPhase = "Finalizing";
                progress?.Report(progressInfo);

                var manifestJson = JsonConvert.SerializeObject(manifest, Formatting.Indented);
                var localManifestFile = Path.Combine(tempDirForRun, "manifest.json");
                File.WriteAllText(localManifestFile, manifestJson);

                var remoteManifestPath = $"{jobRemoteRoot}/{backupSetName}.manifest.json";
                await RetryHelper.ExecuteWithRetryAsync(
                    () => destination.UploadFileAsync(localManifestFile, remoteManifestPath, null, cancellationToken),
                    retryCount,
                    2000,
                    null,
                    cancellationToken);

                if (!job.Compression.Enabled)
                {
                    // Also save manifest inside uncompressed directory for self-containment
                    try
                    {
                        var innerManifestPath = $"{setRemoteDir}/manifest.json";
                        await destination.UploadFileAsync(localManifestFile, innerManifestPath, null, cancellationToken);
                    }
                    catch { }
                }

                result.ManifestPathOnDestination = remoteManifestPath;

                // Step 7: Save local manifest copy for incremental change detection
                _configStore.SaveLocalManifest(manifest);

                // Step 8: Apply retention
                progressInfo.CurrentPhase = "Applying Retention";
                progress?.Report(progressInfo);

                try
                {
                    var retention = new RetentionManager(destination);
                    var pruned = await retention.ApplyRetentionAsync(jobRemoteRoot, job.Retention, cancellationToken);
                    if (pruned > 0)
                    {
                        // Pruning old sets per retention policy is normal operation, not a warning
                        Debug.WriteLine($"[BackupEngine] Retention: pruned {pruned} old backup set(s).");
                    }
                }
                catch (Exception ex)
                {
                    result.Warnings.Add($"Retention failed: {ex.Message}");
                }

                result.Status = result.Warnings.Count > 0 ? JobExecutionStatus.Warning : JobExecutionStatus.Success;
                result.Message = $"Backup completed successfully. {filesToBackup.Count} files ({FormatBytes(changes.TotalBytesToBackup)}).";
            }
            catch (OperationCanceledException)
            {
                result.Status = JobExecutionStatus.Cancelled;
                result.Message = "Backup was cancelled by user.";
            }
            catch (Exception ex)
            {
                result.Status = JobExecutionStatus.Failed;
                result.Message = "Backup failed: " + ex.Message;
                result.Errors.Add(ex.ToString());
            }
            finally
            {
                result.EndTimeUtc = DateTime.UtcNow;
                result.WarningCount = result.Warnings.Count;
                result.ErrorCount = result.Errors.Count;
                destination?.Dispose();
                vssContext?.Dispose();

                // Clean up temp dir
                if (tempDirForRun != null && Directory.Exists(tempDirForRun))
                {
                    try { Directory.Delete(tempDirForRun, true); } catch { }
                }

                // Record history
                _configStore.SaveHistoryRecord(result);

                // Update job state
                job.LastRunUtc = result.StartTimeUtc;
                job.LastStatus = result.Status;
                job.LastMessage = result.Message;
                job.LastSizeBytes = result.BackedUpBytes;
                job.LastFileCount = result.BackedUpFiles;
            }

            return result;
        }

        private static async Task ExecuteCompressedBackupAsync(
            BackupJob job,
            List<ScannedFile> files,
            string tempDir,
            IDestination destination,
            string jobRemoteRoot,
            string backupSetName,
            BackupSetManifest manifest,
            JobProgressInfo progressInfo,
            IProgress<JobProgressInfo> progress,
            int retryCount,
            bool verifySha256,
            CancellationToken cancellationToken)
        {
            var zipFileName = $"{backupSetName}.zip";
            var localZipPath = Path.Combine(tempDir, zipFileName);
            var remoteZipFinal = $"{jobRemoteRoot}/{zipFileName}";
            var remoteZipPartial = $"{jobRemoteRoot}/{zipFileName}.partial";

            manifest.ArchiveFileName = zipFileName;

            // Compress to local temp
            progressInfo.CurrentPhase = "Compressing";
            progress?.Report(progressInfo);

            var password = job.Compression.EncryptWithPassword
                ? SecretProtector.Unprotect(job.Compression.PasswordProtected)
                : null;

            var compressProgress = new Progress<long>(bytes =>
            {
                progressInfo.ProcessedBytes = bytes;
                if (progressInfo.TotalBytes > 0)
                    progressInfo.PercentComplete = Math.Min(50.0, (double)bytes / progressInfo.TotalBytes * 50.0);
                progress?.Report(progressInfo);
            });

            await Task.Run(() =>
            {
                ZipArchiveHelper.CreateZipArchive(
                    localZipPath,
                    files,
                    (int)job.Compression.Level,
                    password,
                    compressProgress,
                    cancellationToken);
            }, cancellationToken);

            // Compute local SHA-256 before upload
            string localSha = string.Empty;
            if (verifySha256)
            {
                progressInfo.CurrentPhase = "Calculating Checksum";
                progress?.Report(progressInfo);
                localSha = ChecksumVerifier.ComputeSha256(localZipPath, cancellationToken);
            }

            // Upload zip file with retry
            progressInfo.CurrentPhase = "Uploading";
            var zipSize = new FileInfo(localZipPath).Length;
            progressInfo.TotalBytes = zipSize;
            progress?.Report(progressInfo);

            var uploadProgress = new Progress<long>(bytes =>
            {
                progressInfo.ProcessedBytes = bytes;
                if (zipSize > 0)
                    progressInfo.PercentComplete = 50.0 + ((double)bytes / zipSize * 50.0);
                progress?.Report(progressInfo);
            });

            await RetryHelper.ExecuteWithRetryAsync(
                () => destination.UploadFileAsync(localZipPath, remoteZipPartial, uploadProgress, cancellationToken),
                retryCount,
                3000,
                null,
                cancellationToken);

            // Verify checksum at destination if enabled
            if (verifySha256 && !string.IsNullOrEmpty(localSha))
            {
                progressInfo.CurrentPhase = "Verifying Checksum";
                progress?.Report(progressInfo);

                using (var remoteStream = await destination.OpenReadStreamAsync(remoteZipPartial, cancellationToken))
                {
                    var remoteSha = await ChecksumVerifier.ComputeSha256Async(remoteStream, cancellationToken);
                    if (!ChecksumVerifier.VerifyChecksum(localSha, remoteSha))
                    {
                        throw new InvalidOperationException($"Checksum verification failed for '{zipFileName}'. " +
                            $"Nguồn ({localSha}) không khớp với đích ({remoteSha}). Dữ liệu có thể bị lỗi đường truyền.");
                    }
                }
            }

            // Commit upload by renaming partial to final
            await RetryHelper.ExecuteWithRetryAsync(
                () => destination.RenameAsync(remoteZipPartial, remoteZipFinal, cancellationToken),
                retryCount,
                2000,
                null,
                cancellationToken);
        }

        private static async Task ExecuteUncompressedBackupAsync(
            List<ScannedFile> files,
            IDestination destination,
            string setRemoteDir,
            BackupSetManifest manifest,
            JobProgressInfo progressInfo,
            IProgress<JobProgressInfo> progress,
            int retryCount,
            bool verifySha256,
            CancellationToken cancellationToken)
        {
            progressInfo.CurrentPhase = "Uploading";
            long totalBytesUploaded = 0;
            int fileIndex = 0;

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                fileIndex++;
                progressInfo.CurrentFile = file.RelativePath;
                progressInfo.ProcessedFiles = fileIndex;

                var remoteFilePath = $"{setRemoteDir}/{file.RelativePath.Replace('\\', '/')}";

                var fileProgress = new Progress<long>(bytes =>
                {
                    var currentTotal = totalBytesUploaded + bytes;
                    progressInfo.ProcessedBytes = currentTotal;
                    if (progressInfo.TotalBytes > 0)
                        progressInfo.PercentComplete = Math.Min(100.0, (double)currentTotal / progressInfo.TotalBytes * 100.0);
                    progress?.Report(progressInfo);
                });

                await RetryHelper.ExecuteWithRetryAsync(
                    () => destination.UploadFileAsync(file.FullPath, remoteFilePath, fileProgress, cancellationToken),
                    retryCount,
                    2000,
                    null,
                    cancellationToken);

                // Verify file checksum after upload if enabled
                if (verifySha256)
                {
                    var manifestEntry = manifest?.Files.Find(x => x.RelativePath == file.RelativePath);
                    if (manifestEntry != null && !string.IsNullOrEmpty(manifestEntry.Sha256))
                    {
                        using (var remoteStream = await destination.OpenReadStreamAsync(remoteFilePath, cancellationToken))
                        {
                            var remoteSha = await ChecksumVerifier.ComputeSha256Async(remoteStream, cancellationToken);
                            if (!ChecksumVerifier.VerifyChecksum(manifestEntry.Sha256, remoteSha))
                            {
                                throw new InvalidOperationException($"Checksum verification failed for '{file.RelativePath}'. " +
                                    $"Nguồn ({manifestEntry.Sha256}) không khớp với đích ({remoteSha}).");
                            }
                        }
                    }
                }

                totalBytesUploaded += file.SizeBytes;
            }
        }

        private static string SanitizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Job";
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim();
        }

        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int i = 0;
            double dbl = bytes;
            while (dbl >= 1024 && i < suffixes.Length - 1)
            {
                dbl /= 1024;
                i++;
            }
            return $"{dbl:0.##} {suffixes[i]}";
        }
    }
}
