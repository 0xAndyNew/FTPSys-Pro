using System;

namespace FtpSysPro.Core.Logging
{
    public static class LogFilterHelper
    {
        /// <summary>
        /// Xác định xem một dòng có phải là tiêu đề của bản ghi nhật ký (entry header) hay dòng tiếp nối (stack trace).
        /// Hỗ trợ định dạng Serilog: '2026-11-11 15:53:32.982 +07:00 [INF] ...' hoặc '[Job ...]' hoặc '[INF] ...'
        /// </summary>
        public static bool IsLogEntryHeader(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return false;

            // Dòng tiếp nối (như Exception stack trace) thường bắt đầu bằng khoảng trắng hoặc tab
            if (char.IsWhiteSpace(line[0])) return false;

            // Định dạng Serilog mặc định: '2026-11-11 ...'
            if (line.Length >= 23 && char.IsDigit(line[0]) && line[4] == '-' && line[7] == '-')
                return true;

            // Thẻ bắt đầu bằng dấu ngoặc vuông: '[Job '...']', '[INF]', '[ERR]', ...
            if (line.StartsWith("["))
                return true;

            return false;
        }

        /// <summary>
        /// Kiểm tra dòng nhật ký có liên quan đến tác vụ (theo tên hoặc ID) hay không.
        /// </summary>
        public static bool LineMatchesJob(string line, string jobName, string jobId)
        {
            if (string.IsNullOrEmpty(jobName) && string.IsNullOrEmpty(jobId))
                return true;

            if (string.IsNullOrEmpty(line))
                return false;

            // 1. Tag chuẩn: [Job 'TênJob']
            if (!string.IsNullOrEmpty(jobName) && line.IndexOf("[Job '" + jobName + "']", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            // 2. Định dạng cũ: job 'TênJob' hoặc 'TênJob'
            if (!string.IsNullOrEmpty(jobName) && line.IndexOf("'" + jobName + "'", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            // 3. Từ khoá tên job đứng độc lập hoặc trong câu
            if (!string.IsNullOrEmpty(jobName) && line.IndexOf(jobName, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            // 4. Khớp theo JobId (GUID)
            if (!string.IsNullOrEmpty(jobId) && line.IndexOf(jobId, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return false;
        }

        /// <summary>
        /// Kiểm tra dòng nhật ký theo mức độ (0: Tất cả, 1: ERR, 2: WRN, 3: INF)
        /// </summary>
        public static bool LineMatchesLevel(string line, int levelIndex)
        {
            if (levelIndex <= 0) return true;
            if (string.IsNullOrEmpty(line)) return false;

            if (levelIndex == 1) // ERR
            {
                return line.Contains("[ERR]") || line.Contains("[FTL]") ||
                       line.IndexOf("Exception", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       line.Contains("Thất bại") || line.Contains("Failed");
            }
            if (levelIndex == 2) // WRN
            {
                return line.Contains("[WRN]") ||
                       line.IndexOf("Warning", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       line.Contains("Cảnh báo");
            }
            if (levelIndex == 3) // INF
            {
                return line.Contains("[INF]") ||
                       line.IndexOf("Information", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            return true;
        }
    }
}
