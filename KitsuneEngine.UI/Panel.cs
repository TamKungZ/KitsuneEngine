using System.Numerics;

namespace KitsuneEngine.UI;

public enum PanelLayout
{
    Free,
    Vertical,
    Horizontal
}

public class Panel : UIElement
{
    public PanelLayout Layout { get; set; } = PanelLayout.Free;
    public float Spacing { get; set; } = 4f;

    public override void Update(float deltaTime)
    {
        if (Layout != PanelLayout.Free)
        {
            ApplyLayout();
        }

        base.Update(deltaTime);
    }

    private void ApplyLayout()
    {
        var padding = Style.Padding;
        var cursor = new Vector2(padding.Left, padding.Top);

        for (int i = 0; i < Children.Count; i++)
        {
            var child = Children[i];

            if (Layout == PanelLayout.Vertical)
            {
                child.Position = new Vector2(cursor.X, cursor.Y);
                cursor.Y += child.Size.Y + Spacing;
            }
            else
            {
                child.Position = new Vector2(cursor.X, cursor.Y);
                cursor.X += child.Size.X + Spacing;
            }
        }
    }
}
