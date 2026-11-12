using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;

namespace FtpSysPro.Core.Compression
{
    public static class ZipArchiveHelper
    {
        public static void CreateZipArchive(
            string outputZipPath,
            IEnumerable<Engine.ScannedFile> files,
            int compressionLevel = 5,
            string password = null,
            IProgress<long> progress = null,
            CancellationToken cancellationToken = default)
        {
            var dir = Path.GetDirectoryName(outputZipPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            using (var fs = new FileStream(outputZipPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var zipStream = new ZipOutputStream(fs))
            {
                zipStream.SetLevel(Math.Max(0, Math.Min(9, compressionLevel)));

                if (!string.IsNullOrEmpty(password))
                {
                    zipStream.Password = password;
                }

                byte[] buffer = new byte[81920];
                long totalBytesWritten = 0;

                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Convert Windows backslashes to standard ZIP forward slashes
                    var entryName = ZipEntry.CleanName(file.RelativePath.Replace('\\', '/'));
                    var newEntry = new ZipEntry(entryName)
                    {
                        DateTime = file.LastWriteTimeUtc.ToLocalTime(),
                        Size = file.SizeBytes
                    };

                    if (!string.IsNullOrEmpty(password))
                    {
                        // AES-256 encryption via SharpZipLib
                        newEntry.AESKeySize = 256;
                    }

                    try
                    {
                        using (var fileStream = new FileStream(file.FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                        {
                            zipStream.PutNextEntry(newEntry);
                            int bytesRead;
                            while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                zipStream.Write(buffer, 0, bytesRead);
                                totalBytesWritten += bytesRead;
                                progress?.Report(totalBytesWritten);
                            }
                            zipStream.CloseEntry();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        // If file cannot be read during zip creation, log warning and continue so remaining files can be compressed
                        Serilog.Log.Warning("[ZipArchiveHelper] Không thể nén tệp '{FilePath}': {Message}. Bỏ qua tệp này.", file.FullPath, ex.Message);
                    }
                }

                zipStream.IsStreamOwner = true;
                zipStream.Finish();
            }
        }

        public static void ExtractZipArchive(
            string zipPath,
            string targetDirectory,
            string password = null,
            bool overwriteExisting = true,
            IProgress<long> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (!File.Exists(zipPath))
                throw new FileNotFoundException("ZIP archive not found", zipPath);

            if (!Directory.Exists(targetDirectory))
                Directory.CreateDirectory(targetDirectory);

            using (var fs = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var zipFile = new ZipFile(fs))
            {
                if (!string.IsNullOrEmpty(password))
                {
                    zipFile.Password = password;
                }

                byte[] buffer = new byte[81920];
                long totalExtracted = 0;

                foreach (ZipEntry entry in zipFile)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!entry.IsFile)
                        continue;

                    // Sanitize path to prevent Zip Slip vulnerability
                    var entryPath = entry.Name.Replace('/', '\\');
                    var fullDestPath = Path.GetFullPath(Path.Combine(targetDirectory, entryPath));
                    var fullTargetDir = Path.GetFullPath(targetDirectory) + Path.DirectorySeparatorChar;

                    if (!fullDestPath.StartsWith(fullTargetDir, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException($"Zip Slip detected in entry '{entry.Name}'");
                    }

                    if (File.Exists(fullDestPath) && !overwriteExisting)
                        continue;

                    var entryDir = Path.GetDirectoryName(fullDestPath);
                    if (!string.IsNullOrEmpty(entryDir) && !Directory.Exists(entryDir))
                        Directory.CreateDirectory(entryDir);

                    using (var zipStream = zipFile.GetInputStream(entry))
                    using (var outFs = new FileStream(fullDestPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        int bytesRead;
                        while ((bytesRead = zipStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            outFs.Write(buffer, 0, bytesRead);
                            totalExtracted += bytesRead;
                            progress?.Report(totalExtracted);
                        }
                    }

                    try
                    {
                        File.SetLastWriteTimeUtc(fullDestPath, entry.DateTime.ToUniversalTime());
                    }
                    catch { }
                }
            }
        }
    }
}
