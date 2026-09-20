using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;

namespace Plugin.Maui.Pulse;

/// <summary>
/// Registers the automatic Pulse Debug sink.
/// </summary>
public static class MauiAppBuilderExtensions
{
    /// <summary>
    /// Forwards allow-listed <c>Plugin.Maui.*</c> events to <c>maui-pulse</c>.
    /// Call this once. Do not POST from the host.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.UseMauiApp&lt;App&gt;().UseMauiPulse();
    /// </code>
    /// </example>
    public static MauiAppBuilder UseMauiPulse(this MauiAppBuilder builder, Action<PulseHostOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new PulseHostOptions();
#if !DEBUG
        options.Enabled = false;
#endif
        configure?.Invoke(options);
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<PulseHostRuntime>();
        builder.Services.AddTransient<IMauiInitializeService, PulseHostInitializer>();
        return builder;
    }
}
