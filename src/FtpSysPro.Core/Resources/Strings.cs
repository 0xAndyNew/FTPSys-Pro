using System.Globalization;
using System.Threading;

namespace FtpSysPro.Core.Resources
{
    public static class Strings
    {
        private static string _currentLanguage = "en";

        public static bool IsVi => string.Equals(_currentLanguage, "vi", System.StringComparison.OrdinalIgnoreCase);

        public static void SetLanguage(string lang)
        {
            _currentLanguage = string.Equals(lang, "vi", System.StringComparison.OrdinalIgnoreCase) ? "vi" : "en";
            var cultureName = IsVi ? "vi-VN" : "en-US";
            var culture = new CultureInfo(cultureName);
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
        }

        public static string AppName => IsVi ? "FTPSys Pro - Sao lưu Dữ liệu Doanh nghiệp" : "FTPSys Pro - Enterprise Backup & Recovery";

        // Navigation / Tabs
        public static string TabGeneral => IsVi ? "1. Tổng quan" : "1. General";
        public static string TabSources => IsVi ? "2. Dữ liệu nguồn" : "2. Sources";
        public static string TabDestination => IsVi ? "3. Kho lưu trữ đích" : "3. Destination";
        public static string TabOptions => IsVi ? "4. Đóng gói & Nén" : "4. Options";
        public static string TabSchedule => IsVi ? "5. Lịch thực thi" : "5. Schedule";
        public static string TabRetention => IsVi ? "6. Chính sách lưu giữ" : "6. Retention";
        public static string TabNotification => IsVi ? "7. Cảnh báo Email" : "7. Alerts";

        // Step titles & descriptions
        public static string StepTitle1 => IsVi ? "1. Thông tin chung & Chế độ" : "1. General & Backup Mode";
        public static string StepTitle2 => IsVi ? "2. Chọn thư mục dữ liệu nguồn" : "2. Select Backup Sources";
        public static string StepTitle3 => IsVi ? "3. Cấu hình kho lưu trữ đích" : "3. Destination Repository";
        public static string StepTitle4 => IsVi ? "4. Đóng gói & Nén tệp" : "4. Compression & Packaging";
        public static string StepTitle5 => IsVi ? "5. Thiết lập lịch thực thi" : "5. Schedule Configuration";
        public static string StepTitle6 => IsVi ? "6. Chính sách lưu giữ bản sao" : "6. Retention Policy";
        public static string StepTitle7 => IsVi ? "7. Cảnh báo Email & Xác nhận" : "7. Email Alerts & Summary";

        public static string StepIndicator(int current, int total) =>
            IsVi ? $"Bước {current + 1} / {total}" : $"Step {current + 1} of {total}";

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

        // Backup Modes
        public static string BackupMode => IsVi ? "Chế độ sao lưu" : "Backup Mode";
        public static string ModeIncremental => IsVi ? "Sao lưu gia tăng" : "Incremental Backup";
        public static string ModeFull => IsVi ? "Sao lưu toàn phần" : "Full Backup";

        // General Tab
        public static string JobName => IsVi ? "Tên Job sao lưu (*):" : "Backup Job Name (*):";
        public static string JobDescription => IsVi ? "Mô tả mục đích:" : "Description:";
        public static string JobEnabled => IsVi ? "Kích hoạt Job sao lưu này" : "Enable this backup job";

        // Sources Tab
        public static string AddFolder => IsVi ? "📁 Thêm thư mục" : "📁 Add Folder";
        public static string AddDrive => IsVi ? "💾 Thêm toàn bộ ổ đĩa" : "💾 Add Entire Drive";
        public static string RemoveSource => IsVi ? "🗑 Xóa khỏi danh sách" : "🗑 Remove";
        public static string ClearAllSources => IsVi ? "Xóa tất cả" : "Clear All";
        public static string ExcludePatterns => IsVi ? "Mẫu tệp / thư mục loại trừ (phân cách bằng dấu chấm phẩy ;):" : "Exclude Patterns (semicolon separated ;):";
        public static string TotalSourceSize => IsVi ? "Tổng dung lượng dữ liệu nguồn:" : "Total Source Data Size:";
        public static string ScanningSize => IsVi ? "Đang tính toán dung lượng nguồn..." : "Calculating source size...";

        // Destination Tab
        public static string DestType => IsVi ? "Loại kho lưu trữ đích:" : "Destination Repository Type:";
        public static string DestPath => IsVi ? "Đường dẫn kho lưu trữ đích (Thư mục cục bộ / Ổ đĩa / Mạng UNC):" : "Destination Repository Path (Local / External Drive / UNC Share):";
        public static string FtpHost => IsVi ? "Máy chủ FTP:" : "FTP Server Host:";
        public static string FtpPort => IsVi ? "Cổng:" : "Port:";
        public static string Username => IsVi ? "Tài khoản đăng nhập:" : "Username:";
        public static string Password => IsVi ? "Mật khẩu:" : "Password:";
        public static string RemotePath => IsVi ? "Thư mục lưu trữ từ xa:" : "Remote Path:";
        public static string SecurityProtocol => IsVi ? "Giao thức bảo mật:" : "Security Protocol:";
        public static string FtpPassiveMode => IsVi ? "Chế độ truyền thụ động (Passive Mode)" : "Passive Mode";
        public static string SftpHost => IsVi ? "Máy chủ SFTP:" : "SFTP Server Host:";
        public static string PrivateKey => IsVi ? "Tệp Private Key (tùy chọn):" : "Private Key File (optional):";
        public static string KeyPassphrase => IsVi ? "Mật khẩu giải mã khóa (nếu có):" : "Key Passphrase (if any):";

