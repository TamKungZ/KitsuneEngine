using Silk.NET.OpenGL;
using System.Numerics;

namespace KitsuneEngine.Graphics;

public class LightingSystem : IDisposable
{
    private GL _gl;
    private List<Light> _lights = new();
    private const int MAX_LIGHTS = 32;

    public Vector4 AmbientColor { get; set; } = new Vector4(0.2f, 0.2f, 0.3f, 1.0f);

    public LightingSystem(GL gl)
    {
        _gl = gl;
    }

    public Light AddLight(Vector2 position, Vector4 color, float intensity = 1.0f, float radius = 200f)
    {
        var light = new Light
        {
            Position = position,
            Color = color,
            Intensity = intensity,
            Radius = radius,
            IsActive = true
        };

        _lights.Add(light);
        return light;
    }

    public void RemoveLight(Light light)
    {
        _lights.Remove(light);
    }

    public void ClearLights()
    {
        _lights.Clear();
    }

    public void ApplyLights(uint shader)
    {
        _gl.UseProgram(shader);

        var activeLights = _lights.Where(l => l.IsActive).Take(MAX_LIGHTS).ToList();

        _gl.Uniform1(_gl.GetUniformLocation(shader, "uLightCount"), activeLights.Count);
        _gl.Uniform4(_gl.GetUniformLocation(shader, "uAmbientColor"),
            AmbientColor.X, AmbientColor.Y, AmbientColor.Z, AmbientColor.W);

        for (int i = 0; i < activeLights.Count; i++)
        {
            var light = activeLights[i];

            _gl.Uniform2(_gl.GetUniformLocation(shader, $"uLightPositions[{i}]"),
                light.Position.X, light.Position.Y);

            _gl.Uniform4(_gl.GetUniformLocation(shader, $"uLightColors[{i}]"),
                light.Color.X, light.Color.Y, light.Color.Z, light.Color.W);

            _gl.Uniform1(_gl.GetUniformLocation(shader, $"uLightIntensities[{i}]"),
                light.Intensity);

            _gl.Uniform1(_gl.GetUniformLocation(shader, $"uLightRadii[{i}]"),
                light.Radius);
        }
    }

    public void Update(float deltaTime)
    {
        foreach (var light in _lights)
        {
            light.Update(deltaTime);
        }
    }

    public void Dispose()
    {
        _lights.Clear();
    }
}

public class Light
{
    public Vector2 Position { get; set; }
    public Vector4 Color { get; set; }
    public float Intensity { get; set; }
    public float Radius { get; set; }
    public bool IsActive { get; set; }

    // Animation properties
    public bool Flicker { get; set; }
    public float FlickerSpeed { get; set; } = 5.0f;
    public float FlickerAmount { get; set; } = 0.1f;

    private float _baseIntensity;
    private float _flickerTime;

    public Light()
    {
        _baseIntensity = Intensity;
    }

    public void Update(float deltaTime)
    {
        if (Flicker)
        {
            _flickerTime += deltaTime * FlickerSpeed;
            float flickerOffset = MathF.Sin(_flickerTime) * FlickerAmount;
            Intensity = Math.Clamp(_baseIntensity + flickerOffset, 0, 2);
        }
    }

    public void SetFlicker(bool enable, float speed = 5.0f, float amount = 0.1f)
    {
        Flicker = enable;
        FlickerSpeed = speed;
        FlickerAmount = amount;
        _baseIntensity = Intensity;
    }
}