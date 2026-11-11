using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using FtpSysPro.Core.Engine;
using FtpSysPro.Core.Ipc;
using FtpSysPro.Core.Models;
using FtpSysPro.Core.Resources;

namespace FtpSysPro.Manager.UI
{
    public class RestoreWizardDialog : Form
    {
        private readonly List<BackupJob> _jobs;
        private readonly PipeClient _pipeClient;

        private ComboBox cboJob;
        private ListView lvBackupSets;
        private TextBox txtTargetFolder;
        private Button btnBrowseTarget;
        private CheckBox chkOverwrite;
        private TextBox txtPassword;
        private Label lblPassword;
        private Button btnRestore;
        private Button btnCancel;
        private ProgressBar progressBar;
        private Label lblStatus;

        private List<BackupSetManifest> _loadedSets = new List<BackupSetManifest>();

        public RestoreWizardDialog(List<BackupJob> jobs, PipeClient pipeClient, string preselectedJobId = null)
        {
            _jobs = jobs ?? new List<BackupJob>();
            _pipeClient = pipeClient;

            InitializeComponents();
            PopulateJobs(preselectedJobId);
        }

        private void InitializeComponents()
        {
            Text = Strings.AppName + " - " + Strings.Restore;
            Size = new Size(720, 570);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = new Font("Segoe UI", 9F);

            var lblJob = new Label
            {
                Text = Strings.IsVi ? "1. Lựa chọn Job sao lưu cần phục hồi dữ liệu:" : "1. Select Backup Job to restore:",
                Location = new Point(20, 15),
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold)
            };
            cboJob = new ComboBox { Location = new Point(20, 38), Width = 450, DropDownStyle = ComboBoxStyle.DropDownList };
            cboJob.SelectedIndexChanged += async (s, e) => await LoadBackupSetsForSelectedJobAsync();

            var lblSets = new Label
            {
                Text = Strings.IsVi ? "2. Lựa chọn điểm phục hồi cần khôi phục:" : "2. Select Restore Point to restore:",
                Location = new Point(20, 75),
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold)
            };
            lvBackupSets = new ListView
            {
                Location = new Point(20, 98),
                Size = new Size(665, 180),
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false
            };
            lvBackupSets.Columns.Add(Strings.IsVi ? "Thời điểm tạo (UTC)" : "Created (UTC)", 140);
            lvBackupSets.Columns.Add(Strings.IsVi ? "Chế độ" : "Mode", 80);
            lvBackupSets.Columns.Add(Strings.IsVi ? "Mã điểm phục hồi" : "Restore Point ID", 200);
            lvBackupSets.Columns.Add(Strings.IsVi ? "Số tệp" : "Files", 70);
            lvBackupSets.Columns.Add(Strings.IsVi ? "Dung lượng" : "Size", 90);
            lvBackupSets.Columns.Add(Strings.IsVi ? "Định dạng" : "Format", 80);

