using System.Numerics;

namespace KitsuneEngine.Graphics;

public class Camera2D
{
    public Vector2 Position { get; set; }
    public float Rotation { get; set; }
    public float Zoom { get; set; } = 1.0f;
    public Vector2 Origin { get; set; }

    private int _viewportWidth;
    private int _viewportHeight;
    private Matrix4x4 _viewMatrix;
    private Matrix4x4 _projectionMatrix;
    private Matrix4x4 _viewProjectionMatrix;
    private bool _isDirty = true;

    // Camera bounds for world limits
    public Rectangle? Bounds { get; set; }

    public Camera2D(int viewportWidth, int viewportHeight)
    {
        _viewportWidth = viewportWidth;
        _viewportHeight = viewportHeight;
        Origin = new Vector2(viewportWidth / 2f, viewportHeight / 2f);
        UpdateMatrices();
    }

    public void Update(float deltaTime)
    {
        if (_isDirty)
        {
            UpdateMatrices();
            _isDirty = false;
        }

        // Apply bounds if set
        if (Bounds.HasValue)
        {
            var bounds = Bounds.Value;
            var halfWidth = (_viewportWidth / Zoom) / 2f;
            var halfHeight = (_viewportHeight / Zoom) / 2f;

            Position = new Vector2(
                Math.Clamp(Position.X, bounds.X + halfWidth, bounds.X + bounds.Width - halfWidth),
                Math.Clamp(Position.Y, bounds.Y + halfHeight, bounds.Y + bounds.Height - halfHeight)
            );
        }
    }

    private void UpdateMatrices()
    {
        // View matrix (camera transform)
        _viewMatrix = Matrix4x4.CreateTranslation(-Position.X, -Position.Y, 0) *
                      Matrix4x4.CreateRotationZ(-Rotation) *
                      Matrix4x4.CreateScale(Zoom, Zoom, 1) *
                      Matrix4x4.CreateTranslation(Origin.X, Origin.Y, 0);

        // Orthographic projection
        _projectionMatrix = Matrix4x4.CreateOrthographicOffCenter(
            0, _viewportWidth, _viewportHeight, 0, -1, 1);

        _viewProjectionMatrix = _viewMatrix * _projectionMatrix;
    }

    public Matrix4x4 GetViewMatrix() => _viewMatrix;
    public Matrix4x4 GetProjectionMatrix() => _projectionMatrix;
    public Matrix4x4 GetViewProjectionMatrix()
    {
        if (_isDirty) UpdateMatrices();
        return _viewProjectionMatrix;
    }

    public void SetViewport(int width, int height)
    {
        _viewportWidth = width;
        _viewportHeight = height;
        Origin = new Vector2(width / 2f, height / 2f);
        _isDirty = true;
    }

    // Convert screen space to world space
    public Vector2 ScreenToWorld(Vector2 screenPosition)
    {
        if (_isDirty) UpdateMatrices();

        Matrix4x4.Invert(_viewProjectionMatrix, out var inverse);
        var worldPos = Vector2.Transform(screenPosition, inverse);
        return worldPos;
    }

    // Convert world space to screen space
    public Vector2 WorldToScreen(Vector2 worldPosition)
    {
        if (_isDirty) UpdateMatrices();
        return Vector2.Transform(worldPosition, _viewProjectionMatrix);
    }

    // Camera movement helpers
    public void Follow(Vector2 target, float lerp = 1.0f)
    {
        Position = Vector2.Lerp(Position, target, lerp);
        _isDirty = true;
    }

    public void LookAt(Vector2 target)
    {
        Position = target;
        _isDirty = true;
    }

    public void Move(Vector2 offset)
    {
        Position += offset;
        _isDirty = true;
    }

    public void ZoomTo(float zoom, float lerp = 1.0f)
    {
        Zoom = MathHelper.Lerp(Zoom, zoom, lerp);
        _isDirty = true;
    }
}

public struct Rectangle
{
    public float X, Y, Width, Height;

    public Rectangle(float x, float y, float width, float height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public bool Contains(Vector2 point) =>
        point.X >= X && point.X <= X + Width &&
        point.Y >= Y && point.Y <= Y + Height;

    public bool Intersects(Rectangle other) =>
        X < other.X + other.Width && X + Width > other.X &&
        Y < other.Y + other.Height && Y + Height > other.Y;
}

public static class MathHelper
{
    public static float Lerp(float a, float b, float t) =>
        a + (b - a) * Math.Clamp(t, 0, 1);

    public static Vector2 Lerp(Vector2 a, Vector2 b, float t) =>
        new Vector2(Lerp(a.X, b.X, t), Lerp(a.Y, b.Y, t));
}