using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FtpSysPro.Core.Destinations
{
    public class RemoteFileInfo
    {
        public string FullPath { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        public long SizeBytes { get; set; }
        public DateTime? LastWriteTimeUtc { get; set; }
    }

    public interface IDestination : IDisposable
    {
        Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
        Task EnsureDirectoryAsync(string remotePath, CancellationToken cancellationToken = default);
        Task UploadFileAsync(
            string localFilePath,
            string remoteFilePath,
            IProgress<long> progress = null,
            CancellationToken cancellationToken = default);
        Task UploadStreamAsync(
            Stream sourceStream,
            string remoteFilePath,
            IProgress<long> progress = null,
            CancellationToken cancellationToken = default);
        Task DownloadFileAsync(
            string remoteFilePath,
            string localFilePath,
            IProgress<long> progress = null,
            CancellationToken cancellationToken = default);
        Task<Stream> OpenReadStreamAsync(
            string remoteFilePath,
            CancellationToken cancellationToken = default);
        Task<List<RemoteFileInfo>> ListAsync(
            string remotePath,
            CancellationToken cancellationToken = default);
        Task DeleteFileAsync(string remoteFilePath, CancellationToken cancellationToken = default);
        Task DeleteDirectoryAsync(string remotePath, bool recursive, CancellationToken cancellationToken = default);
        Task RenameAsync(string oldPath, string newPath, CancellationToken cancellationToken = default);
        Task<bool> FileExistsAsync(string remoteFilePath, CancellationToken cancellationToken = default);
        Task<bool> DirectoryExistsAsync(string remotePath, CancellationToken cancellationToken = default);
    }
}
