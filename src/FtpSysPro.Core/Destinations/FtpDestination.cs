using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using FluentFTP;
using FtpSysPro.Core.Models;

namespace FtpSysPro.Core.Destinations
{
    public class FtpDestination : IDestination
    {
        private readonly DestinationConfig _config;
        private readonly string _plainPassword;
        private AsyncFtpClient _client;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public FtpDestination(DestinationConfig config, string plainPassword)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _plainPassword = plainPassword ?? string.Empty;
        }

        private async Task<AsyncFtpClient> GetClientAsync(CancellationToken cancellationToken)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                if (_client != null && _client.IsConnected)
                    return _client;

                _client?.Dispose();

                var clientConfig = new FtpConfig
                {
                    DataConnectionType = _config.FtpPassiveMode ? FtpDataConnectionType.AutoPassive : FtpDataConnectionType.AutoActive,
                    ConnectTimeout = 15000,
                    ReadTimeout = 30000,
                    DataConnectionConnectTimeout = 15000,
                    DataConnectionReadTimeout = 30000,
                    RetryAttempts = 3
                };

                if (!_config.FtpValidateCertificate)
                {
                    clientConfig.ValidateAnyCertificate = true;
                }

                _client = new AsyncFtpClient(
                    _config.FtpHost,
                    new NetworkCredential(_config.FtpUsername, _plainPassword),
                    _config.FtpPort,
                    clientConfig);

                switch (_config.FtpEncryption)
                {
                    case Models.FtpEncryptionMode.Explicit:
                        _client.Config.EncryptionMode = FluentFTP.FtpEncryptionMode.Explicit;
                        _client.Config.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls;
                        break;
                    case Models.FtpEncryptionMode.Implicit:
                        _client.Config.EncryptionMode = FluentFTP.FtpEncryptionMode.Implicit;
                        _client.Config.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls;
                        break;
                    default:
                        _client.Config.EncryptionMode = FluentFTP.FtpEncryptionMode.None;
                        break;
                }

                await _client.AutoConnect(cancellationToken);
                return _client;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private string ResolveRemotePath(string path)
        {
            var baseRemote = (_config.FtpRemotePath ?? "/").TrimEnd('/');
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
            await client.CreateDirectory(resolved, cancellationToken);
        }

        public async Task UploadFileAsync(
            string localFilePath,
            string remoteFilePath,
            IProgress<long> progress = null,
            CancellationToken cancellationToken = default)
        {
            var client = await GetClientAsync(cancellationToken);
            var resolved = ResolveRemotePath(remoteFilePath);

            var dir = GetParentDirectory(resolved);
            if (!string.IsNullOrEmpty(dir))
            {
                await client.CreateDirectory(dir, cancellationToken);
            }

            IProgress<FtpProgress> p = null;
            if (progress != null)
            {
                p = new Progress<FtpProgress>(ftpProg => progress.Report(ftpProg.TransferredBytes));
            }

            await client.UploadFile(
                localFilePath,
                resolved,
                FtpRemoteExists.Overwrite,
                createRemoteDir: true,
                progress: p,
                token: cancellationToken);
        }

        public async Task UploadStreamAsync(
            Stream sourceStream,
            string remoteFilePath,
            IProgress<long> progress = null,
            CancellationToken cancellationToken = default)
        {
            var client = await GetClientAsync(cancellationToken);
            var resolved = ResolveRemotePath(remoteFilePath);

            IProgress<FtpProgress> p = null;
            if (progress != null)
            {
                p = new Progress<FtpProgress>(ftpProg => progress.Report(ftpProg.TransferredBytes));
            }

            await client.UploadStream(
                sourceStream,
                resolved,
                FtpRemoteExists.Overwrite,
                createRemoteDir: true,
                progress: p,
                token: cancellationToken);
        }

