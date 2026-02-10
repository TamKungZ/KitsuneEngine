using Silk.NET.Windowing;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using System.Numerics;

namespace KitsuneEngine.Core;

public abstract class Game : IDisposable
{
    private IWindow _window = null!;
    private GL _gl = null!;
    private IInputContext _input = null!;

    public int Width { get; private set; }
    public int Height { get; private set; }
    public string Title { get; set; }
    public bool StartFullscreen { get; set; } = false;
    public bool StartBorderless { get; set; } = false;
    public bool IsResizable { get; set; } = true;

    protected GL GL => _gl;
    protected double DeltaTime { get; private set; }
    protected double TotalTime { get; private set; }

    public ServiceContainer Services { get; private set; }
    public SceneManager SceneManager { get; private set; } = null!;

    public Game(int width = 1280, int height = 720, string title = "KitsuneEngine Game")
    {
        Width = width;
        Height = height;
        Title = title;
        Services = new ServiceContainer();
    }

    public void Run()
    {
        var options = WindowOptions.Default;
        options.Size = new Silk.NET.Maths.Vector2D<int>(Width, Height);
        options.Title = Title;
        options.VSync = true;
        options.WindowState = StartFullscreen ? WindowState.Fullscreen : WindowState.Normal;
        options.WindowBorder = StartBorderless ? WindowBorder.Hidden :
                              (IsResizable ? WindowBorder.Resizable : WindowBorder.Fixed);

        _window = Window.Create(options);

        _window.Load += OnWindowLoad;
        _window.Update += OnWindowUpdate;
        _window.Render += OnWindowRender;
        _window.Closing += OnWindowClose;
        _window.Resize += OnWindowResize;

        _window.Run();
    }

    public void SetFullscreen(bool borderless = false)
    {
        _window.WindowState = WindowState.Fullscreen;
        _window.WindowBorder = borderless ? WindowBorder.Hidden : WindowBorder.Fixed;
    }

    public void SetWindowed(int width, int height, bool borderless = false, bool resizable = true)
    {
        _window.WindowState = WindowState.Normal;
        _window.WindowBorder = borderless ? WindowBorder.Hidden :
                              (resizable ? WindowBorder.Resizable : WindowBorder.Fixed);
        SetWindowSize(width, height);
    }

    public void SetBorderless(bool borderless)
    {
        _window.WindowBorder = borderless ? WindowBorder.Hidden :
                              (IsResizable ? WindowBorder.Resizable : WindowBorder.Fixed);
    }

    public void SetWindowSize(int width, int height)
    {
        if (_window.WindowState != WindowState.Fullscreen)
        {
            _window.Size = new Silk.NET.Maths.Vector2D<int>(width, height);
        }
    }

    public void ToggleFullscreen(bool borderless = false)
    {
        if (_window.WindowState == WindowState.Fullscreen)
        {
            SetWindowed(Width, Height);
        }
        else
        {
            SetFullscreen(borderless);
        }
    }

    public bool IsFullscreen => _window.WindowState == WindowState.Fullscreen;
    public bool IsBorderless => _window.WindowBorder == WindowBorder.Hidden;
    public (int Width, int Height) WindowSize => (Width, Height);

    private void OnWindowLoad()
    {
        _gl = _window.CreateOpenGL();
        _input = _window.CreateInput();

        _gl.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        Services.Register(_gl);
        Services.Register(_window);
        Services.Register(new KitsuneEngine.Input.InputManager(_input));
        Services.Register(new Time());

        SceneManager = new SceneManager(Services);
        Services.Register(SceneManager);

        Initialize();
        LoadContent();
    }

    private void OnWindowUpdate(double deltaTime)
    {
        DeltaTime = deltaTime;
        TotalTime += deltaTime;

        var time = Services.Get<Time>();
        time.Update((float)deltaTime);

        var input = Services.Get<KitsuneEngine.Input.InputManager>();
        input.Update();

        Update((float)deltaTime);
        SceneManager.Update((float)deltaTime);
    }

    private void OnWindowRender(double deltaTime)
    {
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        Render((float)deltaTime);
        SceneManager.Render((float)deltaTime);
    }

    private void OnWindowResize(Silk.NET.Maths.Vector2D<int> size)
    {
        Width = size.X;
        Height = size.Y;
        _gl.Viewport(0, 0, (uint)Width, (uint)Height);
        OnResize(size.X, size.Y);
    }

    private void OnWindowClose()
    {
        UnloadContent();
        Services.Dispose();
    }

    protected virtual void Initialize() { }
    protected virtual void LoadContent() { }
    protected virtual void Update(float deltaTime) { }
    protected virtual void Render(float deltaTime) { }
    protected virtual void UnloadContent() { }
    protected virtual void OnResize(int width, int height) { }

    public void Exit() => _window?.Close();

    public void Dispose()
    {
        _input?.Dispose();
        _gl?.Dispose();
        _window?.Dispose();
    }
}

public class ServiceContainer : IDisposable
{
    private Dictionary<Type, object> _services = new();

    public void Register<T>(T service) where T : class
    {
        _services[typeof(T)] = service;
    }

    public T Get<T>() where T : class
    {
        if (_services.TryGetValue(typeof(T), out var service))
            return (service as T)!;
        throw new InvalidOperationException($"Service {typeof(T).Name} not registered");
    }

    public bool TryGet<T>(out T? service) where T : class
    {
        if (_services.TryGetValue(typeof(T), out var obj))
        {
            service = obj as T;
            return true;
        }
        service = null;
        return false;
    }

    public void Dispose()
    {
        foreach (var service in _services.Values)
        {
            if (service is IDisposable disposable)
                disposable.Dispose();
        }
        _services.Clear();
    }
}

public class Time
{
    public float DeltaTime { get; private set; }
    public float TotalTime { get; private set; }
    public int FrameCount { get; private set; }
    public float FPS { get; private set; }

    private float _fpsTimer;
    private int _fpsCounter;

    public void Update(float deltaTime)
    {
        DeltaTime = deltaTime;
        TotalTime += deltaTime;
        FrameCount++;

        _fpsTimer += deltaTime;
        _fpsCounter++;

        if (_fpsTimer >= 1.0f)
        {
            FPS = _fpsCounter / _fpsTimer;
            _fpsTimer = 0;
            _fpsCounter = 0;
        }
    }
}