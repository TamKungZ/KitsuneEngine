using K4os.Compression.LZ4;
using System.Text;

namespace KitsuneEngine.Assets;

public class KpakPacker
{
    private const uint MAGIC = 0x4B50414B; // "KPAK"
    private const ushort VERSION = 1;

    private bool _useCompression = true;
    private int _compressionLevel = (int)LZ4Level.L00_FAST; // Store as int

    // Fixed: Changed parameter type from int to LZ4Level
    public KpakPacker SetCompression(bool enabled, LZ4Level level = LZ4Level.L00_FAST)
    {
        _useCompression = enabled;
        _compressionLevel = (int)level; // Cast to int for storage
        return this;
    }

    public void Pack(string sourceDir, string outputPath)
    {
        if (!Directory.Exists(sourceDir))
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");

        var files = Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith(".kpak") && !f.EndsWith(".meta"))
            .ToList();

        Console.WriteLine($"Packing {files.Count} files...");

        using var fs = File.Create(outputPath);
        using var bw = new BinaryWriter(fs);

        // Write placeholder header
        var header = new KpakHeader
        {
            Magic = MAGIC,
            Version = VERSION,
            Flags = _useCompression ? (ushort)KpakFlags.Compressed : (ushort)0,
            FileCount = 0,
            IndexOffset = 0,
            MetadataOffset = 0
        };
        bw.WriteStruct(header);

        var index = new List<KpakIndexEntry>();
        int totalOriginal = 0;
        int totalCompressed = 0;

        foreach (var filePath in files)
        {
            var relativePath = Path.GetRelativePath(sourceDir, filePath);
            var data = File.ReadAllBytes(filePath);
            var hash = BinaryExtensions.HashPath(relativePath);

            totalOriginal += data.Length;

            long offset = fs.Position;
            byte[] finalData = data;
            int compressedSize = data.Length;
            byte flags = (byte)KpakEntryFlags.None;

            // Compress if enabled and beneficial
            if (_useCompression && data.Length > 128)
            {
                var compressed = new byte[LZ4Codec.MaximumOutputSize(data.Length)];
                int encodedSize = LZ4Codec.Encode(
                    data, 0, data.Length,
                    compressed, 0, compressed.Length,
                    (LZ4Level)_compressionLevel // Cast int back to LZ4Level
                );

                // Only use compression if it actually reduces size
                if (encodedSize > 0 && encodedSize < data.Length * 0.95f)
                {
                    finalData = compressed.AsSpan(0, encodedSize).ToArray();
                    compressedSize = encodedSize;
                    flags |= (byte)KpakEntryFlags.Compressed;
                }
            }

            bw.Write(finalData);
            totalCompressed += compressedSize;

            var entry = new KpakIndexEntry
            {
                PathHash = hash,
                Offset = offset,
                Size = data.Length,
                CompressedSize = compressedSize,
                Type = (byte)DetectAssetType(relativePath),
                Flags = flags
            };

            index.Add(entry);

            Console.WriteLine($"  [{index.Count}/{files.Count}] {relativePath} ({FormatBytes(data.Length)} → {FormatBytes(compressedSize)})");
        }

        // Write index
        header.IndexOffset = fs.Position;
        header.FileCount = index.Count;

        foreach (var entry in index)
            bw.WriteStruct(entry);

        // Write metadata (file paths for debugging)
        header.MetadataOffset = fs.Position;
        foreach (var filePath in files)
        {
            var relativePath = Path.GetRelativePath(sourceDir, filePath);
            var hash = BinaryExtensions.HashPath(relativePath);
            bw.Write(hash);
            bw.Write(relativePath);
        }

        // Rewrite header
        fs.Seek(0, SeekOrigin.Begin);
        bw.WriteStruct(header);

        var ratio = totalOriginal > 0 ? (1.0f - (float)totalCompressed / totalOriginal) * 100 : 0;
        Console.WriteLine($"\nPacked: {files.Count} files");
        Console.WriteLine($"Size: {FormatBytes(totalOriginal)} → {FormatBytes(totalCompressed)} ({ratio:F1}% saved)");
        Console.WriteLine($"Output: {outputPath}");
    }

    private KpakAssetType DetectAssetType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".png" or ".jpg" or ".jpeg" or ".bmp" or ".tga" => KpakAssetType.Texture,
            ".wav" or ".mp3" or ".ogg" => KpakAssetType.Audio,
            ".glsl" or ".hlsl" or ".shader" => KpakAssetType.Shader,
            ".ttf" or ".otf" => KpakAssetType.Font,
            ".scene" => KpakAssetType.Scene,
            ".prefab" => KpakAssetType.Prefab,
            _ => KpakAssetType.Data
        };
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double size = bytes;
        int order = 0;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}

// CLI Tool
public class KpakPackerCLI
{
    public static void Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: KpakPacker <source_dir> <output.kpak> [--no-compress]");
            return;
        }

        var sourceDir = args[0];
        var outputPath = args[1];
        var compress = !args.Contains("--no-compress");

        var packer = new KpakPacker()
            .SetCompression(compress, LZ4Level.L04_HC);

        try
        {
            packer.Pack(sourceDir, outputPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}