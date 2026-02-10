using Silk.NET.OpenGL;
using System.Numerics;
using System.Runtime.InteropServices;

namespace KitsuneEngine.Graphics;

public class SpriteBatch : IDisposable
{
    private readonly GL _gl;
    private uint _vao, _vbo, _ebo;
    private uint _defaultShader;
    private uint _lightingShader;
    private uint _currentShader;
    private int _maxSprites = 10000;
    private Vertex[] _vertices;
    private int _spriteCount;
    private Matrix4x4 _transform = Matrix4x4.Identity;
    private Dictionary<uint, int> _textureSlots = new();
    private int _currentTextureSlot = 0;
    private bool _begun;

    // Lighting support
    private bool _useLighting;
    private Vector4 _ambientColor = new Vector4(0.3f, 0.3f, 0.3f, 1.0f);

    // Reference to LightingSystem (optional)
    private LightingSystem? _lightingSystem;

    // Tone control
    private Vector3 _tone = Vector3.One;
    private float _exposure = 1.0f;

    private const int MAX_TEXTURE_SLOTS = 16;

    [StructLayout(LayoutKind.Sequential)]
    private struct Vertex
    {
        public Vector2 Position;  // 2 floats
        public Vector2 TexCoord;  // 2 floats
        public Vector4 Color;     // 4 floats
        public float TexIndex;    // 1 float (diffuse sampler index)
        public float NormalIndex; // 1 float (normal sampler index, -1 if none)
    }

    public SpriteBatch(GL gl)
    {
        _gl = gl;
        _vertices = new Vertex[_maxSprites * 4];

        CreateBuffers();
        CreateShaders();
    }

    public void SetLightingSystem(LightingSystem lightingSystem)
    {
        _lightingSystem = lightingSystem;
    }

    public void SetTone(Vector3 tone) => _tone = tone;
    public void SetExposure(float exposure) => _exposure = exposure;
    public void SetAmbientLight(Vector4 color) => _ambientColor = color;

    private unsafe void CreateBuffers()
    {
        _vao = _gl.GenVertexArray();
        _gl.BindVertexArray(_vao);

        _vbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(_maxSprites * 4 * sizeof(Vertex)), null, BufferUsageARB.DynamicDraw);

        var indices = new uint[_maxSprites * 6];
        for (uint i = 0; i < _maxSprites; i++)
        {
            indices[i * 6 + 0] = i * 4 + 0;
            indices[i * 6 + 1] = i * 4 + 1;
            indices[i * 6 + 2] = i * 4 + 2;
            indices[i * 6 + 3] = i * 4 + 2;
            indices[i * 6 + 4] = i * 4 + 3;
            indices[i * 6 + 5] = i * 4 + 0;
        }

