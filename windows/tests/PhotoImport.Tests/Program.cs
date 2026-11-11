using System.Drawing;
using Simcraft.Photos;

internal static class Program {
    [STAThread]
    static int Main(string[] args) {
        string root = Path.Combine(Path.GetTempPath(), "simcraft-photo-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        int passed = 0;
        try {
            void Check(string name, Action test) {
                test(); passed++; Console.WriteLine("PASS " + name);
            }
            Check("Folder browsing stays at one level and filters non-images", () => {
                string folder = Directory.CreateDirectory(Path.Combine(root, "browse")).FullName;
                string child = Directory.CreateDirectory(Path.Combine(folder, "child")).FullName;
                File.WriteAllText(Path.Combine(child, "nested.png"), "not decoded while browsing");
                File.WriteAllText(Path.Combine(folder, "reference.JPG"), "not decoded while browsing");
                File.WriteAllText(Path.Combine(folder, "notes.txt"), "ignore");
                var listing = PhotoFiles.ReadFolder(folder, CancellationToken.None);
                Assert(!listing.Truncated && listing.Entries.Count == 2, "Unexpected entries");
                Assert(listing.Entries[0].IsDirectory, "Folders should sort first");
                Assert(listing.Entries.All(e => Path.GetDirectoryName(e.Path) == folder), "A subtree was scanned");
            });
            Check("Enumeration work is capped even for non-image files", () => {
                string folder = Directory.CreateDirectory(Path.Combine(root, "large")).FullName;
                for (int i = 0; i < 12; i++) File.WriteAllText(Path.Combine(folder, $"{i}.txt"), "");
                var listing = PhotoFiles.ReadFolder(folder, CancellationToken.None, limit: 5);
                Assert(listing.Truncated && listing.Entries.Count == 0, "The cap must count all inspected entries");
            });
            Check("Canceled scans and imports stop before filesystem access", () => {
                using var cancel = new CancellationTokenSource(); cancel.Cancel();
                Throws<OperationCanceledException>(() => PhotoFiles.ReadFolder(Path.Combine(root, "missing"), cancel.Token));
                Throws<OperationCanceledException>(() => PhotoFiles.DecodePreview(Path.Combine(root, "missing.png"), cancel.Token));
            });
            Check("Quoted Unicode paths load and the source file is released", () => {
                string file = Path.Combine(root, "фото с пробелами.PNG");
                using (var source = new Bitmap(160, 120)) source.Save(file, System.Drawing.Imaging.ImageFormat.Png);
                using var preview = PhotoFiles.DecodePreview(PhotoFiles.NormalizePath(" \"" + file + "\" "), CancellationToken.None);
                Assert(preview.Width == 800 && preview.Height == 600, "Wrong preview size");
                using var exclusive = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            });
            Check("Invalid, oversized and excessively wide images are rejected", () => {
                string invalid = Path.Combine(root, "invalid.png"); File.WriteAllText(invalid, "not an image");
                Throws<ArgumentException>(() => PhotoFiles.DecodePreview(invalid, CancellationToken.None));
                string large = Path.Combine(root, "oversized.png");
                using (var stream = File.Create(large)) stream.SetLength(40_000_001);
                Throws<InvalidDataException>(() => PhotoFiles.DecodePreview(large, CancellationToken.None));
                string wide = Path.Combine(root, "wide.png");
                using (var source = new Bitmap(8193, 1)) source.Save(wide, System.Drawing.Imaging.ImageFormat.Png);
                Throws<InvalidDataException>(() => PhotoFiles.DecodePreview(wide, CancellationToken.None));
            });
            Check("Picker can be created and canceled without opening a shell dialog", () => {
                using var picker = new PhotoPickerDialog(null);
                Assert(picker.CancelButton != null && picker.SelectedPhoto == null, "Missing cancel behavior");
            });
            if (args.Length == 1) Check("Provided reference photo decodes", () => {
                using var preview = PhotoFiles.DecodePreview(args[0], CancellationToken.None);
                Assert(preview.Width == 800 && preview.Height == 600, "Reference photo preview failed");
            });
            Console.WriteLine($"{passed} photo import checks passed.");
            return 0;
        } catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { Directory.Delete(root, recursive: true); }
    }

    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Throws<T>(Action action) where T : Exception {
        try { action(); } catch (T) { return; }
        throw new Exception("Expected " + typeof(T).Name);
    }
}
