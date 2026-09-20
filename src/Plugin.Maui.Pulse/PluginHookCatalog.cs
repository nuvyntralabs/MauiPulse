namespace Plugin.Maui.Pulse;

static class PluginHookCatalog
{
    public static IReadOnlyList<PluginHook> Hooks { get; } =
    [
        new("Plugin.Maui.NetworkMonitor", "network", ["Maui.NetworkMonitor.INetworkMonitor"],
            ["StatusChanged"], ["Maui.NetworkMonitor", "Plugin.Maui.NetworkMonitor"]),
        new("Plugin.Maui.NetworkDiagnostics", "api", ["Plugin.Maui.NetworkDiagnostics.INetworkDiagnostics"],
            ["Completed"], ["Plugin.Maui.NetworkDiagnostics"]),
        new("Plugin.Maui.JobQueue", "queue", ["Plugin.Maui.JobQueue.IJobQueue"],
            ["JobFailed"], ["Plugin.Maui.JobQueue"]),
        new("Plugin.Maui.RetryQueue", "queue", ["Plugin.Maui.RetryQueue.IRetryQueue"],
            ["OperationFailed"], ["Plugin.Maui.RetryQueue"]),
        new("Plugin.Maui.OfflineSync", "sync",
            ["Plugin.Maui.OfflineSync.IOfflineSyncEngine", "Plugin.Maui.OfflineSync.Abstractions.IOfflineSyncEngine"],
            ["ConflictDetected", "StatusChanged", "SyncCompleted"], ["Plugin.Maui.OfflineSync"]),
        new("Plugin.Maui.PermissionFlow", "perms", ["Plugin.Maui.PermissionFlow.IPermissionFlow"],
            ["FlowCompleted", "PermissionChanged"], ["Plugin.Maui.PermissionFlow"]),
        new("Plugin.Maui.AppHealth", "health", ["Plugin.Maui.AppHealth.IAppHealth"],
            ["HealthChanged"], ["Plugin.Maui.AppHealth"]),
        new("Plugin.Maui.LeakAnalyser", "leak", ["Plugin.Maui.LeakAnalyser.ILeakAnalyser"],
            ["OnLeaked"], ["Plugin.Maui.LeakAnalyser"]),
        new("Plugin.Maui.Diagnostics", "crash", ["Plugin.Maui.Diagnostics.IDiagnostics"],
            ["AnrDetected"], ["Plugin.Maui.Diagnostics"]),
        new("Plugin.Maui.DeviceSession", "session", ["Plugin.Maui.DeviceSession.IDeviceSession"],
            ["SessionStarted"], ["Plugin.Maui.DeviceSession"])
    ];
}
