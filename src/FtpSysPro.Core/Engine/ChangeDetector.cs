using System;
using System.Collections.Generic;
using System.Linq;
using FtpSysPro.Core.Models;

namespace FtpSysPro.Core.Engine
{
    public class DetectedChanges
    {
        public List<ScannedFile> AddedFiles { get; set; } = new List<ScannedFile>();
        public List<ScannedFile> ModifiedFiles { get; set; } = new List<ScannedFile>();
        public List<string> DeletedRelativePaths { get; set; } = new List<string>();
        public List<ScannedFile> UnchangedFiles { get; set; } = new List<ScannedFile>();

        public List<ScannedFile> FilesToBackup
        {
            get
            {
                var list = new List<ScannedFile>();
                list.AddRange(AddedFiles);
                list.AddRange(ModifiedFiles);
                return list;
            }
        }

        public long TotalBytesToBackup => FilesToBackup.Sum(f => f.SizeBytes);
        public bool HasChanges => AddedFiles.Count > 0 || ModifiedFiles.Count > 0 || DeletedRelativePaths.Count > 0;
    }

    public static class ChangeDetector
    {
        public static DetectedChanges Detect(
            List<ScannedFile> currentScan,
            BackupSetManifest previousManifest)
        {
            var changes = new DetectedChanges();

            if (previousManifest == null || previousManifest.Files == null || previousManifest.Files.Count == 0)
            {
                // First backup or no prior manifest -> everything is added
                changes.AddedFiles.AddRange(currentScan);
                return changes;
            }

            // Build dictionary of previous files keyed by relative path (case-insensitive for Windows)
            var prevDict = new Dictionary<string, ManifestFileEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in previousManifest.Files)
            {
                prevDict[entry.RelativePath] = entry;
            }

            var currentSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var current in currentScan)
            {
                currentSeen.Add(current.RelativePath);

                if (!prevDict.TryGetValue(current.RelativePath, out var prev))
                {
                    // New file
                    changes.AddedFiles.Add(current);
                }
                else
                {
                    // Check if modified: different size or different LastWriteTime
                    // Allow 2-second tolerance for FAT filesystem timestamp differences
                    var timeDiff = Math.Abs((current.LastWriteTimeUtc - prev.LastWriteTimeUtc).TotalSeconds);
                    bool modified = (current.SizeBytes != prev.SizeBytes) || (timeDiff > 2.0);

                    if (modified)
                    {
                        changes.ModifiedFiles.Add(current);
                    }
                    else
                    {
                        changes.UnchangedFiles.Add(current);
                    }
                }
            }

            // Detect deleted files: present in previous manifest but not in current scan
            foreach (var kvp in prevDict)
            {
                if (!currentSeen.Contains(kvp.Key))
                {
                    changes.DeletedRelativePaths.Add(kvp.Key);
                }
            }

            return changes;
        }
    }
}
