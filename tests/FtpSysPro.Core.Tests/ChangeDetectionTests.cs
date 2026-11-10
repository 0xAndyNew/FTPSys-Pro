using System;
using System.Collections.Generic;
using FtpSysPro.Core.Engine;
using FtpSysPro.Core.Models;
using Xunit;

namespace FtpSysPro.Core.Tests
{
    public class ChangeDetectionTests
    {
        [Fact]
        public void DetectChanges_AllAdded_WhenNoPreviousManifest()
        {
            var scanned = new List<ScannedFile>
            {
                new ScannedFile { RelativePath = "file1.txt", SizeBytes = 100, LastWriteTimeUtc = DateTime.UtcNow },
                new ScannedFile { RelativePath = "sub/file2.txt", SizeBytes = 200, LastWriteTimeUtc = DateTime.UtcNow }
            };

            var changes = ChangeDetector.Detect(scanned, null);

            Assert.Equal(2, changes.AddedFiles.Count);
            Assert.Empty(changes.ModifiedFiles);
            Assert.Empty(changes.DeletedRelativePaths);
            Assert.Empty(changes.UnchangedFiles);
        }

        [Fact]
        public void DetectChanges_IdentifiesModifiedAndDeleted()
        {
            var baseTime = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

            var previousManifest = new BackupSetManifest
            {
                Files = new List<ManifestFileEntry>
                {
                    new ManifestFileEntry { RelativePath = "keep.txt", SizeBytes = 100, LastWriteTimeUtc = baseTime },
                    new ManifestFileEntry { RelativePath = "modified.txt", SizeBytes = 100, LastWriteTimeUtc = baseTime },
                    new ManifestFileEntry { RelativePath = "deleted.txt", SizeBytes = 100, LastWriteTimeUtc = baseTime }
                }
            };

            var currentScan = new List<ScannedFile>
            {
                new ScannedFile { RelativePath = "keep.txt", SizeBytes = 100, LastWriteTimeUtc = baseTime },
                new ScannedFile { RelativePath = "modified.txt", SizeBytes = 150, LastWriteTimeUtc = baseTime.AddHours(1) },
                new ScannedFile { RelativePath = "new.txt", SizeBytes = 50, LastWriteTimeUtc = baseTime }
            };

            var changes = ChangeDetector.Detect(currentScan, previousManifest);

            Assert.Single(changes.AddedFiles);
            Assert.Equal("new.txt", changes.AddedFiles[0].RelativePath);

            Assert.Single(changes.ModifiedFiles);
            Assert.Equal("modified.txt", changes.ModifiedFiles[0].RelativePath);

            Assert.Single(changes.DeletedRelativePaths);
            Assert.Equal("deleted.txt", changes.DeletedRelativePaths[0]);

            Assert.Single(changes.UnchangedFiles);
            Assert.Equal("keep.txt", changes.UnchangedFiles[0].RelativePath);
        }
    }
}
