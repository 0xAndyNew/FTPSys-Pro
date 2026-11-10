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
            Size = new Size(680, 560);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = new Font("Segoe UI", 9F);

            var lblJob = new Label { Text = "1. Chọn tác vụ cần khôi phục:", Location = new Point(20, 15), AutoSize = true, Font = new Font(Font, FontStyle.Bold) };
            cboJob = new ComboBox { Location = new Point(20, 38), Width = 400, DropDownStyle = ComboBoxStyle.DropDownList };
            cboJob.SelectedIndexChanged += async (s, e) => await LoadBackupSetsForSelectedJobAsync();

            var lblSets = new Label { Text = "2. Chọn mốc thời gian muốn khôi phục:", Location = new Point(20, 75), AutoSize = true, Font = new Font(Font, FontStyle.Bold) };
            lvBackupSets = new ListView
            {
                Location = new Point(20, 98),
                Size = new Size(625, 180),
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false
            };
            lvBackupSets.Columns.Add("Thời gian (UTC)", 140);
            lvBackupSets.Columns.Add("Loại", 70);
            lvBackupSets.Columns.Add("Tên bộ backup", 180);
            lvBackupSets.Columns.Add("Số file", 70);
            lvBackupSets.Columns.Add("Dung lượng", 90);
            lvBackupSets.Columns.Add("Nén/Khóa", 70);

            var lblTarget = new Label { Text = "3. Thư mục đích nhận dữ liệu khôi phục:", Location = new Point(20, 290), AutoSize = true, Font = new Font(Font, FontStyle.Bold) };
            txtTargetFolder = new TextBox { Location = new Point(20, 313), Width = 520 };
            btnBrowseTarget = new Button { Text = Strings.Browse, Location = new Point(545, 311), Size = new Size(100, 26) };
            btnBrowseTarget.Click += (s, e) =>
            {
                var folder = FolderBrowserEx.SelectFolder(this, txtTargetFolder.Text, "Chọn thư mục đích để khôi phục dữ liệu");
                if (!string.IsNullOrEmpty(folder)) txtTargetFolder.Text = folder;
            };

            chkOverwrite = new CheckBox { Text = "Ghi đè nếu file đã tồn tại", Location = new Point(20, 345), AutoSize = true, Checked = true };

            lblPassword = new Label { Text = "Mật khẩu giải mã (nếu ZIP có mật khẩu):", Location = new Point(20, 375), AutoSize = true };
            txtPassword = new TextBox { Location = new Point(270, 372), Width = 200, UseSystemPasswordChar = true };

            progressBar = new ProgressBar { Location = new Point(20, 420), Size = new Size(625, 20), Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 0, Visible = false };
            lblStatus = new Label { Location = new Point(20, 445), AutoSize = true, Text = "" };

            btnRestore = new Button { Text = Strings.Restore, Location = new Point(450, 475), Size = new Size(100, 32), BackColor = Color.FromArgb(0, 120, 215), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnCancel = new Button { Text = Strings.Close, DialogResult = DialogResult.Cancel, Location = new Point(560, 475), Size = new Size(85, 32) };

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
            lblStatus.Text = "Đang đọc danh sách các bản backup từ đích...";

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
                        item.SubItems.Add(s.BackupType.ToString());
                        item.SubItems.Add(s.BackupSetId);
                        item.SubItems.Add(s.FileCount.ToString("N0"));
                        item.SubItems.Add(FormatBytes(s.TotalSizeBytes));
                        item.SubItems.Add((s.IsCompressed ? "ZIP" : "Raw") + (s.IsEncrypted ? " [Enc]" : ""));
                        item.Tag = s;
                        lvBackupSets.Items.Add(item);
                    }
                    lblStatus.Text = $"Đã tìm thấy {_loadedSets.Count} bản backup.";
                    if (lvBackupSets.Items.Count > 0)
                        lvBackupSets.Items[0].Selected = true;
                }
                else
                {
                    lblStatus.Text = "Không đọc được danh sách: " + resp?.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Lỗi kết nối: " + ex.Message;
            }
        }

        private async System.Threading.Tasks.Task ExecuteRestoreAsync()
        {
            if (lvBackupSets.SelectedItems.Count == 0)
            {
                MessageBox.Show(this, "Vui lòng chọn một mốc backup cần khôi phục.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtTargetFolder.Text))
            {
                MessageBox.Show(this, "Vui lòng chọn thư mục đích nhận dữ liệu khôi phục.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtTargetFolder.Focus();
                return;
            }

            var selectedSet = (BackupSetManifest)lvBackupSets.SelectedItems[0].Tag;
            var job = _jobs[cboJob.SelectedIndex];

            var confirmMsg = $"Bạn có chắc muốn khôi phục dữ liệu về mốc:\n{selectedSet.BackupSetId}\n\nVào thư mục:\n{txtTargetFolder.Text}?";
            if (MessageBox.Show(this, confirmMsg, "Xác nhận khôi phục", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            btnRestore.Enabled = false;
            btnCancel.Enabled = false;
            progressBar.Visible = true;
            progressBar.MarqueeAnimationSpeed = 30;
            lblStatus.Text = "Đang tiến hành khôi phục dữ liệu (tải và ghép chuỗi Full + Incremental)...";

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
                        MessageBox.Show(this, result.Message, "Khôi phục thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        Close();
                    }
                    else
                    {
                        MessageBox.Show(this, "Khôi phục thất bại: " + result?.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else
                {
                    MessageBox.Show(this, "Lỗi từ service: " + resp?.ErrorMessage, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