        _ebo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        fixed (uint* ptr = indices)
        {
            _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(indices.Length * sizeof(uint)), ptr, BufferUsageARB.StaticDraw);
        }

        // Attribute layout based on Vertex struct:
        // Position (location=0) vec2  offset 0
        // TexCoord (1) vec2          offset 2 * sizeof(float)
        // Color (2) vec4             offset 4 * sizeof(float)
        // TexIndex (3) float         offset 8 * sizeof(float)
        // NormalIndex (4) float      offset 9 * sizeof(float)

        uint stride = (uint)sizeof(Vertex);

        // Position
        _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, (void*)0);
        _gl.EnableVertexAttribArray(0);

        // TexCoord
        _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, (void*)(2 * sizeof(float)));
        _gl.EnableVertexAttribArray(1);

        // Color
        _gl.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, stride, (void*)(4 * sizeof(float)));
        _gl.EnableVertexAttribArray(2);

        // TexIndex
        _gl.VertexAttribPointer(3, 1, VertexAttribPointerType.Float, false, stride, (void*)(8 * sizeof(float)));
        _gl.EnableVertexAttribArray(3);

        // NormalIndex
        _gl.VertexAttribPointer(4, 1, VertexAttribPointerType.Float, false, stride, (void*)(9 * sizeof(float)));
        _gl.EnableVertexAttribArray(4);

        _gl.BindVertexArray(0);
    }

    private void CreateShaders()
    {
        _defaultShader = CreateDefaultShader();
        _lightingShader = CreateLightingShader();
        _currentShader = _defaultShader;
    }

    private uint CreateDefaultShader()
    {
        string vertexCode = @"
            #version 330 core
            layout (location = 0) in vec2 aPosition;
            layout (location = 1) in vec2 aTexCoord;
            layout (location = 2) in vec4 aColor;
            layout (location = 3) in float aTexIndex;
            layout (location = 4) in float aNormalIndex;
            
            uniform mat4 uTransform;
            
            out vec2 TexCoord;
            out vec4 Color;
            out float TexIndex;
            out float NormalIndex;
            out vec2 FragPos;
            
            void main()
            {
                vec4 worldPos = uTransform * vec4(aPosition, 0.0, 1.0);
                gl_Position = worldPos;
                FragPos = aPosition;
                TexCoord = aTexCoord;
                Color = aColor;
                TexIndex = aTexIndex;
                NormalIndex = aNormalIndex;
            }
        ";

        string fragmentCode = @"
            #version 330 core
            in vec2 TexCoord;
            in vec4 Color;
            in float TexIndex;
            in float NormalIndex;
            in vec2 FragPos;
            
            uniform sampler2D uTextures[16];
            uniform sampler2D uNormalMaps[16];
            
            out vec4 FragColor;
            
            void main()
            {
                int index = int(TexIndex);
                vec4 texColor = texture(uTextures[index], TexCoord);
                FragColor = texColor * Color;
            }
        ";

        return CompileShaderProgram(vertexCode, fragmentCode);
    }

    private uint CreateLightingShader()
    {
        string vertexCode = @"
            #version 330 core
            layout (location = 0) in vec2 aPosition;
            layout (location = 1) in vec2 aTexCoord;
            layout (location = 2) in vec4 aColor;
            layout (location = 3) in float aTexIndex;
            layout (location = 4) in float aNormalIndex;
            
            uniform mat4 uTransform;
            
            out vec2 TexCoord;
            out vec4 Color;
            out float TexIndex;
            out float NormalIndex;
            out vec2 FragPos;
            
            void main()
            {
                vec4 worldPos = uTransform * vec4(aPosition, 0.0, 1.0);
                gl_Position = worldPos;
                FragPos = aPosition;
                TexCoord = aTexCoord;
                Color = aColor;
                TexIndex = aTexIndex;
                NormalIndex = aNormalIndex;
            }
        ";

        string fragmentCode = @"
            #version 330 core
            in vec2 TexCoord;
            in vec4 Color;
            in float TexIndex;
            in float NormalIndex;
            in vec2 FragPos;
            
            uniform sampler2D uTextures[16];
            uniform sampler2D uNormalMaps[16];

            uniform vec4 uAmbientColor;
            uniform vec3 uTone;
            uniform float uExposure;
            uniform float uSpecularPower;
            uniform float uSpecularIntensity;

            // lights
            uniform vec2 uLightPositions[32];
            uniform vec4 uLightColors[32];
            uniform float uLightIntensities[32];
            uniform float uLightRadii[32];
            uniform int uLightCount;

            out vec4 FragColor;
            
            void main()
            {
                int index = int(TexIndex);
                vec4 texColor = texture(uTextures[index], TexCoord);

                // compute normal: if NormalIndex < 0 use default (0,0,1)
                vec3 N = vec3(0.0, 0.0, 1.0);
                if (NormalIndex >= 0.0)
                {
                    int nidx = int(NormalIndex);
                    vec3 nSample = texture(uNormalMaps[nidx], TexCoord).rgb;
                    N = normalize(nSample * 2.0 - 1.0);
                }

                vec3 ambient = uAmbientColor.rgb * uAmbientColor.a;
                vec3 lighting = ambient;

                vec3 viewDir = vec3(0.0, 0.0, 1.0); // orthographic 2D camera, view towards +Z

                for (int i = 0; i < uLightCount; i++)
                {
                    vec2 lightPos = uLightPositions[i];
                    vec2 lightDir2 = lightPos - FragPos;
                    float distance = length(lightDir2);

                    if (distance < uLightRadii[i])
                    {
                        float attenuation = 1.0 - (distance / uLightRadii[i]);
                        attenuation = attenuation * attenuation; // quadratic falloff

                        vec3 L = normalize(vec3(lightDir2, 0.0));
                        float diff = max(dot(N, L), 0.0);

                        // specular (Blinn-Phong)
                        vec3 H = normalize(L + viewDir);
                        float spec = pow(max(dot(N, H), 0.0), uSpecularPower) * uSpecularIntensity;

                        vec3 lightContribution = uLightColors[i].rgb * (uLightIntensities[i] * (diff + spec)) * attenuation;
                        lighting += lightContribution;
                    }
                }

                lighting = clamp(lighting, 0.0, 1.0);

                vec3 color = (texColor.rgb * Color.rgb) * lighting;

                // apply tone and exposure
                color = color * uTone;
                color = vec3(1.0) - exp(-color * uExposure); // simple exposure mapping

                FragColor = vec4(color, texColor.a * Color.a);
            }
        ";

        return CompileShaderProgram(vertexCode, fragmentCode);
    }

    private uint CompileShaderProgram(string vertexCode, string fragmentCode)
    {
        uint vertexShader = CompileShader(ShaderType.VertexShader, vertexCode);
        uint fragmentShader = CompileShader(ShaderType.FragmentShader, fragmentCode);

        uint program = _gl.CreateProgram();
        _gl.AttachShader(program, vertexShader);
        _gl.AttachShader(program, fragmentShader);
        _gl.LinkProgram(program);

        _gl.DeleteShader(vertexShader);
        _gl.DeleteShader(fragmentShader);

        _gl.UseProgram(program);
        int[] samplers = new int[MAX_TEXTURE_SLOTS];
        for (int i = 0; i < MAX_TEXTURE_SLOTS; i++) samplers[i] = i;

        int locTex = _gl.GetUniformLocation(program, "uTextures");
        if (locTex >= 0)
            _gl.Uniform1(locTex, samplers);

        int locNormal = _gl.GetUniformLocation(program, "uNormalMaps");
        if (locNormal >= 0)
            _gl.Uniform1(locNormal, samplers);

        return program;
    }

    private uint CompileShader(ShaderType type, string source)
    {
        uint shader = _gl.CreateShader(type);
        _gl.ShaderSource(shader, source);
        _gl.CompileShader(shader);

        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int success);
        if (success == 0)
        {
            string log = _gl.GetShaderInfoLog(shader);
            throw new Exception($"Shader compilation error: {log}");
        }

        return shader;
    }

    public void Begin(Matrix4x4? transform = null, bool useLighting = false)
    {
        _begun = true;
        _spriteCount = 0;
        _textureSlots.Clear();
        _currentTextureSlot = 0;
        _transform = transform ?? Matrix4x4.Identity;
        _useLighting = useLighting;
        _currentShader = useLighting ? _lightingShader : _defaultShader;
    }

    // Added optional normalTexture parameter. Pass 0 for none / omit parameter.
    public void Draw(uint texture, Vector2 position, Vector2 size, Vector4 color,
                     Vector2? sourcePos = null, Vector2? sourceSize = null,
                     float rotation = 0, Vector2? origin = null, uint? normalTexture = null)
    {
        if (!_begun) throw new InvalidOperationException("Begin() must be called before Draw()");
        if (_spriteCount >= _maxSprites) Flush();

        Vector2 orig = origin ?? Vector2.Zero;
        Vector2 texMin = sourcePos ?? Vector2.Zero;
        Vector2 texMax = sourceSize ?? Vector2.One;

        // ensure there is room in texture slots for any new textures we need to add
        int need = 0;
        if (!_textureSlots.ContainsKey(texture)) need++;
        if (normalTexture.HasValue && !_textureSlots.ContainsKey(normalTexture.Value)) need++;
        if (_currentTextureSlot + need > MAX_TEXTURE_SLOTS)
            Flush();

        // diffuse slot
        if (!_textureSlots.TryGetValue(texture, out int texSlot))
        {
            texSlot = _currentTextureSlot++;
            _textureSlots[texture] = texSlot;
        }

        int normalSlot = -1;
        if (normalTexture.HasValue)
        {
            uint ntex = normalTexture.Value;
            if (!_textureSlots.TryGetValue(ntex, out normalSlot))
            {
                normalSlot = _currentTextureSlot++;
                _textureSlots[ntex] = normalSlot;
            }
        }

        // Calculate corners with rotation
        Vector2[] corners = new Vector2[4];
        if (rotation != 0)
        {
            float cos = MathF.Cos(rotation);
            float sin = MathF.Sin(rotation);

            corners[0] = RotatePoint(new Vector2(0, 0) - orig, cos, sin) + position;
            corners[1] = RotatePoint(new Vector2(size.X, 0) - orig, cos, sin) + position;
            corners[2] = RotatePoint(new Vector2(size.X, size.Y) - orig, cos, sin) + position;
            corners[3] = RotatePoint(new Vector2(0, size.Y) - orig, cos, sin) + position;
        }
        else
        {
            corners[0] = position - orig;
            corners[1] = position + new Vector2(size.X, 0) - orig;
            corners[2] = position + size - orig;
            corners[3] = position + new Vector2(0, size.Y) - orig;
        }

        int idx = _spriteCount * 4;
        float nIndex = normalSlot >= 0 ? (float)normalSlot : -1.0f;
        _vertices[idx + 0] = new Vertex { Position = corners[0], TexCoord = new Vector2(texMin.X, texMin.Y), Color = color, TexIndex = texSlot, NormalIndex = nIndex };
        _vertices[idx + 1] = new Vertex { Position = corners[1], TexCoord = new Vector2(texMax.X, texMin.Y), Color = color, TexIndex = texSlot, NormalIndex = nIndex };
        _vertices[idx + 2] = new Vertex { Position = corners[2], TexCoord = new Vector2(texMax.X, texMax.Y), Color = color, TexIndex = texSlot, NormalIndex = nIndex };
        _vertices[idx + 3] = new Vertex { Position = corners[3], TexCoord = new Vector2(texMin.X, texMax.Y), Color = color, TexIndex = texSlot, NormalIndex = nIndex };

        _spriteCount++;
    }

    private Vector2 RotatePoint(Vector2 point, float cos, float sin)
    {
        return new Vector2(
            point.X * cos - point.Y * sin,
            point.X * sin + point.Y * cos
        );
    }

    public void End()
    {
        if (!_begun) return;
        Flush();
        _begun = false;
    }

    private unsafe void Flush()
    {
        if (_spriteCount == 0) return;

        _gl.UseProgram(_currentShader);

        // set transform
        fixed (float* ptr = &_transform.M11)
        {
            int loc = _gl.GetUniformLocation(_currentShader, "uTransform");
            if (loc >= 0)
                _gl.UniformMatrix4(loc, 1, false, ptr);
        }

        // ambient color
        if (_useLighting)
        {
            int locAmb = _gl.GetUniformLocation(_currentShader, "uAmbientColor");
            if (locAmb >= 0)
                _gl.Uniform4(locAmb, _ambientColor.X, _ambientColor.Y, _ambientColor.Z, _ambientColor.W);

            // tone/exposure/specular: set if uniforms present
            int locTone = _gl.GetUniformLocation(_currentShader, "uTone");
            if (locTone >= 0)
                _gl.Uniform3(locTone, _tone.X, _tone.Y, _tone.Z);

            int locExp = _gl.GetUniformLocation(_currentShader, "uExposure");
            if (locExp >= 0)
                _gl.Uniform1(locExp, _exposure);

            // let lighting system upload lights + specular params
            _lightingSystem?.ApplyLights(_currentShader);
        }

        // bind textures (both diffuse and normal maps are stored in same _textureSlots dictionary)
        foreach (var kvp in _textureSlots)
        {
            _gl.ActiveTexture(TextureUnit.Texture0 + kvp.Value);
            _gl.BindTexture(TextureTarget.Texture2D, kvp.Key);
        }

        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        fixed (Vertex* ptr = _vertices)
        {
            _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(_spriteCount * 4 * sizeof(Vertex)), ptr);
        }

        _gl.DrawElements(PrimitiveType.Triangles, (uint)(_spriteCount * 6), DrawElementsType.UnsignedInt, null);

        _spriteCount = 0;
        _textureSlots.Clear();
        _currentTextureSlot = 0;
    }

    public void Dispose()
    {
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteBuffer(_ebo);
        _gl.DeleteProgram(_defaultShader);
        _gl.DeleteProgram(_lightingShader);
    }
}