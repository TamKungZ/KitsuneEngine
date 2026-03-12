using System.Numerics;

namespace KitsuneEngine.Core;

public enum HitboxType
{
    Rectangle,
    Circle
}

/// <summary>
/// A lightweight hitbox definition for 2D overlap checks.
/// </summary>
public sealed class Hitbox
{
    public int Id { get; }
    public HitboxType Type { get; }

    /// <summary>
    /// Center position in world space.
    /// </summary>
    public Vector2 Center { get; set; }

    /// <summary>
    /// Rectangle size (Width, Height). Used only when Type is Rectangle.
    /// </summary>
    public Vector2 Size { get; set; }

    /// <summary>
    /// Circle radius. Used only when Type is Circle.
    /// </summary>
    public float Radius { get; set; }

    public string Tag { get; set; }
    public int Layer { get; set; }
    public bool Enabled { get; set; } = true;
    public bool IsTrigger { get; set; }

    internal Hitbox(int id, Vector2 center, Vector2 size, string? tag, int layer, bool isTrigger)
    {
        Id = id;
        Type = HitboxType.Rectangle;
        Center = center;
        Size = size;
        Radius = 0f;
        Tag = tag ?? string.Empty;
        Layer = layer;
        IsTrigger = isTrigger;
    }

    internal Hitbox(int id, Vector2 center, float radius, string? tag, int layer, bool isTrigger)
    {
        Id = id;
        Type = HitboxType.Circle;
        Center = center;
        Size = Vector2.Zero;
        Radius = radius;
        Tag = tag ?? string.Empty;
        Layer = layer;
        IsTrigger = isTrigger;
    }
}

/// <summary>
/// Optional helper system for managing and querying 2D hitboxes.
/// You can use it as-is, extend it, or ignore it completely.
/// </summary>
public sealed class HitboxManager
{
    private readonly Dictionary<int, Hitbox> _hitboxes = new();
    private int _nextId = 1;

    public int Count => _hitboxes.Count;
    public IReadOnlyCollection<Hitbox> Hitboxes => _hitboxes.Values;

    public Hitbox CreateRectangle(Vector2 center, Vector2 size, string? tag = null, int layer = 0, bool isTrigger = false)
    {
        if (size.X < 0f || size.Y < 0f)
            throw new ArgumentOutOfRangeException(nameof(size), "Rectangle size cannot be negative.");

        var hitbox = new Hitbox(_nextId++, center, size, tag, layer, isTrigger);
        _hitboxes[hitbox.Id] = hitbox;
        return hitbox;
    }

    public Hitbox CreateCircle(Vector2 center, float radius, string? tag = null, int layer = 0, bool isTrigger = false)
    {
        if (radius < 0f)
            throw new ArgumentOutOfRangeException(nameof(radius), "Circle radius cannot be negative.");

        var hitbox = new Hitbox(_nextId++, center, radius, tag, layer, isTrigger);
        _hitboxes[hitbox.Id] = hitbox;
        return hitbox;
    }

    public bool Remove(int id) => _hitboxes.Remove(id);

    public void Clear() => _hitboxes.Clear();

    public bool TryGet(int id, out Hitbox hitbox) => _hitboxes.TryGetValue(id, out hitbox!);

    public bool Overlaps(int firstId, int secondId)
    {
        if (!_hitboxes.TryGetValue(firstId, out var a) || !_hitboxes.TryGetValue(secondId, out var b))
            return false;

        return Overlaps(a, b);
    }

    public bool Overlaps(Hitbox a, Hitbox b)
    {
        if (!a.Enabled || !b.Enabled)
            return false;

        if (a.Layer != b.Layer)
            return false;

        return a.Type switch
        {
            HitboxType.Rectangle when b.Type == HitboxType.Rectangle => RectangleIntersectsRectangle(a, b),
            HitboxType.Circle when b.Type == HitboxType.Circle => CircleIntersectsCircle(a, b),
            HitboxType.Rectangle when b.Type == HitboxType.Circle => RectangleIntersectsCircle(a, b),
            HitboxType.Circle when b.Type == HitboxType.Rectangle => RectangleIntersectsCircle(b, a),
            _ => false
        };
    }

    public List<Hitbox> QueryOverlaps(Hitbox source, bool includeTriggers = true)
    {
        var result = new List<Hitbox>();

        if (!source.Enabled)
            return result;

        foreach (var candidate in _hitboxes.Values)
        {
            if (candidate.Id == source.Id)
                continue;

            if (!includeTriggers && (candidate.IsTrigger || source.IsTrigger))
                continue;

            if (Overlaps(source, candidate))
                result.Add(candidate);
        }

        return result;
    }

    public List<Hitbox> QueryPoint(Vector2 point, int? layer = null)
    {
        var result = new List<Hitbox>();

        foreach (var hitbox in _hitboxes.Values)
        {
            if (!hitbox.Enabled)
                continue;

            if (layer.HasValue && hitbox.Layer != layer.Value)
                continue;

            if (ContainsPoint(hitbox, point))
                result.Add(hitbox);
        }

        return result;
    }

    public static bool ContainsPoint(Hitbox hitbox, Vector2 point)
    {
        return hitbox.Type switch
        {
            HitboxType.Rectangle => ContainsPointRectangle(hitbox, point),
            HitboxType.Circle => ContainsPointCircle(hitbox, point),
            _ => false
        };
    }

    private static bool RectangleIntersectsRectangle(Hitbox a, Hitbox b)
    {
        var aMin = a.Center - a.Size * 0.5f;
        var aMax = a.Center + a.Size * 0.5f;
        var bMin = b.Center - b.Size * 0.5f;
        var bMax = b.Center + b.Size * 0.5f;

        return aMin.X <= bMax.X &&
               aMax.X >= bMin.X &&
               aMin.Y <= bMax.Y &&
               aMax.Y >= bMin.Y;
    }

    private static bool CircleIntersectsCircle(Hitbox a, Hitbox b)
    {
        var distanceSquared = Vector2.DistanceSquared(a.Center, b.Center);
        var radiusSum = a.Radius + b.Radius;
        return distanceSquared <= radiusSum * radiusSum;
    }

    private static bool RectangleIntersectsCircle(Hitbox rectangle, Hitbox circle)
    {
        var half = rectangle.Size * 0.5f;
        var min = rectangle.Center - half;
        var max = rectangle.Center + half;

        var closest = new Vector2(
            Math.Clamp(circle.Center.X, min.X, max.X),
            Math.Clamp(circle.Center.Y, min.Y, max.Y)
        );

        var distanceSquared = Vector2.DistanceSquared(circle.Center, closest);
        return distanceSquared <= circle.Radius * circle.Radius;
    }

    private static bool ContainsPointRectangle(Hitbox rectangle, Vector2 point)
    {
        var half = rectangle.Size * 0.5f;
        var min = rectangle.Center - half;
        var max = rectangle.Center + half;

        return point.X >= min.X && point.X <= max.X &&
               point.Y >= min.Y && point.Y <= max.Y;
    }

    private static bool ContainsPointCircle(Hitbox circle, Vector2 point)
    {
        var distanceSquared = Vector2.DistanceSquared(circle.Center, point);
        return distanceSquared <= circle.Radius * circle.Radius;
    }
}
