using System.Numerics;

namespace KitsuneEngine.UI;

public struct UIThickness
{
    public float Left;
    public float Top;
    public float Right;
    public float Bottom;

    public UIThickness(float uniform)
    {
        Left = Top = Right = Bottom = uniform;
    }

    public UIThickness(float left, float top, float right, float bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }
}

public struct UIStyle
{
    public Vector4 BackgroundColor;
    public Vector4 BorderColor;
    public float BorderThickness;
    public UIThickness Padding;

    public static UIStyle Default => new UIStyle
    {
        BackgroundColor = new Vector4(0.2f, 0.2f, 0.2f, 1f),
        BorderColor = new Vector4(0f, 0f, 0f, 0f),
        BorderThickness = 0f,
        Padding = new UIThickness(0f)
    };

    public static UIStyle Button => new UIStyle
    {
        BackgroundColor = new Vector4(0.25f, 0.35f, 0.8f, 1f),
        BorderColor = new Vector4(0.12f, 0.2f, 0.5f, 1f),
        BorderThickness = 1f,
        Padding = new UIThickness(8f, 4f, 8f, 4f)
    };
}
