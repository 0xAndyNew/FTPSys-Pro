using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using FtpSysPro.Core.Resources;

namespace FtpSysPro.Manager.UI
{
    public class SubFolderPickerDialog : Form
    {
        private readonly string _backupSetId;
        private readonly List<string> _allFolders;
        private TreeView treeFolders;
        private TextBox txtSearch;
        private Label lblSelected;
        private Button btnOk;
        private Button btnCancel;
        private Button btnSelectAll;

        public string SelectedSubFolder { get; private set; } = string.Empty;

        public SubFolderPickerDialog(string backupSetId, List<string> folders, string currentSelected = null)
        {
            _backupSetId = backupSetId ?? string.Empty;
            _allFolders = folders ?? new List<string>();
            SelectedSubFolder = currentSelected ?? string.Empty;

            InitializeComponents();
            BuildTree(string.Empty);

            if (!string.IsNullOrEmpty(currentSelected))
            {
                SelectNodeByPath(currentSelected);
            }
        }

        private void InitializeComponents()
        {
            Text = Strings.SelectSubFolderTitle;
            ClientSize = new Size(520, 560);
            MinimumSize = new Size(420, 400);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = new Font("Segoe UI", 9F);
            BackColor = Color.FromArgb(248, 250, 252);

            // Top Panel
            var pnlTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 85,
                Padding = new Padding(15, 12, 15, 8),
                BackColor = Color.White
            };

            var lblHeader = new Label
            {
                Text = Strings.IsVi ? "Chọn thư mục con cần phục hồi:" : "Select specific subfolder to restore:",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42),
                Location = new Point(15, 10),
                AutoSize = true
            };

            var lblSetInfo = new Label
            {
                Text = (Strings.IsVi ? "Bản sao lưu: " : "Backup Set: ") + _backupSetId,
                Font = new Font("Segoe UI", 8.25F),
                ForeColor = Color.FromArgb(100, 116, 139),
                Location = new Point(15, 33),
                AutoSize = true
            };

