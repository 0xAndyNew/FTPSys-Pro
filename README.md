# FTPSys-Pro 🛡️

[![Platform](https://img.shields.io/badge/Platform-Windows%207%20SP1%20%E2%86%92%2011%20%7C%20Server%202008%20R2%20%E2%86%92%202025-blue.svg)](https://dotnet.microsoft.com/)
[![Framework](https://img.shields.io/badge/.NET%20Framework-4.8%20AnyCPU-brightgreen.svg)](https://dotnet.microsoft.com/)
[![Service](https://img.shields.io/badge/Architecture-Windows%20Service%20%2B%20Tray%20Manager-purple.svg)]()
[![Security](https://img.shields.io/badge/Security-DPAPI%20%2B%20AES--256%20%2B%20TLS%201.2%2F1.3-red.svg)]()
[![License](https://img.shields.io/badge/License-MIT-orange.svg)](LICENSE)

> **FTPSys-Pro** là giải pháp phần mềm sao lưu dữ liệu tự động, đa giao thức, chuẩn công nghiệp dành cho mọi phiên bản Windows và Windows Server. Ứng dụng hoạt động độc lập dưới dạng **Dịch vụ hệ thống Windows Service (`FtpSysProService`)** chạy ngầm bằng tài khoản `LocalSystem`, đi kèm ứng dụng quản trị khay hệ thống (**System Tray Manager**) hiện đại, hỗ trợ Volume Shadow Copy (VSS), kiểm tra mã băm SHA-256 đầu cuối, và cơ chế chống tràn đĩa trước khi sao lưu.

---

## 📑 Mục lục
- [🌟 Tính năng nổi bật](#-tính-năng-nổi-bật)
- [🏛 Kiến trúc hệ thống](#-kiến-trúc-hệ-thống)
- [📦 Hỗ trợ đa dạng kho lưu trữ đích](#-hỗ-trợ-đa-dạng-kho-lưu-trữ-đích)
- [🔒 Bảo mật & Toàn vẹn dữ liệu vận hành](#-bảo-mật--toàn-vẹn-dữ-liệu-vận-hành)
- [🖥 Giao diện quản trị hệ thống](#-giao-diện-quản-trị-hệ-thống)
- [🚀 Hướng dẫn cài đặt & Biên dịch](#-hướng-dẫn-cài-đặt--biên-dịch)
- [📖 Tài liệu hướng dẫn vận hành](#-tài-liệu-hướng-dẫn-vận-hành)
- [⚙️ Tham số dòng lệnh (CLI)](#️-tham-số-dòng-lệnh-cli)
- [👤 Thông tin tác giả & Giấy phép](#-thông-tin-tác-giả--giấy-phép)

---

## 🌟 Tính năng nổi bật

### 1. Cơ chế đóng gói & Đồng bộ dữ liệu linh hoạt
- **Đồng bộ trực tiếp cấu trúc thư mục (Directory Structure Synchronization - Mặc định)**: Giữ nguyên vẹn 100% cây thư mục nguồn sang kho lưu trữ đích mà không cần nén. Phù hợp cho việc đồng bộ thư viện dữ liệu lớn, server tài liệu, web assets.
- **Đóng gói nén .ZIP + Mã hoá bảo mật AES-256**: Tuỳ chọn nén gọn thành tệp lưu trữ `.zip` với 4 cấp độ nén (Store, Fastest, Normal, Maximum), hỗ trợ đặt mật khẩu mã hoá tiêu chuẩn quân sự **AES-256** chống rò rỉ dữ liệu khi lưu trữ tại máy chủ ngoại vi.

### 2. Quét & Lựa chọn dữ liệu nguồn thông minh
- **Hỗ trợ chọn toàn bộ phân vùng ổ đĩa (Root Drive Backup)**: Cho phép chọn trực tiếp toàn bộ ổ đĩa (ví dụ: `C:\`, `D:\`) hoặc từng thư mục con tùy ý. Hệ thống tự động loại trừ các thư mục bảo vệ của hệ điều hành (`System Volume Information`, `$RECYCLE.BIN`, `pagefile.sys`, `hiberfil.sys`, `swapfile.sys`).
- **Tính toán dung lượng nguồn trực quan (Live Source Size Calculation)**: Quét và tính toán tổng dung lượng cần sao lưu ngay khi thêm thư mục nguồn trong màn hình cấu hình Job, không cần đợi thực thi mới biết dung lượng.
- **Bảng điều khiển chi tiết (Side Info Card)**: Hiển thị tên Job sao lưu, tổng dung lượng nguồn, chế độ thực thi, kho lưu trữ đích, danh sách nguồn và lịch sử vận hành.

### 3. Chế độ Sao lưu toàn phần & Sao lưu gia tăng chuẩn Enterprise
- **Sao lưu toàn phần (Full Backup)**: Tạo ra một điểm phục hồi (Restore Point) hoàn chỉnh và độc lập tại kho lưu trữ đích, chứa toàn bộ dữ liệu nguồn.
- **Sao lưu gia tăng (Incremental Backup)**: Tự động so khớp với manifest của điểm phục hồi trước đó, chỉ sao lưu các tệp mới thêm hoặc có nội dung thay đổi (dựa trên size + LastWriteUtc), đồng thời ghi nhận danh sách tệp đã bị xoá.

### 4. Bốn lớp bảo vệ độ tin cậy dữ liệu vận hành (Enterprise Data Protection)
1. **Volume Shadow Copy (VSS) Snapshot**: Tự động chụp snapshot ổ đĩa để sao lưu trơn tru các tệp tin đang bị khóa độc quyền bởi tiến trình khác (SQL Server `.mdf`/`.ldf`, Hyper-V `.vhdx`, Outlook `.pst`/`.ost`, máy ảo đang chạy) mà không làm gián đoạn dịch vụ.
2. **Kiểm tra tính toàn vẹn SHA-256 đầu cuối (Cryptographic Checksum Verification)**: Tính toán mã băm SHA-256 của tệp nguồn và stream kiểm tra ngược lại tệp trên máy chủ đích trước khi hoàn tất cam kết (commit).
3. **Kiểm tra dung lượng khả dụng trước khi thực thi (Pre-flight Disk Space Check)**: Đánh giá dung lượng trống trên cả ổ tạm (Temp) lẫn kho lưu trữ đích (Local/UNC) trước khi thực hiện. So sánh chính xác 1:1, ngăn ngừa sự cố tràn đĩa gây gián đoạn hệ điều hành.
4. **Tự động thử lại luỹ tiến (Exponential Backoff Auto-Retry)**: Khả năng chống chịu đứt gãy kết nối mạng chập chờn với chu kỳ thử lại thông minh (2s -> 4s -> 8s) cho FTP, SFTP và UNC share.

### 5. Lập lịch thực thi tự động & Tinh gọn (Flexible Scheduling)
- Hỗ trợ cả định dạng giờ **24h** và **12h (AM/PM)** trực quan, tiện dụng.
- **Tần suất đa dạng**:
  - Chạy một lần (Once) vào thời điểm xác định.
  - Hằng ngày (Daily) vào giờ chỉ định.
  - Hằng tuần (Weekly) theo các thứ trong tuần được chọn.
  - Hằng tháng (Monthly) vào ngày trong tháng.
  - Lặp lại theo chu kỳ (Repeat every N minutes/hours) trong khung giờ làm việc.
- Hỗ trợ chạy bù lần bị lỡ khi khởi động lại máy tính (Catch-up missed runs).

### 6. Chính sách lưu giữ điểm phục hồi thích ứng theo chế độ Job (Retention Policy)
- **Đối với Job Sao lưu toàn phần (Full Backup)**: Hệ thống duy trì $N$ điểm phục hồi Full mới nhất và tự động dọn dẹp các điểm Full cũ hơn để tối ưu hóa dung lượng kho lưu trữ.
- **Đối với Job Sao lưu gia tăng (Incremental Backup)**: Hệ thống duy trì $N$ chuỗi sao lưu (Full + Incremental) gần nhất. Mỗi chuỗi gồm 1 điểm Full cơ sở và các điểm Incremental phụ thuộc; hệ thống tuyệt đối bảo toàn điểm Full cơ sở khi các điểm Incremental liên quan vẫn còn hiệu lực lưu trữ.
- Tuỳ chọn duy trì điểm phục hồi theo thời gian ($N$ ngày) hoặc lưu giữ vĩnh viễn.

### 7. Phục hồi dữ liệu tức thì (Data Restoration Wizard)
- Trình hướng dẫn phục hồi dữ liệu chuyên dụng tích hợp sẵn trong ứng dụng.
- Tự động nhận diện chuỗi `Full -> Incr 1 -> Incr 2 -> ... -> Điểm phục hồi đã chọn` để tái cấu trúc chính xác dữ liệu về thư mục ban đầu hoặc thư mục chỉ định mới.
- Hỗ trợ cả tệp nén `.zip` (tự động giải mã AES-256) lẫn tệp đồng bộ trực tiếp cấu trúc thư mục.

### 8. Báo cáo Email tự động (SMTP TLS)
- Báo cáo HTML định dạng song ngữ chuyên nghiệp gửi tự động sau mỗi lần Job sao lưu thực thi: Tên Job, Chế độ sao lưu, Kết quả vận hành, Thời gian bắt đầu/kết thúc (UTC), Thời lượng thực thi, Dữ liệu quét/sao lưu, Mã điểm phục hồi, Chi tiết cảnh báo & lỗi.
- Hỗ trợ SSL/TLS (Port 465) và STARTTLS (Port 587) tương thích Gmail, Office 365, Zimbra, Exchange, SMTP Server nội bộ.
- Tuỳ chọn điều kiện gửi: Luôn luôn, chỉ khi phát sinh lỗi, hoặc chỉ khi thành công.

---

## 🏛 Kiến trúc hệ thống

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           FTPSys-Pro Architecture                       │
└─────────────────────────────────────────────────────────────────────────┘

  ┌────────────────────────────────┐       Named Pipe IPC (JSON-RPC)
  │    FtpSysPro.Manager.exe       │ ◄──────────────────────────────────┐
  │  (WinForms System Tray App)    │   \\.\pipe\FtpSysPro.Pipe          │
  │  - Quản lý Backup Jobs         │   (Restricted to Admin & SYSTEM)   │
  │  - Live Size & Transfer Status │                                    │
  │  - Restore Wizard / Settings   │                                    │
  │  - Log Viewer (ERR/WRN/INF)    │                                    │
  └────────────────────────────────┘                                    ▼
                                                       ┌─────────────────────────────────┐
                                                       │     FtpSysPro.Service.exe       │
                                                       │   (Windows Service: LocalSystem)│
                                                       │   - Background Scheduler        │
                                                       │   - Job Runner Orchestrator     │
                                                       │   - Named Pipe Server           │
                                                       └────────────────┬────────────────┘
                                                                        │
                   ┌────────────────────────────────────────────────────┴──────────────────────┐
                   │                                                                           │
                   ▼                                                                           ▼
     ┌───────────────────────────┐                                               ┌───────────────────────────┐
     │     FtpSysPro.Core        │                                               │    C:\ProgramData\        │
     │  - Backup & Restore Engine│                                               │         FtpSysPro\        │
     │  - VSS Snapshot Context   │                                               │  - config.json (DPAPI)    │
     │  - DiskSpaceChecker       │                                               │  - state.json             │
     │  - SHA-256 & RetryHelper  │                                               │  - catalog\{jobId}\       │
     │  - ZipArchive (AES-256)   │                                               │  - logs\                  │
     └─────────────┬─────────────┘                                               └───────────────────────────┘
                   │
  ┌────────────────┼──────────────────────────────┬──────────────────────────────┐
  ▼                ▼                              ▼                              ▼
┌──────────────┐ ┌──────────────────────────────┐ ┌────────────────────────────┐ ┌──────────────────────────┐
│  Local / USB │ │ Network Share (UNC)          │ │ FTP / FTPS Server          │ │ SFTP (SSH) Server        │
│  Hard Drives │ │ \\server\share               │ │ TLS Explicit / Implicit    │ │ Password / Private Key   │
│              │ │ (WNetAddConnection2 Logon)   │ │ Passive / Active Mode      │ │ Host Key Pinning Fingerp.│
└──────────────┘ └──────────────────────────────┘ └────────────────────────────┘ └──────────────────────────┘
```

- **Dịch vụ hệ thống Windows Service (`FtpSysProService`)**: Đảm bảo các Job sao lưu thực thi chuẩn xác theo lịch ngay cả khi chưa có người dùng đăng nhập hệ thống (Unattended Execution).
- **Inter-Process Communication (IPC)**: Kết nối giữa Tray Manager và Windows Service qua Named Pipe tốc độ cao với danh sách kiểm soát truy cập (ACL) nghiêm ngặt chỉ cho phép `Administrators` và `SYSTEM`.
- **Lưu trữ bảo mật**: Cấu hình tại `C:\ProgramData\FtpSysPro`, mọi thông tin nhạy cảm (mật khẩu FTP/SFTP/UNC/SMTP/ZIP) đều được mã hoá an toàn bằng Windows DPAPI (`DataProtectionScope.LocalMachine`) kết hợp Application Entropy.

---

## 📦 Hỗ trợ đa dạng kho lưu trữ đích

| Loại kho lưu trữ đích | Giao thức / Cơ chế | Tính năng hỗ trợ |
| :--- | :--- | :--- |
| **Local / External Drive** | Win32 File I/O, NTFS/FAT32/exFAT | Hỗ trợ đường dẫn dài (>260 ký tự), phân quyền bảo mật |
| **Network Share (UNC)** | SMB / CIFS (`\\server\share`) | Xác thực `WNetAddConnection2` với tài khoản/mật khẩu riêng biệt |
| **FTP / FTPS** | RFC 959, RFC 4217, TLS 1.2/1.3 | Hỗ trợ mã hoá Explicit/Implicit TLS, Passive/Active, Resume upload |
| **SFTP** | SSH-2 File Transfer Protocol | Mật khẩu hoặc Private Key (OpenSSH/PuTTY), ghim host key fingerprint |

---

## 🔒 Bảo mật & Toàn vẹn dữ liệu vận hành

- **Mã hoá tệp ZIP**: Sử dụng thuật toán **AES-256 bit** đạt tiêu chuẩn bảo mật quân sự.
- **Bảo vệ mật khẩu cấu hình**: Sử dụng **Windows Data Protection API (DPAPI)** với khóa máy tính cục bộ, chống trích xuất mật khẩu trái phép kể cả khi file `config.json` bị sao chép ra ngoài.
- **Xác thực mã băm SHA-256**: Mỗi tệp tin trước khi gửi được tính băm SHA-256. Sau khi truyền sang kho lưu trữ đích, hệ thống mở stream xác minh tính toàn vẹn 100% bit-for-bit rồi mới cam kết hoàn tất.
- **Cơ chế Commit nguyên tử (Atomic Commit)**: Dữ liệu tải lên được đặt tên tạm thời `.partial`. Chỉ khi quá trình truyền và kiểm tra mã băm thành công, tệp mới được đổi tên chính thức.

---

## 🖥 Giao diện quản trị hệ thống

- **Thiết kế chuyên nghiệp, phân cấp rõ ràng**:
  - Giao diện chính dạng chia đôi (Split View): Bảng danh sách Job sao lưu bên trái và Thẻ thông tin chi tiết (Info Card) bên phải.
  - Hiển thị trực tiếp: Dung lượng nguồn cần sao lưu, chế độ truyền dữ liệu, kết quả lần chạy cuối, lịch chạy tiếp theo.
  - Cập nhật tiến độ thời gian thực (Real-time Progress Bar & Percent).
  - Khay hệ thống: Thu nhỏ ứng dụng không chiếm không gian thanh tác vụ, hiển thị thông báo Balloon khi hoàn tất sao lưu.
  - **Trình duyệt nhật ký vận hành chuyên sâu ([LogViewerDialog.cs](src/FtpSysPro.Manager/UI/LogViewerDialog.cs))**: Xem nhanh toàn bộ nhật ký dịch vụ, phân loại rõ ràng mức độ `ERR (Lỗi vận hành)`, `WRN (Cảnh báo)`, `INF (Thông tin vận hành)`, lọc theo từng Job và tìm kiếm từ khóa.
- **Hỗ trợ song ngữ**: Chuyển đổi linh hoạt giữa **Tiếng Việt** và **English**.

---

## 🚀 Hướng dẫn cài đặt & Biên dịch

### 1. Yêu cầu hệ thống
- **Hệ điều hành**: Windows 7 SP1, Windows 8.1, Windows 10, Windows 11; Windows Server 2008 R2, 2012, 2016, 2019, 2022, 2025.
- **Runtime**: Microsoft .NET Framework 4.8 (mặc định đã có trên Windows 10 version 1903 trở lên và Windows Server 2019 trở lên).

### 2. Biên dịch từ mã nguồn (Build from source)
Yêu cầu máy tính có cài đặt [.NET SDK](https://dotnet.microsoft.com/download) (hỗ trợ .NET Framework 4.8):

```powershell
# Di chuyển vào thư mục dự án
cd D:\ftpsyspro

# Chạy script tự động restore NuGet, build Release và chạy Unit Tests
.\build.ps1
```

Script sẽ tự động:
1. Restore toàn bộ gói NuGet phụ thuộc (`FluentFTP`, `SSH.NET`, `SharpZipLib`, `MailKit`, `Newtonsoft.Json`, `Serilog`).
2. Biên dịch Solution ở cấu hình `Release AnyCPU`.
3. Chạy toàn bộ bộ kiểm thử tự động xUnit (38+ tests).
4. Tự động đóng gói bộ cài đặt `dist\FTPSys_Pro_Setup_v1.2.0.exe` bằng Inno Setup (nếu máy có Inno Setup 6).

### 3. Cài đặt bộ phần mềm hoàn chỉnh
1. Chạy file cài đặt `FTPSys_Pro_Setup_v1.2.0.exe` bằng quyền Administrator.
2. Trình cài đặt sẽ:
   - Cài đặt các file nhị phân vào thư mục `C:\Program Files\FTPSys Pro\`.
   - Tự động đăng ký và khởi chạy **Windows Service: `FtpSysProService`** (tự động bật cùng Windows).
   - Đăng ký Scheduled Task khởi chạy **FTPSys Pro Manager** xuống khay hệ thống khi người dùng đăng nhập.
   - Tạo shortcut trên màn hình Desktop và Start Menu.

---

## 📖 Tài liệu hướng dẫn vận hành

Xem tài liệu hướng dẫn vận hành chi tiết từng bước tại:
👉 **[Hướng dẫn vận hành chi tiết FtpSysPro (docs/HUONG_DAN_SU_DUNG.md)](docs/HUONG_DAN_SU_DUNG.md)**

Nội dung bao gồm:
- Hướng dẫn thiết lập Job sao lưu đầu tiên theo chuẩn IT Enterprise.
- Cách thiết lập Volume Shadow Copy (VSS) cho cơ sở dữ liệu SQL Server / tệp đang mở.
- Hướng dẫn chọn toàn bộ phân vùng ổ đĩa gốc (C:\, D:\...).
- Cách kết nối tới kho lưu trữ đích FTP/FTPS và SFTP an toàn.
- Thiết lập lịch sao lưu với định dạng 24h / 12h linh hoạt.
- Sử dụng Restore Wizard để phục hồi dữ liệu từ điểm phục hồi chỉ định.
- Hướng dẫn cài đặt cấu hình thông báo Email qua Gmail / Office 365.
- Tra cứu và phân tích nhật ký vận hành (Operation Logs).

---

## ⚙️ Tham số dòng lệnh (CLI)

`FtpSysPro.Service.exe` hỗ trợ các tham số dòng lệnh tiện ích:

```bash
# Chạy service dưới dạng Console (thuận tiện cho việc gỡ lỗi / kiểm tra trực tiếp)
FtpSysPro.Service.exe --console

# Cài đặt Windows Service vào hệ điều hành
FtpSysPro.Service.exe --install

# Gỡ bỏ Windows Service khỏi hệ điều hành
FtpSysPro.Service.exe --uninstall
```

---

## 👤 Thông tin tác giả & Giấy phép

- **Tác giả / Duy trì dự án**: [0xAndyNew](https://github.com/0xAndyNew)
- **Kho lưu trữ mã nguồn**: [https://github.com/0xAndyNew/FTPSys-Pro.git](https://github.com/0xAndyNew/FTPSys-Pro.git)
- **Giấy phép**: Phát hành theo giấy phép mã nguồn mở [MIT License](LICENSE).
