# HƯỚNG DẪN SỬ DỤNG PHẦN MỀM FTPSYS-PRO

Chào mừng bạn đến với tài liệu hướng dẫn sử dụng phần mềm sao lưu **FTPSys-Pro**. Tài liệu này cung cấp hướng dẫn chi tiết từng bước từ cài đặt, cấu hình các tác vụ sao lưu, thiết lập bảo vệ dữ liệu nâng cao đến quy trình khôi phục dữ liệu khi gặp sự cố.

---

## 📑 Mục lục
1. [Giới thiệu tổng quan](#1-giới-thiệu-tổng-quan)
2. [Cài đặt và Khởi động hệ thống](#2-cài-đặt-và-khởi-động-hệ-thống)
3. [Giao diện điều khiển chính](#3-giao-diện-điều-khiển-chính)
4. [Hướng dẫn tạo tác vụ sao lưu mới (Backup Job)](#4-hướng-dẫn-tạo-tác-vụ-sao-lưu-mới-backup-job)
   - [Tab 1: Thông tin chung (General)](#tab-1-thông-tin-chung-general)
   - [Tab 2: Nguồn dữ liệu (Source Folders)](#tab-2-nguồn-dữ-liệu-source-folders)
   - [Tab 3: Đích đến sao lưu (Destination)](#tab-3-đích-đến-sao-lưu-destination)
   - [Tab 4: Tuỳ chọn nén & Độ tin cậy (Options & Integrity)](#tab-4-tuỳ-chọn-nén--độ-tin-cậy-options--integrity)
   - [Tab 5: Lập lịch tự động (Schedule)](#tab-5-lập-lịch-tự-động-schedule)
   - [Tab 6: Chính sách lưu giữ bản sao lưu (Retention)](#tab-6-chính-sách-lưu-giữ-bản-sao-lưu-retention)
   - [Tab 7: Cấu hình thông báo Email (Notification)](#tab-7-cấu-hình-thông-báo-email-notification)
5. [Các tính năng bảo vệ độ tin cậy nâng cao](#5-các-tính-năng-bảo-vệ-độ-tin-cậy-nâng-cao)
   - [Volume Shadow Copy (VSS) cho file bị khóa](#volume-shadow-copy-vss-cho-file-bị-khóa)
   - [Kiểm tra dung lượng đĩa trước khi chạy (Pre-flight Disk Check)](#kiểm-tra-dung-lượng-đĩa-trước-khi-chạy-pre-flight-disk-check)
   - [Xác minh mã băm SHA-256 đầu cuối](#xác-minh-mã-băm-sha-256-đầu-cuối)
   - [Tự động thử lại khi rớt mạng (Exponential Backoff)](#tự-động-thử-lại-khi-rớt-mạng-exponential-backoff)
6. [Hướng dẫn Khôi phục dữ liệu (Restore Wizard)](#6-hướng-dẫn-khôi-phục-dữ-liệu-restore-wizard)
7. [Cấu hình hệ thống (Settings)](#7-cấu-hình-hệ-thống-settings)
8. [Quản trị Windows Service và Khay hệ thống](#8-quản-trị-windows-service-và-khay-hệ-thống)
9. [Xử lý sự cố thường gặp (Troubleshooting)](#9-xử-lý-sự-cố-thường-gặp-troubleshooting)

---

## 1. Giới thiệu tổng quan

**FTPSys-Pro** là giải pháp sao lưu độc lập, chạy trên 2 thành phần chính:
1. **Windows Service (`FtpSysProService`)**: Chạy ngầm với quyền `LocalSystem`, thực thi các lịch sao lưu tự động ngay cả khi máy chủ chưa đăng nhập người dùng.
2. **Tray Manager (`FtpSysPro.Manager.exe`)**: Ứng dụng giao diện đồ hoạ WinForms nằm trên khay hệ thống, dùng để cấu hình, kiểm tra tiến độ và thực hiện khôi phục.

Hai thành phần giao tiếp qua **Named Pipe IPC** bảo mật cao, đảm bảo tốc độ phản hồi tức thì và ngăn chặn truy cập trái phép.

---

## 2. Cài đặt và Khởi động hệ thống

### Cài đặt qua bộ cài đặt Setup (.exe)
1. Tải bộ cài đặt `FtpSysPro_Setup_v1.0.0.exe` từ trang [Releases của GitHub](https://github.com/0xAndyNew/FTPSys-Pro/releases).
2. Nhấp chuột phải vào file cài đặt và chọn **Run as administrator**.
3. Tiến hành các bước cài đặt thông thường. Trình cài đặt sẽ:
   - Cài đặt phần mềm vào thư mục `C:\Program Files\FtpSysPro\`.
   - Tạo thư mục dữ liệu bảo mật tại `C:\ProgramData\FtpSysPro\`.
   - Tự động đăng ký và khởi chạy dịch vụ Windows Service `FtpSysProService`.
   - Tạo tác vụ tự khởi động ứng dụng khay khi Windows đăng nhập.

### Khởi động ứng dụng quản lý
- Nhấp đúp vào biểu tượng **FTPSys-Pro** trên màn hình Desktop hoặc tìm trong menu Start.
- Khi ứng dụng mở ra, biểu tượng khiên bảo vệ sẽ xuất hiện tại **khay hệ thống (System Tray)** góc dưới bên phải màn hình.

---

## 3. Giao diện điều khiển chính

Màn hình chính của FTPSys-Pro được thiết kế theo cấu trúc chia đôi (Split View) hiện đại:

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│ [Thêm Job] [Sửa] [Nhân bản] [Xoá] | [Chạy Full] [Chạy Incr] [Dừng] | [Khôi phục] [Cài đặt]
├───────────────────────────────────────────────────────────────┬────────────────────────┤
│ Tên tác vụ  │ Bật │ Dung lượng cần backup │ Chế độ truyền   │ ... │ CHI TIẾT TÁC VỤ        │
├─────────────┼─────┼───────────────────────┼─────────────────┼─────┤ ────────────────────── │
│ Backup_DB   │  ✓  │ 12.4 GB (1,250 files) │ Đẩy cả thư mục  │ ... │ Tên: Backup_DB         │
│ Web_Upload  │  ✓  │ 450.2 MB (5,400 files)│ Nén ZIP (AES)   │ ... │ Dung lượng: 12.4 GB    │
│             │     │                       │                 │     │ Truyền: Đẩy cả thư mục │
│             │     │                       │                 │     │ Đích: \\backup\data    │
│             │     │                       │                 │     │ Nguồn: D:\Database     │
└─────────────┴─────┴───────────────────────┴─────────────────┴─────┴────────────────────────┘
│ Service FtpSysPro: Đang chạy                                       [Tiến độ: 100%]     │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

- **Thanh công cụ (Toolbar)**: Các nút thao tác nhanh: Thêm, Sửa, Nhân bản, Xoá, Chạy Full, Chạy Incremental, Dừng tác vụ đang chạy, Mở trình Khôi phục, Cài đặt hệ thống.
- **Bảng danh sách Job (Bên trái)**:
  - Cột `Dung lượng cần backup`: Hiển thị tổng dung lượng nguồn được tính toán ngay khi lưu cấu hình job.
  - Cột `Chế độ truyền`: Hiển thị rõ ràng chế độ đẩy thẳng thư mục hay nén ZIP AES-256.
  - Cột `Trạng thái`: Hiển thị trạng thái lần chạy gần nhất (Sẵn sàng, Đang chạy, Thành công, Cảnh báo, Thất bại).
- **Khung thông tin chi tiết (Bên phải - Info Card)**:
  - Khi nhấp chuột vào bất kỳ job nào, khung bên phải sẽ hiển thị toàn bộ chi tiết: tên, dung lượng cần backup cỡ chữ lớn, chế độ truyền, đích đến, danh sách thư mục nguồn và lịch chạy kế tiếp.
- **Thanh trạng thái (Status Bar)**: Báo hiệu kết nối với Windows Service (`Service FtpSysPro: Đang chạy`). Nếu service bị dừng, sẽ hiển thị nút bấm để khởi động lại ngay.

---

## 4. Hướng dẫn tạo tác vụ sao lưu mới (Backup Job)

Trên thanh công cụ, nhấn nút **Thêm (Add)** để mở hộp thoại cấu hình tác vụ.

### Tab 1: Thông tin chung (General)
- **Tên tác vụ (Job Name)**: Đặt tên gợi nhớ cho tác vụ (ví dụ: `Sao_Luu_SQL_Server`, `Backup_Ke_Toan`).
- **Mô tả (Description)**: Ghi chú mục đích của tác vụ.
- **Bật tác vụ này (Enabled)**: Tích chọn để tác vụ tự động thực thi theo lịch.

### Tab 2: Nguồn dữ liệu (Source Folders)
- Nhấn nút **Chọn thư mục... (Browse...)** để mở trình duyệt thư mục dạng Windows Explorer chọn các thư mục cần sao lưu. Có thể thêm nhiều thư mục nguồn khác nhau.
- **Mẫu loại trừ (Exclude Patterns)**: Nhập các mẫu file không cần sao lưu, phân cách bằng dấu phẩy (ví dụ: `*.tmp, ~$*, *.log, node_modules, bin, obj`).
- **Dòng tổng kết dung lượng thời gian thực**: Ngay bên dưới danh sách thư mục, hệ thống sẽ tự động quét và hiển thị ngay:  
  `Tổng nguồn: 15.2 GB (1,420 files)`  
  giúp bạn nắm bắt chính xác khối lượng dữ liệu ngay lập tức.

### Tab 3: Đích đến sao lưu (Destination)
FTPSys-Pro hỗ trợ 3 nhóm đích đến:

#### A. Thư mục cục bộ / Ổ đĩa / Mạng UNC:
- Chọn đường dẫn thư mục cục bộ (ví dụ: `E:\Backups`) hoặc ổ cứng di động cắm ngoài.
- Nếu là thư mục chia sẻ mạng UNC (ví dụ: `\\192.168.1.100\SharedBackups`), bạn có thể điền thông tin tài khoản đăng nhập (`Domain\Username` và Mật khẩu). Hệ thống sử dụng Windows API `WNetAddConnection2` để xác thực phiên làm việc an toàn.

#### B. Máy chủ FTP / FTPS:
- **Host**: Tên miền hoặc IP máy chủ FTP (ví dụ: `ftp.company.com`).
- **Port**: Cổng kết nối (mặc định: `21` cho FTP thường/Explicit TLS, `990` cho Implicit TLS).
- **User / Password**: Thông tin đăng nhập.
- **Thư mục từ xa (Remote path)**: Thư mục lưu trữ trên server (ví dụ: `/backups/sql/`).
- **Mã hoá (Encryption)**: Chọn `None` (FTP thường), `Explicit (FTPS TLS)` (Khuyến nghị) hoặc `Implicit`.
- **Passive Mode**: Bật tính năng Passive mode (khuyến nghị khi máy chủ chạy sau Firewall/NAT).

#### C. Máy chủ SFTP (SSH File Transfer):
- **Host / Port**: IP/domain và cổng SSH (mặc định: `22`).
- **User / Password**: Đăng nhập bằng mật khẩu SSH.
- **Private Key**: Tuỳ chọn chọn tệp Private Key (`.pem`, `.ppk`, `id_rsa`) và nhập passphrase nếu key được bảo vệ.
- **Thư mục từ xa (Remote path)**: Đường dẫn đích trên máy chủ Linux/Unix.

### Tab 4: Tuỳ chọn nén & Độ tin cậy (Options & Integrity)
- **Chế độ nén ZIP (Tùy chọn)**:
  - Nếu **KHÔNG tích chọn** (Mặc định): Hệ thống sẽ **đẩy thẳng toàn bộ cấu trúc cây thư mục** sang đích mà không nén. File giữ nguyên cấu trúc cha/con, tiện cho việc truy cập trực tiếp tại đích.
  - Nếu **TÍCH CHỌN**: Hệ thống sẽ đóng gói thành file `.zip`. Bạn có thể chọn mức độ nén (`Fastest`, `Normal`, `Maximum`) và tích chọn **Mã hoá mật khẩu AES-256** để bảo vệ dữ liệu tối mật.
- **Độ tin cậy & Đảm bảo an toàn dữ liệu**:
  - `Volume Shadow Copy (VSS)`: Tự động chụp snapshot sao lưu file đang bị khóa bởi SQL Server, Outlook, máy ảo Hyper-V...
  - `Kiểm tra dung lượng trống trước khi chạy`: Tự động kiểm tra ổ đĩa Temp và ổ đĩa đích, ngăn ngừa lỗi tràn đĩa giữa chừng.
  - `Xác minh mã băm SHA-256`: Kiểm tra tính toàn vẹn 100% bit-for-bit của từng file sau khi truyền.
  - `Số lần tự động thử lại`: Số lần retry khi gặp sự cố chập chờn đường truyền mạng (mặc định 3 lần).

### Tab 5: Lập lịch tự động (Schedule)
Có thể thêm nhiều lịch trình độc lập cho cùng 1 Job:
- **Kiểu sao lưu**: Chọn `Full` (Sao lưu toàn bộ) hoặc `Incremental` (Sao lưu thay đổi).
- **Chu kỳ chạy**:
  - *Một lần*: Vào ngày giờ cụ thể.
  - *Hằng ngày*: Vào giờ cố định (ví dụ: 23:00 mỗi tối).
  - *Hằng tuần*: Vào các ngày thứ Hai, thứ Tư, thứ Sáu lúc 18:00.
  - *Hằng tháng*: Vào ngày mùng 1 hàng tháng.
  - *Lặp lại*: Mỗi 30 phút một lần từ 08:00 đến 17:00.
- **Chạy bù nếu bị lỡ**: Nếu máy chủ tắt lúc tới lịch, khi máy bật lại hệ thống sẽ tự động chạy bù ngay.

### Tab 6: Chính sách lưu giữ bản sao lưu (Retention)
Để tránh đầy ổ đĩa tại nơi lưu trữ, hệ thống cung cấp các chính sách tự dọn dẹp:
- **Chỉ giữ lại N bộ Full gần nhất**: Hệ thống sẽ giữ đúng N bản Full mới nhất và toàn bộ các bản Incremental liên quan của chúng. Các bộ cũ hơn sẽ được tự động xóa an toàn.
- **Chỉ giữ trong vòng N ngày gần nhất**: Tự động dọn dẹp các bộ backup có tuổi thọ vượt quá số ngày quy định.
- **Giữ tất cả**: Không tự động xoá bản nào.

### Tab 7: Cấu hình thông báo Email (Notification)
- Tích chọn **Gửi thông báo email sau khi tác vụ hoàn thành**.
- **Điều kiện gửi**: `Luôn luôn gửi`, `Chỉ khi thất bại`, hoặc `Chỉ khi thành công`.
- **Email người nhận**: Nhập các địa chỉ email nhận báo cáo, cách nhau bởi dấu phẩy (ví dụ: `admin@company.com, it-support@company.com`).
- **Tiền tố tiêu đề**: Tiền tố hiển thị trong Subject email (mặc định: `[FtpSysPro]`).

---

## 5. Các tính năng bảo vệ độ tin cậy nâng cao

### Volume Shadow Copy (VSS) cho file bị khóa
- Khi các phần mềm như **Microsoft SQL Server**, **Hyper-V**, **VirtualBox**, hay **Microsoft Outlook** đang hoạt động, các file `.mdf`, `.ldf`, `.vhdx`, `.pst` bị hệ điều hành khóa độc quyền (Exclusive Lock).
- Nếu không có VSS, việc đọc các file này sẽ sinh lỗi `The process cannot access the file because it is being used by another process`.
- **FTPSys-Pro** tự động kích hoạt tiến trình tạo bản chụp VSS (`vssadmin create shadow`), ánh xạ liên kết tượng trưng (Symbolic Link) vào snapshot, sao lưu dữ liệu nguyên vẹn tại thời điểm snapshot rồi tự động giải phóng tài nguyên. Dịch vụ cơ sở dữ liệu của bạn hoạt động liên tục 100% không cần ngừng trệ!

### Kiểm tra dung lượng đĩa trước khi chạy (Pre-flight Disk Check)
- Trước khi thực hiện sao lưu, hệ thống tính toán chính xác tổng dung lượng các tệp cần truyền.
- Tiến hành kiểm tra dung lượng còn trống (`AvailableFreeSpace`) của:
  1. Ổ đĩa chứa thư mục tạm (Temp drive).
  2. Ổ đĩa đích (nếu lưu tại Local hoặc UNC Network Share).
- Nếu dung lượng trống không đủ, tác vụ sẽ dừng ngay từ đầu và ghi nhận cảnh báo rõ ràng, tránh tình trạng sao lưu dở dang dẫn đến tràn ổ cứng làm sập hệ điều hành.

### Xác minh mã băm SHA-256 đầu cuối
- Dữ liệu truyền qua mạng Internet hay mạng LAN đều có nguy cơ bị suy hao bit (bit rot / packet corruption).
- FTPSys-Pro tính toán mã băm mật mã học **SHA-256** của từng file nguồn.
- Sau khi file được tải lên đích, hệ thống mở stream từ xa để kiểm tra đối chiếu mã băm. Chỉ khi hai giá trị SHA-256 trùng khớp hoàn hảo, file mới được ghi nhận là thành công.

### Tự động thử lại khi rớt mạng (Exponential Backoff)
- Khi gặp lỗi mất kết nối mạng tạm thời (Socket Timeout, Network Drop, Reset Connection), hệ thống không dừng công việc ngay mà kích hoạt cơ chế thử lại lũy tiến:
  - Lần 1: Nghỉ 2 giây rồi thử lại.
  - Lần 2: Nghỉ 4 giây rồi thử lại.
  - Lần 3: Nghỉ 8 giây rồi thử lại.
- Giúp các tác vụ truyền file dung lượng lớn qua FTP/SFTP/UNC diễn ra ổn định qua đêm mà không bị gián đoạn do sự cố mạng thoáng qua.

---

## 6. Hướng dẫn Khôi phục dữ liệu (Restore Wizard)

Khi gặp sự cố mất dữ liệu, ransomware hoặc ổ cứng bị hỏng:
1. Trên giao diện chính, chọn tác vụ cần khôi phục rồi nhấn nút **Khôi phục (Restore)** trên thanh công cụ.
2. Cửa sổ **Restore Wizard** sẽ hiển thị:
   - Hệ thống tự động kết nối đến đích và hiển thị danh sách tất cả các điểm sao lưu (Restore Points) theo thứ tự thời gian từ mới nhất đến cũ nhất.
   - Nhận diện rõ bản sao lưu là `Full` hay `Incremental`, dạng nén hay không nén.
3. Chọn điểm thời gian bạn muốn khôi phục về.
4. Chọn **Thư mục đích để khôi phục**: Có thể khôi phục về thư mục gốc ban đầu hoặc một thư mục mới tùy chọn.
5. Tuỳ chọn: Ghi đè file nếu đã tồn tại hay giữ nguyên.
6. Nhấn nút **Bắt đầu khôi phục (Start Restore)**:
   - Hệ thống tự động truy vết chuỗi backup từ bản Full gốc đến các bản Incremental liên tiếp.
   - Tự động giải nén (nhập mật khẩu AES nếu có) hoặc tải các file uncompressed về, phục hồi cấu trúc thư mục nguyên vẹn 100% như tại thời điểm sao lưu.

---

## 7. Cấu hình hệ thống (Settings)

Nhấn nút **Cài đặt (Settings)** trên thanh công cụ để mở cấu hình chung:
- **Cấu hình SMTP gửi Mail**:
  - `SMTP Host`: Địa chỉ máy chủ (ví dụ: `smtp.gmail.com`, `smtp.office365.com`).
  - `Port`: `587` (STARTTLS) hoặc `465` (SSL).
  - `Tài khoản & Mật khẩu`: Thông tin xác thực (với Gmail, sử dụng Mật khẩu ứng dụng - App Password).
  - `Nút Gửi email thử`: Nhấn để kiểm tra ngay kết nối SMTP có hoạt động hay không.
- **Ngôn ngữ (Language)**: Chuyển đổi giữa **Tiếng Việt** và **English**.
- **Số tác vụ chạy đồng thời (Concurrent Jobs)**: Mặc định là 2 (tối đa 5), tránh làm nghẽn I/O ổ đĩa khi có nhiều lịch trùng nhau.
- **Thư mục tạm (Temp Directory)**: Tuỳ chọn đổi vị trí lưu file tạm sang ổ đĩa có dung lượng lớn.
- **Số ngày giữ file Log**: Tự động dọn dẹp các tệp log cũ sau N ngày.

---

## 8. Quản trị Windows Service và Khay hệ thống

### Quản trị qua ứng dụng khay
- Nhấp chuột phải vào biểu tượng **FTPSys-Pro** ở khay hệ thống:
  - **Mở giao diện FTPSys-Pro**: Mở lại cửa sổ quản lý chính.
  - **Chạy tác vụ...**: Chạy nhanh Full hoặc Incremental cho một job cụ thể mà không cần mở giao diện.
  - **Thoát ứng dụng quản lý**: Đóng ứng dụng khay (Lưu ý: Windows Service vẫn tiếp tục chạy ngầm đảm bảo đúng lịch sao lưu).

### Quản trị qua dòng lệnh (Command Line / PowerShell)
Mở Command Prompt hoặc PowerShell bằng quyền Administrator:

```powershell
# Xem trạng thái service
Get-Service FtpSysProService

# Khởi động service
Start-Service FtpSysProService

# Dừng service
Stop-Service FtpSysProService

# Chạy thử chế độ debug console
cd "C:\Program Files\FtpSysPro"
.\FtpSysPro.Service.exe --console
```

---

## 9. Xử lý sự cố thường gặp (Troubleshooting)

| Sự cố | Nguyên nhân khả dĩ | Hướng khắc phục |
| :--- | :--- | :--- |
| **Báo lỗi "Service Disconnected" trên giao diện** | Windows Service chưa khởi động hoặc bị chặn | Nhấn vào chữ *"Bật Service"* trên thanh trạng thái hoặc mở `services.msc` kiểm tra dịch vụ `FtpSysProService`. |
| **Không kết nối được thư mục chia sẻ mạng UNC** | Tài khoản máy tính không có quyền vào share | Điền đầy đủ `Domain\Username` và Mật khẩu mạng trong phần cấu hình đích UNC của Job. |
| **Lỗi kết nối FTPS "Certificate validation failed"** | Chứng chỉ SSL tự ký của máy chủ FTP không được tin cậy | FTPSys-Pro tích hợp cơ chế tự động chấp nhận chứng chỉ hợp lệ từ server cấu hình; kiểm tra lại cấu hình cổng (Explicit: 21, Implicit: 990). |
| **Không gửi được email qua Gmail** | Gmail chặn truy cập từ ứng dụng | Cần bật Xác thực 2 bước trên tài khoản Google và tạo **Mật khẩu ứng dụng (App Password)** 16 ký tự để điền vào phần mật khẩu SMTP. |
| **Lỗi không đủ dung lượng đĩa** | Ổ đĩa đích hoặc ổ đĩa tạm (C:) bị đầy | Dọn dẹp ổ đĩa hoặc vào Cài đặt đổi thư mục Temp sang ổ đĩa khác có dung lượng trống lớn hơn. |
