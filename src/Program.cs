using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FolderFree
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length > 0 && string.Equals(args[0], "--self-test", StringComparison.OrdinalIgnoreCase))
            {
                Environment.Exit(SelfTest.Run());
                return;
            }

            if (args.Length > 1 && string.Equals(args[0], "--hold-lock", StringComparison.OrdinalIgnoreCase))
            {
                HoldLock(args[1]);
                return;
            }

            if (args.Length > 1 && string.Equals(args[0], "--hold-cwd", StringComparison.OrdinalIgnoreCase))
            {
                Directory.SetCurrentDirectory(args[1]);
                Thread.Sleep(15000);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        private static void HoldLock(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Thread.Sleep(15000);
            }
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly TextBox folderTextBox = new TextBox();
        private readonly ModernButton browseButton = new ModernButton();
        private readonly ModernButton scanButton = new ModernButton();
        private readonly ModernButton freeSelectedButton = new ModernButton();
        private readonly ModernButton freeAllButton = new ModernButton();
        private readonly ModernButton adminButton = new ModernButton();
        private readonly CheckBox forceCheckBox = new CheckBox();
        private readonly Label statusLabel = new Label();
        private readonly Label adminLabel = new Label();
        private readonly ListBox selectedFoldersList = new ListBox();
        private readonly DataGridView grid = new DataGridView();
        private readonly TextBox logBox = new TextBox();
        private readonly List<LockingApp> currentApps = new List<LockingApp>();
        private HeaderPanel headerPanel;

        public MainForm()
        {
            Text = "FolderFree";
            MinimumSize = new Size(900, 680);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Page;
            Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            BuildLayout();
            Shown += (s, e) =>
            {
                ArrangeHeaderControls();
            };
            UpdateAdminState();
        }

        private void BuildLayout()
        {
            var shell = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Page,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(24),
            };
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 122));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 116));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 68));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 32));
            Controls.Add(shell);

            headerPanel = new HeaderPanel { Dock = DockStyle.Fill };
            shell.Controls.Add(headerPanel, 0, 0);

            var title = new Label
            {
                Text = "FolderFree",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 28F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = Theme.Text,
                BackColor = Color.Transparent,
                Location = new Point(26, 22)
            };
            headerPanel.Controls.Add(title);

            var subtitle = new Label
            {
                Text = "Release local folders from apps that are holding file handles.",
                AutoSize = true,
                Font = new Font("Segoe UI", 11F, FontStyle.Regular, GraphicsUnit.Point),
                ForeColor = Theme.Muted,
                BackColor = Color.Transparent,
                Location = new Point(29, 72)
            };
            headerPanel.Controls.Add(subtitle);

            adminLabel.AutoSize = true;
            adminLabel.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
            adminLabel.ForeColor = Theme.Muted;
            adminLabel.BackColor = Color.Transparent;
            headerPanel.Controls.Add(adminLabel);

            adminButton.Text = "Run as administrator";
            adminButton.Width = 178;
            adminButton.Height = 38;
            adminButton.Click += (s, e) => RelaunchAsAdmin();
            headerPanel.Controls.Add(adminButton);
            headerPanel.SizeChanged += (s, e) => ArrangeHeaderControls();

            var controlPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Page,
                Padding = new Padding(0, 12, 0, 8),
                ColumnCount = 7,
                RowCount = 2
            };
            controlPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            controlPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 142F));
            controlPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 10F));
            controlPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 126F));
            controlPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
            controlPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 10F));
            controlPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 106F));
            controlPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            controlPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            shell.Controls.Add(controlPanel, 0, 1);

            folderTextBox.BorderStyle = BorderStyle.FixedSingle;
            folderTextBox.Font = new Font("Segoe UI", 10.5F, FontStyle.Regular, GraphicsUnit.Point);
            folderTextBox.ForeColor = Theme.Text;
            folderTextBox.BackColor = Color.White;
            folderTextBox.Dock = DockStyle.Fill;
            folderTextBox.Margin = new Padding(0, 5, 10, 7);
            controlPanel.Controls.Add(folderTextBox, 0, 0);

            browseButton.Text = "Select folder";
            browseButton.Dock = DockStyle.Fill;
            browseButton.Margin = new Padding(0, 0, 0, 8);
            browseButton.Click += (s, e) => SelectFolder();
            controlPanel.Controls.Add(browseButton, 1, 0);

            scanButton.Text = "Scan locks";
            scanButton.Dock = DockStyle.Fill;
            scanButton.Margin = new Padding(0, 0, 0, 8);
            scanButton.Click += async (s, e) => await ScanAsync();
            controlPanel.Controls.Add(scanButton, 3, 0);

            freeSelectedButton.Text = "Free selected";
            freeSelectedButton.Dock = DockStyle.Fill;
            freeSelectedButton.Margin = new Padding(0, 0, 0, 6);
            freeSelectedButton.Click += async (s, e) => await FreeSelectedAsync(false);
            controlPanel.Controls.Add(freeSelectedButton, 4, 1);

            freeAllButton.Text = "Free all";
            freeAllButton.Dock = DockStyle.Fill;
            freeAllButton.Margin = new Padding(0, 0, 0, 6);
            freeAllButton.Click += async (s, e) => await FreeSelectedAsync(true);
            controlPanel.Controls.Add(freeAllButton, 6, 1);

            forceCheckBox.Text = "Force close if needed";
            forceCheckBox.Checked = true;
            forceCheckBox.AutoSize = true;
            forceCheckBox.ForeColor = Theme.Text;
            forceCheckBox.BackColor = Theme.Page;
            forceCheckBox.Dock = DockStyle.Left;
            forceCheckBox.Margin = new Padding(0, 4, 0, 0);
            controlPanel.Controls.Add(forceCheckBox, 0, 1);

            statusLabel.AutoSize = false;
            statusLabel.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold, GraphicsUnit.Point);
            statusLabel.ForeColor = Theme.Muted;
            statusLabel.Text = "Choose a folder to begin.";
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.Margin = new Padding(0, 6, 10, 0);
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            controlPanel.Controls.Add(statusLabel, 1, 1);
            controlPanel.SetColumnSpan(statusLabel, 3);

            ConfigureSelectedFolders();
            shell.Controls.Add(CreateSelectedFolderPanel(), 0, 2);

            ConfigureGrid();
            shell.Controls.Add(grid, 0, 3);

            logBox.Dock = DockStyle.Fill;
            logBox.Multiline = true;
            logBox.ReadOnly = true;
            logBox.ScrollBars = ScrollBars.Vertical;
            logBox.BorderStyle = BorderStyle.FixedSingle;
            logBox.BackColor = Color.White;
            logBox.ForeColor = Theme.Text;
            logBox.Font = new Font("Cascadia Mono", 9F, FontStyle.Regular, GraphicsUnit.Point);
            shell.Controls.Add(logBox, 0, 4);
        }

        private void ArrangeHeaderControls()
        {
            if (headerPanel == null) return;

            int right = Math.Max(280, headerPanel.Width - 30);
            adminLabel.Location = new Point(Math.Max(30, right - adminLabel.PreferredWidth), 22);
            adminButton.Location = new Point(Math.Max(30, right - adminButton.Width), 58);
        }

        private void ConfigureGrid()
        {
            grid.Dock = DockStyle.Fill;
            grid.BackgroundColor = Color.White;
            grid.BorderStyle = BorderStyle.None;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.GridColor = Theme.Border;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Theme.Panel;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Theme.Text;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold, GraphicsUnit.Point);
            grid.ColumnHeadersHeight = 42;
            grid.RowTemplate.Height = 38;
            grid.DefaultCellStyle.BackColor = Color.White;
            grid.DefaultCellStyle.ForeColor = Theme.Text;
            grid.DefaultCellStyle.SelectionBackColor = Theme.Selected;
            grid.DefaultCellStyle.SelectionForeColor = Theme.Text;

            grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Select", HeaderText = "", Width = 46, FillWeight = 12 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "App", HeaderText = "App", FillWeight = 32 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Pid", HeaderText = "PID", FillWeight = 16 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Locks", HeaderText = "Locks", FillWeight = 16 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Path", HeaderText = "Executable / sample", FillWeight = 78 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", FillWeight = 30 });
        }

        private Control CreateSelectedFolderPanel()
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Page,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(0, 2, 0, 8)
            };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var label = new Label
            {
                Text = "Selected folders",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = Theme.Muted,
                TextAlign = ContentAlignment.MiddleLeft
            };

            panel.Controls.Add(label, 0, 0);
            panel.Controls.Add(selectedFoldersList, 0, 1);
            return panel;
        }

        private void ConfigureSelectedFolders()
        {
            selectedFoldersList.Dock = DockStyle.Fill;
            selectedFoldersList.BorderStyle = BorderStyle.FixedSingle;
            selectedFoldersList.BackColor = Color.White;
            selectedFoldersList.ForeColor = Theme.Text;
            selectedFoldersList.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
            selectedFoldersList.HorizontalScrollbar = true;
            selectedFoldersList.IntegralHeight = false;
            selectedFoldersList.SelectedIndexChanged += (s, e) =>
            {
                if (selectedFoldersList.SelectedItem != null)
                {
                    folderTextBox.Text = selectedFoldersList.SelectedItem.ToString();
                }
            };
        }

        private void UpdateAdminState()
        {
            bool admin = IsAdministrator();
            adminLabel.Text = admin ? "Administrator mode" : "Standard mode";
            adminLabel.ForeColor = admin ? Theme.Success : Theme.Muted;
            adminButton.Visible = !admin;
            ArrangeHeaderControls();
        }

        private static bool IsAdministrator()
        {
            using (var identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        private void SelectFolder()
        {
            string selected = ModernFolderPicker.Show(this.Handle, Directory.Exists(folderTextBox.Text) ? folderTextBox.Text : null);
            if (!string.IsNullOrWhiteSpace(selected))
            {
                logBox.Clear();
                folderTextBox.Text = selected;
                AddSelectedFolder(selected);
                _ = ScanAsync();
            }
        }

        private void AddSelectedFolder(string folder)
        {
            for (int i = selectedFoldersList.Items.Count - 1; i >= 0; i--)
            {
                if (string.Equals(selectedFoldersList.Items[i].ToString(), folder, StringComparison.OrdinalIgnoreCase))
                {
                    selectedFoldersList.Items.RemoveAt(i);
                }
            }

            selectedFoldersList.Items.Insert(0, folder);
            selectedFoldersList.SelectedIndex = 0;
        }

        private async Task ScanAsync()
        {
            string folder = folderTextBox.Text.Trim();
            if (!Directory.Exists(folder))
            {
                MessageBox.Show(this, "Select an existing folder first.", "FolderFree", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SetBusy(true, "Scanning folder locks...");
            grid.Rows.Clear();
            currentApps.Clear();
            AddSelectedFolder(folder);
            Log("Scanning: " + folder);

            try
            {
                var progress = new Progress<string>(message => statusLabel.Text = message);
                var apps = await Task.Run(() => FolderScanner.Scan(folder, progress));
                currentApps.AddRange(apps);
                PopulateGrid();

                statusLabel.Text = apps.Count == 0
                    ? "No locking apps found. The folder should be free."
                    : apps.Count + " locking app" + (apps.Count == 1 ? "" : "s") + " found.";
                Log(statusLabel.Text);
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Scan failed.";
                Log("Scan failed: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Scan failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false, null);
            }
        }

        private void PopulateGrid()
        {
            grid.Rows.Clear();
            foreach (var app in currentApps.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase))
            {
                int rowIndex = grid.Rows.Add(true, app.Name, app.Pid, app.LockCount, app.DisplayPath, app.ReasonSummary);
                grid.Rows[rowIndex].Tag = app;
            }
        }

        private async Task FreeSelectedAsync(bool all)
        {
            var targets = new List<LockingApp>();
            foreach (DataGridViewRow row in grid.Rows)
            {
                var app = row.Tag as LockingApp;
                if (app == null) continue;

                bool selected = all || Convert.ToBoolean(row.Cells["Select"].Value ?? false);
                if (selected) targets.Add(app);
            }

            if (targets.Count == 0)
            {
                MessageBox.Show(this, "Select at least one app to free.", "FolderFree", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string message = "FolderFree will close " + targets.Count + " app" + (targets.Count == 1 ? "" : "s") + ". Unsaved work in those apps can be lost.";
            if (MessageBox.Show(this, message, "Free folder", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
            {
                return;
            }

            SetBusy(true, "Freeing selected apps...");
            Log("Freeing " + targets.Count + " app(s).");

            try
            {
                var force = forceCheckBox.Checked;
                var results = await Task.Run(() => ProcessReleaser.Release(targets, force));
                ApplyReleaseResults(results);
                await ScanAsync();
            }
            finally
            {
                SetBusy(false, null);
            }
        }

        private void ApplyReleaseResults(IEnumerable<ReleaseResult> results)
        {
            foreach (var result in results)
            {
                Log(result.App.Name + " [" + result.App.Pid + "]: " + result.Message);
                foreach (DataGridViewRow row in grid.Rows)
                {
                    var rowApp = row.Tag as LockingApp;
                    if (rowApp != null && rowApp.Pid == result.App.Pid)
                    {
                        row.Cells["Status"].Value = result.Message;
                        break;
                    }
                }
            }
        }

        private void SetBusy(bool busy, string message)
        {
            browseButton.Enabled = !busy;
            scanButton.Enabled = !busy;
            freeSelectedButton.Enabled = !busy;
            freeAllButton.Enabled = !busy;
            folderTextBox.Enabled = !busy;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
            if (!string.IsNullOrEmpty(message)) statusLabel.Text = message;
        }

        private void RelaunchAsAdmin()
        {
            try
            {
                var info = new ProcessStartInfo(Application.ExecutablePath)
                {
                    Verb = "runas",
                    UseShellExecute = true
                };
                Process.Start(info);
                Close();
            }
            catch (Win32Exception)
            {
                Log("Administrator relaunch was cancelled.");
            }
        }

        private void Log(string message)
        {
            logBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message + Environment.NewLine);
        }
    }

    internal static class Theme
    {
        public static readonly Color Page = Color.FromArgb(247, 250, 252);
        public static readonly Color Panel = Color.FromArgb(238, 246, 249);
        public static readonly Color Border = Color.FromArgb(217, 226, 232);
        public static readonly Color Text = Color.FromArgb(20, 33, 45);
        public static readonly Color Muted = Color.FromArgb(91, 108, 122);
        public static readonly Color Accent = Color.FromArgb(22, 126, 224);
        public static readonly Color AccentDark = Color.FromArgb(18, 92, 166);
        public static readonly Color Success = Color.FromArgb(13, 139, 102);
        public static readonly Color Selected = Color.FromArgb(223, 240, 255);
    }

    internal sealed class HeaderPanel : Panel
    {
        public HeaderPanel()
        {
            DoubleBuffered = true;
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var brush = new LinearGradientBrush(ClientRectangle, Color.White, Theme.Panel, 12F))
            {
                using (var path = RoundedRect(new RectangleF(0, 0, Width - 1, Height - 1), 22))
                {
                    e.Graphics.FillPath(brush, path);
                    using (var pen = new Pen(Theme.Border))
                    {
                        e.Graphics.DrawPath(pen, path);
                    }
                }
            }

            using (var accent = new LinearGradientBrush(new Rectangle(0, 0, 190, Height), Color.FromArgb(48, 56, 189, 248), Color.FromArgb(0, 20, 184, 166), 0F))
            {
                e.Graphics.FillEllipse(accent, Width - 250, -70, 220, 220);
            }
        }

        private static GraphicsPath RoundedRect(RectangleF rect, float radius)
        {
            float d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class ModernButton : Button
    {
        public ModernButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Theme.Accent;
            ForeColor = Color.White;
            Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold, GraphicsUnit.Point);
            Cursor = Cursors.Hand;
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = RoundedRect(new RectangleF(0, 0, Width - 1, Height - 1), 9))
            using (var brush = new SolidBrush(Enabled ? BackColor : Color.FromArgb(180, 194, 205)))
            {
                pevent.Graphics.FillPath(brush, path);
            }

            TextRenderer.DrawText(pevent.Graphics, Text, Font, ClientRectangle, ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            BackColor = Theme.AccentDark;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            BackColor = Theme.Accent;
        }

        private static GraphicsPath RoundedRect(RectangleF rect, float radius)
        {
            float d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal static class ModernFolderPicker
    {
        private const uint FOS_NOCHANGEDIR = 0x00000008;
        private const uint FOS_PICKFOLDERS = 0x00000020;
        private const uint FOS_FORCEFILESYSTEM = 0x00000040;
        private const uint FOS_PATHMUSTEXIST = 0x00000800;
        private const uint SIGDN_FILESYSPATH = 0x80058000;
        private const int ERROR_CANCELLED = unchecked((int)0x800704C7);

        public static string Show(IntPtr owner, string initialFolder)
        {
            try
            {
                var dialog = (IFileOpenDialog)new FileOpenDialog();
                uint options;
                dialog.GetOptions(out options);
                dialog.SetOptions(options | FOS_PICKFOLDERS | FOS_FORCEFILESYSTEM | FOS_PATHMUSTEXIST | FOS_NOCHANGEDIR);
                dialog.SetTitle("Select a folder to free");
                dialog.SetOkButtonLabel("Select folder");

                if (!string.IsNullOrWhiteSpace(initialFolder) && Directory.Exists(initialFolder))
                {
                    IShellItem item;
                    int itemResult = SHCreateItemFromParsingName(initialFolder, IntPtr.Zero, typeof(IShellItem).GUID, out item);
                    if (itemResult == 0)
                    {
                        dialog.SetFolder(item);
                    }
                }

                int result = dialog.Show(owner);
                if (result == ERROR_CANCELLED) return null;
                if (result != 0) Marshal.ThrowExceptionForHR(result);

                IShellItem selected;
                dialog.GetResult(out selected);
                IntPtr pathPointer;
                selected.GetDisplayName(SIGDN_FILESYSPATH, out pathPointer);
                try
                {
                    return Marshal.PtrToStringUni(pathPointer);
                }
                finally
                {
                    if (pathPointer != IntPtr.Zero)
                    {
                        Marshal.FreeCoTaskMem(pathPointer);
                    }
                }
            }
            catch
            {
                return ShowFallback(initialFolder);
            }
        }

        private static string ShowFallback(string initialFolder)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select a folder to free";
                dialog.ShowNewFolderButton = false;
                if (!string.IsNullOrWhiteSpace(initialFolder) && Directory.Exists(initialFolder))
                {
                    dialog.SelectedPath = initialFolder;
                }

                return dialog.ShowDialog() == DialogResult.OK ? dialog.SelectedPath : null;
            }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        private static extern int SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            IntPtr pbc,
            [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            out IShellItem ppv);

        [ComImport]
        [Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
        private class FileOpenDialog
        {
        }

        [ComImport]
        [Guid("42f85136-db7e-439c-85f1-e4075d135fc8")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileOpenDialog
        {
            [PreserveSig]
            int Show(IntPtr parent);
            void SetFileTypes(uint cFileTypes, IntPtr rgFilterSpec);
            void SetFileTypeIndex(uint iFileType);
            void GetFileTypeIndex(out uint piFileType);
            void Advise(IntPtr pfde, out uint pdwCookie);
            void Unadvise(uint dwCookie);
            void SetOptions(uint fos);
            void GetOptions(out uint pfos);
            void SetDefaultFolder(IShellItem psi);
            void SetFolder(IShellItem psi);
            void GetFolder(out IShellItem ppsi);
            void GetCurrentSelection(out IShellItem ppsi);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
            void GetResult(out IShellItem ppsi);
            void AddPlace(IShellItem psi, int fdap);
            void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
            void Close(int hr);
            void SetClientGuid(ref Guid guid);
            void ClearClientData();
            void SetFilter(IntPtr pFilter);
            void GetResults(out IntPtr ppenum);
            void GetSelectedItems(out IntPtr ppsai);
        }

        [ComImport]
        [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler(IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid bhid, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);
            void GetParent(out IShellItem ppsi);
            void GetDisplayName(uint sigdnName, out IntPtr ppszName);
            void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
            void Compare(IShellItem psi, uint hint, out int piOrder);
        }
    }

    internal sealed class LockingApp
    {
        public int Pid { get; set; }
        public string Name { get; set; }
        public string ExecutablePath { get; set; }
        public int LockCount { get; set; }
        public List<string> Samples { get; private set; }
        public List<string> Reasons { get; private set; }

        public LockingApp()
        {
            Samples = new List<string>();
            Reasons = new List<string>();
        }

        public string ReasonSummary
        {
            get
            {
                return Reasons.Count == 0 ? "Holding" : string.Join(", ", Reasons.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
            }
        }

        public string DisplayPath
        {
            get
            {
                if (!string.IsNullOrEmpty(ExecutablePath)) return ExecutablePath;
                if (Samples.Count > 0) return Samples[0];
                return "";
            }
        }

        public void AddReason(string reason)
        {
            if (!string.IsNullOrWhiteSpace(reason) && !Reasons.Contains(reason, StringComparer.OrdinalIgnoreCase))
            {
                Reasons.Add(reason);
            }
        }
    }

    internal static class FolderScanner
    {
        private const int BatchSize = 80;
        private const int MaxResources = 5000;

        public static List<LockingApp> Scan(string folder, IProgress<string> progress)
        {
            var resources = CollectResources(folder, progress);
            var found = new Dictionary<int, LockingApp>();
            int processed = 0;

            foreach (var batch in Batch(resources, BatchSize))
            {
                processed += batch.Count;
                if (progress != null) progress.Report("Scanning " + processed + " of " + resources.Count + " paths...");
                foreach (var process in RestartManager.FindProcesses(batch))
                {
                    LockingApp app;
                    if (!found.TryGetValue(process.Pid, out app))
                    {
                        app = process;
                        app.AddReason("File handle");
                        found.Add(app.Pid, app);
                    }

                    app.LockCount += 1;
                    foreach (string sample in batch.Take(3))
                    {
                        if (app.Samples.Count < 3 && !app.Samples.Contains(sample, StringComparer.OrdinalIgnoreCase))
                        {
                            app.Samples.Add(sample);
                        }
                    }
                }
            }

            if (progress != null) progress.Report("Checking Explorer windows...");
            foreach (var app in ExplorerFolderScanner.Find(folder))
            {
                Merge(found, app);
            }

            if (progress != null) progress.Report("Checking process working folders...");
            foreach (var app in CurrentDirectoryScanner.Find(folder))
            {
                Merge(found, app);
            }

            if (progress != null) progress.Report("Resolving process names...");
            foreach (var app in found.Values)
            {
                ResolveProcessInfo(app);
            }

            return found.Values
                .Where(app => app.Pid != Process.GetCurrentProcess().Id)
                .OrderBy(app => app.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void Merge(Dictionary<int, LockingApp> found, LockingApp detected)
        {
            if (detected == null || detected.Pid <= 0) return;

            LockingApp app;
            if (!found.TryGetValue(detected.Pid, out app))
            {
                found.Add(detected.Pid, detected);
                return;
            }

            app.LockCount += Math.Max(1, detected.LockCount);
            foreach (var sample in detected.Samples)
            {
                if (app.Samples.Count < 5 && !app.Samples.Contains(sample, StringComparer.OrdinalIgnoreCase))
                {
                    app.Samples.Add(sample);
                }
            }

            foreach (var reason in detected.Reasons)
            {
                app.AddReason(reason);
            }
        }

        private static List<string> CollectResources(string folder, IProgress<string> progress)
        {
            var resources = new List<string> { folder };
            try
            {
                foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
                {
                    resources.Add(file);
                    if (resources.Count >= MaxResources) break;
                    if (resources.Count % 250 == 0 && progress != null)
                    {
                        progress.Report("Collected " + resources.Count + " paths...");
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                if (progress != null) progress.Report("Some folders require administrator access.");
            }
            catch (PathTooLongException)
            {
                if (progress != null) progress.Report("Skipped a path that is too long for Windows Restart Manager.");
            }

            return resources.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static IEnumerable<List<string>> Batch(List<string> items, int size)
        {
            for (int i = 0; i < items.Count; i += size)
            {
                yield return items.Skip(i).Take(size).ToList();
            }
        }

        private static void ResolveProcessInfo(LockingApp app)
        {
            try
            {
                using (var process = Process.GetProcessById(app.Pid))
                {
                    app.Name = string.IsNullOrWhiteSpace(process.ProcessName) ? app.Name : process.ProcessName;
                    try
                    {
                        app.ExecutablePath = process.MainModule.FileName;
                    }
                    catch
                    {
                        app.ExecutablePath = "";
                    }
                }
            }
            catch
            {
                if (string.IsNullOrWhiteSpace(app.Name)) app.Name = "Process";
            }
        }
    }

    internal static class ExplorerFolderScanner
    {
        public static IEnumerable<LockingApp> Find(string folder)
        {
            object shell = null;
            object windows = null;

            try
            {
                Type shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType == null) yield break;

                shell = Activator.CreateInstance(shellType);
                windows = shellType.InvokeMember("Windows", System.Reflection.BindingFlags.InvokeMethod, null, shell, null);
                if (windows == null) yield break;

                int count = Convert.ToInt32(windows.GetType().InvokeMember("Count", System.Reflection.BindingFlags.GetProperty, null, windows, null));
                for (int i = 0; i < count; i++)
                {
                    object window = null;
                    try
                    {
                        window = windows.GetType().InvokeMember("Item", System.Reflection.BindingFlags.InvokeMethod, null, windows, new object[] { i });
                        if (window == null) continue;

                        object locationUrl = window.GetType().InvokeMember("LocationURL", System.Reflection.BindingFlags.GetProperty, null, window, null);
                        string path = UrlToPath(Convert.ToString(locationUrl));
                        if (!PathTools.IsSameOrInside(path, folder)) continue;

                        object hwndValue = window.GetType().InvokeMember("HWND", System.Reflection.BindingFlags.GetProperty, null, window, null);
                        int pid;
                        GetWindowThreadProcessId(new IntPtr(Convert.ToInt64(hwndValue)), out pid);
                        if (pid <= 0) continue;

                        yield return new LockingApp
                        {
                            Pid = pid,
                            Name = "explorer",
                            LockCount = 1,
                            Samples = { path },
                            Reasons = { "Explorer window" }
                        };
                    }
                    finally
                    {
                        if (window != null && Marshal.IsComObject(window)) Marshal.ReleaseComObject(window);
                    }
                }
            }
            finally
            {
                if (windows != null && Marshal.IsComObject(windows)) Marshal.ReleaseComObject(windows);
                if (shell != null && Marshal.IsComObject(shell)) Marshal.ReleaseComObject(shell);
            }
        }

        private static string UrlToPath(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return "";
            try
            {
                return new Uri(url).LocalPath;
            }
            catch
            {
                return "";
            }
        }

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);
    }

    internal static class CurrentDirectoryScanner
    {
        private const int ProcessBasicInformation = 0;
        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        private const uint PROCESS_VM_READ = 0x0010;

        public static IEnumerable<LockingApp> Find(string folder)
        {
            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    if (process.Id == Process.GetCurrentProcess().Id) continue;

                    string currentDirectory = TryGetCurrentDirectory(process.Id);
                    if (!PathTools.IsSameOrInside(currentDirectory, folder)) continue;

                    yield return new LockingApp
                    {
                        Pid = process.Id,
                        Name = process.ProcessName,
                        LockCount = 1,
                        Samples = { currentDirectory },
                        Reasons = { "Working folder" }
                    };
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        private static string TryGetCurrentDirectory(int pid)
        {
            IntPtr processHandle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_VM_READ, false, pid);
            if (processHandle == IntPtr.Zero) return "";

            try
            {
                bool wow64;
                if (IsWow64Process(processHandle, out wow64) && wow64)
                {
                    return "";
                }

                PROCESS_BASIC_INFORMATION basicInfo;
                int returnLength;
                int status = NtQueryInformationProcess(processHandle, ProcessBasicInformation, out basicInfo, Marshal.SizeOf(typeof(PROCESS_BASIC_INFORMATION)), out returnLength);
                if (status != 0 || basicInfo.PebBaseAddress == IntPtr.Zero) return "";

                IntPtr processParametersAddress = ReadIntPtr(processHandle, IntPtr.Add(basicInfo.PebBaseAddress, 0x20));
                if (processParametersAddress == IntPtr.Zero) return "";

                return ReadUnicodeString(processHandle, IntPtr.Add(processParametersAddress, 0x38));
            }
            catch
            {
                return "";
            }
            finally
            {
                CloseHandle(processHandle);
            }
        }

        private static IntPtr ReadIntPtr(IntPtr processHandle, IntPtr address)
        {
            byte[] buffer = new byte[IntPtr.Size];
            IntPtr bytesRead;
            if (!ReadProcessMemory(processHandle, address, buffer, buffer.Length, out bytesRead) || bytesRead.ToInt64() != buffer.Length)
            {
                return IntPtr.Zero;
            }

            return IntPtr.Size == 8
                ? new IntPtr(BitConverter.ToInt64(buffer, 0))
                : new IntPtr(BitConverter.ToInt32(buffer, 0));
        }

        private static string ReadUnicodeString(IntPtr processHandle, IntPtr address)
        {
            byte[] header = new byte[16];
            IntPtr bytesRead;
            if (!ReadProcessMemory(processHandle, address, header, header.Length, out bytesRead))
            {
                return "";
            }

            ushort length = BitConverter.ToUInt16(header, 0);
            if (length == 0 || length > 32766) return "";

            long bufferAddress = BitConverter.ToInt64(header, 8);
            if (bufferAddress == 0) return "";

            byte[] pathBytes = new byte[length];
            if (!ReadProcessMemory(processHandle, new IntPtr(bufferAddress), pathBytes, pathBytes.Length, out bytesRead))
            {
                return "";
            }

            return Encoding.Unicode.GetString(pathBytes).TrimEnd('\0');
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool IsWow64Process(IntPtr hProcess, out bool wow64Process);

        [DllImport("ntdll.dll")]
        private static extern int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass, out PROCESS_BASIC_INFORMATION processInformation, int processInformationLength, out int returnLength);

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_BASIC_INFORMATION
        {
            public IntPtr Reserved1;
            public IntPtr PebBaseAddress;
            public IntPtr Reserved2_0;
            public IntPtr Reserved2_1;
            public IntPtr UniqueProcessId;
            public IntPtr Reserved3;
        }
    }

    internal static class PathTools
    {
        public static bool IsSameOrInside(string path, string folder)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(folder)) return false;

            try
            {
                string normalizedPath = Normalize(path);
                string normalizedFolder = Normalize(folder);
                return string.Equals(normalizedPath, normalizedFolder, StringComparison.OrdinalIgnoreCase)
                    || normalizedPath.StartsWith(normalizedFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string Normalize(string path)
        {
            string fullPath = Path.GetFullPath(path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }

    internal static class ProcessReleaser
    {
        private static readonly HashSet<string> ProtectedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Idle", "System", "Registry", "smss", "csrss", "wininit", "services", "lsass", "fontdrvhost", "dwm"
        };

        public static List<ReleaseResult> Release(IEnumerable<LockingApp> apps, bool force)
        {
            var results = new List<ReleaseResult>();
            foreach (var app in apps)
            {
                results.Add(ReleaseOne(app, force));
            }

            return results;
        }

        private static ReleaseResult ReleaseOne(LockingApp app, bool force)
        {
            if (ProtectedNames.Contains(app.Name ?? ""))
            {
                return new ReleaseResult(app, "Skipped protected process");
            }

            try
            {
                using (var process = Process.GetProcessById(app.Pid))
                {
                    if (process.HasExited) return new ReleaseResult(app, "Already closed");

                    bool sentClose = false;
                    try
                    {
                        sentClose = process.CloseMainWindow();
                    }
                    catch
                    {
                        sentClose = false;
                    }

                    if (sentClose && process.WaitForExit(2500))
                    {
                        return new ReleaseResult(app, "Closed");
                    }

                    if (!force)
                    {
                        return new ReleaseResult(app, sentClose ? "Close requested" : "Needs force close");
                    }

                    process.Kill();
                    process.WaitForExit(3500);
                    return new ReleaseResult(app, "Terminated");
                }
            }
            catch (Win32Exception ex)
            {
                return new ReleaseResult(app, "Access denied: " + ex.NativeErrorCode);
            }
            catch (InvalidOperationException)
            {
                return new ReleaseResult(app, "Already closed");
            }
            catch (Exception ex)
            {
                return new ReleaseResult(app, ex.Message);
            }
        }
    }

    internal sealed class ReleaseResult
    {
        public LockingApp App { get; private set; }
        public string Message { get; private set; }

        public ReleaseResult(LockingApp app, string message)
        {
            App = app;
            Message = message;
        }
    }

    internal static class RestartManager
    {
        private const int RmRebootReasonNone = 0;
        private const int ErrorMoreData = 234;
        private const int CchRmSessionKey = 32;
        private const int CchRmMaxAppName = 255;
        private const int CchRmMaxSvcName = 63;

        public static IEnumerable<LockingApp> FindProcesses(List<string> resources)
        {
            uint handle;
            var key = new StringBuilder(CchRmSessionKey + 1);
            int result = RmStartSession(out handle, 0, key);
            if (result != 0) yield break;

            try
            {
                result = RmRegisterResources(handle, (uint)resources.Count, resources.ToArray(), 0, null, 0, null);
                if (result != 0) yield break;

                uint needed = 0;
                uint count = 0;
                uint rebootReasons = RmRebootReasonNone;
                result = RmGetList(handle, out needed, ref count, null, ref rebootReasons);
                if (result == ErrorMoreData)
                {
                    var infos = new RM_PROCESS_INFO[needed];
                    count = needed;
                    result = RmGetList(handle, out needed, ref count, infos, ref rebootReasons);
                    if (result == 0)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            yield return new LockingApp
                            {
                                Pid = infos[i].Process.dwProcessId,
                                Name = infos[i].strAppName,
                                LockCount = 0
                            };
                        }
                    }
                }
            }
            finally
            {
                RmEndSession(handle);
            }
        }

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        private static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, StringBuilder strSessionKey);

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        private static extern int RmRegisterResources(
            uint pSessionHandle,
            uint nFiles,
            string[] rgsFilenames,
            uint nApplications,
            [In] RM_UNIQUE_PROCESS[] rgApplications,
            uint nServices,
            string[] rgsServiceNames);

        [DllImport("rstrtmgr.dll")]
        private static extern int RmGetList(
            uint dwSessionHandle,
            out uint pnProcInfoNeeded,
            ref uint pnProcInfo,
            [In, Out] RM_PROCESS_INFO[] rgAffectedApps,
            ref uint lpdwRebootReasons);

        [DllImport("rstrtmgr.dll")]
        private static extern int RmEndSession(uint pSessionHandle);

        [StructLayout(LayoutKind.Sequential)]
        private struct RM_UNIQUE_PROCESS
        {
            public int dwProcessId;
            public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct RM_PROCESS_INFO
        {
            public RM_UNIQUE_PROCESS Process;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchRmMaxAppName + 1)]
            public string strAppName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchRmMaxSvcName + 1)]
            public string strServiceShortName;

            public RM_APP_TYPE ApplicationType;
            public uint AppStatus;
            public uint TSSessionId;

            [MarshalAs(UnmanagedType.Bool)]
            public bool bRestartable;
        }

        private enum RM_APP_TYPE
        {
            RmUnknownApp = 0,
            RmMainWindow = 1,
            RmOtherWindow = 2,
            RmService = 3,
            RmExplorer = 4,
            RmConsole = 5,
            RmCritical = 1000
        }
    }

    internal static class SelfTest
    {
        public static int Run()
        {
            string temp = Path.Combine(Path.GetTempPath(), "FolderFreeSelfTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            string file = Path.Combine(temp, "locked.txt");
            File.WriteAllText(file, "locked");

            try
            {
                var info = new ProcessStartInfo(Application.ExecutablePath, "--hold-lock \"" + file + "\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var child = Process.Start(info))
                {
                    Thread.Sleep(1000);
                    var results = FolderScanner.Scan(temp, null);
                    bool detected = child != null && results.Any(r => r.Pid == child.Id);
                    if (child != null && !child.HasExited)
                    {
                        child.Kill();
                        child.WaitForExit(3000);
                    }

                    if (!detected) return 2;
                }

                var cwdInfo = new ProcessStartInfo(Application.ExecutablePath, "--hold-cwd \"" + temp + "\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = temp
                };

                using (var child = Process.Start(cwdInfo))
                {
                    Thread.Sleep(1000);
                    var results = FolderScanner.Scan(temp, null);
                    bool detected = child != null && results.Any(r => r.Pid == child.Id && r.Reasons.Contains("Working folder", StringComparer.OrdinalIgnoreCase));
                    if (child != null && !child.HasExited)
                    {
                        child.Kill();
                        child.WaitForExit(3000);
                    }

                    return detected ? 0 : 4;
                }
            }
            catch
            {
                return 3;
            }
            finally
            {
                try { Directory.Delete(temp, true); } catch { }
            }
        }
    }
}
