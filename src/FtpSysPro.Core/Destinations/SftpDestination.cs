using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FtpSysPro.Core.Models;
using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;

namespace FtpSysPro.Core.Destinations
{
    public class SftpDestination : IDestination
    {
        private readonly DestinationConfig _config;
        private readonly string _plainPassword;
        private readonly string _plainKeyPassphrase;
        private SftpClient _client;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public SftpDestination(DestinationConfig config, string plainPassword, string plainKeyPassphrase = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _plainPassword = plainPassword ?? string.Empty;
            _plainKeyPassphrase = plainKeyPassphrase ?? string.Empty;
        }

        private async Task<SftpClient> GetClientAsync(CancellationToken cancellationToken)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                if (_client != null && _client.IsConnected)
                    return _client;

                _client?.Dispose();

                ConnectionInfo connInfo;
                var authMethods = new List<AuthenticationMethod>();

                if (!string.IsNullOrEmpty(_plainPassword))
                {
                    authMethods.Add(new PasswordAuthenticationMethod(_config.SftpUsername, _plainPassword));
                }

                if (!string.IsNullOrEmpty(_config.SftpPrivateKeyPath) && File.Exists(_config.SftpPrivateKeyPath))
                {
                    PrivateKeyFile keyFile;
                    if (!string.IsNullOrEmpty(_plainKeyPassphrase))
                    {
                        keyFile = new PrivateKeyFile(_config.SftpPrivateKeyPath, _plainKeyPassphrase);
                    }
                    else
                    {
                        keyFile = new PrivateKeyFile(_config.SftpPrivateKeyPath);
                    }
                    authMethods.Add(new PrivateKeyAuthenticationMethod(_config.SftpUsername, keyFile));
                }

                if (authMethods.Count == 0)
                {
                    throw new InvalidOperationException("No authentication method configured for SFTP");
                }

                connInfo = new ConnectionInfo(
                    _config.SftpHost,
                    _config.SftpPort,
                    _config.SftpUsername,
                    authMethods.ToArray())
                {
                    Timeout = TimeSpan.FromSeconds(30)
                };

                _client = new SftpClient(connInfo);

                // Host key verification (pinning)
                _client.HostKeyReceived += (sender, e) =>
                {
                    var fingerPrintHex = BitConverter.ToString(e.FingerPrint).Replace("-", ":").ToLowerInvariant();
                    if (!string.IsNullOrEmpty(_config.SftpTrustedFingerprint))
                    {
                        if (string.Equals(_config.SftpTrustedFingerprint, fingerPrintHex, StringComparison.OrdinalIgnoreCase))
                        {
                            e.CanTrust = true;
                        }
                        else
                        {
                            e.CanTrust = false;
                        }
                    }
                    else
                    {
                        // First time trust (TOFU: Trust On First Use)
                        e.CanTrust = true;
                    }
                };

                await Task.Run(() => _client.Connect(), cancellationToken);
                return _client;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private string ResolveRemotePath(string path)
        {
            var baseRemote = (_config.SftpRemotePath ?? "/").TrimEnd('/');
            if (string.IsNullOrEmpty(baseRemote)) baseRemote = "";

            var clean = path.Replace('\\', '/').TrimStart('/');
            return string.IsNullOrEmpty(baseRemote) ? "/" + clean : baseRemote + "/" + clean;
        }

        public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
        {
            var client = await GetClientAsync(cancellationToken);
            return client.IsConnected;
        }

        public async Task EnsureDirectoryAsync(string remotePath, CancellationToken cancellationToken = default)
        {
            var client = await GetClientAsync(cancellationToken);
            var resolved = ResolveRemotePath(remotePath);

            await Task.Run(() =>
            {
                var segments = resolved.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                var current = "";
                foreach (var seg in segments)
                {
                    current += "/" + seg;
                    if (!client.Exists(current))
                    {
                        try { client.CreateDirectory(current); } catch { }
                    }
                }
            }, cancellationToken);
        }

        public async Task UploadFileAsync(
            string localFilePath,
            string remoteFilePath,
            IProgress<long> progress = null,
            CancellationToken cancellationToken = default)
        {
            var client = await GetClientAsync(cancellationToken);
            var resolved = ResolveRemotePath(remoteFilePath);
            var parentDir = GetParentDirectory(resolved);

            await EnsureDirectoryAsync(parentDir, cancellationToken);

            await Task.Run(() =>
            {
                using (var fs = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    Action<ulong> p = null;
                    if (progress != null)
                    {
                        p = transferred => progress.Report((long)transferred);
                    }
                    client.UploadFile(fs, resolved, true, p);
                }
            }, cancellationToken);
        }

        public async Task UploadStreamAsync(
            Stream sourceStream,
            string remoteFilePath,
            IProgress<long> progress = null,
            CancellationToken cancellationToken = default)
        {
            var client = await GetClientAsync(cancellationToken);
            var resolved = ResolveRemotePath(remoteFilePath);
            var parentDir = GetParentDirectory(resolved);

            await EnsureDirectoryAsync(parentDir, cancellationToken);

            await Task.Run(() =>
            {
                Action<ulong> p = null;
                if (progress != null)
                {
                    p = transferred => progress.Report((long)transferred);
                }
                client.UploadFile(sourceStream, resolved, true, p);
            }, cancellationToken);
        }

