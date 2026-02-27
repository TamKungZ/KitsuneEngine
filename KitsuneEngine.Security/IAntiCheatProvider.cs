namespace KitsuneEngine.Security;

public interface IAntiCheatProvider : IDisposable
{
    string Name { get; }
    AntiCheatProviderState State { get; }

    event Action<AntiCheatDetection>? DetectionRaised;
    event Action<string>? Log;

    Task InitializeAsync(AntiCheatSessionOptions options, CancellationToken ct = default);
    Task StartSessionAsync(AntiCheatPlayerContext player, CancellationToken ct = default);
    Task SubmitClientEventAsync(string eventName, IReadOnlyDictionary<string, string>? payload = null, CancellationToken ct = default);
    Task StopSessionAsync(CancellationToken ct = default);
}
