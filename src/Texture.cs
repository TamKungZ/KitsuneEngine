using Silk.NET.OpenGL;
using StbImageSharp;

namespace KitsuneEngine.Graphics;

public class Texture : IDisposable
{
    private readonly GL _gl;
    public uint Handle { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }

    public Texture(GL gl, string path)
    {
        _gl = gl;
        LoadFromFile(path);
    }

    public Texture(GL gl, int width, int height, byte[] data)
    {
        _gl = gl;
        Width = width;
        Height = height;
        CreateTexture(data);
    }

    private unsafe void LoadFromFile(string path)
    {
        StbImage.stbi_set_flip_vertically_on_load(1);

        using var stream = File.OpenRead(path);
        ImageResult image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

        Width = image.Width;
        Height = image.Height;

        CreateTexture(image.Data);
    }

    private unsafe void CreateTexture(byte[] data)
    {
        Handle = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, Handle);

        fixed (byte* ptr = data)
        {
            _gl.TexImage2D(
                TextureTarget.Texture2D,
                0,
                InternalFormat.Rgba,
                (uint)Width,
                (uint)Height,
                0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                ptr
            );
        }

        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);

        _gl.GenerateMipmap(TextureTarget.Texture2D);
        _gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    public void Bind(uint slot = 0)
    {
        _gl.ActiveTexture(TextureUnit.Texture0 + (int)slot);
        _gl.BindTexture(TextureTarget.Texture2D, Handle);
    }

    public static Texture CreateWhiteTexture(GL gl)
    {
        byte[] data = new byte[] { 255, 255, 255, 255 };
        return new Texture(gl, 1, 1, data);
    }

    public void Dispose()
    {
        _gl.DeleteTexture(Handle);
    }
}