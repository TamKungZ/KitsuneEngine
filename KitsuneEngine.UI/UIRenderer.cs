using KitsuneEngine.Graphics;
using System.Numerics;

namespace KitsuneEngine.UI;

public class UIRenderer
{
    private readonly SpriteBatch _spriteBatch;
    private readonly Texture _whitePixel;
    private Matrix4x4 _transform = Matrix4x4.Identity;

    public UIRenderer(SpriteBatch spriteBatch, Texture? whitePixel = null)
    {
        _spriteBatch = spriteBatch;
        _whitePixel = whitePixel ?? throw new ArgumentNullException(nameof(whitePixel),
            "A 1x1 white texture is required for UI primitive rendering.");
    }

    public void Begin(Matrix4x4? transform = null)
    {
        _transform = transform ?? Matrix4x4.Identity;
        _spriteBatch.Begin(_transform, useLighting: false, layerName: "UI");
    }

    public void End()
    {
        _spriteBatch.End();
    }

    public void DrawRect(Vector2 position, Vector2 size, Vector4 color)
    {
        _spriteBatch.Draw(_whitePixel.Handle, position, size, color);
    }

    public void DrawBorder(Vector2 position, Vector2 size, float thickness, Vector4 color)
    {
        if (thickness <= 0f)
            return;

        var top = new Vector2(size.X, thickness);
        var bottom = new Vector2(size.X, thickness);
        var left = new Vector2(thickness, size.Y);
        var right = new Vector2(thickness, size.Y);

        DrawRect(position, top, color);
        DrawRect(new Vector2(position.X, position.Y + size.Y - thickness), bottom, color);
        DrawRect(position, left, color);
        DrawRect(new Vector2(position.X + size.X - thickness, position.Y), right, color);
    }

    public void Render(UIElement root, Matrix4x4? transform = null)
    {
        Begin(transform);
        root.Draw(this);
        End();
    }
}