        public async Task DownloadFileAsync(
            string remoteFilePath,
            string localFilePath,
            IProgress<long> progress = null,
            CancellationToken cancellationToken = default)
        {
            var client = await GetClientAsync(cancellationToken);
            var resolved = ResolveRemotePath(remoteFilePath);

            var localDir = Path.GetDirectoryName(localFilePath);
            if (!string.IsNullOrEmpty(localDir) && !Directory.Exists(localDir))
                Directory.CreateDirectory(localDir);

            IProgress<FtpProgress> p = null;
            if (progress != null)
            {
                p = new Progress<FtpProgress>(ftpProg => progress.Report(ftpProg.TransferredBytes));
            }

            await client.DownloadFile(
                localFilePath,
                resolved,
                FtpLocalExists.Overwrite,
                progress: p,
                token: cancellationToken);
        }

        public async Task<Stream> OpenReadStreamAsync(string remoteFilePath, CancellationToken cancellationToken = default)
        {
            var client = await GetClientAsync(cancellationToken);
            var resolved = ResolveRemotePath(remoteFilePath);
            return await client.OpenRead(resolved, token: cancellationToken);
        }

        public async Task<List<RemoteFileInfo>> ListAsync(string remotePath, CancellationToken cancellationToken = default)
        {
            var client = await GetClientAsync(cancellationToken);
            var resolved = ResolveRemotePath(remotePath);
            var list = new List<RemoteFileInfo>();

            if (!await client.DirectoryExists(resolved, cancellationToken))
                return list;

            var items = await client.GetListing(resolved, cancellationToken);
            foreach (var item in items)
            {
                list.Add(new RemoteFileInfo
                {
                    FullPath = item.FullName,
                    Name = item.Name,
                    IsDirectory = item.Type == FtpObjectType.Directory,
                    SizeBytes = item.Size,
                    LastWriteTimeUtc = item.Modified.ToUniversalTime()
                });
            }

            return list;
        }

        public async Task DeleteFileAsync(string remoteFilePath, CancellationToken cancellationToken = default)
        {
            var client = await GetClientAsync(cancellationToken);
            var resolved = ResolveRemotePath(remoteFilePath);
            if (await client.FileExists(resolved, cancellationToken))
                await client.DeleteFile(resolved, cancellationToken);
        }

        public async Task DeleteDirectoryAsync(string remotePath, bool recursive, CancellationToken cancellationToken = default)
        {
            var client = await GetClientAsync(cancellationToken);
            var resolved = ResolveRemotePath(remotePath);
            if (await client.DirectoryExists(resolved, cancellationToken))
            {
                var options = recursive ? FtpListOption.AllFiles : FtpListOption.Auto;
                await client.DeleteDirectory(resolved, options, cancellationToken);
            }
        }

        public async Task RenameAsync(string oldPath, string newPath, CancellationToken cancellationToken = default)
        {
            var client = await GetClientAsync(cancellationToken);
            var resolvedOld = ResolveRemotePath(oldPath);
            var resolvedNew = ResolveRemotePath(newPath);

            if (await client.FileExists(resolvedNew, cancellationToken))
                await client.DeleteFile(resolvedNew, cancellationToken);

            await client.Rename(resolvedOld, resolvedNew, cancellationToken);
        }

        public async Task<bool> FileExistsAsync(string remoteFilePath, CancellationToken cancellationToken = default)
        {
            var client = await GetClientAsync(cancellationToken);
            return await client.FileExists(ResolveRemotePath(remoteFilePath), cancellationToken);
        }

        public async Task<bool> DirectoryExistsAsync(string remotePath, CancellationToken cancellationToken = default)
        {
            var client = await GetClientAsync(cancellationToken);
            return await client.DirectoryExists(ResolveRemotePath(remotePath), cancellationToken);
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
                    if (_client.IsConnected)
                    {
                        // Sync disconnect on dispose
                        _client.Disconnect().GetAwaiter().GetResult();
                    }
                    _client.Dispose();
                }
            }
            catch { }
            _semaphore?.Dispose();
        }
    }
}
