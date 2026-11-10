using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FtpSysPro.Core.Compression;
using FtpSysPro.Core.Engine;
using Xunit;

namespace FtpSysPro.Core.Tests
{
    public class CompressionTests : IDisposable
    {
        private readonly string _tempDir;

        public CompressionTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "FtpSysPro_Test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
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
        public void ZipArchive_CreatesAndExtracts_Plain()
        {
            var srcDir = Path.Combine(_tempDir, "src");
            var outDir = Path.Combine(_tempDir, "out");
            Directory.CreateDirectory(srcDir);

            var file1 = Path.Combine(srcDir, "hello.txt");
            File.WriteAllText(file1, "Hello World from FtpSysPro!");

            var zipPath = Path.Combine(_tempDir, "test.zip");

            var files = new List<ScannedFile>
            {
                new ScannedFile
                {
                    FullPath = file1,
                    RelativePath = "hello.txt",
                    SizeBytes = new FileInfo(file1).Length,
                    LastWriteTimeUtc = DateTime.UtcNow
                }
            };

            ZipArchiveHelper.CreateZipArchive(zipPath, files, 5);
            Assert.True(File.Exists(zipPath));

            ZipArchiveHelper.ExtractZipArchive(zipPath, outDir);
            var extracted = Path.Combine(outDir, "hello.txt");
            Assert.True(File.Exists(extracted));
            Assert.Equal("Hello World from FtpSysPro!", File.ReadAllText(extracted));
        }

        [Fact]
        public void ZipArchive_EncryptsWithPassword_AndExtracts()
        {
            var srcDir = Path.Combine(_tempDir, "src_enc");
            var outDir = Path.Combine(_tempDir, "out_enc");
            Directory.CreateDirectory(srcDir);

            var file1 = Path.Combine(srcDir, "secret.txt");
            File.WriteAllText(file1, "TopSecretBackupContent123");

            var zipPath = Path.Combine(_tempDir, "encrypted.zip");
            var password = "StrongPassword@2026";

            var files = new List<ScannedFile>
            {
                new ScannedFile
                {
                    FullPath = file1,
                    RelativePath = "secret.txt",
                    SizeBytes = new FileInfo(file1).Length,
                    LastWriteTimeUtc = DateTime.UtcNow
                }
            };

            ZipArchiveHelper.CreateZipArchive(zipPath, files, 5, password);
            Assert.True(File.Exists(zipPath));

            ZipArchiveHelper.ExtractZipArchive(zipPath, outDir, password);
            var extracted = Path.Combine(outDir, "secret.txt");
            Assert.True(File.Exists(extracted));
            Assert.Equal("TopSecretBackupContent123", File.ReadAllText(extracted));
        }
    }
}
