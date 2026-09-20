namespace Plugin.Maui.Pulse;

/// <summary>
/// Options for <see cref="MauiAppBuilderExtensions.UseMauiPulse"/>. Defaults are enough for Debug.
/// </summary>
public sealed class PulseHostOptions
{
    /// <summary>
    /// When false, <see cref="MauiAppBuilderExtensions.UseMauiPulse"/> does nothing.
    /// Default is <c>true</c> in Debug and <c>false</c> in Release.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Pulse listen URL. Empty means emulator <c>http://10.0.2.2:7878/</c> or device <c>http://127.0.0.1:7878/</c>.
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Android applicationId / iOS bundle id. Empty means <c>AppInfo.PackageName</c>.
    /// </summary>
    public string? Package { get; set; }
}
