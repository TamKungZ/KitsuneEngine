using Silk.NET.OpenGL;
using System.Numerics;

namespace KitsuneEngine.Graphics;

public class LightingSystem : IDisposable
{
    private GL _gl;
    private readonly List<Light> _lights = new();
    private readonly Dictionary<string, List<Light>> _layerLights = new(StringComparer.Ordinal);
    private const int MAX_LIGHTS = 32;
    public const string DefaultLayer = "Default";

    public Vector4 AmbientColor { get; set; } = new Vector4(0.2f, 0.2f, 0.3f, 1.0f);

    // Specular tuning for shaders
    public float SpecularPower { get; set; } = 32f;
    public float SpecularIntensity { get; set; } = 0.5f;

    public LightingSystem(GL gl)
    {
        _gl = gl;
        _layerLights[DefaultLayer] = new List<Light>();
    }

    public Light AddLight(Vector2 position, Vector4 color, float intensity = 1.0f, float radius = 200f)
        => AddLight(DefaultLayer, position, color, intensity, radius);

    public Light AddLight(string layerName, Vector2 position, Vector4 color, float intensity = 1.0f, float radius = 200f)
    {
        if (string.IsNullOrWhiteSpace(layerName))
            layerName = DefaultLayer;

        var light = new Light
        {
            Position = position,
            Color = color,
            Intensity = intensity,
            Radius = radius,
            IsActive = true,
            Layer = layerName
        };

        _lights.Add(light);
        GetOrCreateLayerLights(layerName).Add(light);
        return light;
    }

    public void RemoveLight(Light light)
    {
        _lights.Remove(light);

        if (_layerLights.TryGetValue(light.Layer, out var lights))
            lights.Remove(light);
    }

    public void ClearLights()
    {
        _lights.Clear();
        foreach (var layer in _layerLights.Values)
            layer.Clear();
    }

    public void ApplyLights(uint shader)
        => ApplyLights(shader, null);

    public void ApplyLights(uint shader, string? layerName)
    {
        _gl.UseProgram(shader);

        var activeLights = GetActiveLightsForLayer(layerName).Take(MAX_LIGHTS).ToList();

        int locCount = _gl.GetUniformLocation(shader, "uLightCount");
        if (locCount >= 0)
            _gl.Uniform1(locCount, activeLights.Count);

        int locAmb = _gl.GetUniformLocation(shader, "uAmbientColor");
        if (locAmb >= 0)
            _gl.Uniform4(locAmb, AmbientColor.X, AmbientColor.Y, AmbientColor.Z, AmbientColor.W);

        // specular settings
        int locSpecPow = _gl.GetUniformLocation(shader, "uSpecularPower");
        if (locSpecPow >= 0)
            _gl.Uniform1(locSpecPow, SpecularPower);

        int locSpecInt = _gl.GetUniformLocation(shader, "uSpecularIntensity");
        if (locSpecInt >= 0)
            _gl.Uniform1(locSpecInt, SpecularIntensity);

        for (int i = 0; i < activeLights.Count; i++)
        {
            var light = activeLights[i];

            int locPos = _gl.GetUniformLocation(shader, $"uLightPositions[{i}]");
            if (locPos >= 0)
                _gl.Uniform2(locPos, light.Position.X, light.Position.Y);

            int locCol = _gl.GetUniformLocation(shader, $"uLightColors[{i}]");
            if (locCol >= 0)
                _gl.Uniform4(locCol, light.Color.X, light.Color.Y, light.Color.Z, light.Color.W);

            int locInt = _gl.GetUniformLocation(shader, $"uLightIntensities[{i}]");
            if (locInt >= 0)
                _gl.Uniform1(locInt, light.Intensity);

            int locRad = _gl.GetUniformLocation(shader, $"uLightRadii[{i}]");
            if (locRad >= 0)
                _gl.Uniform1(locRad, light.Radius);
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
        _layerLights.Clear();
    }

    public void EnsureLayer(string layerName)
    {
        if (string.IsNullOrWhiteSpace(layerName))
            return;

        GetOrCreateLayerLights(layerName);
    }

    public bool RemoveLayer(string layerName)
    {
        if (string.IsNullOrWhiteSpace(layerName) || string.Equals(layerName, DefaultLayer, StringComparison.Ordinal))
            return false;

        if (!_layerLights.TryGetValue(layerName, out var lights))
            return false;

        foreach (var light in lights)
            _lights.Remove(light);

        return _layerLights.Remove(layerName);
    }

    private IEnumerable<Light> GetActiveLightsForLayer(string? layerName)
    {
        if (string.IsNullOrWhiteSpace(layerName))
            return _lights.Where(l => l.IsActive);

        if (_layerLights.TryGetValue(layerName, out var lights))
            return lights.Where(l => l.IsActive);

        // fallback behavior: unknown layer returns global active lights
        return _lights.Where(l => l.IsActive);
    }

    private List<Light> GetOrCreateLayerLights(string layerName)
    {
        if (!_layerLights.TryGetValue(layerName, out var lights))
        {
            lights = new List<Light>();
            _layerLights[layerName] = lights;
        }

        return lights;
    }
}

public class Light
{
    public Vector2 Position { get; set; }
    public Vector4 Color { get; set; }
    public float Intensity { get; set; }
    public float Radius { get; set; }
    public bool IsActive { get; set; }
    public string Layer { get; set; } = LightingSystem.DefaultLayer;

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
