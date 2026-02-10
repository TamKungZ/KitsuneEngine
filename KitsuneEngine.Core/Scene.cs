namespace KitsuneEngine.Core;

public abstract class Scene
{
    protected ServiceContainer Services { get; private set; } = null!;
    public bool IsActive { get; set; } = true;

    public void Initialize(ServiceContainer services)
    {
        Services = services;
        OnLoad();
    }

    protected abstract void OnLoad();
    public abstract void Update(float deltaTime);
    public abstract void Render(float deltaTime);
    public virtual void OnUnload() { }
    public virtual void OnResize(int width, int height) { }
}

public class SceneManager
{
    private Scene? _currentScene;
    private Scene? _nextScene;
    private ServiceContainer _services;

    public SceneManager(ServiceContainer services)
    {
        _services = services;
    }

    public void LoadScene(Scene scene)
    {
        _nextScene = scene;
    }

    public void Update(float deltaTime)
    {
        if (_nextScene != null)
        {
            _currentScene?.OnUnload();
            _currentScene = _nextScene;
            _currentScene.Initialize(_services);
            _nextScene = null;
        }

        if (_currentScene != null && _currentScene.IsActive)
        {
            _currentScene.Update(deltaTime);
        }
    }

    public void Render(float deltaTime)
    {
        if (_currentScene != null && _currentScene.IsActive)
        {
            _currentScene.Render(deltaTime);
        }
    }

    public T? GetCurrentScene<T>() where T : Scene => _currentScene as T;
}