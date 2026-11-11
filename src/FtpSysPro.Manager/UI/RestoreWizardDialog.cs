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
            Text = Strings.AppName + " - Phục hồi dữ liệu / Data Restoration";
            Size = new Size(720, 570);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = new Font("Segoe UI", 9F);

            var lblJob = new Label { Text = "1. Lựa chọn Job sao lưu cần phục hồi dữ liệu:", Location = new Point(20, 15), AutoSize = true, Font = new Font(Font, FontStyle.Bold) };
            cboJob = new ComboBox { Location = new Point(20, 38), Width = 450, DropDownStyle = ComboBoxStyle.DropDownList };
            cboJob.SelectedIndexChanged += async (s, e) => await LoadBackupSetsForSelectedJobAsync();

            var lblSets = new Label { Text = "2. Lựa chọn điểm phục hồi (Restore Point) cần khôi phục:", Location = new Point(20, 75), AutoSize = true, Font = new Font(Font, FontStyle.Bold) };
            lvBackupSets = new ListView
            {
                Location = new Point(20, 98),
                Size = new Size(665, 180),
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false
            };
            lvBackupSets.Columns.Add("Thời điểm tạo (UTC)", 140);
            lvBackupSets.Columns.Add("Chế độ", 80);
            lvBackupSets.Columns.Add("Mã điểm phục hồi (Restore Point)", 200);
            lvBackupSets.Columns.Add("Số tệp", 70);
            lvBackupSets.Columns.Add("Dung lượng", 90);
            lvBackupSets.Columns.Add("Định dạng", 80);

            var lblTarget = new Label { Text = "3. Thư mục đích nhận dữ liệu phục hồi (Target Directory):", Location = new Point(20, 290), AutoSize = true, Font = new Font(Font, FontStyle.Bold) };
            txtTargetFolder = new TextBox { Location = new Point(20, 313), Width = 555 };
            btnBrowseTarget = new Button { Text = Strings.Browse, Location = new Point(585, 311), Size = new Size(100, 26) };
            btnBrowseTarget.Click += (s, e) =>
            {
                var folder = FolderBrowserEx.SelectFolder(this, txtTargetFolder.Text, "Chọn thư mục đích tiếp nhận dữ liệu phục hồi");
                if (!string.IsNullOrEmpty(folder)) txtTargetFolder.Text = folder;
            };

            chkOverwrite = new CheckBox { Text = "Ghi đè tệp nếu đã tồn tại ở thư mục đích (Overwrite existing files)", Location = new Point(20, 345), AutoSize = true, Checked = true };

            lblPassword = new Label { Text = "Mật khẩu giải mã dữ liệu (nếu bản sao lưu có mã hóa AES-256):", Location = new Point(20, 375), AutoSize = true };
            txtPassword = new TextBox { Location = new Point(370, 372), Width = 205, UseSystemPasswordChar = true };

            progressBar = new ProgressBar { Location = new Point(20, 420), Size = new Size(665, 20), Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 0, Visible = false };
            lblStatus = new Label { Location = new Point(20, 445), AutoSize = true, Text = "" };

            btnRestore = new Button { Text = "Phục hồi dữ liệu", Location = new Point(460, 480), Size = new Size(130, 32), BackColor = Color.FromArgb(0, 120, 215), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font(Font, FontStyle.Bold) };
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
            lblStatus.Text = "Đang truy vấn danh sách điểm phục hồi từ kho lưu trữ đích...";

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
                        item.SubItems.Add(s.BackupType == BackupType.Full ? "Full" : "Incremental");
                        item.SubItems.Add(s.BackupSetId);
                        item.SubItems.Add(s.FileCount.ToString("N0"));
                        item.SubItems.Add(FormatBytes(s.TotalSizeBytes));
                        item.SubItems.Add((s.IsCompressed ? "ZIP" : "Raw") + (s.IsEncrypted ? " [AES-256]" : ""));
                        item.Tag = s;
                        lvBackupSets.Items.Add(item);
                    }
                    lblStatus.Text = $"Đã tìm thấy {_loadedSets.Count} điểm phục hồi (Restore Points) khả dụng.";
                    if (lvBackupSets.Items.Count > 0)
                        lvBackupSets.Items[0].Selected = true;
                }
                else
                {
                    lblStatus.Text = "Không thể đọc danh sách điểm phục hồi: " + resp?.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Lỗi kết nối IPC đến dịch vụ FtpSysProService: " + ex.Message;
            }
        }

        private async System.Threading.Tasks.Task ExecuteRestoreAsync()
        {
            if (lvBackupSets.SelectedItems.Count == 0)
            {
                MessageBox.Show(this, "Vui lòng chọn một điểm phục hồi (Restore Point) cần khôi phục.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtTargetFolder.Text))
            {
                MessageBox.Show(this, "Vui lòng chỉ định thư mục đích tiếp nhận dữ liệu phục hồi.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtTargetFolder.Focus();
                return;
            }

            var selectedSet = (BackupSetManifest)lvBackupSets.SelectedItems[0].Tag;
            var job = _jobs[cboJob.SelectedIndex];

            var confirmMsg = $"Hệ thống chuẩn bị tiến hành phục hồi dữ liệu từ điểm phục hồi:\n{selectedSet.BackupSetId}\n\nVào thư mục đích:\n{txtTargetFolder.Text}\n\nBạn có chắc chắn muốn thực hiện tiến trình này không?";
            if (MessageBox.Show(this, confirmMsg, "Xác nhận phục hồi dữ liệu", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            btnRestore.Enabled = false;
            btnCancel.Enabled = false;
            progressBar.Visible = true;
            progressBar.MarqueeAnimationSpeed = 30;
            lblStatus.Text = "Đang tiến hành phục hồi dữ liệu (tải và tái tạo chuỗi Full + Incremental)...";

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
                        MessageBox.Show(this, result.Message, "Phục hồi dữ liệu thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        Close();
                    }
                    else
                    {
                        MessageBox.Show(this, "Phục hồi dữ liệu thất bại: " + result?.Message, "Lỗi thực thi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else
                {
                    MessageBox.Show(this, "Lỗi từ dịch vụ hệ thống FtpSysProService: " + resp?.ErrorMessage, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Lỗi: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