            var lblTarget = new Label
            {
                Text = Strings.IsVi ? "3. Thư mục đích nhận dữ liệu phục hồi:" : "3. Target Restoration Directory:",
                Location = new Point(20, 290),
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold)
            };
            txtTargetFolder = new TextBox { Location = new Point(20, 313), Width = 555 };
            btnBrowseTarget = new Button { Text = Strings.Browse, Location = new Point(585, 311), Size = new Size(100, 26) };
            btnBrowseTarget.Click += (s, e) =>
            {
                var folder = FolderBrowserEx.SelectFolder(this, txtTargetFolder.Text, Strings.IsVi ? "Chọn thư mục đích tiếp nhận dữ liệu phục hồi" : "Select target directory for data restoration");
                if (!string.IsNullOrEmpty(folder)) txtTargetFolder.Text = folder;
            };

            chkOverwrite = new CheckBox
            {
                Text = Strings.IsVi ? "Ghi đè tệp nếu đã tồn tại ở thư mục đích" : "Overwrite existing files in target directory",
                Location = new Point(20, 345),
                AutoSize = true,
                Checked = true
            };

            lblPassword = new Label
            {
                Text = Strings.IsVi ? "Mật khẩu giải mã dữ liệu (nếu có mã hóa AES-256):" : "Decryption Password (if AES-256 encrypted):",
                Location = new Point(20, 375),
                AutoSize = true
            };
            txtPassword = new TextBox { Location = new Point(370, 372), Width = 205, UseSystemPasswordChar = true };

            progressBar = new ProgressBar { Location = new Point(20, 420), Size = new Size(665, 20), Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 0, Visible = false };
            lblStatus = new Label { Location = new Point(20, 445), AutoSize = true, Text = "" };

            btnRestore = new Button
            {
                Text = "🔄 " + Strings.Restore,
                Location = new Point(460, 480),
                Size = new Size(130, 32),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font(Font, FontStyle.Bold)
            };
            btnCancel = new Button { Text = Strings.Close, DialogResult = DialogResult.Cancel, Location = new Point(600, 480), Size = new Size(85, 32) };

            btnRestore.Click += async (s, e) => await ExecuteRestoreAsync();

            Controls.AddRange(new Control[] {
                lblJob, cboJob,
                lblSets, lvBackupSets,
                lblTarget, txtTargetFolder, btnBrowseTarget,
                chkOverwrite, lblPassword, txtPassword,
                progressBar, lblStatus,
                btnRestore, btnCancel
            });
        }

        private void PopulateJobs(string preselectedJobId)
        {
            cboJob.Items.Clear();
            int selectedIndex = 0;
            for (int i = 0; i < _jobs.Count; i++)
            {
                var j = _jobs[i];
                cboJob.Items.Add(j.Name);
                if (!string.IsNullOrEmpty(preselectedJobId) && j.Id == preselectedJobId)
                {
                    selectedIndex = i;
                }
            }

            if (cboJob.Items.Count > 0)
                cboJob.SelectedIndex = selectedIndex;
        }

        private async System.Threading.Tasks.Task LoadBackupSetsForSelectedJobAsync()
        {
            if (cboJob.SelectedIndex < 0 || cboJob.SelectedIndex >= _jobs.Count)
                return;

            var job = _jobs[cboJob.SelectedIndex];
            lvBackupSets.Items.Clear();
            lblStatus.Text = Strings.IsVi
                ? "Đang truy vấn danh sách điểm phục hồi từ kho lưu trữ đích..."
                : "Querying restore points from destination repository...";

            try
            {
                var resp = await _pipeClient.SendAsync(new IpcMessage
                {
                    Action = IpcActions.ListBackupSets,
                    PayloadJson = job.Id
                });

                if (resp != null && resp.Success)
                {
                    _loadedSets = Newtonsoft.Json.JsonConvert.DeserializeObject<List<BackupSetManifest>>(resp.PayloadJson) ?? new List<BackupSetManifest>();
                    foreach (var s in _loadedSets)
                    {
                        var item = new ListViewItem(s.CreatedUtc.ToString("yyyy-MM-dd HH:mm:ss"));
                        item.SubItems.Add(s.BackupType == BackupType.Full ? (Strings.IsVi ? "Toàn phần" : "Full") : (Strings.IsVi ? "Gia tăng" : "Incremental"));
                        item.SubItems.Add(s.BackupSetId);
                        item.SubItems.Add(s.FileCount.ToString("N0"));
                        item.SubItems.Add(FormatBytes(s.TotalSizeBytes));
                        item.SubItems.Add((s.IsCompressed ? "ZIP" : "Raw") + (s.IsEncrypted ? " [AES-256]" : ""));
                        item.Tag = s;
                        lvBackupSets.Items.Add(item);
                    }
                    lblStatus.Text = Strings.IsVi
                        ? $"Đã tìm thấy {_loadedSets.Count} điểm phục hồi khả dụng."
                        : $"Found {_loadedSets.Count} available restore points.";
                    if (lvBackupSets.Items.Count > 0)
                        lvBackupSets.Items[0].Selected = true;
                }
                else
                {
                    lblStatus.Text = (Strings.IsVi ? "Không thể đọc danh sách điểm phục hồi: " : "Failed to load restore points: ") + resp?.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = (Strings.IsVi ? "Lỗi kết nối IPC đến dịch vụ: " : "IPC connection error: ") + ex.Message;
            }
        }

        private async System.Threading.Tasks.Task ExecuteRestoreAsync()
        {
            if (lvBackupSets.SelectedItems.Count == 0)
            {
                MessageBox.Show(this,
                    Strings.IsVi ? "Vui lòng chọn một điểm phục hồi cần khôi phục." : "Please select a restore point to restore.",
                    Strings.IsVi ? "Thông báo" : "Notice",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtTargetFolder.Text))
            {
                MessageBox.Show(this,
                    Strings.IsVi ? "Vui lòng chỉ định thư mục đích tiếp nhận dữ liệu phục hồi." : "Please specify a target directory for restoration.",
                    Strings.IsVi ? "Thông báo" : "Notice",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtTargetFolder.Focus();
                return;
            }

            var selectedSet = (BackupSetManifest)lvBackupSets.SelectedItems[0].Tag;
            var job = _jobs[cboJob.SelectedIndex];

            var confirmMsg = Strings.IsVi
                ? $"Hệ thống chuẩn bị tiến hành phục hồi dữ liệu từ điểm phục hồi:\n{selectedSet.BackupSetId}\n\nVào thư mục đích:\n{txtTargetFolder.Text}\n\nBạn có chắc chắn muốn thực hiện tiến trình này không?"
                : $"Ready to restore data from restore point:\n{selectedSet.BackupSetId}\n\nInto target directory:\n{txtTargetFolder.Text}\n\nDo you want to proceed with restoration?";

            if (MessageBox.Show(this, confirmMsg, Strings.IsVi ? "Xác nhận phục hồi dữ liệu" : "Confirm Data Restoration", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            btnRestore.Enabled = false;
            btnCancel.Enabled = false;
            progressBar.Visible = true;
            progressBar.MarqueeAnimationSpeed = 30;
            lblStatus.Text = Strings.IsVi
                ? "Đang tiến hành phục hồi dữ liệu (tải và tái tạo chuỗi Full + Incremental)..."
                : "Restoring data (downloading and reconstructing backup chain)...";

            try
            {
                var req = new RestoreRequest
                {
                    JobId = job.Id,
                    BackupSetId = selectedSet.BackupSetId,
                    TargetDirectory = txtTargetFolder.Text.Trim(),
                    OverwriteExisting = chkOverwrite.Checked,
                    ZipPassword = txtPassword.Text
                };

                var resp = await _pipeClient.SendAsync(new IpcMessage
                {
                    Action = IpcActions.Restore,
                    PayloadJson = Newtonsoft.Json.JsonConvert.SerializeObject(req)
                }, 300000); // 5 min timeout

                if (resp != null && resp.Success)
                {
                    var result = Newtonsoft.Json.JsonConvert.DeserializeObject<RestoreResult>(resp.PayloadJson);
                    if (result != null && result.Success)
                    {
                        MessageBox.Show(this, result.Message, Strings.IsVi ? "Phục hồi dữ liệu thành công" : "Restoration Succeeded", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        Close();
                    }
                    else
                    {
                        MessageBox.Show(this,
                            (Strings.IsVi ? "Phục hồi dữ liệu thất bại: " : "Restoration failed: ") + result?.Message,
                            Strings.IsVi ? "Lỗi thực thi" : "Execution Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else
                {
                    MessageBox.Show(this,
                        (Strings.IsVi ? "Lỗi từ dịch vụ hệ thống: " : "Service error: ") + resp?.ErrorMessage,
                        Strings.IsVi ? "Lỗi" : "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    (Strings.IsVi ? "Lỗi: " : "Error: ") + ex.Message,
                    Strings.IsVi ? "Lỗi" : "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnRestore.Enabled = true;
                btnCancel.Enabled = true;
                progressBar.Visible = false;
                lblStatus.Text = "";
            }
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
