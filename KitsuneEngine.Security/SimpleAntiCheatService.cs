using System.Diagnostics;

namespace KitsuneEngine.Security;

public sealed class SimpleAntiCheatService : IDisposable
{
    private readonly IAntiCheatProvider? _provider;
    private readonly Dictionary<string, float> _lastPositionByPlayer = new();
    private bool _disposed;

    public event Action<AntiCheatDetection>? DetectionRaised;
    public event Action<string>? Log;

    /// <summary>
    /// Max allowed delta movement per update tick (units per tick).
    /// Tune this to your game movement model.
    /// </summary>
    public float MaxPositionDeltaPerTick { get; set; } = 30f;

    public SimpleAntiCheatService(IAntiCheatProvider? provider = null)
    {
        _provider = provider;
        if (_provider != null)
        {
            _provider.DetectionRaised += ForwardProviderDetection;
            _provider.Log += ForwardProviderLog;
        }
    }

    public async Task InitializeAsync(AntiCheatSessionOptions options, AntiCheatPlayerContext player, CancellationToken ct = default)
    {
        ThrowIfDisposed();

        if (_provider == null)
            return;

        await _provider.InitializeAsync(options, ct);
        await _provider.StartSessionAsync(player, ct);
    }

    /// <summary>
    /// Example built-in movement sanity check.
    /// </summary>
    public void ValidatePositionDelta(string playerId, float currentX, float previousX)
    {
        ThrowIfDisposed();

        float delta = MathF.Abs(currentX - previousX);
        if (delta <= MaxPositionDeltaPerTick)
        {
            _lastPositionByPlayer[playerId] = currentX;
            return;
        }

        RaiseDetection(new AntiCheatDetection
        {
            RuleId = "SIMPLE.MOVEMENT_DELTA",
            Message = $"Movement delta exceeded threshold: {delta:0.00} > {MaxPositionDeltaPerTick:0.00}",
            Severity = AntiCheatSeverity.Warning,
            Metadata = new Dictionary<string, string>
            {
                ["playerId"] = playerId,
                ["currentX"] = currentX.ToString("0.###"),
                ["previousX"] = previousX.ToString("0.###")
            }
        });

        _lastPositionByPlayer[playerId] = currentX;
    }

    /// <summary>
    /// Example check for suspicious process names on client side.
    /// Keep this optional and conservative to avoid false positives.
    /// </summary>
    public void ValidateProcessAllowList(IEnumerable<string> blockedProcessNames)
    {
        ThrowIfDisposed();

        var blocked = new HashSet<string>(blockedProcessNames.Select(p => p.ToLowerInvariant()));
        if (blocked.Count == 0)
            return;

        foreach (var proc in Process.GetProcesses())
        {
            string name = proc.ProcessName.ToLowerInvariant();
            if (!blocked.Contains(name))
                continue;

            RaiseDetection(new AntiCheatDetection
            {
                RuleId = "SIMPLE.BLOCKED_PROCESS",
                Message = $"Blocked process detected: {proc.ProcessName}",
                Severity = AntiCheatSeverity.Critical,
                Metadata = new Dictionary<string, string>
                {
                    ["process"] = proc.ProcessName,
                    ["pid"] = proc.Id.ToString()
                }
            });
        }
    }

    public Task SubmitEventAsync(string eventName, IReadOnlyDictionary<string, string>? payload = null, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return _provider?.SubmitClientEventAsync(eventName, payload, ct) ?? Task.CompletedTask;
    }

    public async Task ShutdownAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();

        if (_provider != null)
        {
            await _provider.StopSessionAsync(ct);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        if (_provider != null)
        {
            _provider.DetectionRaised -= ForwardProviderDetection;
            _provider.Log -= ForwardProviderLog;
            _provider.Dispose();
        }

        _disposed = true;
    }

    private void RaiseDetection(AntiCheatDetection detection)
    {
        DetectionRaised?.Invoke(detection);
    }

    private void ForwardProviderDetection(AntiCheatDetection detection)
    {
        DetectionRaised?.Invoke(detection);
    }

    private void ForwardProviderLog(string message)
    {
        Log?.Invoke(message);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