        // Options Tab
        public static string TransferDirect => IsVi ? "Sao chép trực tiếp cấu trúc thư mục" : "Direct Directory Synchronization";
        public static string TransferZip => IsVi ? "Đóng gói nén tệp .ZIP" : "Compress into .ZIP Archive";
        public static string CompressionLevel => IsVi ? "Mức độ nén:" : "Compression Level:";
        public static string EncryptZip => IsVi ? "Mã hóa tệp ZIP bằng mật khẩu (AES-256)" : "Encrypt ZIP Archive (AES-256)";
        public static string ZipPassword => IsVi ? "Mật khẩu nén:" : "Archive Password:";
        public static string VssSnapshot => IsVi ? "Volume Shadow Copy (VSS): Tự động chụp snapshot sao lưu tệp đang mở / đang khóa" : "Volume Shadow Copy (VSS): Snapshot open / locked files";
        public static string RetryAttempts => IsVi ? "Số lần tự động thử lại khi mất kết nối mạng:" : "Automatic retry attempts on network disconnection:";
        public static string RetryTimesSuffix => IsVi ? "lần" : "times";

        // Schedule Tab
        public static string ScheduleColType => IsVi ? "Kiểu lịch" : "Schedule Type";
        public static string ScheduleColTime => IsVi ? "Thời gian" : "Time";
        public static string ScheduleColDetails => IsVi ? "Chi tiết chu kỳ" : "Details";
        public static string AddScheduleBtn => IsVi ? "Thêm lịch..." : "Add Schedule...";
        public static string EditScheduleBtn => IsVi ? "Chỉnh sửa..." : "Edit...";
        public static string DeleteScheduleBtn => IsVi ? "Xóa lịch" : "Delete";

        // Retention Tab
        public static string RetentionPolicyTitle => IsVi ? "Chính sách lưu giữ điểm phục hồi" : "Restore Point Retention Policy";
        public static string RetentionKeepCount => IsVi ? "Duy trì số điểm phục hồi gần nhất:" : "Keep latest restore points:";
        public static string RetentionKeepDays => IsVi ? "Duy trì các điểm phục hồi trong số ngày gần nhất:" : "Keep restore points within the last:";
        public static string RetentionKeepAll => IsVi ? "Lưu giữ vĩnh viễn toàn bộ điểm phục hồi (không tự động xóa)" : "Keep all restore points permanently (no auto deletion)";
        public static string RetentionSetsUnit => IsVi ? "điểm" : "points";
        public static string RetentionDaysUnit => IsVi ? "ngày" : "days";

        // Alert Tab
        public static string AlertEmailSettings => IsVi ? "Cấu hình cảnh báo Email" : "Email Notification Settings";
        public static string AlertEnable => IsVi ? "Kích hoạt gửi báo cáo Email sau khi sao lưu" : "Enable email notification after backup execution";
        public static string AlertRecipients => IsVi ? "Địa chỉ Email nhận báo cáo (phân cách bằng dấu phẩy ,):" : "Recipient Email Addresses (comma separated ,):";
        public static string AlertCondition => IsVi ? "Điều kiện gửi Email:" : "Send Condition:";
        public static string ConditionAlways => IsVi ? "Luôn luôn gửi" : "Always";
        public static string ConditionFailureOnly => IsVi ? "Chỉ gửi khi có lỗi" : "On Failure Only";
        public static string ConditionSuccessOnly => IsVi ? "Chỉ gửi khi thành công" : "On Success Only";
        public static string AlertSubjectPrefix => IsVi ? "Tiền tố tiêu đề Email:" : "Email Subject Prefix:";

        // Detail Card in MainForm
        public static string HeaderSourceSize => IsVi ? "DUNG LƯỢNG NGUỒN:" : "SOURCE SIZE:";
        public static string HeaderTransfer => IsVi ? "HÌNH THỨC ĐÓNG GÓI:" : "PACKAGING MODE:";
        public static string HeaderDest => IsVi ? "KHO LƯU TRỮ ĐÍCH:" : "DESTINATION REPOSITORY:";
        public static string HeaderSources => IsVi ? "THƯ MỤC DỮ LIỆU NGUỒN:" : "SOURCE DIRECTORIES:";
        public static string HeaderLast => IsVi ? "LẦN THỰC THI GẦN NHẤT:" : "LAST RUN:";
        public static string HeaderNext => IsVi ? "LỊCH THỰC THI TIẾP THEO:" : "NEXT RUN:";
        public static string NoJobSelected => IsVi ? "(Chưa chọn Job)" : "(No Job Selected)";
        public static string NeverExecuted => IsVi ? "Chưa thực thi" : "Never executed";
        public static string NotScanned => IsVi ? "Chưa quét" : "Not scanned";
        public static string DirectCopy => IsVi ? "Sao chép trực tiếp" : "Direct Copy";
        public static string ZipCompression => IsVi ? "Nén ZIP" : "ZIP Compression";
        public static string FilesUnit => IsVi ? "tệp" : "files";

        // Job Grid columns
        public static string ColJobName => IsVi ? "Tên Job sao lưu" : "Job Name";
        public static string ColEnabled => IsVi ? "Kích hoạt" : "Enabled";
        public static string ColSource => IsVi ? "Dữ liệu nguồn" : "Source";
        public static string ColDestination => IsVi ? "Kho lưu trữ đích" : "Destination";
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
        public static string DestFtp => IsVi ? "Máy chủ FTP / FTPS" : "FTP / FTPS Server";
        public static string DestSftp => IsVi ? "Máy chủ SFTP" : "SFTP (SSH) Server";

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
    }
}
