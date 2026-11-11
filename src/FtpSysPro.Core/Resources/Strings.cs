using System.Globalization;
using System.Threading;

namespace FtpSysPro.Core.Resources
{
    public static class Strings
    {
        public static string AppName => IsVi ? "FTPSys Pro - Sao lưu Dữ liệu Doanh nghiệp" : "FTPSys Pro - Enterprise Backup & Recovery";

        // Navigation / Tabs
        public static string TabGeneral => IsVi ? "1. Tổng quan" : "1. General";
        public static string TabSources => IsVi ? "2. Dữ liệu nguồn" : "2. Sources";
        public static string TabDestination => IsVi ? "3. Kho lưu trữ đích" : "3. Destination";
        public static string TabOptions => IsVi ? "4. Đóng gói & Nén" : "4. Options";
        public static string TabSchedule => IsVi ? "5. Lịch thực thi" : "5. Schedule";
        public static string TabRetention => IsVi ? "6. Chính sách lưu giữ" : "6. Retention";
        public static string TabNotification => IsVi ? "7. Cảnh báo Email" : "7. Alerts";

        // Common actions
        public static string Add => IsVi ? "Thêm Job" : "Add Job";
        public static string Edit => IsVi ? "Chỉnh sửa" : "Edit";
        public static string Delete => IsVi ? "Xóa" : "Delete";
        public static string Clone => IsVi ? "Nhân bản" : "Clone";
        public static string Run => IsVi ? "Thực thi Job" : "Run Job";
        public static string RunFull => IsVi ? "Chạy Full Backup" : "Run Full Backup";
        public static string RunIncr => IsVi ? "Chạy Incremental" : "Run Incremental";
        public static string Stop => IsVi ? "Dừng thực thi" : "Stop";
        public static string Restore => IsVi ? "Phục hồi dữ liệu" : "Restore Data";
        public static string Settings => IsVi ? "Cài đặt hệ thống" : "Settings";
        public static string Logs => IsVi ? "Nhật ký vận hành" : "Operation Logs";
        public static string History => IsVi ? "Lịch sử thực thi" : "History";
        public static string Refresh => IsVi ? "Làm mới" : "Refresh";
        public static string Save => IsVi ? "Lưu cấu hình" : "Save";
        public static string Cancel => IsVi ? "Hủy bỏ" : "Cancel";
        public static string Close => IsVi ? "Đóng" : "Close";
        public static string Next => IsVi ? "Tiếp tục >" : "Next >";
        public static string Back => IsVi ? "< Quay lại" : "< Back";
        public static string Finish => IsVi ? "Hoàn tất" : "Finish";
        public static string Browse => IsVi ? "Duyệt tìm..." : "Browse...";
        public static string TestConnection => IsVi ? "Kiểm tra kết nối" : "Test Connection";
        public static string SendTestEmail => IsVi ? "Gửi email kiểm tra" : "Send Test Email";

        // Job Grid columns
        public static string ColJobName => IsVi ? "Tên Job sao lưu" : "Job Name";
        public static string ColEnabled => IsVi ? "Kích hoạt" : "Enabled";
        public static string ColSource => IsVi ? "Dữ liệu nguồn (Source)" : "Source";
        public static string ColDestination => IsVi ? "Kho lưu trữ đích (Repository)" : "Destination";
        public static string ColBackupType => IsVi ? "Chế độ sao lưu" : "Mode";
        public static string ColSourceSize => IsVi ? "Dung lượng nguồn" : "Source Size";
        public static string ColTransferMode => IsVi ? "Định dạng đóng gói" : "Transfer Mode";
        public static string ColLastRun => IsVi ? "Lần thực thi cuối" : "Last Run";
        public static string ColStatus => IsVi ? "Trạng thái vận hành" : "Status";
        public static string ColNextRun => IsVi ? "Lịch chạy kế tiếp" : "Next Run";
        public static string ColSize => IsVi ? "Dung lượng bản sao" : "Backup Size";

        // Statuses
        public static string StatusIdle => IsVi ? "Sẵn sàng" : "Ready";
        public static string StatusRunning => IsVi ? "Đang thực thi" : "Running";
        public static string StatusSuccess => IsVi ? "Thành công" : "Success";
        public static string StatusWarning => IsVi ? "Có cảnh báo" : "Warning";
        public static string StatusFailed => IsVi ? "Lỗi thực thi" : "Failed";
        public static string StatusCancelled => IsVi ? "Đã hủy" : "Cancelled";

