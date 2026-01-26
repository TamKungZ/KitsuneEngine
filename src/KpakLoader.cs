using K4os.Compression.LZ4;
using System.Buffers;

namespace KitsuneEngine.Assets;

public class KpakLoader : IDisposable
{
    private FileStream? _stream;
    private BinaryReader? _reader;
    private KpakHeader _header;
    private Dictionary<ulong, KpakIndexEntry>? _index;
    private Dictionary<ulong, string>? _pathMap; // For debugging

    public int FileCount => _header.FileCount;
    public bool IsLoaded { get; private set; }

    public KpakLoader(string path)
    {
        Load(path);
    }

    private void Load(string filePath) // Changed parameter name from 'path' to 'filePath'
    {
        _stream = File.OpenRead(filePath);
        _reader = new BinaryReader(_stream);

        // Read header
        _header = _reader.ReadStruct<KpakHeader>();

        if (_header.Magic != 0x4B50414B)
            throw new InvalidDataException("Invalid KPAK magic number");

        if (_header.Version != 1)
            throw new InvalidDataException($"Unsupported KPAK version: {_header.Version}");

        // Read index
        _stream.Seek(_header.IndexOffset, SeekOrigin.Begin);
        _index = new Dictionary<ulong, KpakIndexEntry>(_header.FileCount);

        for (int i = 0; i < _header.FileCount; i++)
        {
            var entry = _reader.ReadStruct<KpakIndexEntry>();
            _index[entry.PathHash] = entry;
        }

        // Read metadata (optional)
        if (_header.MetadataOffset > 0)
        {
            _stream.Seek(_header.MetadataOffset, SeekOrigin.Begin);
            _pathMap = new Dictionary<ulong, string>(_header.FileCount);

            for (int i = 0; i < _header.FileCount; i++)
            {
                var hash = _reader.ReadUInt64();
                var pathName = _reader.ReadString(); // Changed variable name
                _pathMap[hash] = pathName;
            }
        }

        IsLoaded = true;
        Console.WriteLine($"Loaded KPAK: {_header.FileCount} files, v{_header.Version}");
    }

    public byte[] LoadAsset(string path)
    {
        var hash = BinaryExtensions.HashPath(path);

        if (_index == null || !_index.TryGetValue(hash, out var entry))
            throw new FileNotFoundException($"Asset not found: {path} (hash: {hash:X16})");

        return LoadAssetByEntry(entry);
    }

    public bool TryLoadAsset(string path, out byte[] data)
    {
        try
        {
            data = LoadAsset(path);
            return true;
        }
        catch
        {
            data = null!;
            return false;
        }
    }

    private byte[] LoadAssetByEntry(KpakIndexEntry entry)
    {
        if (_stream == null || _reader == null)
            throw new InvalidOperationException("KPAK not loaded");

        _stream.Seek(entry.Offset, SeekOrigin.Begin);

        if ((entry.Flags & (byte)KpakEntryFlags.Compressed) != 0)
        {
            // Load compressed data
            var compressed = _reader.ReadBytes(entry.CompressedSize);
            var decompressed = new byte[entry.Size];

            int decoded = LZ4Codec.Decode(
                compressed, 0, compressed.Length,
                decompressed, 0, decompressed.Length
            );

            if (decoded != entry.Size)
                throw new InvalidDataException($"Decompression failed: expected {entry.Size}, got {decoded}");

            return decompressed;
        }
        else
        {
            // Load uncompressed data
            return _reader.ReadBytes(entry.Size);
        }
    }

    public async Task<byte[]> LoadAssetAsync(string path, CancellationToken ct = default)
    {
        return await Task.Run(() => LoadAsset(path), ct);
    }

    public bool ContainsAsset(string path)
    {
        var hash = BinaryExtensions.HashPath(path);
        return _index != null && _index.ContainsKey(hash);
    }

    public IEnumerable<string> GetAllPaths()
    {
        if (_pathMap != null)
            return _pathMap.Values;

        return Enumerable.Empty<string>();
    }

    public IEnumerable<string> GetAssetsByType(KpakAssetType type)
    {
        if (_pathMap == null || _index == null)
            return Enumerable.Empty<string>();

        return _index
            .Where(kvp => kvp.Value.Type == (byte)type)
            .Select(kvp => _pathMap.TryGetValue(kvp.Key, out var path) ? path : null)
            .Where(p => p != null)!;
    }

    public KpakAssetType GetAssetType(string path)
    {
        var hash = BinaryExtensions.HashPath(path);
        return _index != null && _index.TryGetValue(hash, out var entry)
            ? (KpakAssetType)entry.Type
            : KpakAssetType.Unknown;
    }

    public (int size, int compressedSize) GetAssetInfo(string path)
    {
        var hash = BinaryExtensions.HashPath(path);
        if (_index != null && _index.TryGetValue(hash, out var entry))
            return (entry.Size, entry.CompressedSize);

        return (0, 0);
    }

    public void Dispose()
    {
        _reader?.Dispose();
        _stream?.Dispose();
        _index?.Clear();
        _pathMap?.Clear();
        IsLoaded = false;
    }
}

// Streaming loader for large assets
public class KpakStreamLoader : IDisposable
{
    private KpakLoader _loader;
    private const int CHUNK_SIZE = 64 * 1024; // 64KB chunks

    public KpakStreamLoader(KpakLoader loader)
    {
        _loader = loader;
    }

    public IEnumerable<byte[]> LoadAssetChunks(string path)
    {
        var data = _loader.LoadAsset(path);

        for (int i = 0; i < data.Length; i += CHUNK_SIZE)
        {
            int size = Math.Min(CHUNK_SIZE, data.Length - i);
            var chunk = new byte[size];
            Array.Copy(data, i, chunk, 0, size);
            yield return chunk;
        }
    }

    public async IAsyncEnumerable<byte[]> LoadAssetChunksAsync(string path)
    {
        var data = await _loader.LoadAssetAsync(path);

        for (int i = 0; i < data.Length; i += CHUNK_SIZE)
        {
            int size = Math.Min(CHUNK_SIZE, data.Length - i);
            var chunk = new byte[size];
            Array.Copy(data, i, chunk, 0, size);

            await Task.Yield(); // Allow other tasks to run
            yield return chunk;
        }
    }

    public void Dispose()
    {
        // Loader is managed externally
    }
}