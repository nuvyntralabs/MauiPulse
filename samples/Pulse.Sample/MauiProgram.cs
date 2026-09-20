using Maui.NetworkMonitor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Plugin.Maui.AppHealth;
using Plugin.Maui.DeviceSession;
using Plugin.Maui.Diagnostics;
using Plugin.Maui.JobQueue;
using Plugin.Maui.LeakAnalyser;
using Plugin.Maui.NetworkDiagnostics;
using Plugin.Maui.OfflineSync;
using Plugin.Maui.PermissionFlow;
using Plugin.Maui.Pulse;

namespace Pulse.Sample;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.Services.AddSingleton<DemoPlugins>();
        builder.Services.AddSingleton<INetworkMonitor>(services => services.GetRequiredService<DemoPlugins>().Network);
        builder.Services.AddSingleton<INetworkDiagnostics>(services => services.GetRequiredService<DemoPlugins>().Api);
        builder.Services.AddSingleton<IJobQueue>(services => services.GetRequiredService<DemoPlugins>().Queue);
        builder.Services.AddSingleton<IOfflineSyncEngine>(services => services.GetRequiredService<DemoPlugins>().Sync);
        builder.Services.AddSingleton<IPermissionFlow>(services => services.GetRequiredService<DemoPlugins>().Perms);
        builder.Services.AddSingleton<IAppHealth>(services => services.GetRequiredService<DemoPlugins>().Health);
        builder.Services.AddSingleton<ILeakAnalyser>(services => services.GetRequiredService<DemoPlugins>().Leak);
        builder.Services.AddSingleton<IDiagnostics>(services => services.GetRequiredService<DemoPlugins>().Crash);
        builder.Services.AddSingleton<IDeviceSession>(services => services.GetRequiredService<DemoPlugins>().Session);
        builder.UseMauiApp<App>().UseMauiPulse();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
