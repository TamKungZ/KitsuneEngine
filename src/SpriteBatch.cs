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

    [StructLayout(LayoutKind.Sequential)]
    private struct Vertex
    {
        public Vector2 Position;
        public Vector2 TexCoord;
        public Vector4 Color;
        public float TexIndex;
        public Vector2 Normal; // For normal mapping
    }

    public SpriteBatch(GL gl)
    {
        _gl = gl;
        _vertices = new Vertex[_maxSprites * 4];

        CreateBuffers();
        CreateShaders();
    }

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

        // Position
        _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, (uint)sizeof(Vertex), (void*)0);
        _gl.EnableVertexAttribArray(0);

        // TexCoord
        _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, (uint)sizeof(Vertex), (void*)(2 * sizeof(float)));
        _gl.EnableVertexAttribArray(1);

        // Color
        _gl.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, (uint)sizeof(Vertex), (void*)(4 * sizeof(float)));
        _gl.EnableVertexAttribArray(2);

        // TexIndex
        _gl.VertexAttribPointer(3, 1, VertexAttribPointerType.Float, false, (uint)sizeof(Vertex), (void*)(8 * sizeof(float)));
        _gl.EnableVertexAttribArray(3);

        // Normal
        _gl.VertexAttribPointer(4, 2, VertexAttribPointerType.Float, false, (uint)sizeof(Vertex), (void*)(9 * sizeof(float)));
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
            layout (location = 4) in vec2 aNormal;
            
            uniform mat4 uTransform;
            
            out vec2 TexCoord;
            out vec4 Color;
            out float TexIndex;
            out vec2 FragPos;
            out vec2 Normal;
            
            void main()
            {
                vec4 worldPos = uTransform * vec4(aPosition, 0.0, 1.0);
                gl_Position = worldPos;
                FragPos = aPosition;
                TexCoord = aTexCoord;
                Color = aColor;
                TexIndex = aTexIndex;
                Normal = aNormal;
            }
        ";

        string fragmentCode = @"
            #version 330 core
            in vec2 TexCoord;
            in vec4 Color;
            in float TexIndex;
            in vec2 FragPos;
            in vec2 Normal;
            
            uniform sampler2D uTextures[16];
            
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
            layout (location = 4) in vec2 aNormal;
            
            uniform mat4 uTransform;
            
            out vec2 TexCoord;
            out vec4 Color;
            out float TexIndex;
            out vec2 FragPos;
            out vec2 Normal;
            
            void main()
            {
                vec4 worldPos = uTransform * vec4(aPosition, 0.0, 1.0);
                gl_Position = worldPos;
                FragPos = aPosition;
                TexCoord = aTexCoord;
                Color = aColor;
                TexIndex = aTexIndex;
                Normal = aNormal;
            }
        ";

        string fragmentCode = @"
            #version 330 core
            in vec2 TexCoord;
            in vec4 Color;
            in float TexIndex;
            in vec2 FragPos;
            in vec2 Normal;
            
            uniform sampler2D uTextures[16];
            uniform vec4 uAmbientColor;
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
                
                vec3 ambient = uAmbientColor.rgb * uAmbientColor.a;
                vec3 lighting = ambient;
                
                for (int i = 0; i < uLightCount; i++)
                {
                    vec2 lightDir = uLightPositions[i] - FragPos;
                    float distance = length(lightDir);
                    
                    if (distance < uLightRadii[i])
                    {
                        float attenuation = 1.0 - (distance / uLightRadii[i]);
                        attenuation = pow(attenuation, 2.0);
                        
                        vec3 lightContribution = uLightColors[i].rgb * uLightIntensities[i] * attenuation;
                        lighting += lightContribution;
                    }
                }
                
                lighting = clamp(lighting, 0.0, 1.0);
                FragColor = texColor * Color * vec4(lighting, 1.0);
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
        int[] samplers = new int[16];
        for (int i = 0; i < 16; i++) samplers[i] = i;
        _gl.Uniform1(_gl.GetUniformLocation(program, "uTextures"), samplers);

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

    public void Draw(uint texture, Vector2 position, Vector2 size, Vector4 color,
                     Vector2? sourcePos = null, Vector2? sourceSize = null,
                     float rotation = 0, Vector2? origin = null)
    {
        if (!_begun) throw new InvalidOperationException("Begin() must be called before Draw()");
        if (_spriteCount >= _maxSprites) Flush();

        if (!_textureSlots.TryGetValue(texture, out int texSlot))
        {
            if (_currentTextureSlot >= 16) Flush();
            texSlot = _currentTextureSlot++;
            _textureSlots[texture] = texSlot;
        }

        Vector2 orig = origin ?? Vector2.Zero;
        Vector2 texMin = sourcePos ?? Vector2.Zero;
        Vector2 texMax = sourceSize ?? Vector2.One;

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
        _vertices[idx + 0] = new Vertex { Position = corners[0], TexCoord = new Vector2(texMin.X, texMin.Y), Color = color, TexIndex = texSlot };
        _vertices[idx + 1] = new Vertex { Position = corners[1], TexCoord = new Vector2(texMax.X, texMin.Y), Color = color, TexIndex = texSlot };
        _vertices[idx + 2] = new Vertex { Position = corners[2], TexCoord = new Vector2(texMax.X, texMax.Y), Color = color, TexIndex = texSlot };
        _vertices[idx + 3] = new Vertex { Position = corners[3], TexCoord = new Vector2(texMin.X, texMax.Y), Color = color, TexIndex = texSlot };

        _spriteCount++;
    }

    private Vector2 RotatePoint(Vector2 point, float cos, float sin)
    {
        return new Vector2(
            point.X * cos - point.Y * sin,
            point.X * sin + point.Y * cos
        );
    }

    public void SetAmbientLight(Vector4 color) => _ambientColor = color;

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

        fixed (float* ptr = &_transform.M11)
        {
            _gl.UniformMatrix4(_gl.GetUniformLocation(_currentShader, "uTransform"), 1, false, ptr);
        }

        if (_useLighting)
        {
            _gl.Uniform4(_gl.GetUniformLocation(_currentShader, "uAmbientColor"), _ambientColor.X, _ambientColor.Y, _ambientColor.Z, _ambientColor.W);
        }

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