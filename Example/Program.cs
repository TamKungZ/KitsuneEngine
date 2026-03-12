using K4os.Compression.LZ4;
using KitsuneEngine.Assets;
using KitsuneEngine.Audio;
using KitsuneEngine.Core;
using KitsuneEngine.Graphics;
using KitsuneEngine.Input;
using Silk.NET.Input;
using System.Numerics;

namespace Example;

public class ExampleGame : Game
{
    private SpriteBatch? _spriteBatch;
    private InputManager? _input;
    private Camera2D? _camera;
    private Texture? _buttonTexture;
    private AudioSystem? _audio;
    private bool _audioAvailable;

    private readonly Rectangle _buttonRect = new Rectangle(260, 230, 280, 140);
    private bool _hoverButton;

    private readonly string _assetsSourceDir = Path.Combine(AppContext.BaseDirectory, "AssetsSource");
    private readonly string _assetsPackPath = Path.Combine(AppContext.BaseDirectory, "Assets", "game.kpak");

    public ExampleGame() : base(800, 600, "KitsuneEngine Example - KPAK Menu Test")
    {
    }

    protected override void Initialize()
    {
        Console.WriteLine("Initializing KPAK menu test...");
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GL);
        _input = Services.Get<InputManager>();
        _camera = new Camera2D(Width, Height)
        {
            Position = new Vector2(Width * 0.5f, Height * 0.5f)
        };
        _camera.Update(0f);

        BuildKpakIfNeeded();

        var assetManager = new AssetManager(GL);
        Services.Register(assetManager);

        try
        {
            _audio = new AudioSystem();
            Services.Register(_audio);
            _audioAvailable = true;
        }
        catch (Exception ex)
        {
            _audioAvailable = false;
            Console.WriteLine($"Audio unavailable (OpenAL not found): {ex.Message}");
            Console.WriteLine("Continue without sound. Install OpenAL Soft to enable audio playback.");
        }

        assetManager.Mount(_assetsPackPath);
        _buttonTexture = assetManager.LoadTexture("button.jpg");

        if (_audioAvailable && _audio != null)
        {
            var buttonSound = assetManager.LoadAudio("button.ogg");
            _audio.CreateSource();
            _audio.RegisterClip("button_click", buttonSound);
        }

        Console.WriteLine("KPAK mounted. Main menu button texture + sound loaded from KPAK.");
        Console.WriteLine("Left click the button to play sound. ESC to exit.");
    }

    protected override void Update(float deltaTime)
    {
        var worldMouse = _camera!.ScreenToWorld(_input!.MousePosition);
        _hoverButton = _buttonRect.Contains(worldMouse);

        if (_hoverButton && _input.IsMousePressed(MouseButton.Left))
        {
            if (_audioAvailable && _audio != null)
                _audio.PlaySound("button_click", 0.9f, 1.0f, loop: false);
            Console.WriteLine("[MainMenu] Button clicked!");
        }

        if (_input.IsKeyPressed(Key.Escape))
            Exit();
    }

    protected override void Render(float deltaTime)
    {
        GL.ClearColor(0.08f, 0.08f, 0.12f, 1.0f);

        _spriteBatch!.Begin(_camera!.GetViewProjectionMatrix());

        // background
        _spriteBatch.Draw(
            texture: _buttonTexture!.Handle,
            position: new Vector2(0, 0),
            size: new Vector2(800, 600),
            color: new Vector4(0.12f, 0.12f, 0.18f, 1f)
        );

        // main menu button (image from KPAK)
        _spriteBatch.Draw(
            texture: _buttonTexture.Handle,
            position: new Vector2(_buttonRect.X, _buttonRect.Y),
            size: new Vector2(_buttonRect.Width, _buttonRect.Height),
            color: _hoverButton ? new Vector4(1f, 1f, 1f, 1f) : new Vector4(0.85f, 0.85f, 0.85f, 1f)
        );

        _spriteBatch.End();
    }

    protected override void OnResize(int width, int height)
    {
        _camera?.SetViewport(width, height);
    }

    protected override void UnloadContent()
    {
        _spriteBatch?.Dispose();
    }

    private void BuildKpakIfNeeded()
    {
        var assetsDir = Path.Combine(AppContext.BaseDirectory, "Assets");
        Directory.CreateDirectory(assetsDir);

        // If package already exists beside EXE, use it directly.
        if (File.Exists(_assetsPackPath))
            return;

        EnsureSeedAssetsIfMissing();

        // Candidate source locations (publish output, workspace-relative, cwd-relative)
        var sourceCandidates = new[]
        {
            _assetsSourceDir,
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AssetsSource")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Example", "AssetsSource")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "AssetsSource"))
        };

        var sourceDir = sourceCandidates.FirstOrDefault(Directory.Exists);
        if (string.IsNullOrWhiteSpace(sourceDir))
            throw new DirectoryNotFoundException(
                $"Assets source not found. Checked: {string.Join(" | ", sourceCandidates)}");

        var packer = new KpakPacker().SetCompression(true, LZ4Level.L04_HC);
        packer.Pack(sourceDir, _assetsPackPath);
    }

    private void EnsureSeedAssetsIfMissing()
    {
        if (Directory.Exists(_assetsSourceDir))
            return;

        Directory.CreateDirectory(_assetsSourceDir);

        var imageSource = @"C:\Users\USRE\Downloads\blue-vitamin-pill-vector-illustration-with-glossy-finish.jpg";
        var soundSource = @"G:\Projects\Code\Java\Minecraft\.Work\atm\f-1.20.1\src\main\resources\assets\realcraft_atm\sounds\button.ogg";

        var imageTarget = Path.Combine(_assetsSourceDir, "button.jpg");
        var soundTarget = Path.Combine(_assetsSourceDir, "button.ogg");

        if (File.Exists(imageSource) && !File.Exists(imageTarget))
            File.Copy(imageSource, imageTarget, overwrite: true);

        if (File.Exists(soundSource) && !File.Exists(soundTarget))
            File.Copy(soundSource, soundTarget, overwrite: true);
    }
}

internal static class Program
{
    private static void Main(string[] args)
    {
        using var game = new ExampleGame();
        game.Run();
    }
}
