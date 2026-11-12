using System;
using System.IO;

namespace FtpSysPro.Core.Engine
{
    public class DiskSpaceCheckResult
    {
        public bool IsSufficient { get; set; } = true;
        public long RequiredBytes { get; set; }
        public long AvailableBytes { get; set; }
        public string DriveName { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public static class DiskSpaceChecker
    {
        /// <summary>
        /// Checks if the drive hosting the specified directory has enough free space for the backup files.
        /// Compares available disk space directly against required bytes (1:1).
        /// </summary>
        public static DiskSpaceCheckResult CheckFreeSpace(string directoryPath, long requiredBytes, double safetyMarginRatio = 0.0)
        {
            var result = new DiskSpaceCheckResult();

            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                result.IsSufficient = true;
                return result;
            }

            try
            {
                // Run in a background task with 3-second timeout to prevent hangs on slow/network drives
                var task = System.Threading.Tasks.Task.Run(() => InternalCheckFreeSpace(directoryPath, requiredBytes, safetyMarginRatio));
                if (task.Wait(3000))
                {
                    return task.Result;
                }
                else
                {
                    result.IsSufficient = true;
                    result.ErrorMessage = "Kiểm tra dung lượng ổ đĩa vượt quá thời gian chờ (3s). Bỏ qua và tiếp tục.";
                    return result;
                }
            }
            catch (Exception ex)
            {
                result.IsSufficient = true;
                result.ErrorMessage = "Không thể kiểm tra dung lượng ổ đĩa: " + ex.Message;
                return result;
            }
        }

        private static DiskSpaceCheckResult InternalCheckFreeSpace(string directoryPath, long requiredBytes, double safetyMarginRatio)
        {
            var result = new DiskSpaceCheckResult();

            try
            {
                // Ensure directory or parent exists to find root
                var fullPath = Path.GetFullPath(directoryPath);
                var root = Path.GetPathRoot(fullPath);
                if (string.IsNullOrEmpty(root))
                {
                    result.IsSufficient = true;
                    return result;
                }

                // If path is a UNC network path, DriveInfo may not work; skip gracefully
                if (root.StartsWith(@"\\"))
                {
                    result.IsSufficient = true;
                    return result;
                }

                var drive = new DriveInfo(root);
                if (!drive.IsReady)
                {
                    result.IsSufficient = false;
                    result.DriveName = root;
                    result.ErrorMessage = $"Drive '{root}' is not ready.";
                    return result;
                }

                long totalRequired = safetyMarginRatio > 0
                    ? (long)(requiredBytes * (1.0 + safetyMarginRatio))
                    : requiredBytes;

                result.RequiredBytes = totalRequired;
                result.AvailableBytes = drive.AvailableFreeSpace;
                result.DriveName = drive.Name;

                if (drive.AvailableFreeSpace < totalRequired)
                {
                    result.IsSufficient = false;
                    result.ErrorMessage = $"Ổ đĩa '{drive.Name}' không đủ dung lượng trống. " +
                        $"Cần tối thiểu {FormatBytes(totalRequired)}, " +
                        $"hiện chỉ còn {FormatBytes(drive.AvailableFreeSpace)}.";
                }
            }
            catch (Exception ex)
            {
                // If disk space cannot be queried, log and allow backup to proceed
                result.IsSufficient = true;
                result.ErrorMessage = "Không thể kiểm tra dung lượng ổ đĩa: " + ex.Message;
            }

            return result;
        }

        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int i = 0;
            double dbl = bytes;
            while (dbl >= 1024 && i < suffixes.Length - 1)
            {
                dbl /= 1024;
                i++;
            }
            return $"{dbl:0.##} {suffixes[i]}";
        }
    }
}
