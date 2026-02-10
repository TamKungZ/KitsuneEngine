using KitsuneEngine.Core;
using KitsuneEngine.Graphics;
using KitsuneEngine.Input;
using System.Numerics;

namespace Example;

public class ExampleGame : Game
{
    private SpriteBatch? _spriteBatch;
    private Texture? _whiteTexture;
    private InputManager? _input;
    private Camera2D? _camera;
    
    private Vector2 _playerPosition = new Vector2(400, 300);
    private float _playerSpeed = 200f;

    public ExampleGame() : base(800, 600, "KitsuneEngine Example")
    {
    }

    protected override void Initialize()
    {
        Console.WriteLine("Initializing KitsuneEngine Example...");
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GL);
        _whiteTexture = Texture.CreateWhiteTexture(GL);
        _input = Services.Get<InputManager>();
        _camera = new Camera2D(Width, Height);
        
        Console.WriteLine("Content loaded!");
    }

    protected override void Update(float deltaTime)
    {
        // Get input
        var movement = _input!.GetMovementVector();
        
        // Move player
        _playerPosition += movement * _playerSpeed * deltaTime;
        
        // Camera follows player
        _camera!.Follow(_playerPosition, 0.1f);
        _camera.Update(deltaTime);
        
        // Exit on ESC
        if (_input.IsKeyPressed(Silk.NET.Input.Key.Escape))
        {
            Exit();
        }
    }

    protected override void Render(float deltaTime)
    {
        // Clear screen
        GL.ClearColor(0.1f, 0.1f, 0.15f, 1.0f);
        
        // Begin batch with camera
        _spriteBatch!.Begin(_camera!.GetViewProjectionMatrix());
        
        // Draw player (white square)
        _spriteBatch.Draw(
            _whiteTexture!.Handle,
            _playerPosition,
            new Vector2(32, 32),
            new Vector4(1, 1, 1, 1),
            origin: new Vector2(16, 16)
        );
        
        // Draw some background tiles
        for (int x = 0; x < 50; x++)
        {
            for (int y = 0; y < 50; y++)
            {
                var color = (x + y) % 2 == 0 
                    ? new Vector4(0.2f, 0.2f, 0.25f, 1) 
                    : new Vector4(0.15f, 0.15f, 0.2f, 1);
                
                _spriteBatch.Draw(
                    _whiteTexture!.Handle,
                    new Vector2(x * 32, y * 32),
                    new Vector2(32, 32),
                    color
                );
            }
        }
        
        _spriteBatch.End();
    }

    protected override void UnloadContent()
    {
        _spriteBatch?.Dispose();
        _whiteTexture?.Dispose();
    }

    protected override void OnResize(int width, int height)
    {
        _camera?.SetViewport(width, height);
    }
}

// Entry point
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Starting KitsuneEngine Example...");
        Console.WriteLine("Controls: WASD or Arrow Keys to move, ESC to exit");
        Console.WriteLine();
        
        using var game = new ExampleGame();
        game.Run();
    }
}
