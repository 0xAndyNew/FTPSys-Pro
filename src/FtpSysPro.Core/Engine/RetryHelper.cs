using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace FtpSysPro.Core.Engine
{
    public static class RetryHelper
    {
        public static bool IsDiskFull(Exception ex)
        {
            if (ex == null) return false;
            if (ex is IOException ioEx)
            {
                int hr = Marshal.GetHRForException(ioEx) & 0xFFFF;
                if (hr == 112 || hr == 39) return true; // ERROR_DISK_FULL (112), ERROR_HANDLE_DISK_FULL (39)
            }
            var msg = ex.Message?.ToLowerInvariant() ?? "";
            return msg.Contains("not enough space") || msg.Contains("không đủ dung lượng") || msg.Contains("disk full");
        }

        public static async Task ExecuteWithRetryAsync(
            Func<Task> action,
            int maxAttempts = 3,
            int initialDelayMs = 2000,
            Action<int, Exception> onRetry = null,
            CancellationToken cancellationToken = default)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            int attempts = 0;
            int delay = initialDelayMs;

            while (true)
            {
                attempts++;
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await action();
                    return;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // If disk is full, retrying is futile and wastes time; abort immediately
                    if (IsDiskFull(ex) || attempts >= maxAttempts)
                    {
                        throw;
                    }

                    onRetry?.Invoke(attempts, ex);
                    await Task.Delay(delay, cancellationToken);
                    delay *= 2; // Exponential backoff: 2s -> 4s -> 8s
                }
            }
        }

        public static async Task<T> ExecuteWithRetryAsync<T>(
            Func<Task<T>> action,
            int maxAttempts = 3,
            int initialDelayMs = 2000,
            Action<int, Exception> onRetry = null,
            CancellationToken cancellationToken = default)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            int attempts = 0;
            int delay = initialDelayMs;

            while (true)
            {
                attempts++;
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return await action();
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // If disk is full, abort immediately
                    if (IsDiskFull(ex) || attempts >= maxAttempts)
                    {
                        throw;
                    }

                    onRetry?.Invoke(attempts, ex);
                    await Task.Delay(delay, cancellationToken);
                    delay *= 2;
                }
            }
        }
    }
}