        // Destination types
        public static string DestLocal => IsVi ? "Thư mục cục bộ / Ổ lưu trữ / Chia sẻ mạng UNC" : "Local / Storage Drive / Network Share (UNC)";
        public static string DestFtp => IsVi ? "Máy chủ FTP / FTPS (File Transfer Protocol)" : "FTP / FTPS Server";
        public static string DestSftp => IsVi ? "Máy chủ SFTP (SSH File Transfer Protocol)" : "SFTP (SSH) Server";

        // Dialog prompts
        public static string ConfirmDeleteJob => IsVi
            ? "Bạn có chắc chắn muốn xóa Job sao lưu này khỏi hệ thống không?"
            : "Are you sure you want to delete this backup job?";
        public static string ConfirmStopJob => IsVi
            ? "Bạn có chắc chắn muốn dừng Job sao lưu đang thực thi không?"
            : "Are you sure you want to stop the running job?";
        public static string TestConnectionSuccess => IsVi
            ? "Kiểm tra kết nối thành công! Kho lưu trữ sẵn sàng."
            : "Connection successful! Backup repository is ready.";
        public static string TestConnectionFailed => IsVi
            ? "Kiểm tra kết nối thất bại: "
            : "Connection failed: ";

        // Tray
        public static string TrayOpen => IsVi ? "Mở bảng điều khiển FTPSys Pro" : "Open FTPSys Pro Dashboard";
        public static string TrayExit => IsVi ? "Thoát ứng dụng quản lý" : "Exit Manager";
        public static string TrayRunJob => IsVi ? "Thực thi Job sao lưu" : "Run Backup Job";
        public static string TrayBalloonSuccess => IsVi ? "Sao lưu hoàn tất thành công" : "Backup Succeeded";
        public static string TrayBalloonFailed => IsVi ? "Sao lưu phát sinh lỗi" : "Backup Failed";

        // Log Viewer
        public static string LogViewerTitle => IsVi ? "FTPSys Pro - Nhật ký vận hành hệ thống" : "FTPSys Pro - Operation Logs";
        public static string SelectLogFile => IsVi ? "Tệp nhật ký:" : "Log file:";
        public static string FilterJob => IsVi ? "Lọc theo Job:" : "Filter by job:";
        public static string AllJobs => IsVi ? "-- Tất cả Job sao lưu --" : "-- All Backup Jobs --";
        public static string OpenLogFolder => IsVi ? "📁 Mở thư mục Log" : "📁 Open Log Folder";
        public static string OpenInNotepad => IsVi ? "📝 Mở bằng Notepad" : "📝 Open in Notepad";
        public static string AutoScroll => IsVi ? "Tự cuộn" : "Auto-scroll";
        public static string AutoRefresh => IsVi ? "Tự làm mới (3s)" : "Auto-refresh (3s)";
        public static string SearchKeywords => IsVi ? "Tìm từ khóa trong log..." : "Search keywords...";
        public static string ViewJobLog => IsVi ? "Xem nhật ký Job sao lưu..." : "View Job Log...";
        public static string NoLogsFound => IsVi ? "(Chưa có dữ liệu nhật ký phù hợp)" : "(No log entries found)";
        public static string ClearScreen => IsVi ? "Xóa màn hình" : "Clear Screen";
        public static string FilterLevel => IsVi ? "Mức độ log:" : "Level:";
        public static string AllLevels => IsVi ? "-- Tất cả mức độ --" : "-- All Levels --";

        // Service
        public static string ServiceStatusConnected => IsVi ? "Dịch vụ FtpSysProService: Đang hoạt động" : "FtpSysProService: Active (Running)";
        public static string ServiceStatusDisconnected => IsVi ? "Dịch vụ FtpSysProService: Mất kết nối / Đã dừng" : "FtpSysProService: Disconnected / Stopped";
        public static string ServiceStartPrompt => IsVi ? "Khởi động Dịch vụ" : "Start Service";

        private static bool IsVi
        {
            get
            {
                var culture = Thread.CurrentThread.CurrentUICulture;
                return culture.Name.StartsWith("vi", System.StringComparison.OrdinalIgnoreCase);
            }
        }

        public static void SetLanguage(string lang)
        {
            var cultureName = string.Equals(lang, "en", System.StringComparison.OrdinalIgnoreCase) ? "en-US" : "vi-VN";
            var culture = new CultureInfo(cultureName);
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
        }
    }
}
