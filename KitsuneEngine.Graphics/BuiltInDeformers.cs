using System.Numerics;

namespace KitsuneEngine.Graphics;

public sealed class WaveDeformer : IMeshDeformer
{
    public string Name => "Wave";
    public bool Enabled { get; set; } = true;

    public float Amplitude { get; set; } = 0.25f;
    public float Frequency { get; set; } = 2.5f;
    public float Speed { get; set; } = 2f;
    public Vector2 Direction { get; set; } = Vector2.UnitX;

    public void Apply(DeformationMesh mesh, float timeSeconds, float deltaTimeSeconds)
    {
        var dir = Direction;
        if (dir.LengthSquared() <= 0f)
            dir = Vector2.UnitX;
        else
            dir = Vector2.Normalize(dir);

        for (int i = 0; i < mesh.WorkingVertices.Length; i++)
        {
            var baseV = mesh.WorkingVertices[i];
            float phase = Vector2.Dot(new Vector2(baseV.Position.X, baseV.Position.Y), dir) * Frequency + timeSeconds * Speed;
            float offset = MathF.Sin(phase) * Amplitude;

            baseV.Position.Z += offset;
            mesh.WorkingVertices[i] = baseV;
        }
    }
}

public sealed class TwistDeformer : IMeshDeformer
{
    public string Name => "Twist";
    public bool Enabled { get; set; } = true;

    public float Strength { get; set; } = 0.8f;
    public float Bias { get; set; } = 0f;

    public void Apply(DeformationMesh mesh, float timeSeconds, float deltaTimeSeconds)
    {
        for (int i = 0; i < mesh.WorkingVertices.Length; i++)
        {
            var v = mesh.WorkingVertices[i];
            float t = v.Position.Y + Bias;
            float angle = t * Strength;

            float c = MathF.Cos(angle);
            float s = MathF.Sin(angle);
            float x = v.Position.X;
            float z = v.Position.Z;

            v.Position.X = x * c - z * s;
            v.Position.Z = x * s + z * c;

            mesh.WorkingVertices[i] = v;
        }
    }
}

public sealed class NoiseDeformer : IMeshDeformer
{
    public string Name => "Noise";
    public bool Enabled { get; set; } = true;

    public float Amplitude { get; set; } = 0.15f;
    public float Frequency { get; set; } = 3f;
    public float ScrollSpeed { get; set; } = 0.5f;
    public int Seed { get; set; } = 1337;

    public void Apply(DeformationMesh mesh, float timeSeconds, float deltaTimeSeconds)
    {
        float t = timeSeconds * ScrollSpeed;

        for (int i = 0; i < mesh.WorkingVertices.Length; i++)
        {
            var v = mesh.WorkingVertices[i];
            float nx = v.Position.X * Frequency + Seed * 0.01f;
            float ny = v.Position.Y * Frequency + Seed * 0.02f;
            float noise = HashNoise(nx + t, ny + t);

            v.Position.Z += (noise * 2f - 1f) * Amplitude;
            mesh.WorkingVertices[i] = v;
        }
    }

    private static float HashNoise(float x, float y)
    {
        float n = MathF.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f;
        return n - MathF.Floor(n);
    }
}
