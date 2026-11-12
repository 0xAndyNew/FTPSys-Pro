using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace FtpSysPro.Core.Engine
{
    public static class ChecksumVerifier
    {
        public static string ComputeSha256(string filePath, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("File not found for checksum", filePath);

            using (var sha = SHA256.Create())
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                byte[] hash = sha.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        public static async Task<string> ComputeSha256Async(Stream stream, CancellationToken cancellationToken = default)
        {
            using (var sha = SHA256.Create())
            {
                byte[] buffer = new byte[81920];
                int bytesRead;
                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    sha.TransformBlock(buffer, 0, bytesRead, null, 0);
                }
                sha.TransformFinalBlock(new byte[0], 0, 0);
                return BitConverter.ToString(sha.Hash).Replace("-", "").ToLowerInvariant();
            }
        }

        public static bool VerifyChecksum(string expectedHash, string actualHash)
        {
            if (string.IsNullOrEmpty(expectedHash) || string.IsNullOrEmpty(actualHash))
                return false;

            return string.Equals(expectedHash.Trim(), actualHash.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
