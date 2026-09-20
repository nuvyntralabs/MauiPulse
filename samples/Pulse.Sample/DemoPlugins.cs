using Maui.NetworkMonitor;
using Plugin.Maui.AppHealth;
using Plugin.Maui.DeviceSession;
using Plugin.Maui.Diagnostics;
using Plugin.Maui.JobQueue;
using Plugin.Maui.LeakAnalyser;
using Plugin.Maui.NetworkDiagnostics;
using Plugin.Maui.OfflineSync;
using Plugin.Maui.PermissionFlow;

namespace Pulse.Sample;

/// <summary>
/// Stand-ins for allow-listed plugins so <c>UseMauiPulse()</c> can subscribe.
/// A real host registers the actual Plugin.Maui.* services instead.
/// </summary>
public sealed class DemoPlugins
{
    public DemoNetworkMonitor Network { get; } = new();
    public DemoNetworkDiagnostics Api { get; } = new();
    public DemoJobQueue Queue { get; } = new();
    public DemoOfflineSync Sync { get; } = new();
    public DemoPermissionFlow Perms { get; } = new();
    public DemoAppHealth Health { get; } = new();
    public DemoLeakAnalyser Leak { get; } = new();
    public DemoDiagnostics Crash { get; } = new();
    public DemoDeviceSession Session { get; } = new();

    public string RaiseNetwork()
    {
        Network.RaiseCaptivePortal();
        return "NetworkMonitor.StatusChanged";
    }

    public string RaiseApi()
    {
        Api.RaiseCompleted();
        return "NetworkDiagnostics.Completed";
    }

    public string RaiseQueue()
    {
        Queue.RaiseFailed();
        return "JobQueue.JobFailed";
    }

    public string RaiseSync()
    {
        Sync.RaiseConflict();
        return "OfflineSync.ConflictDetected";
    }

    public string RaisePerms()
    {
        Perms.RaiseCompleted();
        return "PermissionFlow.FlowCompleted";
    }

    public string RaiseHealth()
    {
        Health.RaiseChanged();
        return "AppHealth.HealthChanged";
    }

    public string RaiseLeak()
    {
        Leak.RaiseLeaked();
        return "LeakAnalyser.OnLeaked";
    }

    public string RaiseCrash()
    {
        Crash.RaiseAnr();
        return "Diagnostics.AnrDetected";
    }

    public string RaiseSession()
    {
        Session.RaiseStarted();
        return "DeviceSession.SessionStarted";
    }

    public IReadOnlyList<string> RaiseAll() =>
    [
        RaiseNetwork(), RaiseApi(), RaiseQueue(), RaiseSync(),
        RaisePerms(), RaiseHealth(), RaiseLeak(), RaiseCrash(), RaiseSession()
    ];
}

public sealed class DemoNetworkMonitor : INetworkMonitor
{
    public event EventHandler<NetworkStatusChangedEventArgs>? StatusChanged;

    public void RaiseCaptivePortal() =>
        StatusChanged?.Invoke(this, new NetworkStatusChangedEventArgs(new NetworkStatus(Captive: true)));
}

public sealed class DemoNetworkDiagnostics : INetworkDiagnostics
{
    public event EventHandler<DiagnosticsCompletedEventArgs>? Completed;

    public void RaiseCompleted() =>
        Completed?.Invoke(this, new DiagnosticsCompletedEventArgs("dns timeout"));
}

public sealed class DemoJobQueue : IJobQueue
{
    public event EventHandler<JobFailedEventArgs>? JobFailed;

    public void RaiseFailed() => JobFailed?.Invoke(this, new JobFailedEventArgs("4 pending"));
}

public sealed class DemoOfflineSync : IOfflineSyncEngine
{
    public event EventHandler<ConflictDetectedEventArgs>? ConflictDetected;
#pragma warning disable CS0067 // Pulse binds these; the sample raises ConflictDetected only
    public event EventHandler<EventArgs>? StatusChanged;
    public event EventHandler<EventArgs>? SyncCompleted;
#pragma warning restore CS0067

    public void RaiseConflict() =>
        ConflictDetected?.Invoke(this, new ConflictDetectedEventArgs("visits", "184"));
}

public sealed class DemoPermissionFlow : IPermissionFlow
{
    public event EventHandler<FlowCompletedEventArgs>? FlowCompleted;
#pragma warning disable CS0067 // Pulse binds this; the sample raises FlowCompleted only
    public event EventHandler<EventArgs>? PermissionChanged;
#pragma warning restore CS0067

    public void RaiseCompleted() =>
        FlowCompleted?.Invoke(this, new FlowCompletedEventArgs("location granted"));
}

public sealed class DemoAppHealth : IAppHealth
{
    public event EventHandler<HealthChangedEventArgs>? HealthChanged;

    public void RaiseChanged() =>
        HealthChanged?.Invoke(this, new HealthChangedEventArgs(new HealthReport("battery 18%")));
}

public sealed class DemoLeakAnalyser : ILeakAnalyser
{
    public event EventHandler<LeakedEventArgs>? OnLeaked;

    public void RaiseLeaked() => OnLeaked?.Invoke(this, new LeakedEventArgs("MainPage leaked"));
}

public sealed class DemoDiagnostics : IDiagnostics
{
    public event EventHandler<AnrDetectedEventArgs>? AnrDetected;

    public void RaiseAnr() => AnrDetected?.Invoke(this, new AnrDetectedEventArgs("main thread 6s"));
}

public sealed class DemoDeviceSession : IDeviceSession
{
    public event EventHandler<SessionStartedEventArgs>? SessionStarted;

    public void RaiseStarted() =>
        SessionStarted?.Invoke(this, new SessionStartedEventArgs(new SessionInfo("8f2a1c0e-demo")));
}
