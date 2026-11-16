using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using FtpSysPro.Core.Models;
using FtpSysPro.Core.Resources;

namespace FtpSysPro.Manager.UI
{
    public class SubFolderPickerDialog : Form
    {
        private readonly string _backupSetId;
        private readonly List<string> _allFolders;
        private readonly Dictionary<string, SubFolderStats> _folderStats;
        private TreeView treeFolders;
        private TextBox txtSearch;
        private Label lblSelected;
        private Button btnOk;
        private Button btnCancel;
        private Button btnSelectAll;
        private Button btnClearAll;

        private bool _isUpdatingChecks = false;

        public List<string> SelectedSubFolders { get; private set; } = new List<string>();
        public string SelectedSubFolder => SelectedSubFolders.Count > 0 ? string.Join(Environment.NewLine, SelectedSubFolders) : string.Empty;

        public SubFolderPickerDialog(string backupSetId, List<string> folders, string currentSelected = null, Dictionary<string, SubFolderStats> folderStats = null)
        {
            _backupSetId = backupSetId ?? string.Empty;
            _allFolders = folders ?? new List<string>();
            _folderStats = folderStats ?? new Dictionary<string, SubFolderStats>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(currentSelected))
            {
                var parts = currentSelected.Split(new[] { '\r', '\n', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
                SelectedSubFolders = parts.Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }

            InitializeComponents();
            BuildTree(string.Empty);

            if (SelectedSubFolders.Count > 0)
            {
                CheckNodesByPaths(SelectedSubFolders);
            }
            else
            {
                UpdateSelectionDisplay();
            }
        }

        private void InitializeComponents()
        {
            Text = Strings.SelectSubFolderTitle;
            ClientSize = new Size(540, 570);
            MinimumSize = new Size(460, 420);
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
                Text = Strings.IsVi ? "Chọn một hoặc nhiều thư mục con cần phục hồi (Tích chọn ☑):" : "Select one or more subfolders to restore (Check ☑):",
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
                Width = 510,
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
                    if (SelectedSubFolders.Count > 0)
                    {
                        CheckNodesByPaths(SelectedSubFolders);
                    }
                }
            };

            pnlTop.Controls.AddRange(new Control[] { lblHeader, lblSetInfo, txtSearch });

            // Bottom Panel
            var pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 85,
                Padding = new Padding(15, 8, 15, 12),
                BackColor = Color.White
            };

            lblSelected = new Label
            {
                Text = Strings.IsVi ? "Đang chọn: 📦 (Toàn bộ dữ liệu / Thư mục gốc)" : "Selected: 📦 (All data / Root)",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Location = new Point(15, 10),
                Height = 22,
                AutoEllipsis = true
            };

