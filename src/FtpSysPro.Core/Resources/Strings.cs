using System.Globalization;
using System.Threading;

namespace FtpSysPro.Core.Resources
{
    public static class Strings
    {
        public static string AppName => IsVi ? "FtpSysPro - Sao lưu Dữ liệu" : "FtpSysPro - Backup Solution";

        // Navigation / Tabs
        public static string TabGeneral => IsVi ? "Chung" : "General";
        public static string TabSources => IsVi ? "Nguồn dữ liệu" : "Source Folders";
        public static string TabDestination => IsVi ? "Đích đến" : "Destination";
        public static string TabOptions => IsVi ? "Tuỳ chọn sao lưu" : "Backup Options";
        public static string TabSchedule => IsVi ? "Lập lịch" : "Schedule";
        public static string TabRetention => IsVi ? "Giữ bản sao lưu" : "Retention";
        public static string TabNotification => IsVi ? "Thông báo Email" : "Email Notification";

        // Common actions
        public static string Add => IsVi ? "Thêm" : "Add";
        public static string Edit => IsVi ? "Sửa" : "Edit";
        public static string Delete => IsVi ? "Xoá" : "Delete";
        public static string Clone => IsVi ? "Nhân bản" : "Clone";
        public static string RunFull => IsVi ? "Chạy Full" : "Run Full";
        public static string RunIncr => IsVi ? "Chạy Incremental" : "Run Incremental";
        public static string Stop => IsVi ? "Dừng" : "Stop";
        public static string Restore => IsVi ? "Khôi phục" : "Restore";
        public static string Settings => IsVi ? "Cài đặt" : "Settings";
        public static string History => IsVi ? "Lịch sử" : "History";
        public static string Refresh => IsVi ? "Làm mới" : "Refresh";
        public static string Save => IsVi ? "Lưu" : "Save";
        public static string Cancel => IsVi ? "Huỷ" : "Cancel";
        public static string Close => IsVi ? "Đóng" : "Close";
        public static string Browse => IsVi ? "Chọn..." : "Browse...";
        public static string TestConnection => IsVi ? "Kiểm tra kết nối" : "Test Connection";
        public static string SendTestEmail => IsVi ? "Gửi email thử" : "Send Test Email";

        // Job Grid columns
        public static string ColJobName => IsVi ? "Tên tác vụ" : "Job Name";
        public static string ColEnabled => IsVi ? "Bật" : "Enabled";
        public static string ColSourceSize => IsVi ? "Dung lượng cần backup" : "Source Size";
        public static string ColTransferMode => IsVi ? "Chế độ truyền" : "Transfer Mode";
        public static string ColDestination => IsVi ? "Đích" : "Destination";
        public static string ColLastRun => IsVi ? "Lần chạy cuối" : "Last Run";
        public static string ColStatus => IsVi ? "Trạng thái" : "Status";
        public static string ColNextRun => IsVi ? "Lần chạy kế tiếp" : "Next Run";
        public static string ColSize => IsVi ? "Dung lượng backup" : "Backup Size";

        // Statuses
        public static string StatusIdle => IsVi ? "Sẵn sàng" : "Idle";
        public static string StatusRunning => IsVi ? "Đang chạy" : "Running";
        public static string StatusSuccess => IsVi ? "Thành công" : "Success";
        public static string StatusWarning => IsVi ? "Có cảnh báo" : "Warning";
        public static string StatusFailed => IsVi ? "Thất bại" : "Failed";
        public static string StatusCancelled => IsVi ? "Đã huỷ" : "Cancelled";

        // Destination types
        public static string DestLocal => IsVi ? "Thư mục cục bộ / Ổ đĩa / Mạng UNC" : "Local / Drive / Network Share (UNC)";
        public static string DestFtp => IsVi ? "Máy chủ FTP / FTPS" : "FTP / FTPS Server";
        public static string DestSftp => IsVi ? "Máy chủ SFTP (SSH)" : "SFTP (SSH) Server";

        // Dialog prompts
        public static string ConfirmDeleteJob => IsVi
            ? "Bạn có chắc muốn xoá tác vụ sao lưu này không?"
            : "Are you sure you want to delete this backup job?";
        public static string ConfirmStopJob => IsVi
            ? "Bạn có chắc muốn dừng tác vụ đang chạy không?"
            : "Are you sure you want to stop the running job?";
        public static string TestConnectionSuccess => IsVi
            ? "Kết nối thành công!"
            : "Connection successful!";
        public static string TestConnectionFailed => IsVi
            ? "Kết nối thất bại: "
            : "Connection failed: ";

        // Tray
        public static string TrayOpen => IsVi ? "Mở giao diện FtpSysPro" : "Open FtpSysPro";
        public static string TrayExit => IsVi ? "Thoát ứng dụng quản lý" : "Exit Manager";
        public static string TrayRunJob => IsVi ? "Chạy tác vụ" : "Run Job";
        public static string TrayBalloonSuccess => IsVi ? "Sao lưu thành công" : "Backup Succeeded";
        public static string TrayBalloonFailed => IsVi ? "Sao lưu thất bại" : "Backup Failed";

        // Service
        public static string ServiceStatusConnected => IsVi ? "Service FtpSysPro: Đang chạy" : "FtpSysPro Service: Running";
        public static string ServiceStatusDisconnected => IsVi ? "Service FtpSysPro: Không kết nối được" : "FtpSysPro Service: Disconnected";
        public static string ServiceStartPrompt => IsVi ? "Bật Service" : "Start Service";

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
