using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FtpSysPro.Core.Compression;
using FtpSysPro.Core.Config;
using FtpSysPro.Core.Engine;
using FtpSysPro.Core.Models;
using Xunit;

namespace FtpSysPro.Core.Tests
{
    public class RestoreEngineTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly ConfigStore _configStore;

        public RestoreEngineTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "FtpSysPro_RestoreTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            _configStore = new ConfigStore(_tempDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDir))
                    Directory.Delete(_tempDir, true);
            }
            catch { }
        }

        [Fact]
        public async Task ListAvailableBackupSetsFromDestination_DiscoversSetsWithoutJob()
        {
            // Arrange: create backup folder with Full and Incremental directories
            var backupDir = Path.Combine(_tempDir, "BackupRepo");
            var fullDir = Path.Combine(backupDir, "MyData_20261113_080000_FULL");
            var incr1Dir = Path.Combine(backupDir, "MyData_20261113_090000_INCR");
            var incr2Dir = Path.Combine(backupDir, "MyData_20261113_100000_INCR");

            Directory.CreateDirectory(fullDir);
            Directory.CreateDirectory(incr1Dir);
            Directory.CreateDirectory(incr2Dir);

            File.WriteAllText(Path.Combine(fullDir, "doc.txt"), "Full content");
            File.WriteAllText(Path.Combine(incr1Dir, "doc2.txt"), "Incr 1 content");
            File.WriteAllText(Path.Combine(incr2Dir, "doc3.txt"), "Incr 2 content");

            var destConfig = new DestinationConfig
            {
                Type = DestinationType.LocalOrUnc,
                LocalPath = backupDir
            };

            var engine = new RestoreEngine(_configStore);

            // Act
            var sets = await engine.ListAvailableBackupSetsFromDestinationAsync(destConfig, "");

            // Assert
            Assert.Equal(3, sets.Count);
            // Ordered descending by CreatedUtc
            Assert.Equal("MyData_20261113_100000_INCR", sets[0].BackupSetId);
            Assert.Equal(BackupType.Incremental, sets[0].BackupType);
            Assert.Equal("MyData_20261113_090000_INCR", sets[1].BackupSetId);
            Assert.Equal(BackupType.Incremental, sets[1].BackupType);
            Assert.Equal("MyData_20261113_080000_FULL", sets[2].BackupSetId);
            Assert.Equal(BackupType.Full, sets[2].BackupType);
        }

        [Fact]
        public async Task RestoreFromDestination_ReconstructsIncrementalChainAndRestoresData()
        {
            // Arrange
            var backupDir = Path.Combine(_tempDir, "BackupRepoChain");
            var fullDir = Path.Combine(backupDir, "TestJob_20261113_080000_FULL");
            var incr1Dir = Path.Combine(backupDir, "TestJob_20261113_090000_INCR");
            var targetRestoreDir = Path.Combine(_tempDir, "RestoredOutput");

            Directory.CreateDirectory(fullDir);
            Directory.CreateDirectory(incr1Dir);

            // Base full has base.txt
            File.WriteAllText(Path.Combine(fullDir, "base.txt"), "Base Full Data");
            File.WriteAllText(Path.Combine(fullDir, "overwrite.txt"), "Original Version");

            // Incremental has updated overwrite.txt and new incr.txt
            File.WriteAllText(Path.Combine(incr1Dir, "overwrite.txt"), "Updated Incremental Version");
            File.WriteAllText(Path.Combine(incr1Dir, "incr.txt"), "Incr 1 Data");

            var destConfig = new DestinationConfig
            {
                Type = DestinationType.LocalOrUnc,
                LocalPath = backupDir
            };

            var engine = new RestoreEngine(_configStore);
            var options = new RestoreOptions
            {
                TargetDirectory = targetRestoreDir,
                OverwriteExisting = true
            };

            // Act: Restore to Incr 1 point
            var result = await engine.RestoreFromDestinationToPointInTimeAsync(
                destConfig,
                "",
                "TestJob_20261113_090000_INCR",
                options);

            // Assert
            Assert.True(result.Success, result.Message);
            Assert.True(File.Exists(Path.Combine(targetRestoreDir, "base.txt")));
            Assert.Equal("Base Full Data", File.ReadAllText(Path.Combine(targetRestoreDir, "base.txt")));
            Assert.True(File.Exists(Path.Combine(targetRestoreDir, "incr.txt")));
            Assert.Equal("Incr 1 Data", File.ReadAllText(Path.Combine(targetRestoreDir, "incr.txt")));
            // overwrite.txt should contain updated version from Incremental
            Assert.Equal("Updated Incremental Version", File.ReadAllText(Path.Combine(targetRestoreDir, "overwrite.txt")));
        }

        [Fact]
        public async Task RestoreFromDestination_SelectiveSubFolderRestore_OnlyRestoresTargetSubFolder()
        {
            // Arrange
            var backupDir = Path.Combine(_tempDir, "BackupRepoSelective");
            var fullDir = Path.Combine(backupDir, "FinanceJob_20261113_080000_FULL");
            var targetRestoreDir = Path.Combine(_tempDir, "RestoredSelectiveOutput");

            var accountingDir = Path.Combine(fullDir, "Accounting");
            var hrDir = Path.Combine(fullDir, "HumanResources");
            Directory.CreateDirectory(accountingDir);
            Directory.CreateDirectory(hrDir);

            File.WriteAllText(Path.Combine(accountingDir, "ledger.xlsx"), "Ledger Data");
            File.WriteAllText(Path.Combine(accountingDir, "taxes.pdf"), "Tax Report");
            File.WriteAllText(Path.Combine(hrDir, "employees.docx"), "Confidential HR");

            var destConfig = new DestinationConfig
            {
                Type = DestinationType.LocalOrUnc,
                LocalPath = backupDir
            };

            var engine = new RestoreEngine(_configStore);
            var options = new RestoreOptions
            {
                TargetDirectory = targetRestoreDir,
                OverwriteExisting = true,
                SelectedSubFolder = "Accounting" // Only restore Accounting!
            };

            // Act
            var result = await engine.RestoreFromDestinationToPointInTimeAsync(
                destConfig,
                "",
                "FinanceJob_20261113_080000_FULL",
                options);

            // Assert
            Assert.True(result.Success, result.Message);
            Assert.True(File.Exists(Path.Combine(targetRestoreDir, "Accounting", "ledger.xlsx")));
            Assert.True(File.Exists(Path.Combine(targetRestoreDir, "Accounting", "taxes.pdf")));
            // HumanResources should NOT have been restored!
            Assert.False(Directory.Exists(Path.Combine(targetRestoreDir, "HumanResources")));
            Assert.False(File.Exists(Path.Combine(targetRestoreDir, "HumanResources", "employees.docx")));
        }

        [Fact]
        public void ZipArchive_SelectiveSubFolderExtraction_FiltersEntriesCorrectly()
        {
            // Arrange: create zip with multiple folders
            var srcDir = Path.Combine(_tempDir, "ZipSrc");
            var outDir = Path.Combine(_tempDir, "ZipOut");
            var sub1 = Path.Combine(srcDir, "Docs");
            var sub2 = Path.Combine(srcDir, "Images");
            Directory.CreateDirectory(sub1);
            Directory.CreateDirectory(sub2);

            var docFile = Path.Combine(sub1, "readme.txt");
            var imgFile = Path.Combine(sub2, "photo.jpg");
            File.WriteAllText(docFile, "Documentation Text");
            File.WriteAllText(imgFile, "Fake Image Content");

            var zipPath = Path.Combine(_tempDir, "archive.zip");

            var files = new List<ScannedFile>
            {
                new ScannedFile { FullPath = docFile, RelativePath = @"Docs\readme.txt", SizeBytes = 18, LastWriteTimeUtc = DateTime.UtcNow },
                new ScannedFile { FullPath = imgFile, RelativePath = @"Images\photo.jpg", SizeBytes = 18, LastWriteTimeUtc = DateTime.UtcNow }
            };

            ZipArchiveHelper.CreateZipArchive(zipPath, files, 5);

            // Act: extract ONLY Docs
            ZipArchiveHelper.ExtractZipArchive(zipPath, outDir, null, true, null, default, "Docs");

            // Assert
            Assert.True(File.Exists(Path.Combine(outDir, "Docs", "readme.txt")));
            Assert.False(File.Exists(Path.Combine(outDir, "Images", "photo.jpg")));
        }
    }
}
