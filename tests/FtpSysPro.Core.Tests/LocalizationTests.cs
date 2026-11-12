using FtpSysPro.Core.Resources;
using Xunit;

namespace FtpSysPro.Core.Tests
{
    public class LocalizationTests
    {
        [Fact]
        public void DefaultOrEnglish_ReturnsEnglishStrings()
        {
            Strings.SetLanguage("en");

            Assert.False(Strings.IsVi);
            Assert.Equal("FTPSys Pro - Enterprise Backup & Recovery", Strings.AppName);
            Assert.Equal("Add Job", Strings.Add);
            Assert.Equal("Edit", Strings.Edit);
            Assert.Equal("Delete", Strings.Delete);
            Assert.Equal("Run Job", Strings.Run);
            Assert.Equal("Stop", Strings.Stop);
            Assert.Equal("Settings", Strings.Settings);
            Assert.Equal("Operation Logs", Strings.Logs);
            Assert.Equal("Restore Data", Strings.Restore);
            Assert.Equal("Job Name", Strings.ColJobName);
            Assert.Equal("Backup Job Details", Strings.DetailCardTitle);
            Assert.Equal("Full", Strings.ModeFullShort);
            Assert.Equal("Incremental", Strings.ModeIncrementalShort);
            Assert.Equal("ZIP Compression (Level: 5)", Strings.DetailZipLevel(5));
            Assert.Equal("Direct Copy (Uncompressed)", Strings.DetailDirectCopy);
        }

        [Fact]
        public void Vietnamese_ReturnsVietnameseStrings()
        {
            Strings.SetLanguage("vi");

            Assert.True(Strings.IsVi);
            Assert.Equal("FTPSys Pro - Sao lưu Dữ liệu Doanh nghiệp", Strings.AppName);
            Assert.Equal("Thêm Job", Strings.Add);
            Assert.Equal("Chỉnh sửa", Strings.Edit);
            Assert.Equal("Xóa", Strings.Delete);
            Assert.Equal("Thực thi Job", Strings.Run);
            Assert.Equal("Dừng thực thi", Strings.Stop);
            Assert.Equal("Cài đặt hệ thống", Strings.Settings);
            Assert.Equal("Nhật ký vận hành", Strings.Logs);
            Assert.Equal("Phục hồi dữ liệu", Strings.Restore);
            Assert.Equal("Tên Job sao lưu", Strings.ColJobName);
            Assert.Equal("Thông tin chi tiết Job sao lưu", Strings.DetailCardTitle);
            Assert.Equal("Toàn phần", Strings.ModeFullShort);
            Assert.Equal("Gia tăng", Strings.ModeIncrementalShort);
            Assert.Equal("Nén đóng gói .ZIP (Mức: 5)", Strings.DetailZipLevel(5));
            Assert.Equal("Sao chép trực tiếp (Không nén)", Strings.DetailDirectCopy);

            // Clean up: Reset to English default
            Strings.SetLanguage("en");
        }

        [Fact]
        public void SwitchingLanguageBackAndForth_UpdatesDynamically()
        {
            Strings.SetLanguage("en");
            Assert.Equal("Add Job", Strings.Add);

            Strings.SetLanguage("vi");
            Assert.Equal("Thêm Job", Strings.Add);

            Strings.SetLanguage("en");
            Assert.Equal("Add Job", Strings.Add);
        }
    }
}