            txtSearch = new TextBox
            {
                Location = new Point(15, 54),
                Width = 490,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                ForeColor = Color.Gray,
                Text = Strings.IsVi ? "🔍 Tìm kiếm thư mục..." : "🔍 Search folder..."
            };
            txtSearch.GotFocus += (s, e) =>
            {
                if (txtSearch.Text.StartsWith("🔍"))
                {
                    txtSearch.Text = "";
                    txtSearch.ForeColor = Color.Black;
                }
            };
            txtSearch.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtSearch.Text))
                {
                    txtSearch.Text = Strings.IsVi ? "🔍 Tìm kiếm thư mục..." : "🔍 Search folder...";
                    txtSearch.ForeColor = Color.Gray;
                }
            };
            txtSearch.TextChanged += (s, e) =>
            {
                if (!txtSearch.Text.StartsWith("🔍"))
                {
                    BuildTree(txtSearch.Text.Trim());
                }
            };

            pnlTop.Controls.AddRange(new Control[] { lblHeader, lblSetInfo, txtSearch });

            // Bottom Panel
            var pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 80,
                Padding = new Padding(15, 8, 15, 12),
                BackColor = Color.White
            };

            lblSelected = new Label
            {
                Text = Strings.IsVi ? "Đang chọn: (Toàn bộ dữ liệu)" : "Selected: (All data / Root)",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Location = new Point(15, 10),
                Width = 490,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                AutoEllipsis = true
            };

            btnSelectAll = new Button
            {
                Text = Strings.IsVi ? "Khôi phục toàn bộ" : "Restore All",
                Location = new Point(15, 38),
                Size = new Size(135, 32),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                FlatStyle = FlatStyle.System
            };
            btnSelectAll.Click += (s, e) =>
            {
                SelectedSubFolder = string.Empty;
                DialogResult = DialogResult.OK;
                Close();
            };

            btnOk = new Button
            {
                Text = Strings.IsVi ? "Đồng ý" : "Select",
                DialogResult = DialogResult.OK,
                Location = new Point(310, 38),
                Size = new Size(95, 32),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font(Font, FontStyle.Bold)
            };
            btnOk.Click += (s, e) =>
            {
                if (treeFolders.SelectedNode != null)
                {
                    SelectedSubFolder = treeFolders.SelectedNode.Tag as string ?? string.Empty;
                }
                Close();
            };

            btnCancel = new Button
            {
                Text = Strings.IsVi ? "Hủy" : "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(415, 38),
                Size = new Size(90, 32),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                FlatStyle = FlatStyle.System
            };

            pnlBottom.Controls.AddRange(new Control[] { lblSelected, btnSelectAll, btnOk, btnCancel });

            // Center TreeView
            var pnlCenter = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(15, 10, 15, 10)
            };

            treeFolders = new TreeView
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9.25F),
                FullRowSelect = true,
                HideSelection = false,
                ShowLines = true,
                ShowPlusMinus = true,
                ShowRootLines = true
            };

            treeFolders.AfterSelect += (s, e) =>
            {
                var path = e.Node?.Tag as string ?? string.Empty;
                SelectedSubFolder = path;
                lblSelected.Text = string.IsNullOrEmpty(path)
                    ? (Strings.IsVi ? "Đang chọn: (Toàn bộ dữ liệu - Thư mục gốc)" : "Selected: (All data / Root)")
                    : (Strings.IsVi ? $"Đang chọn: 📁 {path}" : $"Selected: 📁 {path}");
            };

            treeFolders.NodeMouseDoubleClick += (s, e) =>
            {
                if (e.Node != null)
                {
                    SelectedSubFolder = e.Node.Tag as string ?? string.Empty;
                    DialogResult = DialogResult.OK;
                    Close();
                }
            };

            pnlCenter.Controls.Add(treeFolders);

            Controls.Add(pnlCenter);
            Controls.Add(pnlTop);
            Controls.Add(pnlBottom);
        }

        private void BuildTree(string filter)
        {
            treeFolders.BeginUpdate();
            treeFolders.Nodes.Clear();

            var rootNode = new TreeNode(Strings.IsVi ? "📦 (Toàn bộ dữ liệu / Thư mục gốc)" : "📦 (All data / Root)")
            {
                Tag = string.Empty,
                NodeFont = new Font(treeFolders.Font, FontStyle.Bold)
            };
            treeFolders.Nodes.Add(rootNode);

            var foldersToDisplay = string.IsNullOrEmpty(filter)
                ? _allFolders
                : _allFolders.Where(f => f.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            var nodeMap = new Dictionary<string, TreeNode>(StringComparer.OrdinalIgnoreCase)
            {
                [string.Empty] = rootNode
            };

            foreach (var folder in foldersToDisplay.OrderBy(x => x))
            {
                var parts = folder.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
                string currentPath = "";
                TreeNode parentNode = rootNode;

                for (int i = 0; i < parts.Length; i++)
                {
                    currentPath = (i == 0) ? parts[0] : currentPath + "\\" + parts[i];
                    if (!nodeMap.TryGetValue(currentPath, out var node))
                    {
                        node = new TreeNode("📁 " + parts[i])
                        {
                            Tag = currentPath
                        };
                        parentNode.Nodes.Add(node);
                        nodeMap[currentPath] = node;
                    }
                    parentNode = node;
                }
            }

            rootNode.Expand();
            foreach (TreeNode child in rootNode.Nodes)
            {
                child.Expand();
            }

            if (treeFolders.Nodes.Count > 0)
            {
                treeFolders.SelectedNode = rootNode;
            }

            treeFolders.EndUpdate();
        }

        private void SelectNodeByPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            var norm = path.Replace('/', '\\').Trim('\\');

            TreeNode FindNode(TreeNode parent)
            {
                if (string.Equals(parent.Tag as string, norm, StringComparison.OrdinalIgnoreCase))
                    return parent;

                foreach (TreeNode child in parent.Nodes)
                {
                    var found = FindNode(child);
                    if (found != null) return found;
                }
                return null;
            }

            foreach (TreeNode root in treeFolders.Nodes)
            {
                var target = FindNode(root);
                if (target != null)
                {
                    treeFolders.SelectedNode = target;
                    target.EnsureVisible();
                    break;
                }
            }
        }
    }
}
