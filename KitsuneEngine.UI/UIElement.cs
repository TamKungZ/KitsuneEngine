using System.Numerics;

namespace KitsuneEngine.UI;

public class UIElement
{
    private readonly List<UIElement> _children = new();

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }

    public UIElement? Parent { get; private set; }
    public IReadOnlyList<UIElement> Children => _children;

    public Vector2 Position { get; set; }
    public Vector2 Size { get; set; } = new Vector2(64, 32);

    public bool Visible { get; set; } = true;
    public bool Enabled { get; set; } = true;
    public int ZIndex { get; set; }

    public UIStyle Style { get; set; } = UIStyle.Default;

    public event Action<UIElement>? OnClick;
    public event Action<UIElement>? OnHover;

    public virtual Vector2 GlobalPosition => Parent == null ? Position : Parent.GlobalPosition + Position;

    public virtual void AddChild(UIElement child)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (ReferenceEquals(child, this))
            throw new InvalidOperationException("Cannot add an element as a child of itself.");

        child.Parent?.RemoveChild(child);
        child.Parent = this;
        _children.Add(child);
    }

    public virtual bool RemoveChild(UIElement child)
    {
        if (_children.Remove(child))
        {
            child.Parent = null;
            return true;
        }

        return false;
    }

    public virtual void Update(float deltaTime)
    {
        if (!Visible || !Enabled)
            return;

        for (int i = 0; i < _children.Count; i++)
        {
            _children[i].Update(deltaTime);
        }
    }

    public virtual void Draw(UIRenderer renderer)
    {
        if (!Visible)
            return;

        var gp = GlobalPosition;
        renderer.DrawRect(gp, Size, Style.BackgroundColor);

        if (Style.BorderThickness > 0f && Style.BorderColor.W > 0f)
        {
            renderer.DrawBorder(gp, Size, Style.BorderThickness, Style.BorderColor);
        }

        for (int i = 0; i < _children.Count; i++)
        {
            _children[i].Draw(renderer);
        }
    }

    public bool ContainsPoint(Vector2 point)
    {
        var gp = GlobalPosition;
        return point.X >= gp.X && point.X <= gp.X + Size.X && point.Y >= gp.Y && point.Y <= gp.Y + Size.Y;
    }

    public void RaiseClick() => OnClick?.Invoke(this);
    public void RaiseHover() => OnHover?.Invoke(this);
}
