using System;
using System.IO;
using System.Threading.Tasks;
using FtpSysPro.Core.Config;
using FtpSysPro.Core.Engine;
using FtpSysPro.Core.Models;
using Xunit;

namespace FtpSysPro.Core.Tests
{
    public class EndToEndBackupTests : IDisposable
    {
        private readonly string _tempRoot;
        private readonly string _sourceDir;
        private readonly string _destDir;
        private readonly string _restoreDir;
        private readonly ConfigStore _configStore;

        public EndToEndBackupTests()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "FtpSysPro_E2E_" + Guid.NewGuid().ToString("N"));
            _sourceDir = Path.Combine(_tempRoot, "source");
            _destDir = Path.Combine(_tempRoot, "destination");
            _restoreDir = Path.Combine(_tempRoot, "restore");

            Directory.CreateDirectory(_sourceDir);
            Directory.CreateDirectory(_destDir);
            Directory.CreateDirectory(_restoreDir);

            _configStore = new ConfigStore(Path.Combine(_tempRoot, "appdata"));
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempRoot))
                    Directory.Delete(_tempRoot, true);
            }
            catch { }
        }

        [Fact]
        public async Task FullThenIncrementalBackup_ThenRestore_RestoresAllCorrectly()
        {
            // Prepare initial source files
            var f1 = Path.Combine(_sourceDir, "doc1.txt");
            var f2 = Path.Combine(_sourceDir, "sub", "doc2.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(f2));

            File.WriteAllText(f1, "Version 1 of Doc 1");
            File.WriteAllText(f2, "Content of Doc 2");

            var job = new BackupJob
            {
                Name = "E2EJob",
                SourceDirectories = { _sourceDir },
                Destination = new DestinationConfig
                {
                    Type = DestinationType.LocalOrUnc,
                    LocalPath = _destDir
                },
                Compression = new CompressionOptions
                {
                    Enabled = true,
                    Level = CompressionLevel.Normal
                },
                Integrity = new IntegrityOptions
                {
                    EnableVss = false
                }
            };

            var engine = new BackupEngine(_configStore);

            // 1. Run FULL backup
            var fullResult = await engine.ExecuteBackupAsync(job, BackupType.Full);
            Assert.Equal(JobExecutionStatus.Success, fullResult.Status);
            Assert.Equal(2, fullResult.BackedUpFiles);

            // 2. Modify doc1, add doc3
            await Task.Delay(1100); // ensure timestamp advances
            File.WriteAllText(f1, "Version 2 of Doc 1 (Modified)");
            var f3 = Path.Combine(_sourceDir, "doc3.txt");
            File.WriteAllText(f3, "Brand new Doc 3");

            // 3. Run INCREMENTAL backup
            var incrResult = await engine.ExecuteBackupAsync(job, BackupType.Incremental);
            Assert.Equal(JobExecutionStatus.Success, incrResult.Status);
            Assert.Equal(2, incrResult.BackedUpFiles); // doc1 modified + doc3 added

            // 4. Restore to latest point in time
            var restoreEngine = new RestoreEngine(_configStore);
            var sets = await restoreEngine.ListAvailableBackupSetsAsync(job);
            Assert.Equal(2, sets.Count);

            var restoreResult = await restoreEngine.RestoreToPointInTimeAsync(
                job,
                sets[0].BackupSetId, // latest (incremental)
                new RestoreOptions { TargetDirectory = _restoreDir });

            Assert.True(restoreResult.Success);

            // Verify restored files
            var restoredF1 = Path.Combine(_restoreDir, "doc1.txt");
            var restoredF2 = Path.Combine(_restoreDir, "sub", "doc2.txt");
            var restoredF3 = Path.Combine(_restoreDir, "doc3.txt");

            Assert.True(File.Exists(restoredF1));
            Assert.True(File.Exists(restoredF2));
            Assert.True(File.Exists(restoredF3));

            Assert.Equal("Version 2 of Doc 1 (Modified)", File.ReadAllText(restoredF1));
            Assert.Equal("Content of Doc 2", File.ReadAllText(restoredF2));
            Assert.Equal("Brand new Doc 3", File.ReadAllText(restoredF3));
        }

        [Fact]
        public async Task UncompressedFolderBackup_ThenRestore_RestoresDirectoryStructureCorrectly()
        {
            var uncompressedSource = Path.Combine(_tempRoot, "uncomp_source");
            var uncompressedDest = Path.Combine(_tempRoot, "uncomp_dest");
            var uncompressedRestore = Path.Combine(_tempRoot, "uncomp_restore");
            Directory.CreateDirectory(uncompressedSource);
            Directory.CreateDirectory(uncompressedDest);
            Directory.CreateDirectory(uncompressedRestore);

            // Source files including nested subdirectories
            var fileA = Path.Combine(uncompressedSource, "fileA.txt");
            var fileB = Path.Combine(uncompressedSource, "folder1", "folder2", "fileB.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(fileB));

            File.WriteAllText(fileA, "Plain File A content");
            File.WriteAllText(fileB, "Nested File B content");

            var job = new BackupJob
            {
                Name = "DirectFolderJob",
                SourceDirectories = { uncompressedSource },
                Destination = new DestinationConfig
                {
                    Type = DestinationType.LocalOrUnc,
                    LocalPath = uncompressedDest
                },
                Compression = new CompressionOptions
                {
                    Enabled = false // KHONG nen, day ca thu muc sang dich
                },
                Integrity = new IntegrityOptions
                {
                    EnableVss = false,
                    CheckFreeDiskSpace = true,
                    VerifySha256Checksum = true,
                    NetworkRetryCount = 3
                }
            };

            var engine = new BackupEngine(_configStore);

            // 1. Run FULL uncompressed backup
            var fullResult = await engine.ExecuteBackupAsync(job, BackupType.Full);
            Assert.Equal(JobExecutionStatus.Success, fullResult.Status);
            Assert.Equal(2, fullResult.BackedUpFiles);

            // Check that files exist uncompressed in destination folder
            var jobFolder = Path.Combine(uncompressedDest, "DirectFolderJob");
            Assert.True(Directory.Exists(jobFolder));

            // 2. Modify fileA, add fileC
            await Task.Delay(1100);
            File.WriteAllText(fileA, "Plain File A modified content");
            var fileC = Path.Combine(uncompressedSource, "fileC.txt");
            File.WriteAllText(fileC, "File C content");

            // 3. Run INCREMENTAL uncompressed backup
            var incrResult = await engine.ExecuteBackupAsync(job, BackupType.Incremental);
            Assert.Equal(JobExecutionStatus.Success, incrResult.Status);
            Assert.Equal(2, incrResult.BackedUpFiles);

            // 4. Restore to latest backup set
            var restoreEngine = new RestoreEngine(_configStore);
            var sets = await restoreEngine.ListAvailableBackupSetsAsync(job);
            Assert.Equal(2, sets.Count);
            Assert.False(sets[0].IsCompressed);

            var restoreResult = await restoreEngine.RestoreToPointInTimeAsync(
                job,
                sets[0].BackupSetId,
                new RestoreOptions { TargetDirectory = uncompressedRestore });

            Assert.True(restoreResult.Success);

            // Verify restored files and directory structure
            var restoredA = Path.Combine(uncompressedRestore, "fileA.txt");
            var restoredB = Path.Combine(uncompressedRestore, "folder1", "folder2", "fileB.txt");
            var restoredC = Path.Combine(uncompressedRestore, "fileC.txt");

            Assert.True(File.Exists(restoredA));
            Assert.True(File.Exists(restoredB));
            Assert.True(File.Exists(restoredC));

            Assert.Equal("Plain File A modified content", File.ReadAllText(restoredA));
            Assert.Equal("Nested File B content", File.ReadAllText(restoredB));
            Assert.Equal("File C content", File.ReadAllText(restoredC));
        }
    }
}
