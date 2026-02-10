using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace KitsuneEngine.Assets;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct KpakHeader
{
    public uint Magic;           // 0x4B50414B = "KPAK"
    public ushort Version;       // Format version
    public ushort Flags;         // Feature flags
    public int FileCount;        // Number of files
    public long IndexOffset;     // Offset to index table
    public long MetadataOffset;  // Offset to metadata (optional)
    public int Reserved;         // Future use
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct KpakIndexEntry
{
    public ulong PathHash;       // FNV-1a hash of path
    public long Offset;          // File data offset
    public int Size;             // Uncompressed size
    public int CompressedSize;   // Compressed size (0 = not compressed)
    public byte Type;            // Asset type
    public byte Flags;           // Entry flags
    public ushort Reserved;      // Alignment
}

public enum KpakAssetType : byte
{
    Unknown = 0,
    Texture = 1,
    Audio = 2,
    Shader = 3,
    Font = 4,
    Data = 5,
    Scene = 6,
    Prefab = 7,
}

[Flags]
public enum KpakFlags : ushort
{
    None = 0,
    Compressed = 1 << 0,
    Encrypted = 1 << 1,
    Streaming = 1 << 2,
}

[Flags]
public enum KpakEntryFlags : byte
{
    None = 0,
    Compressed = 1 << 0,
    Encrypted = 1 << 1,
    Streamed = 1 << 2,
}

// Helper extensions
public static class BinaryExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteStruct<T>(this BinaryWriter bw, T value) where T : unmanaged
    {
        Span<byte> buffer = stackalloc byte[Unsafe.SizeOf<T>()];
        MemoryMarshal.Write(buffer, in value); // Use 'in' instead of 'ref'
        bw.Write(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ReadStruct<T>(this BinaryReader br) where T : unmanaged
    {
        Span<byte> buffer = stackalloc byte[Unsafe.SizeOf<T>()];
        br.Read(buffer);
        return MemoryMarshal.Read<T>(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong HashPath(string path)
    {
        unchecked
        {
            ulong hash = 14695981039346656037UL; // FNV-1a offset basis
            foreach (char c in path.ToLowerInvariant().Replace('\\', '/'))
            {
                hash ^= c;
                hash *= 1099511628211UL; // FNV-1a prime
            }
            return hash;
        }
    }
}