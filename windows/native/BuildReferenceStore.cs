using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace Simcraft.Buildcraft;

/// <summary>Local project reference storage; importing never controls the Sims camera or builds anything.</summary>
public sealed record BuildReference(string Id, string FileName, string OriginalName, int Width, int Height);
public sealed class BuildReferenceProject {
    public List<BuildReference> References { get; set; } = new();
    public string Prompt { get; set; } = "";
}
public static class BuildReferenceStore {
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp" };
    private const long MaxBytes = 25L * 1024 * 1024;
    public static BuildReferenceProject Load(string projectDirectory) {
        var path = Path.Combine(projectDirectory, "buildcraft-references.json");
        if (!File.Exists(path)) return new BuildReferenceProject();
        return JsonSerializer.Deserialize<BuildReferenceProject>(File.ReadAllText(path)) ?? new BuildReferenceProject();
    }
    public static BuildReference Import(string projectDirectory, string sourcePath) {
        if (!Allowed.Contains(Path.GetExtension(sourcePath))) throw new InvalidDataException("Choose PNG, JPG, or WEBP.");
        var info = new FileInfo(sourcePath);
        if (!info.Exists || info.Length == 0 || info.Length > MaxBytes) throw new InvalidDataException("Image must be 1 byte to 25 MB.");
        var bytes = File.ReadAllBytes(sourcePath);
        var (width, height) = ReadDimensions(bytes);
        if (width < 1 || height < 1 || width > 16384 || height > 16384) throw new InvalidDataException("Invalid image dimensions.");
        var id = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var fileName = id + Path.GetExtension(sourcePath).ToLowerInvariant();
        var directory = Path.Combine(projectDirectory, "references");
        Directory.CreateDirectory(directory);
        var destination = Path.Combine(directory, fileName);
        if (!File.Exists(destination)) File.WriteAllBytes(destination, bytes);
        var project = Load(projectDirectory);
        var reference = new BuildReference(id, fileName, Path.GetFileName(sourcePath), width, height);
        if (!project.References.Any(x => x.Id == id)) project.References.Add(reference);
        Save(projectDirectory, project);
        return reference;
    }
    public static void Save(string projectDirectory, BuildReferenceProject project) {
        Directory.CreateDirectory(projectDirectory);
        var path = Path.Combine(projectDirectory, "buildcraft-references.json");
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(project, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, path, true);
    }
    private static (int Width, int Height) ReadDimensions(byte[] b) {
        if (b.Length >= 24 && b.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}))
            return (System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(16,4)), System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(20,4)));
        if (b.Length >= 12 && b[0] == (byte)'R' && b[1] == (byte)'I' && b[2] == (byte)'F' && b[3] == (byte)'F' && b[8] == (byte)'W' && b[9] == (byte)'E' && b[10] == (byte)'B' && b[11] == (byte)'P') {
            if (b.Length >= 30 && b[12] == (byte)'V' && b[13] == (byte)'P' && b[14] == (byte)'8' && b[15] == (byte)'X')
                return (1 + b[24] + (b[25] << 8) + (b[26] << 16), 1 + b[27] + (b[28] << 8) + (b[29] << 16));
            throw new InvalidDataException("WEBP variant not supported by metadata reader yet.");
        }
        if (b.Length >= 4 && b[0] == 0xff && b[1] == 0xd8) {
            int i = 2;
            while (i + 4 < b.Length) {
                if (b[i++] != 0xff) continue;
                byte marker = b[i++];
                if (marker == 0xd9 || marker == 0xda) break;
                if (marker == 0xff || marker == 0x00 || (marker >= 0xd0 && marker <= 0xd7)) continue;
                int len = (b[i] << 8) | b[i+1];
                if (len < 2 || i + len > b.Length) break;
                if ((marker >= 0xc0 && marker <= 0xc3) || (marker >= 0xc5 && marker <= 0xc7) || (marker >= 0xc9 && marker <= 0xcb) || (marker >= 0xcd && marker <= 0xcf)) {
                    if (len < 7) break;
                    return ((b[i+5] << 8) | b[i+6], (b[i+3] << 8) | b[i+4]);
                }
                i += len;
            }
        }
        throw new InvalidDataException("Unsupported or damaged image.");
    }
}