        public async Task DownloadFileAsync(
            string remoteFilePath,
            string localFilePath,
            IProgress<long> progress = null,
            CancellationToken cancellationToken = default)
        {
            var client = await GetClientAsync(cancellationToken);
            var resolved = ResolveRemotePath(remoteFilePath);

            var dir = Path.GetDirectoryName(localFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            await Task.Run(() =>
            {
                using (var fs = new FileStream(localFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    Action<ulong> p = null;
                    if (progress != null)
                    {
                        p = transferred => progress.Report((long)transferred);
                    }
                    client.DownloadFile(resolved, fs, p);
                }
            }, cancellationToken);
        }

        public async Task<Stream> OpenReadStreamAsync(string remoteFilePath, CancellationToken cancellationToken = default)
        {
            var client = await GetClientAsync(cancellationToken);
            var resolved = ResolveRemotePath(remoteFilePath);

            return await Task.Run<Stream>(() =>
            {
                var ms = new MemoryStream();
                client.DownloadFile(resolved, ms);
                ms.Position = 0;
                return ms;
            }, cancellationToken);
        }

        public async Task<List<RemoteFileInfo>> ListAsync(string remotePath, CancellationToken cancellationToken = default)
        {
            var client = await GetClientAsync(cancellationToken);
            var resolved = ResolveRemotePath(remotePath);

            return await Task.Run(() =>
            {
                var list = new List<RemoteFileInfo>();
                if (!client.Exists(resolved)) return list;

                var items = client.ListDirectory(resolved);
                foreach (var item in items)
                {
                    if (item.Name == "." || item.Name == "..") continue;

                    list.Add(new RemoteFileInfo
                    {
                        FullPath = item.FullName,
                        Name = item.Name,
                        IsDirectory = item.IsDirectory,
                        SizeBytes = item.Length,
                        LastWriteTimeUtc = item.LastWriteTimeUtc
                    });
                }
                return list;
            }, cancellationToken);
        }

        public async Task DeleteFileAsync(string remoteFilePath, CancellationToken cancellationToken = default)
        {
            var client = await GetClientAsync(cancellationToken);
            var resolved = ResolveRemotePath(remoteFilePath);
            await Task.Run(() =>
            {
                if (client.Exists(resolved))
                    client.DeleteFile(resolved);
            }, cancellationToken);
        }

        public async Task DeleteDirectoryAsync(string remotePath, bool recursive, CancellationToken cancellationToken = default)
        {
            var client = await GetClientAsync(cancellationToken);
            var resolved = ResolveRemotePath(remotePath);

            await Task.Run(() =>
            {
                if (!client.Exists(resolved)) return;
                if (recursive)
                {
                    DeleteDirectoryRecursive(client, resolved);
                }
                else
                {
                    client.DeleteDirectory(resolved);
                }
            }, cancellationToken);
        }

        private static void DeleteDirectoryRecursive(SftpClient client, string path)
        {
            var items = client.ListDirectory(path);
            foreach (var item in items)
            {
                if (item.Name == "." || item.Name == "..") continue;
                if (item.IsDirectory)
                {
                    DeleteDirectoryRecursive(client, item.FullName);
                }
                else
                {
                    client.DeleteFile(item.FullName);
                }
            }
            client.DeleteDirectory(path);
        }

        public async Task RenameAsync(string oldPath, string newPath, CancellationToken cancellationToken = default)
        {
            var client = await GetClientAsync(cancellationToken);
            var resolvedOld = ResolveRemotePath(oldPath);
            var resolvedNew = ResolveRemotePath(newPath);

            await Task.Run(() =>
            {
                if (client.Exists(resolvedNew))
                    client.DeleteFile(resolvedNew);
                client.RenameFile(resolvedOld, resolvedNew);
            }, cancellationToken);
        }

        public async Task<bool> FileExistsAsync(string remoteFilePath, CancellationToken cancellationToken = default)
        {
            var client = await GetClientAsync(cancellationToken);
            return await Task.Run(() => client.Exists(ResolveRemotePath(remoteFilePath)), cancellationToken);
        }

        public async Task<bool> DirectoryExistsAsync(string remotePath, CancellationToken cancellationToken = default)
        {
            var client = await GetClientAsync(cancellationToken);
            return await Task.Run(() => client.Exists(ResolveRemotePath(remotePath)), cancellationToken);
        }

        private static string GetParentDirectory(string path)
        {
            var idx = path.LastIndexOf('/');
            if (idx > 0) return path.Substring(0, idx);
            return "/";
        }

        public void Dispose()
        {
            try
            {
                if (_client != null)
                {
                    if (_client.IsConnected) _client.Disconnect();
                    _client.Dispose();
                }
            }
            catch { }
            _semaphore?.Dispose();
        }
    }
}
