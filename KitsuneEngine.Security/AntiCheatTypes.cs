namespace KitsuneEngine.Security;

public enum AntiCheatSeverity
{
    Info,
    Warning,
    Critical
}

public enum AntiCheatProviderState
{
    Uninitialized,
    Initializing,
    Ready,
    Running,
    Stopped,
    Failed
}

public sealed class AntiCheatDetection
{
    public string RuleId { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public AntiCheatSeverity Severity { get; init; } = AntiCheatSeverity.Warning;
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}

public sealed class AntiCheatSessionOptions
{
    public string ProductId { get; init; } = "KitsuneEngine";
    public string SandboxId { get; init; } = "default";
    public string DeploymentId { get; init; } = "dev";
    public string BuildVersion { get; init; } = "1.0.0";
    public bool IsServer { get; init; }
    public string? AuthToken { get; init; }
}

public sealed class AntiCheatPlayerContext
{
    public string PlayerId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string SessionId { get; init; } = string.Empty;
    public Dictionary<string, string> Attributes { get; init; } = new();
}
