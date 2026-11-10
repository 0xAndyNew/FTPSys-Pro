using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FtpSysPro.Core.Engine;
using Xunit;

namespace FtpSysPro.Core.Tests
{
    public class IntegrityTests : IDisposable
    {
        private readonly string _tempDir;

        public IntegrityTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "FtpSysPro_IntegrityTest_" + Guid.NewGuid().ToString("N"));
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
        public async Task ChecksumVerifier_ComputesCorrectSha256_ForFileAndStream()
        {
            var testFile = Path.Combine(_tempDir, "sample.dat");
            var content = "The quick brown fox jumps over the lazy dog";
            File.WriteAllText(testFile, content, new UTF8Encoding(false));

            // Known SHA-256 for "The quick brown fox jumps over the lazy dog" (UTF-8 without BOM)
            // d7a8fbb307d7809469ca9abcb0082e4f8d5651e46d3cdb762d02d0bf37c9e592
            const string expectedSha256 = "d7a8fbb307d7809469ca9abcb0082e4f8d5651e46d3cdb762d02d0bf37c9e592";

            var fileHash = ChecksumVerifier.ComputeSha256(testFile);
            Assert.Equal(expectedSha256, fileHash);

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(content)))
            {
                var streamHash = await ChecksumVerifier.ComputeSha256Async(stream);
                Assert.Equal(expectedSha256, streamHash);
            }

            Assert.True(ChecksumVerifier.VerifyChecksum(expectedSha256, fileHash));
            Assert.True(ChecksumVerifier.VerifyChecksum(expectedSha256.ToUpperInvariant(), fileHash));
            Assert.False(ChecksumVerifier.VerifyChecksum(expectedSha256, "0000000000000000000000000000000000000000000000000000000000000000"));
        }

        [Fact]
        public void DiskSpaceChecker_EvaluatesDiskSpaceAccurately()
        {
            // Checking against current temp folder with small requirement
            var result = DiskSpaceChecker.CheckFreeSpace(_tempDir, 1024); // 1 KB
            Assert.True(result.IsSufficient);
            Assert.True(result.AvailableBytes > 0);

            // Checking with astronomical requirement (1,000,000 TB)
            long hugeRequirement = 1000000L * 1024L * 1024L * 1024L * 1024L;
            var hugeResult = DiskSpaceChecker.CheckFreeSpace(_tempDir, hugeRequirement);
            Assert.False(hugeResult.IsSufficient);
            Assert.False(string.IsNullOrEmpty(hugeResult.ErrorMessage));
            Assert.Equal(hugeRequirement, hugeResult.RequiredBytes);
            Assert.True(hugeResult.RequiredBytes > hugeResult.AvailableBytes);
        }

        [Fact]
        public async Task RetryHelper_SucceedsAfterTransientFailures()
        {
            int callCount = 0;
            int retryCallbackCount = 0;

            await RetryHelper.ExecuteWithRetryAsync(
                async () =>
                {
                    await Task.Yield();
                    callCount++;
                    if (callCount < 3)
                    {
                        throw new IOException("Simulated transient network drop attempt " + callCount);
                    }
                },
                maxAttempts: 4,
                initialDelayMs: 50,
                onRetry: (attempt, ex) =>
                {
                    retryCallbackCount++;
                });

            Assert.Equal(3, callCount);
            Assert.Equal(2, retryCallbackCount);
        }

        [Fact]
        public async Task RetryHelper_ThrowsWhenMaxAttemptsExceeded()
        {
            int callCount = 0;

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await RetryHelper.ExecuteWithRetryAsync(
                    async () =>
                    {
                        await Task.Yield();
                        callCount++;
                        throw new InvalidOperationException("Permanent failure");
                    },
                    maxAttempts: 3,
                    initialDelayMs: 20);
            });

            Assert.Equal(3, callCount);
            Assert.Equal("Permanent failure", ex.Message);
        }

        [Fact]
        public async Task RetryHelper_Generic_ReturnsValueOnSuccess()
        {
            int attempts = 0;
            var result = await RetryHelper.ExecuteWithRetryAsync(
                async () =>
                {
                    await Task.Yield();
                    attempts++;
                    if (attempts == 1)
                        throw new TimeoutException("Timeout");
                    return "SuccessOnAttempt" + attempts;
                },
                maxAttempts: 3,
                initialDelayMs: 20);

            Assert.Equal(2, attempts);
            Assert.Equal("SuccessOnAttempt2", result);
        }

        [Fact]
        public void VssSnapshotContext_SafelyHandlesUnmappedPathsAndDisposal()
        {
            using (var vss = new VssSnapshotContext(_tempDir))
            {
                var originalPath = Path.Combine(_tempDir, "non_shadow.txt");
                var mapped = vss.MapPathToShadow(originalPath);
                // When VSS is not actively mounted for this directory, it should safely return original path
                Assert.Equal(originalPath, mapped);
            }
        }
    }
}
