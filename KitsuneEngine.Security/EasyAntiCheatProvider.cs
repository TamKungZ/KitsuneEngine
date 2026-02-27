namespace KitsuneEngine.Security;

/// <summary>
/// Adapter-style provider API for Easy Anti-Cheat-like integrations.
/// This implementation is a lightweight stub ready to be wired to native SDK calls.
/// </summary>
public sealed class EasyAntiCheatProvider : IAntiCheatProvider
{
    private AntiCheatSessionOptions? _options;
    private AntiCheatPlayerContext? _player;
    private bool _disposed;

    public string Name => "EasyAntiCheat";
    public AntiCheatProviderState State { get; private set; } = AntiCheatProviderState.Uninitialized;

    public event Action<AntiCheatDetection>? DetectionRaised;
    public event Action<string>? Log;

    public async Task InitializeAsync(AntiCheatSessionOptions options, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        State = AntiCheatProviderState.Initializing;
        _options = options;

        // Placeholder for third-party SDK bootstrap.
        await Task.Delay(1, ct);

        State = AntiCheatProviderState.Ready;
        Log?.Invoke($"[{Name}] Initialized for {options.ProductId}/{options.BuildVersion}");
    }

    public async Task StartSessionAsync(AntiCheatPlayerContext player, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        EnsureState(AntiCheatProviderState.Ready, AntiCheatProviderState.Stopped);

        _player = player;

        // Placeholder for begin-session call.
        await Task.Delay(1, ct);

        State = AntiCheatProviderState.Running;
        Log?.Invoke($"[{Name}] Session started for player {player.PlayerId}");
    }

    public async Task SubmitClientEventAsync(string eventName, IReadOnlyDictionary<string, string>? payload = null, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        EnsureState(AntiCheatProviderState.Running);

        // Placeholder for event forwarding to provider SDK.
        await Task.Delay(1, ct);
        Log?.Invoke($"[{Name}] Event: {eventName}");

        // Example local signal hook for suspicious events.
        if (eventName.Equals("TamperDetected", StringComparison.OrdinalIgnoreCase))
        {
            DetectionRaised?.Invoke(new AntiCheatDetection
            {
                RuleId = "EAC.Tamper",
                Message = "Tamper signal received from client event stream.",
                Severity = AntiCheatSeverity.Critical,
                Metadata = payload ?? new Dictionary<string, string>()
            });
        }
    }

    public async Task StopSessionAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();

        if (State is not AntiCheatProviderState.Running)
            return;

        await Task.Delay(1, ct);
        State = AntiCheatProviderState.Stopped;
        Log?.Invoke($"[{Name}] Session stopped for player {_player?.PlayerId ?? "unknown"}");
        _player = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        State = AntiCheatProviderState.Stopped;
    }

    private void EnsureState(params AntiCheatProviderState[] states)
    {
        for (int i = 0; i < states.Length; i++)
        {
            if (State == states[i])
                return;
        }

        throw new InvalidOperationException($"Provider '{Name}' is in invalid state '{State}'.");
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
