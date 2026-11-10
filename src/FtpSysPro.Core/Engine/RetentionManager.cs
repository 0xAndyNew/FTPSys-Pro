using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FtpSysPro.Core.Destinations;
using FtpSysPro.Core.Models;
using Newtonsoft.Json;

namespace FtpSysPro.Core.Engine
{
    public class RetentionManager
    {
        private readonly IDestination _destination;

        public RetentionManager(IDestination destination)
        {
            _destination = destination ?? throw new ArgumentNullException(nameof(destination));
        }

        public async Task<int> ApplyRetentionAsync(
            string jobRemoteRoot,
            RetentionPolicy policy,
            CancellationToken cancellationToken = default)
        {
            if (policy == null || policy.Type == RetentionType.KeepAll)
                return 0;

            // List all backup sets in destination
            var items = await _destination.ListAsync(jobRemoteRoot, cancellationToken);
            var manifests = new List<BackupSetManifest>();

            // Find all manifest files
            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (item.Name.EndsWith(".manifest.json", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        using (var stream = await _destination.OpenReadStreamAsync(item.FullPath, cancellationToken))
                        using (var reader = new StreamReader(stream))
                        {
                            var json = await reader.ReadToEndAsync();
                            var m = JsonConvert.DeserializeObject<BackupSetManifest>(json);
                            if (m != null) manifests.Add(m);
                        }
                    }
                    catch { }
                }
                else if (item.IsDirectory)
                {
                    // Check sub-manifest inside uncompressed folder
                    var subManifestPath = $"{item.FullPath}/manifest.json";
                    if (await _destination.FileExistsAsync(subManifestPath, cancellationToken))
                    {
                        try
                        {
                            using (var stream = await _destination.OpenReadStreamAsync(subManifestPath, cancellationToken))
                            using (var reader = new StreamReader(stream))
                            {
                                var json = await reader.ReadToEndAsync();
                                var m = JsonConvert.DeserializeObject<BackupSetManifest>(json);
                                if (m != null) manifests.Add(m);
                            }
                        }
                        catch { }
                    }
                }
            }

            if (manifests.Count == 0)
                return 0;

            // Group manifests by Full backup chain
            // Full set ID -> list of sets (Full + its incrementals)
            var chains = manifests
                .GroupBy(m => string.IsNullOrEmpty(m.BaseFullBackupSetId) ? m.BackupSetId : m.BaseFullBackupSetId)
                .OrderBy(g => g.Min(m => m.CreatedUtc))
                .ToList();

            var setsToDelete = new List<BackupSetManifest>();

            if (policy.Type == RetentionType.KeepFullCount && policy.KeepFullCount > 0)
            {
                if (chains.Count > policy.KeepFullCount)
                {
                    var chainsToRemove = chains.Take(chains.Count - policy.KeepFullCount);
                    foreach (var chain in chainsToRemove)
                    {
                        setsToDelete.AddRange(chain);
                    }
                }
            }
            else if (policy.Type == RetentionType.KeepDays && policy.KeepDays > 0)
            {
                var cutoff = DateTime.UtcNow.AddDays(-policy.KeepDays);
                foreach (var chain in chains)
                {
                    // Only prune chain if the ENTIRE chain is older than cutoff
                    var latestInChain = chain.Max(m => m.CreatedUtc);
                    if (latestInChain < cutoff)
                    {
                        setsToDelete.AddRange(chain);
                    }
                }
            }

            int deletedCount = 0;
            foreach (var set in setsToDelete)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    if (set.IsCompressed)
                    {
                        var zipPath = $"{jobRemoteRoot}/{set.ArchiveFileName}";
                        var manifestPath = $"{jobRemoteRoot}/{set.BackupSetId}.manifest.json";
                        await _destination.DeleteFileAsync(zipPath, cancellationToken);
                        await _destination.DeleteFileAsync(manifestPath, cancellationToken);
                    }
                    else
                    {
                        var dirPath = $"{jobRemoteRoot}/{set.BackupSetId}";
                        await _destination.DeleteDirectoryAsync(dirPath, true, cancellationToken);
                    }
                    deletedCount++;
                }
                catch { }
            }

            return deletedCount;
        }
    }
}
