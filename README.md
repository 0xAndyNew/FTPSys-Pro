# FTPSys-Pro 🛡️

[![Platform](https://img.shields.io/badge/Platform-Windows%207%20SP1%20%E2%86%92%2011%20%7C%20Server%202008%20R2%20%E2%86%92%202025-blue.svg)](https://dotnet.microsoft.com/)
[![Framework](https://img.shields.io/badge/.NET%20Framework-4.8%20AnyCPU-brightgreen.svg)](https://dotnet.microsoft.com/)
[![Service](https://img.shields.io/badge/Architecture-Windows%20Service%20%2B%20Tray%20Manager-purple.svg)]()
[![Security](https://img.shields.io/badge/Security-DPAPI%20%2B%20AES--256%20%2B%20TLS%201.2%2F1.3-red.svg)]()
[![License](https://img.shields.io/badge/License-MIT-orange.svg)](LICENSE)

> **FTPSys-Pro** là giải pháp phần mềm sao lưu dữ liệu tự động, đa giao thức, chuẩn công nghiệp dành cho mọi phiên bản Windows. Ứng dụng chạy nền liên tục dưới dạng **Windows Service (`LocalSystem`)**, đi kèm ứng dụng quản trị khay hệ thống (**System Tray Manager**) hiện đại, hỗ trợ Volume Shadow Copy (VSS), kiểm tra mã băm SHA-256 đầu cuối, và chống tràn đĩa trước khi sao lưu.

---

## 📑 Mục lục
- [🌟 Tính năng nổi bật](#-tính-năng-nổi-bật)
- [🏛 Kiến trúc hệ thống](#-kiến-trúc-hệ-thống)
- [📦 Hỗ trợ đa dạng đích đến](#-hỗ-trợ-đa-dạng-đích-đến)
- [🔒 Bảo mật & Toàn vẹn dữ liệu cao cấp](#-bảo-mật--toàn-vẹn-dữ-liệu-cao-cấp)
- [🖥 Giao diện người dùng](#-giao-diện-người-dùng)
- [🚀 Hướng dẫn cài đặt & Biên dịch](#-hướng-dẫn-cài-đặt--biên-dịch)
- [📖 Tài liệu hướng dẫn sử dụng](#-tài-liệu-hướng-dẫn-sử-dụng)
- [⚙️ Tham số dòng lệnh (CLI)](#️-tham-số-dòng-lệnh-cli)
- [👤 Thông tin tác giả & Giấy phép](#-thông-tin-tác-giả--giấy-phép)

---

## 🌟 Tính năng nổi bật

### 1. Đa dạng chế độ truyền dữ liệu
- **Đẩy thẳng cả thư mục (Mặc định - Uncompressed Folder Mirroring)**: Giữ nguyên vẹn 100% cấu trúc thư mục, các thư mục con và tệp tin sang máy chủ từ xa mà không cần nén. Thích hợp cho việc đồng bộ thư viện dữ liệu lớn, server tài liệu, web assets.
- **Nén đóng gói .ZIP + Mã hoá AES-256**: Tuỳ chọn nén gọn thành file archive `.zip` với 4 cấp độ nén (Store, Fastest, Normal, Maximum), hỗ trợ đặt mật khẩu mã hoá quân sự **AES-256** chống rò rỉ dữ liệu khi đẩy lên cloud/server ngoại vi.

### 2. Tính toán & Hiển thị dung lượng trực quan
- **Hiển thị dung lượng nguồn tức thì (Live Source Size Calculation)**: Quét và tính toán tổng dung lượng cần sao lưu ngay khi thêm thư mục nguồn trong màn hình cấu hình job, không cần đợi chạy backup lần đầu mới biết dung lượng.
- **Khung thông tin chi tiết trên màn hình chính (Side Info Card)**: Bảng điều khiển SplitContainer hiển thị tên tác vụ, tổng dung lượng cần backup, chế độ truyền, đích đến, danh sách thư mục nguồn và lịch sử chạy.

### 3. Sao lưu Full & Incremental thông minh
- **Full Backup**: Tạo bộ sao lưu đầy đủ toàn bộ thư mục nguồn.
- **Incremental Backup**: Tự động so khớp với manifest của lần backup trước, chỉ sao lưu các file mới thêm hoặc có nội dung sửa đổi (dựa trên size + LastWriteUtc), đồng thời ghi nhận danh sách file đã bị xoá.

### 4. Bốn lớp bảo vệ độ tin cậy dữ liệu (Data Reliability)
1. **Volume Shadow Copy (VSS) Snapshot**: Tự động tạo ảnh chụp ổ đĩa bóng để sao lưu trơn tru các tệp tin đang bị khóa bởi tiến trình khác (SQL Server `.mdf`/`.ldf`, Hyper-V `.vhdx`, Outlook `.pst`/`.ost`, máy ảo đang chạy) mà không làm gián đoạn dịch vụ.
2. **Kiểm tra mã băm SHA-256 đầu cuối (Cryptographic Checksum Verification)**: Tự động tính toán mã băm SHA-256 của file nguồn và stream kiểm tra ngược lại file trên máy chủ đích trước khi hoàn tất cam kết (commit).
3. **Kiểm tra trước dung lượng trống (Pre-flight Disk Space Check)**: Đánh giá dung lượng trống trên cả ổ tạm (Temp) lẫn ổ đích (Local/UNC) trước khi thực hiện. So sánh chính xác 1:1, ngăn chặn sự cố tràn đĩa gây đứt quãng và hỏng dữ liệu.
4. **Tự động thử lại luỹ tiến (Exponential Backoff Auto-Retry)**: Khả năng chống chịu đứt gãy kết nối mạng chập chờn với chu kỳ thử lại thông minh (2s -> 4s -> 8s) cho FTP, SFTP và UNC share.

### 5. Lập lịch tự động linh hoạt (Flexible Scheduling)
- Chạy một lần (Once).
- Hằng ngày (Daily) vào giờ chỉ định.
- Hằng tuần (Weekly) theo các thứ trong tuần được chọn.
- Hằng tháng (Monthly) vào ngày trong tháng.
- Lặp lại theo chu kỳ (Repeat every N minutes/hours) trong khung giờ làm việc.
- Hỗ trợ chạy bù lần bị lỡ khi khởi động lại máy tính (Catch-up missed runs).

### 6. Tự động dọn dẹp bản sao lưu cũ (Retention Policy)
- Giữ lại N bộ Full gần nhất (kèm các bản Incremental tương ứng).
- Hoặc giữ các bộ sao lưu trong vòng N ngày gần nhất.
- Thuật toán thông minh: Không bao giờ xoá bản Full gốc nếu còn các bản Incremental đang phụ thuộc vào nó.

### 7. Khôi phục dữ liệu tức thì (Restore Wizard)
- Trình hướng dẫn khôi phục trực quan tích hợp sẵn trong ứng dụng.
- Tự động nhận diện chuỗi `Full -> Incr 1 -> Incr 2 -> ... -> Điểm thời gian chọn` để tái cấu trúc chính xác dữ liệu về thư mục gốc hoặc thư mục chỉ định mới.
- Hỗ trợ cả bản sao lưu nén `.zip` (tự giải mã AES) lẫn bản sao lưu thư mục không nén.

### 8. Báo cáo Email tự động (SMTP TLS)
- Gửi báo cáo HTML chi tiết sau khi hoàn thành công việc: trạng thái, thời gian chạy, số tệp sao lưu, tổng dung lượng, cảnh báo/lỗi.
- Hỗ trợ SSL/TLS và STARTTLS tương thích Gmail, Office 365, Zimbra, Exchange, SMTP Server nội bộ.
- Tuỳ chọn điều kiện gửi: Luôn luôn, chỉ khi lỗi, hoặc chỉ khi thành công.

---

## 🏛 Kiến trúc hệ thống

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           FTPSys-Pro Architecture                       │
└─────────────────────────────────────────────────────────────────────────┘

  ┌────────────────────────────────┐       Named Pipe IPC (JSON-RPC)
  │    FtpSysPro.Manager.exe       │ ◄──────────────────────────────────┐
  │  (WinForms System Tray App)    │   \\.\pipe\FtpSysPro.Pipe          │
  │  - Màn hình quản lý Job        │   (Restricted to Admin & SYSTEM)   │
  │  - Live Size & Transfer Status │                                    │
  │  - Restore Wizard / Settings   │                                    │
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

- **Windows Service (`LocalSystem`)**: Đảm bảo các tác vụ sao lưu chạy đúng giờ và tự động ngay cả khi chưa có người dùng đăng nhập (Unattended execution).
- **Inter-Process Communication (IPC)**: Kết nối giữa Tray Manager và Service qua Named Pipe tốc độ cao với danh sách kiểm soát truy cập (ACL) nghiêm ngặt chỉ cho phép `Administrators` và `SYSTEM`.
- **Lưu trữ bảo mật**: Cấu hình tại `C:\ProgramData\FtpSysPro`, mọi mật khẩu FTP/SFTP/UNC/SMTP/ZIP đều được mã hoá bằng Windows DPAPI (`DataProtectionScope.LocalMachine`) kết hợp Application Entropy.

---

## 📦 Hỗ trợ đa dạng đích đến

| Loại đích đến | Giao thức / Cơ chế | Tính năng hỗ trợ |
| :--- | :--- | :--- |
| **Local / External Drive** | Win32 File I/O, NTFS/FAT32/exFAT | Hỗ trợ đường dẫn dài (>260 ký tự), phân quyền bảo mật |
| **Network Share (UNC)** | SMB / CIFS (`\\server\share`) | Xác thực `WNetAddConnection2` với tài khoản/mật khẩu riêng biệt |
| **FTP / FTPS** | RFC 959, RFC 4217, TLS 1.2/1.3 | Hỗ trợ mã hoá Explicit/Implicit TLS, Passive/Active, Resume upload |
| **SFTP** | SSH-2 File Transfer Protocol | Mật khẩu hoặc Private Key (OpenSSH/PuTTY), ghim host key fingerprint |

---

## 🔒 Bảo mật & Toàn vẹn dữ liệu cao cấp

- **Mã hoá tệp ZIP**: Sử dụng thuật toán **AES-256 bit** đạt tiêu chuẩn bảo mật chính phủ Hoa Kỳ.
- **Bảo vệ mật khẩu cấu hình**: Sử dụng **Windows Data Protection API (DPAPI)** với khóa máy tính cục bộ, chống trích xuất mật khẩu trái phép kể cả khi file `config.json` bị sao chép ra ngoài.
- **Xác thực SHA-256**: Mỗi tệp tin trước khi gửi được tính băm SHA-256. Sau khi truyền sang server đích, hệ thống mở stream xác minh tính toàn vẹn 100% bit-for-bit rồi mới cam kết hoàn tất.
- **Cơ chế Commit nguyên tử (Atomic Commit)**: Dữ liệu tải lên được đặt tên tạm thời `.partial`. Chỉ khi quá trình truyền và kiểm tra mã băm thành công, file mới được đổi tên chính thức.

---

## 🖥 Giao diện người dùng

- **Thiết kế hiện đại, thông minh**:
  - Giao diện chính chia 2 phần (Split View): Bảng danh sách tác vụ trực quan bên trái và Thẻ thông tin chi tiết (Info Card) bên phải.
  - Hiển thị trực tiếp: Dung lượng nguồn cần sao lưu, chế độ truyền dữ liệu, kết quả lần chạy cuối, lịch chạy tiếp theo.
  - Cập nhật tiến độ thời gian thực (Real-time Progress Bar & Percent).
  - Khay hệ thống: Thu nhỏ ứng dụng không chiếm không gian thanh tác vụ, hiển thị thông báo Balloon khi hoàn tất sao lưu.
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
3. Chạy toàn bộ bộ kiểm thử tự động xUnit (41+ tests).
4. Tự động đóng gói bộ cài đặt `dist\FtpSysPro_Setup_v1.0.0.exe` bằng Inno Setup (nếu máy có Inno Setup 6).

### 3. Cài đặt bộ phần mềm hoàn chỉnh
1. Chạy file cài đặt `FtpSysPro_Setup_v1.0.0.exe` bằng quyền Administrator.
2. Trình cài đặt sẽ:
   - Cài đặt các file nhị phân vào thư mục `C:\Program Files\FtpSysPro\`.
   - Tự động đăng ký và khởi chạy **Windows Service: `FtpSysProService`** (tự động bật cùng Windows).
   - Đăng ký Scheduled Task khởi chạy **FtpSysPro Manager** xuống khay hệ thống khi người dùng đăng nhập.
   - Tạo shortcut trên màn hình Desktop và Start Menu.

---

## 📖 Tài liệu hướng dẫn sử dụng

Xem tài liệu hướng dẫn chi tiết từng bước tại:
👉 **[Hướng dẫn sử dụng chi tiết FtpSysPro (docs/HUONG_DAN_SU_DUNG.md)](docs/HUONG_DAN_SU_DUNG.md)**

Nội dung bao gồm:
- Hướng dẫn thiết lập tác vụ sao lưu đầu tiên.
- Cách thiết lập Volume Shadow Copy (VSS) cho cơ sở dữ liệu SQL Server / tệp đang mở.
- Cách kết nối tới FTP/FTPS và SFTP an toàn.
- Sử dụng Restore Wizard để khôi phục dữ liệu về đúng thời điểm mong muốn.
- Hướng dẫn cài đặt cấu hình thông báo Email qua Gmail / Office 365.

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
- **Email**: `179321164+0xAndyNew@users.noreply.github.com`
- **Kho lưu trữ mã nguồn**: [https://github.com/0xAndyNew/FTPSys-Pro.git](https://github.com/0xAndyNew/FTPSys-Pro.git)
- **Giấy phép**: Phát hành theo giấy phép mã nguồn mở [MIT License](LICENSE).
