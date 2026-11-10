using System;
using System.Security.Cryptography;
using System.Text;

namespace FtpSysPro.Core.Config
{
    public static class SecretProtector
    {
        // Entropy specific to FtpSysPro
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("FtpSysPro-Secret-Entropy-2026-v1");

        public static string Protect(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;

            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] cipherBytes = ProtectedData.Protect(
                    plainBytes,
                    Entropy,
                    DataProtectionScope.LocalMachine);
                return Convert.ToBase64String(cipherBytes);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to protect secret data: " + ex.Message, ex);
            }
        }

        public static string Unprotect(string cipherBase64)
        {
            if (string.IsNullOrEmpty(cipherBase64))
                return string.Empty;

            try
            {
                byte[] cipherBytes = Convert.FromBase64String(cipherBase64);
                byte[] plainBytes = ProtectedData.Unprotect(
                    cipherBytes,
                    Entropy,
                    DataProtectionScope.LocalMachine);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (Exception)
            {
                // Return empty if unprotect fails (e.g. copied to another machine)
                return string.Empty;
            }
        }
    }
}
