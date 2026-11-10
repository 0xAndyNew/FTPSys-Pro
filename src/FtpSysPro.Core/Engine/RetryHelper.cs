using System;
using System.Threading;
using System.Threading.Tasks;

namespace FtpSysPro.Core.Engine
{
    public static class RetryHelper
    {
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
                    if (attempts >= maxAttempts)
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
                    if (attempts >= maxAttempts)
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
