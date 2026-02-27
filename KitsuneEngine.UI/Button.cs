using System.Numerics;

namespace KitsuneEngine.UI;

public class Button : UIElement
{
    public string Text { get; set; } = "Button";
    public Vector4 HoverColor { get; set; } = new Vector4(0.35f, 0.45f, 0.9f, 1f);
    public Vector4 PressedColor { get; set; } = new Vector4(0.2f, 0.3f, 0.65f, 1f);

    public bool IsHovered { get; private set; }
    public bool IsPressed { get; private set; }

    public Button()
    {
        Style = UIStyle.Button;
        Size = new Vector2(120, 36);
    }

    public void SetHovered(bool hovered)
    {
        if (hovered && !IsHovered)
            RaiseHover();

        IsHovered = hovered;
    }

    public void SetPressed(bool pressed)
    {
        IsPressed = pressed;
    }

    public void Click()
    {
        RaiseClick();
    }

    public override void Draw(UIRenderer renderer)
    {
        if (!Visible)
            return;

        var gp = GlobalPosition;
        var color = Style.BackgroundColor;
        if (IsPressed)
            color = PressedColor;
        else if (IsHovered)
            color = HoverColor;

        renderer.DrawRect(gp, Size, color);

        if (Style.BorderThickness > 0f && Style.BorderColor.W > 0f)
        {
            renderer.DrawBorder(gp, Size, Style.BorderThickness, Style.BorderColor);
        }

        // Text rendering can be integrated later with a font module.
    }
}
