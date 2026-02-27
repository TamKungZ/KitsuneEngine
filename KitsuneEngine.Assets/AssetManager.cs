using KitsuneEngine.Audio;
using KitsuneEngine.Graphics;
using Silk.NET.OpenGL;
using System.Collections.Concurrent;

namespace KitsuneEngine.Assets;

// Add alias to resolve Texture conflict
using Texture = KitsuneEngine.Graphics.Texture;

public class AssetManager : IDisposable
{
    private List<KpakLoader> _loaders = new();
    private ConcurrentDictionary<string, object> _cache = new();
    private ConcurrentDictionary<string, Task<object>> _loading = new();
    private GL _gl;

    public bool UseCache { get; set; } = true;
    public int CachedAssets => _cache.Count;

    public AssetManager(GL gl)
    {
        _gl = gl;
    }

    // Mount .kpak files
    public void Mount(string kpakPath)
    {
        var loader = new KpakLoader(kpakPath);
        _loaders.Add(loader);
        Console.WriteLine($"Mounted: {kpakPath} ({loader.FileCount} files)");
    }

    public void MountDirectory(string directory, string pattern = "*.kpak")
    {
        foreach (var file in Directory.GetFiles(directory, pattern))
        {
            Mount(file);
        }
    }

    // Load raw bytes
    public byte[] LoadBytes(string path)
    {
        foreach (var loader in _loaders)
        {
            if (loader.TryLoadAsset(path, out var data))
                return data;
        }

        throw new FileNotFoundException($"Asset not found: {path}");
    }

    public async Task<byte[]> LoadBytesAsync(string path)
    {
        foreach (var loader in _loaders)
        {
            if (loader.ContainsAsset(path))
                return await loader.LoadAssetAsync(path);
        }

        throw new FileNotFoundException($"Asset not found: {path}");
    }

    // Load Texture
    public Texture LoadTexture(string path)
    {
        if (UseCache && _cache.TryGetValue(path, out var cached))
            return (Texture)cached;

        var data = LoadBytes(path);
        var texture = LoadTextureFromBytes(data);

        if (UseCache)
            _cache[path] = texture;

        return texture;
    }

    public async Task<Texture> LoadTextureAsync(string path)
    {
        if (UseCache && _cache.TryGetValue(path, out var cached))
            return (Texture)cached;

        // Check if already loading
        if (_loading.TryGetValue(path, out var loadingTask))
            return (Texture)await loadingTask;

        var task = Task.Run(async () =>
        {
            var data = await LoadBytesAsync(path);

            // OpenGL texture creation must happen on the thread that owns the GL context.
            // Keep async work here limited to file/archive I/O.
            return (object)data;
        });

        _loading[path] = task;

        try
        {
            var result = await task;
            var data = (byte[])result;
            var texture = LoadTextureFromBytes(data);

            if (UseCache)
                _cache[path] = texture;

            return texture;
        }
        finally
        {
            _loading.TryRemove(path, out _);
        }
    }

    private Texture LoadTextureFromBytes(byte[] data)
    {
        using var ms = new MemoryStream(data);
        StbImageSharp.ImageResult image = StbImageSharp.ImageResult.FromStream(ms, StbImageSharp.ColorComponents.RedGreenBlueAlpha);
        return new Texture(_gl, image.Width, image.Height, image.Data);
    }

    // Load Audio
    public AudioClip LoadAudio(string path)
    {
        if (UseCache && _cache.TryGetValue(path, out var cached))
            return (AudioClip)cached;

        var data = LoadBytes(path);

        // Write to temp file (OpenAL needs file path)
        var tempPath = Path.GetTempFileName();
        File.WriteAllBytes(tempPath, data);

        var al = Silk.NET.OpenAL.AL.GetApi(true);
        var clip = new AudioClip(al, tempPath);

        File.Delete(tempPath);

        if (UseCache)
            _cache[path] = clip;

        return clip;
    }

    // Load Text
    public string LoadText(string path)
    {
        var data = LoadBytes(path);
        return System.Text.Encoding.UTF8.GetString(data);
    }

    public async Task<string> LoadTextAsync(string path)
    {
        var data = await LoadBytesAsync(path);
        return System.Text.Encoding.UTF8.GetString(data);
    }

    // Load JSON
    public T LoadJson<T>(string path)
    {
        var json = LoadText(path);
        return System.Text.Json.JsonSerializer.Deserialize<T>(json)!;
    }

    public async Task<T> LoadJsonAsync<T>(string path)
    {
        var json = await LoadTextAsync(path);
        return System.Text.Json.JsonSerializer.Deserialize<T>(json)!;
    }

    // Generic asset loading with type inference
    public T Load<T>(string path) where T : class
    {
        if (typeof(T) == typeof(Texture))
            return (T)(object)LoadTexture(path);
        if (typeof(T) == typeof(AudioClip))
            return (T)(object)LoadAudio(path);
        if (typeof(T) == typeof(string))
            return (T)(object)LoadText(path);

        throw new NotSupportedException($"Asset type {typeof(T).Name} not supported");
    }

    public async Task<T> LoadAsync<T>(string path) where T : class
    {
        if (typeof(T) == typeof(Texture))
            return (T)(object)await LoadTextureAsync(path);
        if (typeof(T) == typeof(string))
            return (T)(object)await LoadTextAsync(path);

        throw new NotSupportedException($"Asset type {typeof(T).Name} not supported");
    }

    // Asset existence check
    public bool Exists(string path)
    {
        return _loaders.Any(l => l.ContainsAsset(path));
    }

    // Cache management
    public void Unload(string path)
    {
        if (_cache.TryRemove(path, out var asset))
        {
            if (asset is IDisposable disposable)
                disposable.Dispose();
        }
    }

    public void UnloadAll()
    {
        foreach (var asset in _cache.Values)
        {
            if (asset is IDisposable disposable)
                disposable.Dispose();
        }
        _cache.Clear();
    }

    public void ClearCache()
    {
        _cache.Clear();
    }

    // Hot reload support (dev mode)
    public void Reload(string path)
    {
        Unload(path);
        // Asset will be reloaded on next access
    }

    // Statistics
    public AssetStats GetStats()
    {
        return new AssetStats
        {
            MountedPacks = _loaders.Count,
            TotalAssets = _loaders.Sum(l => l.FileCount),
            CachedAssets = _cache.Count,
            LoadingAssets = _loading.Count
        };
    }

    public void Dispose()
    {
        UnloadAll();

        foreach (var loader in _loaders)
            loader.Dispose();

        _loaders.Clear();
        _loading.Clear();
    }
}

public struct AssetStats
{
    public int MountedPacks;
    public int TotalAssets;
    public int CachedAssets;
    public int LoadingAssets;

    public override string ToString() =>
        $"Packs: {MountedPacks}, Total: {TotalAssets}, Cached: {CachedAssets}, Loading: {LoadingAssets}";
}

// Asset handle system (advanced)
public struct AssetHandle<T> where T : class
{
    private string _path;
    private AssetManager _manager;
    private T? _cached;

    public AssetHandle(AssetManager manager, string path)
    {
        _manager = manager;
        _path = path;
        _cached = null;
    }

    public T Get()
    {
        if (_cached == null)
            _cached = _manager.Load<T>(_path);
        return _cached;
    }

    public async Task<T> GetAsync()
    {
        if (_cached == null)
            _cached = await _manager.LoadAsync<T>(_path);
        return _cached;
    }

    public void Unload()
    {
        _cached = null;
        _manager.Unload(_path);
    }

    public bool IsLoaded => _cached != null;
}
