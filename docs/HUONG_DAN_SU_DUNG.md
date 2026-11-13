# HƯỚNG DẪN VẬN HÀNH HỆ THỐNG FTPSYS-PRO
## Enterprise Backup & Disaster Recovery Solution for Windows

Chào mừng bạn đến với tài liệu hướng dẫn vận hành hệ thống phần mềm sao lưu chuyên dụng **FTPSys-Pro**. Tài liệu này hướng dẫn chi tiết từng bước từ cài đặt, cấu hình Job sao lưu, thiết lập các cơ chế bảo vệ dữ liệu doanh nghiệp nâng cao cho đến quy trình phục hồi dữ liệu khi xảy ra sự cố.

---

## 📑 Mục lục
1. [Giới thiệu tổng quan kiến trúc](#1-giới-thiệu-tổng-quan-kiến-trúc)
2. [Cài đặt và Khởi động hệ thống](#2-cài-đặt-và-khởi-động-hệ-thống)
3. [Giao diện điều khiển & Quản trị trung tâm](#3-giao-diện-điều-khiển--quản-trị-trung-tâm)
4. [Quy trình cấu hình Job sao lưu mới (Backup Job Wizard)](#4-quy-trình-cấu-hình-job-sao-lưu-mới-backup-job-wizard)
   - [Bước 1: Cấu hình thông tin chung (General)](#bước-1-cấu-hình-thông-tin-chung-general)
   - [Bước 2: Nguồn dữ liệu & Hỗ trợ ổ đĩa gốc (Source Folders & Root Drives)](#bước-2-nguồn-dữ-liệu--hỗ-trợ-ổ-đĩa-gốc-source-folders--root-drives)
   - [Bước 3: Kho lưu trữ đích (Backup Repository)](#bước-3-kho-lưu-trữ-đích-backup-repository)
   - [Bước 4: Cơ chế đóng gói & Bảo vệ dữ liệu (Options & Integrity)](#bước-4-cơ-chế-đóng-gói--bảo-vệ-dữ-liệu-options--integrity)
   - [Bước 5: Lập lịch thực thi tự động (Schedule & 12h/24h Format)](#bước-5-lập-lịch-thực-thi-tự-động-schedule--12h24h-format)
   - [Bước 6: Chính sách lưu giữ điểm phục hồi (Retention Policy)](#bước-6-chính-sách-lưu-giữ-điểm-phục-hồi-retention-policy)
   - [Bước 7: Thông báo & Cảnh báo vận hành qua Email (Alerts & Reports)](#bước-7-thông-báo--cảnh-báo-vận-hành-qua-email-alerts--reports)
5. [Cơ chế bảo vệ dữ liệu vận hành chuẩn Enterprise](#5-cơ-chế-bảo-vệ-dữ-liệu-vận-hành-chuẩn-enterprise)
   - [Volume Shadow Copy Service (VSS) cho tệp đang mở](#volume-shadow-copy-service-vss-cho-tệp-đang-mở)
   - [Kiểm tra dung lượng khả dụng trước khi thực thi (Pre-flight Disk Check)](#kiểm-tra-dung-lượng-khả-dụng-trước-khi-thực-thi-pre-flight-disk-check)
   - [Xác minh tính toàn vẹn SHA-256 đầu cuối](#xác-minh-tính-toàn-vẹn-sha-256-đầu-cuối)
   - [Tự động thử lại luỹ tiến khi mất kết nối mạng](#tự-động-thử-lại-luỹ-tiến-khi-mất-kết-nối-mạng)
6. [Quy trình Phục hồi dữ liệu (Data Restoration Wizard)](#6-quy-trình-phục-hồi-dữ-liệu-data-restoration-wizard)
7. [Tra cứu & Phân tích Nhật ký vận hành (Operation Logs Viewer)](#7-tra-cứu--phân-tích-nhật-ký-vận-hành-operation-logs-viewer)
8. [Cài đặt hệ thống (System Settings)](#8-cài-đặt-hệ-thống-system-settings)
9. [Quản trị dịch vụ Windows Service và Khay hệ thống](#9-quản-trị-dịch-vụ-windows-service-và-khay-hệ-thống)
10. [Xử lý sự cố thường gặp (Troubleshooting)](#10-xử-lý-sự-cố-thường-gặp-troubleshooting)

---

## 1. Giới thiệu tổng quan kiến trúc

**FTPSys-Pro** được thiết kế theo kiến trúc chuẩn của các giải pháp Enterprise Backup hàng đầu, phân tách rõ ràng giữa tầng dịch vụ thực thi nền và tầng quản trị giao diện người dùng:

1. **Dịch vụ hệ thống Windows Service (`FtpSysProService`)**:
   - Chạy nền liên tục dưới tài khoản đặc quyền `LocalSystem`.
   - Chịu trách nhiệm thực thi các Job sao lưu độc lập theo lịch định kỳ, quản lý kết nối VSS, truyền dữ liệu qua mạng, tính toán SHA-256, áp dụng chính sách lưu giữ (Retention) và gửi email báo cáo mà không phụ thuộc vào việc người dùng có đăng nhập Windows hay không (Unattended Execution).
2. **Ứng dụng quản trị khay hệ thống (`FtpSysPro.Manager.exe`)**:
   - Giao diện đồ hoạ WinForms hiện đại chạy dưới khay hệ thống (System Tray).
   - Dùng để thiết lập cấu hình Job, giám sát tiến độ thực thi trực tiếp, tra cứu nhật ký và thực hiện quy trình phục hồi dữ liệu (Restore).

Hai thành phần giao tiếp an toàn qua giao thức **Named Pipe IPC (`\\.\pipe\FtpSysPro.Pipe`)** với danh sách kiểm soát truy cập (ACL) hạn chế nghiêm ngặt chỉ cấp phép cho nhóm `Administrators` và `SYSTEM`.

---

## 2. Cài đặt và Khởi động hệ thống

### Cài đặt qua bộ cài đặt Setup (.exe)
1. Tải bộ cài đặt mới nhất `FTPSys_Pro_Setup_v1.2.6.exe` từ thư mục `dist/` hoặc trang [Releases của GitHub](https://github.com/0xAndyNew/FTPSys-Pro/releases).
2. Nhấp chuột phải vào file cài đặt và chọn **Run as administrator**.
3. Thực hiện theo hướng dẫn trên màn hình. Trình cài đặt sẽ tự động:
   - Cài đặt các tệp thực thi vào thư mục hệ thống `C:\Program Files\FTPSys Pro\`.
   - Tạo thư mục dữ liệu bảo mật tại `C:\ProgramData\FtpSysPro\`.
   - Đăng ký và kích hoạt Windows Service `FtpSysProService` ở chế độ tự động bật cùng Windows (`Automatic Startup`).
   - Đăng ký Scheduled Task khởi động ứng dụng khay khi người dùng đăng nhập hệ thống (kèm tuỳ chọn chạy với quyền Quản trị viên).
   - Tạo biểu tượng lối tắt trên Desktop và Start Menu.

### Khởi động ứng dụng quản trị
- Nhấp đúp vào biểu tượng **FTPSys Pro** trên màn hình Desktop hoặc tìm trong menu Start.
- Biểu tượng ứng dụng sẽ xuất hiện tại **khay hệ thống (System Tray)** góc dưới bên phải màn hình.

---

## 3. Giao diện điều khiển & Quản trị trung tâm

Cửa sổ chính của FTPSys-Pro được thiết kế theo cấu trúc chia đôi (Split View) chuyên nghiệp lấy cảm hứng từ Cobian Backup 11 Gravity:

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│ [Thêm Job] [Sửa] [Nhân bản] [Xoá] | [Chạy Full] [Chạy Incr] [Dừng] | [Phục hồi] [Cài đặt]
├───────────────────────────────────────────────────────────────┬────────────────────────┤
│ Tên Job sao lưu │ Bật │ Dung lượng nguồn │ Chế độ sao lưu │ ... │ [Properties][History][Log]
├─────────────────┼─────┼──────────────────┼────────────────┼─────┤ ────────────────────── │
│ Backup_Database │  ✓  │ 12.4 GB (1,250)  │ Sao lưu toàn...│ ... │ [Task Properties]      │
│ Source_Code_App │  ✓  │ 450.2 MB (5,400) │ Sao lưu gia... │ ... │ Name: Backup_Database  │
│                 │     │                  │                │     │ Size: 12.4 GB (1,250)  │
│                 │     │                  │                │     │ Mode: Full Backup      │
│                 │     │                  │                │     │ Dest: \\nas\backup     │
│                 │     │                  │                │     │ VSS: Yes               │
└─────────────────┴─────┴──────────────────┴────────────────┴─────┴────────────────────────┘
│ Dịch vụ FtpSysProService: Đang hoạt động bình thường        [Đang sao lưu: 45%] [====] │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

- **Thanh công cụ (Toolbar)**: Cung cấp đầy đủ thao tác: *Thêm Job, Sửa Job, Nhân bản, Xoá, Thực thi Full Backup, Thực thi Incremental Backup, Dừng Job, Phục hồi dữ liệu, Xem Nhật ký vận hành, Cài đặt hệ thống*.
- **Danh sách Job sao lưu (Bên trái)**: Hiển thị đầy đủ thông tin: *Tên Job, Trạng thái kích hoạt, Dung lượng nguồn, Chế độ sao lưu, Kho lưu trữ đích, Thực thi gần nhất, Kết quả vận hành, Lịch chạy tiếp theo*.
- **Khu vực điều khiển đa thẻ bên phải (3 Thẻ chức năng chuyên sâu)**:
  1. **Task properties**: Bảng thuộc tính chi tiết 2 cột (`Property` | `Value`) liệt kê trọn vẹn mọi thông số của Job: Tên, ID, chế độ sao lưu, thư mục nguồn, dung lượng nguồn, kho lưu trữ đích, trạng thái VSS, nén ZIP, mật khẩu mã hoá AES-256, lịch chạy tự động, chính sách lưu giữ (Retention), thông báo Email, số lần thử lại kết nối, lần chạy cuối và lịch chạy kế tiếp.
  2. **History**: Bảng lịch sử các lần chạy sao lưu của Job được chọn: Thời gian bắt đầu, thời lượng, loại sao lưu (Full/Incremental), trạng thái (Thành công/Cảnh báo/Lỗi), dung lượng sao lưu, số tệp đã sao lưu, số cảnh báo/lỗi, thông điệp kết quả. Kèm các nút: *Làm mới, Xoá lịch sử, Xem chi tiết*.
     - *Tính năng tự động dọn dẹp sạch (v1.2.5)*: Khi xoá một Job, toàn bộ lịch sử và log của Job đó sẽ biến mất ngay lập tức; khi không có Job nào được chọn, bảng History sẽ hoàn toàn trống và các nút tác vụ được vô hiệu hoá.
  3. **Log**: Trình duyệt log thời gian thực chuẩn Notepad++ nhúng trực tiếp ngay tại thẻ thứ 3. Cho phép lọc theo Job, lọc theo mức độ (`INF`, `WRN`, `ERR`), tìm kiếm từ khóa, tự động làm mới mỗi 3s, tự cuộn, nút mở Notepad và mở thư mục chứa log.
- **Thanh trạng thái (Status Bar)**: Báo hiệu kết nối IPC với dịch vụ Windows Service `FtpSysProService`, đồng thời hiển thị **% tiến độ sao lưu trực tiếp** kèm thanh tiến độ mượt mà.

---

## 4. Quy trình cấu hình Job sao lưu mới (Backup Job Wizard)

Trên thanh công cụ, nhấn nút **Thêm (Add)** để mở Wizard cấu hình Job gồm 7 bước với các nút điều hướng **Quay lại (Back)**, **Tiếp tục (Next)** và **Hoàn tất (Finish)** ở bước cuối.

### Bước 1: Cấu hình thông tin chung (General)
- **Tên Job sao lưu (Backup Job Name)**: Đặt tên gợi nhớ cho Job (ví dụ: `Job_Backup_SQL_Database`, `Job_Backup_KeToan`).
- **Mô tả Job (Description)**: Ghi chú mục đích hoặc phân hệ dữ liệu.
- **Kích hoạt Job sao lưu này (Enabled)**: Tích chọn để Job sẵn sàng thực thi theo lịch tự động.
- **Chế độ sao lưu mặc định (Backup Mode)**:
  - `Sao lưu toàn phần (Full Backup)`: Mỗi lần thực thi tạo ra một điểm phục hồi hoàn chỉnh và độc lập tại kho lưu trữ đích.
  - `Sao lưu gia tăng (Incremental Backup)`: Gồm 1 điểm phục hồi Full cơ sở và chuỗi các điểm Incremental chỉ chứa dữ liệu mới hoặc sửa đổi.

### Bước 2: Nguồn dữ liệu & Hỗ trợ ổ đĩa gốc (Source Folders & Root Drives)
- **Hỗ trợ chọn toàn bộ phân vùng ổ đĩa (Root Drive)**:
  - Nhấn nút **Chọn thư mục...** để chọn thư mục con hoặc **toàn bộ ổ đĩa gốc** (như `C:\`, `D:\`, `E:\`).
  - Hệ thống tự động phân tích và loại trừ các thư mục nhạy cảm của hệ điều hành (`System Volume Information`, `$RECYCLE.BIN`, `pagefile.sys`, `hiberfil.sys`, `swapfile.sys`).
- **Bộ lọc loại trừ dữ liệu (Exclusion Filters)**: Nhập các mẫu tệp hoặc thư mục không cần sao lưu, phân cách bằng dấu phẩy (ví dụ: `*.tmp, ~$*, *.log, node_modules, bin, obj`).
- **Dung lượng nguồn trực quan (Live Source Size Calculation)**: Hệ thống tự động quét và hiển thị ngay tổng dung lượng và số lượng tệp nguồn giúp người quản trị nắm bắt tức thì.

### Bước 3: Kho lưu trữ đích (Backup Repository)
FTPSys-Pro hỗ trợ 3 nhóm kho lưu trữ đích chuẩn công nghiệp:

#### A. Thư mục cục bộ / Ổ đĩa ngoài / Thư mục mạng UNC:
- Lưu trữ trên ổ cứng nội bộ hoặc ổ cứng di động (ví dụ: `E:\BackupRepository`).
- Nếu sử dụng thư mục chia sẻ mạng UNC (ví dụ: `\\192.168.1.100\BackupShare`), nhập tài khoản `Domain\Username` và Mật khẩu. Dịch vụ Windows Service sử dụng Win32 API `WNetAddConnection2` để xác thực phiên kết nối bảo mật trước khi truyền dữ liệu.

#### B. Máy chủ FTP / FTPS:
- **Host**: Tên miền hoặc IP máy chủ FTP (ví dụ: `ftp.company.com`).
- **Port**: Cổng kết nối (`21` cho FTP thường/Explicit TLS, `990` cho Implicit TLS).
- **User / Password**: Thông tin đăng nhập.
- **Thư mục từ xa (Remote path)**: Đường dẫn thư mục lưu trữ trên server (ví dụ: `/backups/sql/`).
- **Mã hoá (Encryption)**: `Explicit (FTPS TLS)` (Khuyến nghị), `Implicit` hoặc `None`.
- **Passive Mode**: Chế độ truyền thụ động (khuyến nghị khi chạy qua tường lửa/NAT).

#### C. Máy chủ SFTP (SSH File Transfer):
- **Host / Port**: IP/domain và cổng SSH (mặc định: `22`).
- **User / Password**: Đăng nhập bằng tài khoản và mật khẩu SSH.
- **Private Key**: Tuỳ chọn xác thực bằng khóa bí mật OpenSSH/PuTTY (`.pem`, `.ppk`, `id_rsa`) kèm passphrase.
- **Thư mục từ xa (Remote path)**: Đường dẫn lưu trữ trên máy chủ Linux/Unix.

*Lưu ý: Luôn nhấn nút **Kiểm tra kết nối** để đảm bảo thông tin đăng nhập và đường truyền mạng hoàn toàn thông suốt trước khi tiếp tục.*

### Bước 4: Cơ chế đóng gói & Bảo vệ dữ liệu (Options & Integrity)
- **Đóng gói nén thành tệp lưu trữ .ZIP (Tùy chọn)**:
  - Nếu **KHÔNG chọn** (Mặc định): Hệ thống thực hiện đồng bộ trực tiếp cấu trúc thư mục sang đích (Uncompressed Folder Mirroring).
  - Nếu **CHỌN**: Đóng gói thành tệp `.zip` với các mức nén `Fastest`, `Normal`, `Maximum` và tích chọn **Mã hóa dữ liệu AES-256** bảo mật tối đa.
- **Cơ chế an toàn & Toàn vẹn dữ liệu vận hành (Enterprise Data Protection)**:
  - `Volume Shadow Copy Service (VSS)`: Tự động chụp snapshot sao lưu tệp đang mở / đang khóa.
  - `Kiểm tra dung lượng khả dụng trước khi thực thi`: Pre-flight disk space check chống tràn đĩa.
  - `Xác thực tính toàn vẹn SHA-256 sau khi truyền dữ liệu`: So sánh checksum bit-for-bit.
  - `Số lần tự động thử lại khi mất kết nối`: Khắc phục lỗi mạng chập chờn (mặc định 3 lần).

### Bước 5: Lập lịch thực thi tự động (Schedule & 12h/24h Format)
- Hỗ trợ thêm nhiều mốc lịch trình độc lập cho cùng một Job.
- **Chuyển đổi định dạng giờ linh hoạt**: Tùy chọn xem và nhập giờ theo chuẩn **24 giờ** hoặc **12 giờ (AM/PM)**.
- **Tần suất thực thi**:
  - *Một lần (Once)*: Thực thi tại một thời điểm xác định.
  - *Hằng ngày (Daily)*: Thực thi đều đặn vào giờ chỉ định (ví dụ: `23:00`).
  - *Hằng tuần (Weekly)*: Thực thi vào các thứ được chọn trong tuần (ví dụ: Thứ Hai, Thứ Tư, Thứ Sáu lúc `19:30`).
  - *Hằng tháng (Monthly)*: Thực thi vào ngày cố định trong tháng.
  - *Lặp lại theo chu kỳ*: Thực thi mỗi $N$ phút hoặc $N$ giờ trong khung giờ làm việc.
- **Chạy bù khi máy khởi động lại (Catch-up missed runs)**: Tự động kích hoạt chạy bù nếu máy chủ bị tắt vào thời điểm lịch đến hạn.

### Bước 6: Chính sách lưu giữ điểm phục hồi (Retention Policy)
Giao diện tự động tối ưu hóa hiển thị dựa trên chế độ Job đã chọn:
- **Khi Job ở chế độ Sao lưu toàn phần (Full Backup)**:
  - Duy trì $N$ điểm phục hồi Full gần nhất.
  - Hệ thống tự động dọn dẹp các điểm Full cũ hơn khi vượt quá hạn mức để bảo vệ dung lượng lưu trữ.
- **Khi Job ở chế độ Sao lưu gia tăng (Incremental Backup)**:
  - Duy trì $N$ chuỗi sao lưu (Full + Incremental) gần nhất.
  - **Quy tắc bất biến**: Hệ thống tuyệt đối bảo toàn điểm Full cơ sở khi các điểm Incremental liên quan vẫn còn hiệu lực lưu trữ.
- Tùy chọn duy trì điểm phục hồi theo thời gian ($N$ ngày) hoặc lưu giữ vĩnh viễn.

### Bước 7: Thông báo & Cảnh báo vận hành qua Email (Alerts & Reports)
- Tích chọn **Kích hoạt gửi báo cáo vận hành qua Email sau khi Job thực thi**.
- **Điều kiện gửi**: `Luôn luôn gửi báo cáo`, `Chỉ gửi khi có cảnh báo hoặc lỗi`, hoặc `Chỉ gửi khi thực thi thành công`.
- **Danh sách địa chỉ email nhận báo cáo**: Phân tách bằng dấu phẩy (ví dụ: `admin@company.com, it-ops@company.com`).
- **Tiền tố tiêu đề**: Định danh hệ thống trong Subject email (mặc định: `[FtpSysPro]`).

Nhấn nút **Hoàn tất (Finish)** để lưu Job và đồng bộ tức thì xuống Windows Service.

---

## 5. Cơ chế bảo vệ dữ liệu vận hành chuẩn Enterprise

### Volume Shadow Copy Service (VSS) & Sao lưu tệp đang mở / đang khóa
- Khi **Microsoft SQL Server**, **Hyper-V**, **VirtualBox**, hay **Microsoft Outlook** đang hoạt động, các tệp cơ sở dữ liệu (`.mdf`, `.ldf`, `.vhdx`, `.pst`) bị khóa độc quyền bởi hệ điều hành (Exclusive File Lock).
- **FTPSys-Pro** tự động tương tác với Windows VSS để tạo Volume Shadow Copy tại thời điểm thực thi, ánh xạ Symbolic Link để đọc dữ liệu nhất quán tại mốc snapshot mà không làm gián đoạn bất kỳ hoạt động nào của phần mềm máy chủ.
- Đồng thời, công cụ áp dụng cơ chế mở luồng tệp thông minh với `FileShare.ReadWrite | FileShare.Delete` cho phép sao lưu mượt mà các tệp đang được đọc/ghi trực tiếp mà không gây xung đột tiến trình.

### Kiểm tra dung lượng khả dụng & Dừng an toàn khi đầy đĩa (Disk Full Stop)
- Trước khi bắt đầu truyền dữ liệu, hệ thống tự động tính toán tổng dung lượng cần truyền và kiểm tra dung lượng còn trống (`AvailableFreeSpace`) của:
  1. Ổ đĩa chứa thư mục tạm (Temp Directory).
  2. Kho lưu trữ đích (Local Drive hoặc UNC Network Share).
- Nếu dung lượng trống không đảm bảo an toàn, Job sẽ cảnh báo ngay lập tức. Trong quá trình copy tệp, nếu ổ cứng đích bị đầy (Disk Full), hệ thống sẽ chủ động ngắt tiến trình sao lưu một cách an toàn và ghi log rõ ràng, ngăn ngừa hoàn toàn nguy cơ đóng băng hệ thống hoặc hỏng hóc phân vùng lưu trữ.

### Tối ưu tốc độ sao lưu & Giữ sạch kho lưu trữ đích (Zero Destination Pollution)
- Dữ liệu được sao lưu nguyên bản sang thư mục đích với bộ đệm luồng 1MB tối ưu hiệu năng I/O đĩa.
- Hệ thống tuyệt đối không sinh ra bất kỳ file `manifest.json` hay file kê khai lạ nào tại kho lưu trữ đích của người dùng, đảm bảo cây thư mục đích luôn sạch sẽ và nguyên bản 100%.

### Xác minh tính toàn vẹn SHA-256 đầu cuối
- Dữ liệu truyền qua mạng Internet hay mạng LAN đều có nguy cơ phát sinh lỗi suy hao bit (bit rot / corruption).
- FTPSys-Pro tự động tính toán mã băm mật mã học **SHA-256** của từng tệp nguồn.
- Sau khi tệp đã được đưa lên kho lưu trữ đích, hệ thống mở stream kiểm tra ngược đối soát checksum. Chỉ khi hai mã băm trùng khớp tuyệt đối, điểm phục hồi mới được ghi nhận là hợp lệ.

### Tự động thử lại luỹ tiến khi mất kết nối mạng
- Khi xảy ra sự cố gián đoạn đường truyền tạm thời (Socket Timeout, Network Drop), hệ thống tự động kích hoạt cơ chế thử lại lũy tiến (Exponential Backoff):
  - Lần 1: Nghỉ 2 giây rồi thử lại.
  - Lần 2: Nghỉ 4 giây rồi thử lại.
  - Lần 3: Nghỉ 8 giây rồi thử lại.
- Đảm bảo các Job sao lưu dung lượng lớn qua FTP/SFTP/UNC diễn ra ổn định xuyên suốt ban đêm mà không bị gián đoạn do sự cố mạng thoáng qua.

---

## 6. Quy trình Phục hồi dữ liệu Đa nguồn & Chọn lọc (Data Restoration Wizard - v1.2.6)

Khi xảy ra sự cố mất mát dữ liệu, lỗi phần cứng hoặc cần trích xuất tệp sao lưu:

1. Trên thanh công cụ chính, nhấn nút **Phục hồi dữ liệu (Restore)**.
2. Cửa sổ **Restore Wizard** hỗ trợ 3 chế độ nguồn phục hồi linh hoạt:
   - **Từ Job đã lưu (Configured Job)**: Chọn nhanh Job sao lưu có sẵn trong danh sách (tự động nhận diện cả Local, UNC, FTP, SFTP).
   - **Từ Thư mục sao lưu trực tiếp (Local / USB / Mạng UNC)**: Cho phép trỏ thẳng vào bất kỳ thư mục nào trên máy tính, ổ cứng ngoài hoặc chia sẻ mạng UNC mà **không cần phải có Job trong danh sách** (cực kỳ hữu ích khi cài mới phần mềm trên máy khác hoặc sau khi xoá Job).
   - **Từ Máy chủ FTP / SFTP từ xa (Remote Server)**: Nhập nhanh Host, Port, Username, Password và đường dẫn từ xa để kết nối và khôi phục trực tiếp từ server FTP/SFTP độc lập.
3. Nhấn **Quét điểm phục hồi**:
   - Hệ thống tự động quét nhận diện toàn bộ các điểm sao lưu (Full & Incremental).
   - Bảng danh sách hiển thị rõ ràng: *Thời điểm tạo (UTC), Chế độ (Full/Incremental), Mã điểm phục hồi, Số tệp, Dung lượng, Định dạng (ZIP / Raw)*.
4. Chọn điểm phục hồi bạn muốn khôi phục về (Ví dụ: một mốc sao lưu Incremental).
5. Chỉ định **Thư mục đích nhận dữ liệu phục hồi (Target Directory)**: Có thể khôi phục về thư mục ban đầu hoặc một thư mục độc lập mới.
6. **Tuỳ chọn Khôi phục chọn lọc thư mục con (Selective Folder Restore)**:
   - Tích chọn *Chỉ khôi phục thư mục con cụ thể*.
   - Nhập tên thư mục con cần lấy (Ví dụ: `Accounting` hoặc `DuAn_A`). Hệ thống sẽ chỉ trích xuất đúng thư mục đó thay vì khôi phục toàn bộ.
7. Tùy chọn: Tích chọn **Ghi đè tệp nếu đã tồn tại ở thư mục đích (Overwrite existing files)**.
8. Nếu bản sao lưu có mã hóa AES-256, nhập mật khẩu giải mã.
9. Nhấn nút **Bắt đầu phục hồi**:
   - Hệ thống tự động truy vết chuỗi sao lưu từ điểm Full cơ sở xuyên suốt các điểm Incremental liên tiếp đến thời điểm chọn (`Full -> Incr 1 -> Incr 2 -> ... -> Điểm đã chọn`).
   - Tải và tái tạo nguyên vẹn 100% dữ liệu gốc về thư mục đích.

---

## 7. Tra cứu & Phân tích Nhật ký vận hành (Operation Logs Viewer)

FTPSys-Pro cung cấp 2 phương thức theo dõi log linh hoạt:

1. **Xem trực tiếp ngay tại Thẻ Log (Thẻ thứ 3 trên giao diện chính)**:
   - Giao diện thiết kế chuẩn phong cách Notepad++ với nền tối trực quan, phông chữ monospace rõ nét.
   - Hỗ trợ chọn tệp log theo ngày, lọc theo từng Job cụ thể hoặc xem log chung của toàn bộ dịch vụ.
   - Tự động làm mới mỗi 3 giây và tự động cuộn xuống dòng mới nhất khi có sự kiện sao lưu phát sinh.
   - Tích hợp sẵn nút **Mở Notepad** và **Mở thư mục Log** để trích xuất nhanh file log gốc.

2. **Cửa sổ trình duyệt log chuyên sâu (Operation Logs Dialog)**:
   - Nhấn nút **Nhật ký vận hành** trên thanh công cụ chính để mở cửa sổ log toàn màn hình.
   - **Phân loại mức độ rõ ràng**:
     - `ERR (Lỗi vận hành)`: Các sự cố nghiêm trọng cần can thiệp xử lý ngay.
     - `WRN (Cảnh báo)`: Các cảnh báo về dung lượng đĩa, tệp tin bị bỏ qua, hoặc retry mạng.
     - `INF (Thông tin vận hành)`: Tiến trình khởi động, hoàn thành Job, số lượng tệp đã xử lý.
   - **Bộ lọc linh hoạt**: Lọc nhật ký theo từng Job sao lưu cụ thể hoặc xem toàn bộ hệ thống.
   - **Tìm kiếm thời gian thực**: Nhập từ khóa để lọc ngay các dòng log có liên quan.
   - **Nút Mở tệp log**: Mở trực tiếp tệp log thô trên đĩa để phục vụ việc trích xuất và gửi cho đội ngũ hỗ trợ kỹ thuật.

---

## 8. Cài đặt hệ thống (System Settings)

Nhấn nút **Cài đặt (Settings)** trên thanh công cụ chính:

- **Cấu hình chung (General)**:
  - `Ngôn ngữ hiển thị`: Chuyển đổi linh hoạt giữa Tiếng Việt và English.
  - `Giới hạn số Job chạy đồng thời`: Mặc định là 2 (tối đa 10) nhằm tối ưu tài nguyên CPU và I/O đĩa.
  - `Thời hạn lưu trữ nhật ký vận hành`: Tự động dọn dẹp các tệp log cũ sau $N$ ngày.
  - `Khởi động cùng Windows & Thu nhỏ xuống khay`: Duy trì giám sát nền liên tục.
- **Cấu hình máy chủ Email (SMTP)**:
  - Hỗ trợ đầy đủ `Host`, `Port`, `SSL trực tiếp (Port 465)` hoặc `STARTTLS (Port 587)`.
  - Nhấn nút **Gửi email kiểm tra...** để kiểm tra ngay tính sẵn sàng của máy chủ SMTP trước khi đưa vào vận hành thực tế.

---

## 9. Quản trị dịch vụ Windows Service và Khay hệ thống

### Quản trị qua ứng dụng khay (System Tray)
- Nhấp chuột phải vào biểu tượng **FTPSys-Pro** ở góc dưới bên phải màn hình:
  - **Mở giao diện quản lý**: Mở cửa sổ chính.
  - **Thực thi nhanh Job sao lưu**: Chạy Full hoặc Incremental cho từng Job mà không cần mở giao diện.
  - **Thoát ứng dụng quản lý**: Đóng giao diện người dùng. *Lưu ý: Dịch vụ Windows Service `FtpSysProService` vẫn tiếp tục chạy ngầm đảm bảo đúng lịch sao lưu đã định.*

### Quản trị qua lệnh PowerShell / Command Prompt (Administrator)

```powershell
# Kiểm tra trạng thái dịch vụ
Get-Service FtpSysProService

# Khởi động dịch vụ
Start-Service FtpSysProService

# Dừng dịch vụ
Stop-Service FtpSysProService

# Khởi động lại dịch vụ
Restart-Service FtpSysProService

# Chạy thử nghiệm dịch vụ ở chế độ Console trực tiếp để kiểm tra log
cd "C:\Program Files\FTPSys Pro"
.\FtpSysPro.Service.exe --console
```

---

## 10. Xử lý sự cố thường gặp (Troubleshooting)

| Tình huống sự cố | Nguyên nhân khả dĩ | Giải pháp khắc phục chuẩn |
| :--- | :--- | :--- |
| **Giao diện báo "Lỗi kết nối IPC đến FtpSysProService"** | Dịch vụ Windows Service đang bị dừng hoặc bị phần mềm diệt virus chặn Named Pipe | Mở `services.msc`, tìm dịch vụ `FtpSysProService` và nhấn **Start**, hoặc nhấn nút *Bật Service* trực tiếp trên thanh trạng thái của ứng dụng. |
| **Không thể kết nối đến thư mục chia sẻ mạng UNC (`\\server\share`)** | Quyền tài khoản `LocalSystem` không có quyền truy cập mạng domain | Điền đầy đủ tài khoản `Domain\Username` và Mật khẩu trong phần cấu hình kho lưu trữ đích UNC của Job để kích hoạt xác thực mạng `WNetAddConnection2`. |
| **Lỗi chứng chỉ FTPS "Certificate validation failed"** | Máy chủ FTP sử dụng chứng chỉ SSL tự ký (Self-signed) chưa được tin cậy | FTPSys-Pro đã tích hợp cơ chế tự động chấp nhận chứng chỉ của máy chủ đích đã cấu hình; kiểm tra lại cổng kết nối (Explicit: 21, Implicit: 990). |
| **Không gửi được Email thông báo qua Gmail / Google Workspace** | Chính sách bảo mật của Google từ chối mật khẩu thông thường | Bật Xác thực 2 bước (2FA) trên tài khoản Google, tạo **Mật khẩu ứng dụng (App Password)** 16 ký tự và nhập vào ô Mật khẩu SMTP trong Cài đặt hệ thống. |
| **Cảnh báo không đủ dung lượng đĩa (Disk Space Check Failed)** | Ổ đĩa đích hoặc ổ đĩa tạm (C:\) không đủ không gian trống | Dọn dẹp ổ đĩa hoặc vào Cài đặt hệ thống để chuyển thư mục tạm (Temp Directory) sang phân vùng ổ đĩa có dung lượng lớn hơn. |
