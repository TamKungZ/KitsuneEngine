using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using System.Runtime.InteropServices;

namespace KitsuneEngine.Core;

public sealed class DeviceDiagnostics
{
    public string OsDescription { get; init; } = RuntimeInformation.OSDescription;
    public string OsArchitecture { get; init; } = RuntimeInformation.OSArchitecture.ToString();
    public string ProcessArchitecture { get; init; } = RuntimeInformation.ProcessArchitecture.ToString();
    public string FrameworkDescription { get; init; } = RuntimeInformation.FrameworkDescription;

    public string WindowBackend { get; init; } = "Unknown";
    public int WindowWidth { get; init; }
    public int WindowHeight { get; init; }

    public string GpuVendor { get; init; } = "Unknown";
    public string GpuRenderer { get; init; } = "Unknown";
    public string OpenGlVersion { get; init; } = "Unknown";

    public int KeyboardCount { get; init; }
    public int MouseCount { get; init; }
    public int GamepadCount { get; init; }

    public bool OpenAlAvailable { get; init; }

    public static DeviceDiagnostics Collect(IWindow window, GL gl, IInputContext input)
    {
        return new DeviceDiagnostics
        {
            WindowBackend = window?.GetType().Name ?? "Unknown",
            WindowWidth = window?.Size.X ?? 0,
            WindowHeight = window?.Size.Y ?? 0,

            GpuVendor = gl.GetStringS(StringName.Vendor),
            GpuRenderer = gl.GetStringS(StringName.Renderer),
            OpenGlVersion = gl.GetStringS(StringName.Version),

            KeyboardCount = input?.Keyboards.Count ?? 0,
            MouseCount = input?.Mice.Count ?? 0,
            GamepadCount = input?.Gamepads.Count ?? 0,

            OpenAlAvailable = IsOpenAlAvailable()
        };
    }

    private static bool IsOpenAlAvailable()
    {
        try
        {
            var al = Silk.NET.OpenAL.AL.GetApi(true);
            al.Dispose();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
