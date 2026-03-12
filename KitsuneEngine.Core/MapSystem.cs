using System.Numerics;

namespace KitsuneEngine.Core;

/// <summary>
/// Generic map descriptor suitable for visual novel, RPG, and other 2D games.
/// </summary>
public sealed class MapData
{
    public string Id { get; }
    public string Name { get; set; }
    public Vector2 Size { get; set; }
    public string? BackgroundAsset { get; set; }

    /// <summary>
    /// Arbitrary map values (music id, weather, chapter, etc.)
    /// </summary>
    public Dictionary<string, string> Metadata { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Spawn points and named anchors (player start, npc_home, camera_focus, etc.)
    /// </summary>
    public Dictionary<string, Vector2> Anchors { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Connections to other maps for travel/warp/door systems.
    /// </summary>
    public List<MapConnection> Connections { get; } = new();

    /// <summary>
    /// Optional tile map payload. Leave null if this map is not tile-based.
    /// </summary>
    public TileMapData? TileMap { get; set; }

    public MapData(string id, string? name = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Map id cannot be null or empty.", nameof(id));

        Id = id;
        Name = string.IsNullOrWhiteSpace(name) ? id : name;
        Size = Vector2.Zero;
    }
}

/// <summary>
/// Optional tile map data for grid-based 2D maps.
/// Uses flattened tile indices per layer in row-major order.
/// </summary>
public sealed class TileMapData
{
    public int Width { get; }
    public int Height { get; }
    public int TileWidth { get; }
    public int TileHeight { get; }
    public List<TileLayerData> Layers { get; } = new();

    public TileMapData(int width, int height, int tileWidth, int tileHeight)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be greater than zero.");
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be greater than zero.");
        if (tileWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(tileWidth), "Tile width must be greater than zero.");
        if (tileHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(tileHeight), "Tile height must be greater than zero.");

        Width = width;
        Height = height;
        TileWidth = tileWidth;
        TileHeight = tileHeight;
    }

    public int GetIndex(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height)
            throw new ArgumentOutOfRangeException($"Tile coordinate ({x},{y}) is out of range.");

        return y * Width + x;
    }

    public Vector2 TileToWorld(int x, int y)
    {
        return new Vector2(x * TileWidth, y * TileHeight);
    }
}

public sealed class TileLayerData
{
    public string Name { get; }
    public int ZIndex { get; set; }
    public bool Visible { get; set; } = true;
    public int[] Tiles { get; }

    public TileLayerData(string name, int tileCount)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Layer name cannot be null or empty.", nameof(name));
        if (tileCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(tileCount), "Tile count must be greater than zero.");

        Name = name;
        Tiles = new int[tileCount];

        // -1 means empty cell
        Array.Fill(Tiles, -1);
    }

    public int GetTile(int index) => Tiles[index];
    public void SetTile(int index, int tileId) => Tiles[index] = tileId;
}

/// <summary>
/// Optional helper builder for creating tile maps quickly.
/// Developers can use this for convenience or implement their own map pipeline.
/// </summary>
public sealed class TileMapBuilder
{
    private readonly TileMapData _map;
    private readonly Dictionary<string, TileLayerData> _layers = new(StringComparer.OrdinalIgnoreCase);

    public TileMapBuilder(int width, int height, int tileWidth, int tileHeight)
    {
        _map = new TileMapData(width, height, tileWidth, tileHeight);
    }

    public TileMapBuilder AddLayer(string name, int zIndex = 0)
    {
        var layer = new TileLayerData(name, _map.Width * _map.Height)
        {
            ZIndex = zIndex
        };

        _layers[name] = layer;
        return this;
    }

    public TileMapBuilder SetTile(string layerName, int x, int y, int tileId)
    {
        if (!_layers.TryGetValue(layerName, out var layer))
            throw new InvalidOperationException($"Layer '{layerName}' not found.");

        int index = _map.GetIndex(x, y);
        layer.SetTile(index, tileId);
        return this;
    }

    public TileMapBuilder FillRect(string layerName, int startX, int startY, int width, int height, int tileId)
    {
        if (width <= 0 || height <= 0)
            return this;

        for (int y = startY; y < startY + height; y++)
        {
            for (int x = startX; x < startX + width; x++)
            {
                SetTile(layerName, x, y, tileId);
            }
        }

        return this;
    }

    public TileMapData Build()
    {
        _map.Layers.Clear();
        _map.Layers.AddRange(_layers.Values.OrderBy(l => l.ZIndex));
        return _map;
    }
}

public sealed class MapConnection
{
    public string ToMapId { get; }
    public string? ViaAnchor { get; set; }
    public string? RequiredFlag { get; set; }

    public MapConnection(string toMapId)
    {
        if (string.IsNullOrWhiteSpace(toMapId))
            throw new ArgumentException("Destination map id cannot be null or empty.", nameof(toMapId));

        ToMapId = toMapId;
    }
}

/// <summary>
/// Lightweight optional manager for map registry and map transitions.
/// </summary>
public sealed class MapManager
{
    private readonly Dictionary<string, MapData> _maps = new(StringComparer.OrdinalIgnoreCase);

    public MapData? CurrentMap { get; private set; }
    public string? CurrentMapId => CurrentMap?.Id;
    public IReadOnlyCollection<MapData> Maps => _maps.Values;

    public event Action<MapTransitionContext>? MapChanging;
    public event Action<MapTransitionContext>? MapChanged;

    public void RegisterMap(MapData map)
    {
        if (map == null)
            throw new ArgumentNullException(nameof(map));

        _maps[map.Id] = map;
    }

    public bool UnregisterMap(string mapId)
    {
        if (string.IsNullOrWhiteSpace(mapId))
            return false;

        if (CurrentMap != null && string.Equals(CurrentMap.Id, mapId, StringComparison.OrdinalIgnoreCase))
            CurrentMap = null;

        return _maps.Remove(mapId);
    }

    public bool HasMap(string mapId) => _maps.ContainsKey(mapId);

    public bool TryGetMap(string mapId, out MapData map) => _maps.TryGetValue(mapId, out map!);

    public bool TrySetCurrentMap(string mapId)
    {
        if (!_maps.TryGetValue(mapId, out var next))
            return false;

        SetCurrentMap(next);
        return true;
    }

    public void SetCurrentMap(MapData map)
    {
        if (map == null)
            throw new ArgumentNullException(nameof(map));

        var context = new MapTransitionContext(CurrentMap, map);
        MapChanging?.Invoke(context);
        CurrentMap = map;
        MapChanged?.Invoke(context);
    }

    public bool TryTravel(string connectionToMapId)
    {
        if (CurrentMap == null)
            return false;

        var connection = CurrentMap.Connections.FirstOrDefault(c =>
            string.Equals(c.ToMapId, connectionToMapId, StringComparison.OrdinalIgnoreCase));

        if (connection == null)
            return false;

        return TrySetCurrentMap(connection.ToMapId);
    }
}

public sealed class MapTransitionContext
{
    public MapData? FromMap { get; }
    public MapData? ToMap { get; }

    public MapTransitionContext(MapData? fromMap, MapData? toMap)
    {
        FromMap = fromMap;
        ToMap = toMap;
    }
}
