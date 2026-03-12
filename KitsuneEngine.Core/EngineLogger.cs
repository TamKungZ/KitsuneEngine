namespace KitsuneEngine.Core;

public enum LogLevel
{
    Trace,
    Debug,
    Info,
    Warning,
    Error
}

public sealed class EngineLogger
{
    private readonly object _sync = new();

    public LogLevel MinimumLevel { get; set; } = LogLevel.Info;
    public bool UseUtcTimestamp { get; set; } = false;
    public string Category { get; set; } = "Engine";

    public void Trace(string message, string? category = null) => Log(LogLevel.Trace, message, category);
    public void Debug(string message, string? category = null) => Log(LogLevel.Debug, message, category);
    public void Info(string message, string? category = null) => Log(LogLevel.Info, message, category);
    public void Warning(string message, string? category = null) => Log(LogLevel.Warning, message, category);
    public void Error(string message, string? category = null) => Log(LogLevel.Error, message, category);

    public void Log(LogLevel level, string message, string? category = null)
    {
        if (level < MinimumLevel)
            return;

        var now = UseUtcTimestamp ? DateTime.UtcNow : DateTime.Now;
        var stamp = now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var cat = string.IsNullOrWhiteSpace(category) ? Category : category!;

        lock (_sync)
        {
            Console.WriteLine($"[{stamp}] [{level}] [{cat}] {message}");
        }
    }

    public void Exception(Exception ex, string message, string? category = null)
    {
        Log(LogLevel.Error, $"{message} | {ex.GetType().Name}: {ex.Message}", category);
    }
}
