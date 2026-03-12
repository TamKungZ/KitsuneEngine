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
    private string? _nextSceneKey;
    private string? _currentSceneKey;
    private ServiceContainer _services;
    private readonly Dictionary<string, Func<Scene>> _sceneFactories = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SceneMetadata> _sceneMetadata = new(StringComparer.OrdinalIgnoreCase);

    public SceneManager(ServiceContainer services)
    {
        _services = services;
    }

    public event Action<SceneTransitionContext>? SceneChanging;
    public event Action<SceneTransitionContext>? SceneChanged;

    public string? CurrentSceneKey => _currentSceneKey;
    public bool IsTransitioning => _nextScene != null;
    public IReadOnlyCollection<string> RegisteredSceneKeys => _sceneFactories.Keys;

    public void RegisterScene(string key, Func<Scene> factory, SceneMetadata? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Scene key cannot be null or empty.", nameof(key));

        _sceneFactories[key] = factory ?? throw new ArgumentNullException(nameof(factory));
        _sceneMetadata[key] = metadata ?? new SceneMetadata(key);
    }

    public bool UnregisterScene(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        _sceneMetadata.Remove(key);
        return _sceneFactories.Remove(key);
    }

    public bool HasScene(string key) => _sceneFactories.ContainsKey(key);

    public SceneMetadata? GetSceneMetadata(string key)
    {
        if (_sceneMetadata.TryGetValue(key, out var metadata))
            return metadata;

        return null;
    }

    public bool TryLoadScene(string key)
    {
        if (!_sceneFactories.TryGetValue(key, out var factory))
            return false;

        _nextScene = factory();
        _nextSceneKey = key;
        return true;
    }

    public void LoadScene(string key)
    {
        if (!TryLoadScene(key))
            throw new InvalidOperationException($"Scene key '{key}' is not registered.");
    }

    public void LoadScene(Scene scene)
    {
        _nextScene = scene;
        _nextSceneKey = null;
    }

    public void Update(float deltaTime)
    {
        if (_nextScene != null)
        {
            var context = new SceneTransitionContext(_currentSceneKey, _nextSceneKey, _currentScene, _nextScene);
            SceneChanging?.Invoke(context);

            _currentScene?.OnUnload();
            _currentScene = _nextScene;
            _currentSceneKey = _nextSceneKey;
            _currentScene.Initialize(_services);
            _nextScene = null;
            _nextSceneKey = null;

            SceneChanged?.Invoke(context);
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

public sealed class SceneTransitionContext
{
    public string? FromKey { get; }
    public string? ToKey { get; }
    public Scene? FromScene { get; }
    public Scene? ToScene { get; }

    public SceneTransitionContext(string? fromKey, string? toKey, Scene? fromScene, Scene? toScene)
    {
        FromKey = fromKey;
        ToKey = toKey;
        FromScene = fromScene;
        ToScene = toScene;
    }
}

public sealed class SceneMetadata
{
    public string Key { get; }
    public string DisplayName { get; set; }
    public string Category { get; set; } = "General";
    public Dictionary<string, string> Properties { get; } = new(StringComparer.OrdinalIgnoreCase);

    public SceneMetadata(string key, string? displayName = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Scene key cannot be null or empty.", nameof(key));

        Key = key;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? key : displayName;
    }
}