            btnSelectAll = new Button
            {
                Text = Strings.SelectAllFolders,
                Size = new Size(115, 34),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            btnSelectAll.Click += (s, e) =>
            {
                if (treeFolders.Nodes.Count > 0)
                {
                    _isUpdatingChecks = true;
                    try
                    {
                        treeFolders.Nodes[0].Checked = true;
                        CheckChildNodes(treeFolders.Nodes[0], true);
                    }
                    finally
                    {
                        _isUpdatingChecks = false;
                    }
                    UpdateSelectionDisplay();
                }
            };

            btnClearAll = new Button
            {
                Text = Strings.ClearAllSelection,
                Size = new Size(130, 34),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            btnClearAll.Click += (s, e) =>
            {
                if (treeFolders.Nodes.Count > 0)
                {
                    _isUpdatingChecks = true;
                    try
                    {
                        treeFolders.Nodes[0].Checked = false;
                        CheckChildNodes(treeFolders.Nodes[0], false);
                    }
                    finally
                    {
                        _isUpdatingChecks = false;
                    }
                    UpdateSelectionDisplay();
                }
            };

            btnOk = new Button
            {
                Text = "✓ OK",
                Size = new Size(100, 34),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Click += (s, e) =>
            {
                SelectedSubFolders = GetSelectedFolderPaths();

                // If no checkboxes are ticked, fallback to highlighted node if one is selected
                if (SelectedSubFolders.Count == 0 && (treeFolders.Nodes.Count == 0 || !treeFolders.Nodes[0].Checked))
                {
                    if (treeFolders.SelectedNode != null && treeFolders.SelectedNode != treeFolders.Nodes[0])
                    {
                        var tag = treeFolders.SelectedNode.Tag as string;
                        if (!string.IsNullOrEmpty(tag))
                        {
                            SelectedSubFolders.Add(tag);
                        }
                    }
                }

                if (SelectedSubFolders.Count == 0)
                {
                    var confirmRoot = MessageBox.Show(this,
                        Strings.IsVi
                            ? "Bạn chưa tích chọn thư mục con cụ thể nào. Hệ thống sẽ khôi phục toàn bộ bản sao lưu.\n\nNhấn 'Có' (Yes) để khôi phục toàn bộ dữ liệu.\nNhấn 'Không' (No) để quay lại tích chọn các thư mục con."
                            : "No specific subfolders are selected. System will restore the entire backup set.\n\nClick 'Yes' to restore all data.\nClick 'No' to go back and select subfolders.",
                        Strings.IsVi ? "Xác nhận phạm vi phục hồi" : "Confirm Restore Scope",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (confirmRoot != DialogResult.Yes)
                    {
                        return;
                    }
                }

                DialogResult = DialogResult.OK;
                Close();
            };

            btnCancel = new Button
            {
                Text = Strings.IsVi ? "Hủy" : "Cancel",
                DialogResult = DialogResult.Cancel,
                Size = new Size(90, 34),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };

            void RepositionBottomControls()
            {
                int w = pnlBottom.ClientSize.Width;
                if (w <= 0) return;
                lblSelected.Width = Math.Max(100, w - 30);
                int rightPad = 15;
                btnCancel.Location = new Point(w - btnCancel.Width - rightPad, 38);
                btnOk.Location = new Point(btnCancel.Left - btnOk.Width - 10, 38);
                btnSelectAll.Location = new Point(15, 38);
                btnClearAll.Location = new Point(btnSelectAll.Right + 8, 38);
            }

            pnlBottom.Resize += (s, e) => RepositionBottomControls();
            pnlBottom.Layout += (s, e) => RepositionBottomControls();
            Shown += (s, e) => RepositionBottomControls();

            pnlBottom.Controls.AddRange(new Control[] { lblSelected, btnSelectAll, btnClearAll, btnOk, btnCancel });

            AcceptButton = btnOk;
            CancelButton = btnCancel;

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
                CheckBoxes = true,
                FullRowSelect = true,
                HideSelection = false,
                ShowLines = true,
                ShowPlusMinus = true,
                ShowRootLines = true
            };

            treeFolders.AfterCheck += TreeFolders_AfterCheck;

            treeFolders.AfterSelect += (s, e) =>
            {
                if (SelectedSubFolders.Count == 0 && !treeFolders.Nodes[0].Checked)
                {
                    var path = e.Node?.Tag as string ?? string.Empty;
                    lblSelected.Text = string.IsNullOrEmpty(path)
                        ? (Strings.IsVi ? "Đang chọn: 📦 (Toàn bộ dữ liệu / Thư mục gốc)" : "Selected: 📦 (All data / Root)")
                        : (Strings.IsVi ? $"Đang chọn: 📁 {path}" : $"Selected: 📁 {path}");
                }
            };

            treeFolders.NodeMouseDoubleClick += (s, e) =>
            {
                if (e.Node != null && e.Node != treeFolders.Nodes[0])
                {
                    e.Node.Checked = !e.Node.Checked;
                }
            };

            treeFolders.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Delete && treeFolders.SelectedNode != null)
                {
                    _isUpdatingChecks = true;
                    try
                    {
                        treeFolders.SelectedNode.Checked = false;
                        CheckChildNodes(treeFolders.SelectedNode, false);
                        UpdateParentNodes(treeFolders.SelectedNode);
                    }
                    finally
                    {
                        _isUpdatingChecks = false;
                    }
                    UpdateSelectionDisplay();
                    e.Handled = true;
                }
            };

            var treeMenu = new ContextMenuStrip();
            var mnuCheck = new ToolStripMenuItem(Strings.IsVi ? "☑ Chọn mục này" : "☑ Select this item");
            var mnuUncheck = new ToolStripMenuItem(Strings.IsVi ? "☐ Bỏ chọn mục này" : "☐ Deselect this item");
            var mnuCheckAllChildren = new ToolStripMenuItem(Strings.IsVi ? "☑ Chọn tất cả mục con" : "☑ Select all sub-items");
            var mnuUncheckAllChildren = new ToolStripMenuItem(Strings.IsVi ? "☐ Bỏ chọn tất cả mục con" : "☐ Deselect all sub-items");

            mnuCheck.Click += (s, e) =>
            {
                if (treeFolders.SelectedNode != null)
                {
                    _isUpdatingChecks = true;
                    try
                    {
                        treeFolders.SelectedNode.Checked = true;
                        CheckChildNodes(treeFolders.SelectedNode, true);
                        UpdateParentNodes(treeFolders.SelectedNode);
                    }
                    finally { _isUpdatingChecks = false; }
                    UpdateSelectionDisplay();
                }
            };

            mnuUncheck.Click += (s, e) =>
            {
                if (treeFolders.SelectedNode != null)
                {
                    _isUpdatingChecks = true;
                    try
                    {
                        treeFolders.SelectedNode.Checked = false;
                        CheckChildNodes(treeFolders.SelectedNode, false);
                        UpdateParentNodes(treeFolders.SelectedNode);
                    }
                    finally { _isUpdatingChecks = false; }
                    UpdateSelectionDisplay();
                }
            };

            mnuCheckAllChildren.Click += (s, e) =>
            {
                if (treeFolders.SelectedNode != null)
                {
                    _isUpdatingChecks = true;
                    try
                    {
                        treeFolders.SelectedNode.Checked = true;
                        CheckChildNodes(treeFolders.SelectedNode, true);
                        UpdateParentNodes(treeFolders.SelectedNode);
                    }
                    finally { _isUpdatingChecks = false; }
                    UpdateSelectionDisplay();
                }
            };

            mnuUncheckAllChildren.Click += (s, e) =>
            {
                if (treeFolders.SelectedNode != null)
                {
                    _isUpdatingChecks = true;
                    try
                    {
                        CheckChildNodes(treeFolders.SelectedNode, false);
                        UpdateParentNodes(treeFolders.SelectedNode);
                    }
                    finally { _isUpdatingChecks = false; }
                    UpdateSelectionDisplay();
                }
            };

            treeMenu.Items.AddRange(new ToolStripItem[] { mnuCheck, mnuUncheck, new ToolStripSeparator(), mnuCheckAllChildren, mnuUncheckAllChildren });
            treeFolders.ContextMenuStrip = treeMenu;
            treeFolders.NodeMouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    treeFolders.SelectedNode = e.Node;
                }
            };

            pnlCenter.Controls.Add(treeFolders);

            Controls.Add(pnlCenter);
            Controls.Add(pnlTop);
            Controls.Add(pnlBottom);
        }

        private void TreeFolders_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (_isUpdatingChecks) return;
            _isUpdatingChecks = true;
            try
            {
                // Cascade check state down to all child nodes
                CheckChildNodes(e.Node, e.Node.Checked);

                // Cascade check state up to parent nodes
                UpdateParentNodes(e.Node);
            }
            finally
            {
                _isUpdatingChecks = false;
            }

            UpdateSelectionDisplay();
        }

        private void CheckChildNodes(TreeNode node, bool isChecked)
        {
            foreach (TreeNode child in node.Nodes)
            {
                child.Checked = isChecked;
                CheckChildNodes(child, isChecked);
            }
        }

        private void UpdateParentNodes(TreeNode node)
        {
            var parent = node.Parent;
            if (parent == null) return;

            bool allChecked = true;
            foreach (TreeNode sibling in parent.Nodes)
            {
                if (!sibling.Checked)
                {
                    allChecked = false;
                    break;
                }
            }

            if (parent.Checked != allChecked)
            {
                parent.Checked = allChecked;
                UpdateParentNodes(parent);
            }
        }

        public List<string> GetSelectedFolderPaths()
        {
            var list = new List<string>();
            if (treeFolders.Nodes.Count == 0) return list;

            var root = treeFolders.Nodes[0];

            void CollectCheckedRoots(TreeNode node)
            {
                foreach (TreeNode child in node.Nodes)
                {
                    if (child.Checked)
                    {
                        var tag = child.Tag as string;
                        if (!string.IsNullOrEmpty(tag))
                        {
                            list.Add(tag);
                        }
                    }
                    else
                    {
                        CollectCheckedRoots(child);
                    }
                }
            }

            CollectCheckedRoots(root);

            if (list.Count == 0 && root.Checked)
            {
                // If root was checked, collect all immediate top-level folders
                foreach (TreeNode child in root.Nodes)
                {
                    var tag = child.Tag as string;
                    if (!string.IsNullOrEmpty(tag))
                    {
                        list.Add(tag);
                    }
                }
            }

            return list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private void UpdateSelectionDisplay()
        {
            var paths = GetSelectedFolderPaths();
            SelectedSubFolders = paths;

            if (treeFolders.Nodes.Count > 0 && treeFolders.Nodes[0].Checked)
            {
                lblSelected.Text = Strings.IsVi
                    ? "Đang chọn: 📦 (Toàn bộ dữ liệu / Thư mục gốc)"
                    : "Selected: 📦 (All data / Root)";
            }
            else if (paths.Count == 0)
            {
                lblSelected.Text = Strings.IsVi
                    ? "Đang chọn: 📦 (Toàn bộ dữ liệu / Thư mục gốc)"
                    : "Selected: 📦 (All data / Root)";
            }
            else if (paths.Count == 1)
            {
                string sizeStr = (_folderStats != null && _folderStats.TryGetValue(paths[0], out var st) && (st.SizeBytes > 0 || st.FileCount > 0))
                    ? $" - {st.GetSizeDisplay(Strings.IsVi)}"
                    : "";
                lblSelected.Text = Strings.IsVi
                    ? $"Đang chọn (1 thư mục): 📁 {paths[0]}{sizeStr}"
                    : $"Selected (1 folder): 📁 {paths[0]}{sizeStr}";
            }
            else
            {
                long totalSize = 0;
                int totalFiles = 0;
                if (_folderStats != null)
                {
                    foreach (var p in paths)
                    {
                        if (_folderStats.TryGetValue(p, out var st))
                        {
                            totalSize += st.SizeBytes;
                            totalFiles += st.FileCount;
                        }
                    }
                }

                string sizeSummary = (totalSize > 0 || totalFiles > 0)
                    ? $" [{FormatSize(totalSize)} ({totalFiles:N0} {(Strings.IsVi ? "tệp" : "files")})]"
                    : "";

                lblSelected.Text = Strings.IsVi
                    ? $"Đang chọn: {paths.Count} thư mục con{sizeSummary} (bấm OK để xác nhận)"
                    : $"Selected: {paths.Count} subfolders{sizeSummary} (click OK to confirm)";
            }
        }

        private static string FormatSize(long bytes)
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

        private void BuildTree(string filter)
        {
            treeFolders.BeginUpdate();
            treeFolders.Nodes.Clear();

            string rootLabel = Strings.IsVi ? "📦 (Toàn bộ dữ liệu / Thư mục gốc)" : "📦 (All data / Root)";
            if (_folderStats != null && _folderStats.TryGetValue(string.Empty, out var rootSt) && (rootSt.SizeBytes > 0 || rootSt.FileCount > 0))
            {
                rootLabel += $"  [{rootSt.GetSizeDisplay(Strings.IsVi)}]";
            }

            var rootNode = new TreeNode(rootLabel)
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
                        string label = "📁 " + parts[i];
                        if (_folderStats != null && _folderStats.TryGetValue(currentPath, out var st) && (st.SizeBytes > 0 || st.FileCount > 0))
                        {
                            label += $"  [{st.GetSizeDisplay(Strings.IsVi)}]";
                        }

                        node = new TreeNode(label)
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

        private void CheckNodesByPaths(IEnumerable<string> paths)
        {
            if (paths == null) return;
            var set = new HashSet<string>(paths.Select(p => p.Replace('/', '\\').Trim('\\')), StringComparer.OrdinalIgnoreCase);
            if (set.Count == 0) return;

            _isUpdatingChecks = true;
            try
            {
                void CheckRecursive(TreeNode parent)
                {
                    foreach (TreeNode node in parent.Nodes)
                    {
                        var tag = node.Tag as string;
                        if (!string.IsNullOrEmpty(tag) && set.Contains(tag.Trim('\\')))
                        {
                            node.Checked = true;
                            node.EnsureVisible();
                            CheckChildNodes(node, true);
                        }
                        else
                        {
                            CheckRecursive(node);
                        }
                    }
                }

                foreach (TreeNode root in treeFolders.Nodes)
                {
                    CheckRecursive(root);
                }
            }
            finally
            {
                _isUpdatingChecks = false;
            }

            UpdateSelectionDisplay();
        }
    }
}
