using System.Drawing;

namespace Simcraft.Photos;

internal sealed record PhotoEntry(string Path, bool IsDirectory) {
    public string Name => System.IO.Path.GetFileName(Path);
}

internal sealed record PhotoFolder(IReadOnlyList<PhotoEntry> Entries, bool Truncated);

internal static class PhotoFiles {
    public static bool IsSupported(string path) =>
        new[] { ".png", ".jpg", ".jpeg", ".bmp" }.Contains(
            Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    public static string NormalizePath(string path) => Path.GetFullPath(
        Environment.ExpandEnvironmentVariables(path.Trim().Trim('"')));

    // Enumerate just this directory. No shell thumbnails, metadata handlers or subtree scans.
    // Bound the work even when most directory entries are not images.
    public static PhotoFolder ReadFolder(string folder, CancellationToken cancellation, int limit = 1000) {
        if (limit < 1) throw new ArgumentOutOfRangeException(nameof(limit));
        var entries = new List<PhotoEntry>();
        int inspected = 0;
        var options = new EnumerationOptions {
            RecurseSubdirectories = false, IgnoreInaccessible = true,
            ReturnSpecialDirectories = false, AttributesToSkip = 0
        };
        cancellation.ThrowIfCancellationRequested();
        foreach (var path in Directory.EnumerateFileSystemEntries(folder, "*", options)) {
            cancellation.ThrowIfCancellationRequested();
            if (inspected++ == limit) return Sorted(entries, true);
            try {
                bool directory = (File.GetAttributes(path) & FileAttributes.Directory) != 0;
                if (directory || IsSupported(path)) entries.Add(new(path, directory));
            } catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return Sorted(entries, false);
    }

    static PhotoFolder Sorted(List<PhotoEntry> entries, bool truncated) => new(
        entries.OrderByDescending(e => e.IsDirectory)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToArray(), truncated);

    public static Bitmap DecodePreview(string path, CancellationToken cancellation) {
        cancellation.ThrowIfCancellationRequested();
        if (!IsSupported(path)) throw new InvalidDataException("Choose a PNG, JPEG or BMP image.");
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > 40_000_000) throw new InvalidDataException("Image exceeds 40 MB limit.");
        using var image = Image.FromStream(stream);
        if (image.Width > 8192 || image.Height > 8192 || (long)image.Width * image.Height > 30_000_000)
            throw new InvalidDataException("Image dimensions too large. Resize the photo first.");
        cancellation.ThrowIfCancellationRequested();
        var thumbnail = new Bitmap(800, 600);
        try {
            using var g = Graphics.FromImage(thumbnail);
            g.Clear(Color.FromArgb(20, 25, 35));
            double scale = Math.Min(800.0 / image.Width, 600.0 / image.Height);
            int width = (int)(image.Width * scale), height = (int)(image.Height * scale);
            g.DrawImage(image, (800 - width) / 2, (600 - height) / 2, width, height);
            cancellation.ThrowIfCancellationRequested();
            return thumbnail;
        } catch { thumbnail.Dispose(); throw; }
    }
}
