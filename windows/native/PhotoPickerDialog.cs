using System.Windows.Forms;

namespace Simcraft.Photos;

// An owned, cancellable picker that does not instantiate the Windows shell file dialog.
internal sealed class PhotoPickerDialog : Form {
    readonly TextBox location = new() { Dock = DockStyle.Fill };
    readonly ListView files = new() { Dock = DockStyle.Fill, View = View.Details,
        FullRowSelect = true, MultiSelect = false, HideSelection = false };
    readonly Label status = new() { Dock = DockStyle.Fill, AutoSize = true };
    CancellationTokenSource? listing;
    string folder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
    public string? SelectedPhoto { get; private set; }

    public PhotoPickerDialog(string? initialPhoto) {
        Text = "Choose reference photo";
        ClientSize = new System.Drawing.Size(720, 480);
        MinimumSize = new System.Drawing.Size(500, 300);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        if (!string.IsNullOrEmpty(initialPhoto)) folder = Path.GetDirectoryName(initialPhoto) ?? folder;
        if (string.IsNullOrEmpty(folder)) folder = AppContext.BaseDirectory;
        location.Text = folder;
        location.AccessibleName = "Folder or full photo path";
        var go = new Button { Text = "Go / Open", AutoSize = true };
        var up = new Button { Text = "Up", AutoSize = true };
        var open = new Button { Text = "Open selected photo", AutoSize = true };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        var address = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, AutoSize = true };
        address.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        address.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        address.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        address.Controls.Add(location, 0, 0); address.Controls.Add(go, 1, 0); address.Controls.Add(up, 2, 0);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        buttons.Controls.Add(open); buttons.Controls.Add(cancel);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), ColumnCount = 1, RowCount = 5 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label { Text = "Paste a full photo path, or open a folder. Double-click folders to navigate.", AutoSize = true }, 0, 0);
        layout.Controls.Add(address, 0, 1); layout.Controls.Add(files, 0, 2);
        layout.Controls.Add(status, 0, 3); layout.Controls.Add(buttons, 0, 4);
        Controls.Add(layout);
        files.Columns.Add("Name", 500); files.Columns.Add("Type", 100);
        AcceptButton = go; CancelButton = cancel;
        go.Click += async (_, _) => await NavigateAsync(location.Text);
        up.Click += async (_, _) => {
            try {
                var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(folder));
                if (parent != null) await NavigateAsync(parent);
            } catch (Exception ex) { status.Text = ex.Message; }
        };
        open.Click += async (_, _) => await OpenSelectionAsync();
        files.DoubleClick += async (_, _) => await OpenSelectionAsync();
        files.KeyDown += async (_, e) => {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await OpenSelectionAsync(); }
        };
        Shown += async (_, _) => await NavigateAsync(folder);
        FormClosed += (_, _) => listing?.Cancel();
    }

    async Task OpenSelectionAsync() {
        if (files.SelectedItems.Count == 1 && files.SelectedItems[0].Tag is PhotoEntry entry)
            await NavigateAsync(entry.Path, entry.IsDirectory);
        else status.Text = "Select a photo or paste its full path above.";
    }

    async Task NavigateAsync(string path, bool? isDirectory = null) {
        listing?.Cancel();
        using var request = new CancellationTokenSource();
        listing = request;
        files.Items.Clear();
        status.Text = "Loading folder... You can cancel or paste a full photo path.";
        try {
            string fullPath = PhotoFiles.NormalizePath(path);
            // Filesystem checks and enumeration can block on a disconnected drive; keep them off the UI thread.
            var result = await Task.Run(() => {
                request.Token.ThrowIfCancellationRequested();
                if (isDirectory != true && PhotoFiles.IsSupported(fullPath)) {
                    using var file = File.OpenRead(fullPath);
                    return (Photo: fullPath, Folder: (PhotoFolder?)null);
                }
                return (Photo: (string?)null, Folder: (PhotoFolder?)PhotoFiles.ReadFolder(fullPath, request.Token));
            }, request.Token);
            if (request.IsCancellationRequested || IsDisposed || Disposing || listing != request) return;
            if (result.Photo != null) {
                SelectedPhoto = result.Photo;
                DialogResult = DialogResult.OK;
                Close();
                return;
            }
            folder = fullPath; location.Text = fullPath;
            files.BeginUpdate();
            try {
                foreach (var entry in result.Folder!.Entries)
                    files.Items.Add(new ListViewItem(new[] { entry.Name, entry.IsDirectory ? "Folder" : "Image" }) { Tag = entry });
            } finally { files.EndUpdate(); }
            status.Text = result.Folder!.Truncated
                ? "Only the first 1,000 entries were checked. Paste the full photo path to open another file."
                : $"{files.Items.Count} folders/images. Subfolders are opened only when selected.";
        } catch (OperationCanceledException) { }
        catch (Exception ex) {
            if (!IsDisposed && !Disposing && listing == request) status.Text = "Cannot open location: " + ex.Message;
        } finally { if (listing == request) listing = null; }
    }
}
