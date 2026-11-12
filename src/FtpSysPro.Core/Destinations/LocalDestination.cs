using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace FtpSysPro.Core.Destinations
{
    public class LocalDestination : IDestination
    {
        private readonly string _basePath;
        private readonly string _uncUsername;
        private readonly string _uncPassword;
        private bool _uncConnected;

        public LocalDestination(string basePath, string uncUsername = null, string uncPassword = null)
        {
            if (string.IsNullOrWhiteSpace(basePath))
                throw new ArgumentException("Base path cannot be empty", nameof(basePath));

            _basePath = basePath.TrimEnd('\\', '/');
            _uncUsername = uncUsername;
            _uncPassword = uncPassword;
        }

        private void EnsureUncConnection()
        {
            if (_uncConnected) return;
            if (string.IsNullOrEmpty(_uncUsername)) return;
            if (!_basePath.StartsWith(@"\\")) return;

            var nr = new NETRESOURCE
            {
                dwType = RESOURCETYPE_DISK,
                lpRemoteName = _basePath
            };

            int result = WNetAddConnection2(ref nr, _uncPassword, _uncUsername, 0);
            // 0 = OK, 1219 = multiple connections with different creds (already connected)
            if (result != 0 && result != 1219)
            {
                throw new Win32Exception(result, $"Failed to connect to UNC share '{_basePath}'");
            }
            _uncConnected = true;
        }

        private string ResolvePath(string relativeOrFullPath)
        {
            if (Path.IsPathRooted(relativeOrFullPath) && relativeOrFullPath.StartsWith(_basePath, StringComparison.OrdinalIgnoreCase))
                return relativeOrFullPath;

            var rel = relativeOrFullPath.Replace('/', '\\').TrimStart('\\');
            return Path.Combine(_basePath, rel);
        }

        public Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                EnsureUncConnection();
                if (!Directory.Exists(_basePath))
                {
                    Directory.CreateDirectory(_basePath);
                }

                // Try write test file
                var testFile = Path.Combine(_basePath, ".ftpsyspro_test_" + Guid.NewGuid().ToString("N"));
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                return true;
            }, cancellationToken);
        }

        public Task EnsureDirectoryAsync(string remotePath, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                EnsureUncConnection();
                var target = ResolvePath(remotePath);
                if (!Directory.Exists(target))
                {
                    Directory.CreateDirectory(target);
                }
            }, cancellationToken);
        }

        public Task UploadFileAsync(
            string localFilePath,
            string remoteFilePath,
            IProgress<long> progress = null,
            CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                EnsureUncConnection();
                var target = ResolvePath(remoteFilePath);
                var dir = Path.GetDirectoryName(target);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                using (var source = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var dest = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    CopyStreamWithProgress(source, dest, progress, cancellationToken);
                }
            }, cancellationToken);
        }

        public Task UploadStreamAsync(
            Stream sourceStream,
            string remoteFilePath,
            IProgress<long> progress = null,
            CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                EnsureUncConnection();
                var target = ResolvePath(remoteFilePath);
                var dir = Path.GetDirectoryName(target);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                using (var dest = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    CopyStreamWithProgress(sourceStream, dest, progress, cancellationToken);
                }
            }, cancellationToken);
        }

        public Task DownloadFileAsync(
            string remoteFilePath,
            string localFilePath,
            IProgress<long> progress = null,
            CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                EnsureUncConnection();
                var source = ResolvePath(remoteFilePath);
                var dir = Path.GetDirectoryName(localFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                using (var src = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var dest = new FileStream(localFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    CopyStreamWithProgress(src, dest, progress, cancellationToken);
                }
            }, cancellationToken);
        }

        public Task<Stream> OpenReadStreamAsync(string remoteFilePath, CancellationToken cancellationToken = default)
        {
            return Task.Run<Stream>(() =>
            {
                EnsureUncConnection();
                var source = ResolvePath(remoteFilePath);
                return new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
            }, cancellationToken);
        }

        public Task<List<RemoteFileInfo>> ListAsync(string remotePath, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                EnsureUncConnection();
                var target = ResolvePath(remotePath);
                var list = new List<RemoteFileInfo>();

                if (!Directory.Exists(target))
                    return list;

                var dirInfo = new DirectoryInfo(target);
                foreach (var d in dirInfo.GetDirectories())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    list.Add(new RemoteFileInfo
                    {
                        FullPath = d.FullName,
                        Name = d.Name,
                        IsDirectory = true,
                        SizeBytes = 0,
                        LastWriteTimeUtc = d.LastWriteTimeUtc
                    });
                }

                foreach (var f in dirInfo.GetFiles())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    list.Add(new RemoteFileInfo
                    {
                        FullPath = f.FullName,
                        Name = f.Name,
                        IsDirectory = false,
                        SizeBytes = f.Length,
                        LastWriteTimeUtc = f.LastWriteTimeUtc
                    });
                }

                return list;
            }, cancellationToken);
        }

        public Task DeleteFileAsync(string remoteFilePath, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                EnsureUncConnection();
                var target = ResolvePath(remoteFilePath);
                if (File.Exists(target))
                    File.Delete(target);
            }, cancellationToken);
        }

        public Task DeleteDirectoryAsync(string remotePath, bool recursive, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                EnsureUncConnection();
                var target = ResolvePath(remotePath);
                if (Directory.Exists(target))
                    Directory.Delete(target, recursive);
            }, cancellationToken);
        }

        public Task RenameAsync(string oldPath, string newPath, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                EnsureUncConnection();
                var oldTarget = ResolvePath(oldPath);
                var newTarget = ResolvePath(newPath);

                if (File.Exists(oldTarget))
                {
                    if (File.Exists(newTarget)) File.Delete(newTarget);
                    File.Move(oldTarget, newTarget);
                }
                else if (Directory.Exists(oldTarget))
                {
                    if (Directory.Exists(newTarget)) Directory.Delete(newTarget, true);
                    Directory.Move(oldTarget, newTarget);
                }
            }, cancellationToken);
        }

        public Task<bool> FileExistsAsync(string remoteFilePath, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                EnsureUncConnection();
                return File.Exists(ResolvePath(remoteFilePath));
            }, cancellationToken);
        }

        public Task<bool> DirectoryExistsAsync(string remotePath, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                EnsureUncConnection();
                return Directory.Exists(ResolvePath(remotePath));
            }, cancellationToken);
        }

        private static void CopyStreamWithProgress(
            Stream source,
            Stream destination,
            IProgress<long> progress,
            CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[81920]; // 80 KB
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                destination.Write(buffer, 0, bytesRead);
                totalRead += bytesRead;
                progress?.Report(totalRead);
            }
        }

        public void Dispose()
        {
            // Optional: disconnect UNC share if desired, but typically kept open
        }

        #region Win32 P/Invoke for UNC
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct NETRESOURCE
        {
            public uint dwScope;
            public uint dwType;
            public uint dwDisplayType;
            public uint dwUsage;
            public string lpLocalName;
            public string lpRemoteName;
            public string lpComment;
            public string lpProvider;
        }

        private const uint RESOURCETYPE_DISK = 0x00000001;

        [DllImport("mpr.dll", CharSet = CharSet.Auto)]
        private static extern int WNetAddConnection2(
            ref NETRESOURCE lpNetResource,
            string lpPassword,
            string lpUsername,
            uint dwFlags);
        #endregion
    }
}
