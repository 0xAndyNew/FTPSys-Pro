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

        [Fact]
        public void FileScanner_ExcludesSystemVolumeFoldersAndFiles()
        {
            var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "FtpSysPro_ScannerTest_" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(tempDir);
            try
            {
                var recycleBin = System.IO.Path.Combine(tempDir, "$RECYCLE.BIN");
                var sysVolInfo = System.IO.Path.Combine(tempDir, "System Volume Information");
                var docs = System.IO.Path.Combine(tempDir, "Documents");
                System.IO.Directory.CreateDirectory(recycleBin);
                System.IO.Directory.CreateDirectory(sysVolInfo);
                System.IO.Directory.CreateDirectory(docs);

                System.IO.File.WriteAllText(System.IO.Path.Combine(recycleBin, "deleted.bin"), "junk");
                System.IO.File.WriteAllText(System.IO.Path.Combine(sysVolInfo, "tracking.log"), "log");
                System.IO.File.WriteAllText(System.IO.Path.Combine(tempDir, "pagefile.sys"), "pagefile");
                System.IO.File.WriteAllText(System.IO.Path.Combine(tempDir, "swapfile.sys"), "swapfile");
                System.IO.File.WriteAllText(System.IO.Path.Combine(tempDir, "normal.txt"), "hello");
                System.IO.File.WriteAllText(System.IO.Path.Combine(docs, "file.docx"), "data");

                var scan = FileScanner.Scan(new[] { tempDir }, new string[0]);

                Assert.Equal(2, scan.Files.Count);
                var relPaths = new List<string>(scan.Files.ConvertAll(f => f.RelativePath.Replace('\\', '/')));
                Assert.Contains("normal.txt", relPaths);
                Assert.Contains("Documents/file.docx", relPaths);
            }
            finally
            {
                if (System.IO.Directory.Exists(tempDir))
                {
                    try { System.IO.Directory.Delete(tempDir, true); } catch { }
                }
            }
        }

        [Fact]
        public void FileScanner_MultiSource_SeparatesFoldersWithoutCollision()
        {
            var baseDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "FtpSysPro_MultiTest_" + Guid.NewGuid().ToString("N"));
            var dir1 = System.IO.Path.Combine(baseDir, "Source1");
            var dir2 = System.IO.Path.Combine(baseDir, "Source2");

            try
            {
                System.IO.Directory.CreateDirectory(dir1);
                System.IO.Directory.CreateDirectory(dir2);

                // Both sources have a file with the identical relative name ".gitignore"
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir1, ".gitignore"), "ignore1");
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir2, ".gitignore"), "ignore2");

                var scan = FileScanner.Scan(new[] { dir1, dir2 }, new string[0]);

                Assert.Equal(2, scan.Files.Count);
                var relPaths = new List<string>(scan.Files.ConvertAll(f => f.RelativePath.Replace('\\', '/')));
                Assert.Contains("Source1/.gitignore", relPaths);
                Assert.Contains("Source2/.gitignore", relPaths);
            }
            finally
            {
                if (System.IO.Directory.Exists(baseDir))
                {
                    try { System.IO.Directory.Delete(baseDir, true); } catch { }
                }
            }
        }
    }
}
