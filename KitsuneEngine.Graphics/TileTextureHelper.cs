using System.Numerics;

namespace KitsuneEngine.Graphics;

/// <summary>
/// UV and pixel rectangle for a tile region inside a tileset texture.
/// </summary>
public readonly struct TileRegion
{
    public int Index { get; }
    public int TileX { get; }
    public int TileY { get; }
    public int PixelX { get; }
    public int PixelY { get; }
    public int Width { get; }
    public int Height { get; }

    public Vector2 UvMin { get; }
    public Vector2 UvMax { get; }

    public TileRegion(int index, int tileX, int tileY, int pixelX, int pixelY, int width, int height, Vector2 uvMin, Vector2 uvMax)
    {
        Index = index;
        TileX = tileX;
        TileY = tileY;
        PixelX = pixelX;
        PixelY = pixelY;
        Width = width;
        Height = height;
        UvMin = uvMin;
        UvMax = uvMax;
    }
}

/// <summary>
/// Optional helper for slicing tileset textures into tile regions.
/// Developers can use this helper or implement their own tileset system.
/// </summary>
public sealed class TileTextureHelper
{
    public Texture Texture { get; }
    public int TileWidth { get; }
    public int TileHeight { get; }
    public int Spacing { get; }
    public int Margin { get; }
    public int Columns { get; }
    public int Rows { get; }
    public int TileCount => Columns * Rows;

    public TileTextureHelper(Texture texture, int tileWidth, int tileHeight, int spacing = 0, int margin = 0)
    {
        Texture = texture ?? throw new ArgumentNullException(nameof(texture));

        if (tileWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(tileWidth), "Tile width must be greater than zero.");
        if (tileHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(tileHeight), "Tile height must be greater than zero.");
        if (spacing < 0)
            throw new ArgumentOutOfRangeException(nameof(spacing), "Spacing cannot be negative.");
        if (margin < 0)
            throw new ArgumentOutOfRangeException(nameof(margin), "Margin cannot be negative.");

        TileWidth = tileWidth;
        TileHeight = tileHeight;
        Spacing = spacing;
        Margin = margin;

        Columns = ComputeCount(Texture.Width, TileWidth, Spacing, Margin);
        Rows = ComputeCount(Texture.Height, TileHeight, Spacing, Margin);
    }

    public bool TryGetTile(int index, out TileRegion region)
    {
        if (index < 0 || index >= TileCount)
        {
            region = default;
            return false;
        }

        int tileX = index % Columns;
        int tileY = index / Columns;

        region = CreateRegion(index, tileX, tileY);
        return true;
    }

    public bool TryGetTile(int tileX, int tileY, out TileRegion region)
    {
        if (tileX < 0 || tileY < 0 || tileX >= Columns || tileY >= Rows)
        {
            region = default;
            return false;
        }

        int index = tileY * Columns + tileX;
        region = CreateRegion(index, tileX, tileY);
        return true;
    }

    public TileRegion GetTile(int index)
    {
        if (!TryGetTile(index, out var region))
            throw new ArgumentOutOfRangeException(nameof(index), $"Tile index {index} is out of range 0..{TileCount - 1}.");

        return region;
    }

    public TileRegion GetTile(int tileX, int tileY)
    {
        if (!TryGetTile(tileX, tileY, out var region))
            throw new ArgumentOutOfRangeException($"Tile coordinate ({tileX},{tileY}) is out of range.");

        return region;
    }

    public List<TileRegion> SliceAll()
    {
        var result = new List<TileRegion>(TileCount);
        for (int i = 0; i < TileCount; i++)
        {
            result.Add(GetTile(i));
        }
        return result;
    }

    private TileRegion CreateRegion(int index, int tileX, int tileY)
    {
        int pixelX = Margin + tileX * (TileWidth + Spacing);
        int pixelY = Margin + tileY * (TileHeight + Spacing);

        float invW = 1f / Texture.Width;
        float invH = 1f / Texture.Height;

        // Account for bottom-left UV origin used in many GL pipelines.
        float uMin = pixelX * invW;
        float uMax = (pixelX + TileWidth) * invW;

        float top = pixelY * invH;
        float bottom = (pixelY + TileHeight) * invH;

        var uvMin = new Vector2(uMin, top);
        var uvMax = new Vector2(uMax, bottom);

        return new TileRegion(index, tileX, tileY, pixelX, pixelY, TileWidth, TileHeight, uvMin, uvMax);
    }

    private static int ComputeCount(int textureSize, int tileSize, int spacing, int margin)
    {
        int available = textureSize - (margin * 2);
        if (available < tileSize)
            return 0;

        return 1 + (available - tileSize) / (tileSize + spacing);
    }
}
