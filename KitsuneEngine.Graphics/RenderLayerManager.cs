namespace KitsuneEngine.Graphics;

/// <summary>
/// Simple render layer manager for grouping draw/light logic.
/// Lower order renders first.
/// </summary>
public sealed class RenderLayerManager
{
    private readonly Dictionary<string, RenderLayer> _layers = new(StringComparer.Ordinal);

    public RenderLayerManager()
    {
        AddLayer("Default", 0);
    }

    public IReadOnlyList<RenderLayer> Layers => _layers.Values.OrderBy(l => l.Order).ToList();

    public RenderLayer AddLayer(string name, int order)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Layer name cannot be null or empty.", nameof(name));

        var layer = new RenderLayer(name, order);
        _layers[name] = layer;
        return layer;
    }

    public bool RemoveLayer(string name)
    {
        if (string.Equals(name, "Default", StringComparison.Ordinal))
            return false;

        return _layers.Remove(name);
    }

    public RenderLayer GetOrCreateLayer(string name, int order = 0)
    {
        if (_layers.TryGetValue(name, out var layer))
            return layer;

        return AddLayer(name, order);
    }

    public bool TryGetLayer(string name, out RenderLayer layer)
    {
        if (_layers.TryGetValue(name, out var found))
        {
            layer = found;
            return true;
        }

        layer = null!;
        return false;
    }

    public bool SetLayerOrder(string name, int order)
    {
        if (!_layers.TryGetValue(name, out var layer))
            return false;

        layer.Order = order;
        return true;
    }
}

public sealed class RenderLayer
{
    public string Name { get; }
    public int Order { get; internal set; }
    public bool LightingEnabled { get; set; } = true;

    public RenderLayer(string name, int order)
    {
        Name = name;
        Order = order;
    }
}

