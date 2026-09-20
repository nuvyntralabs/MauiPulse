namespace Plugin.Maui.Pulse;

public enum PulseLane
{
    Network,
    Api,
    Queue,
    Sync,
    Perms,
    Health,
    Leak,
    Crash,
    Session
}

public enum SourceState
{
    NotInstalled,
    NotRegistered,
    Quiet,
    Running,
    FromFile
}

public sealed record PluginSource(string PackageId, PulseLane Lane, string FileHint);

/// <summary>
/// Closed allow-list. Pulse never reads logcat, Firebase, Sentry, or MAUI framework APIs.
/// </summary>
public static class PluginCatalog
{
    public const string NetworkMonitor = "Plugin.Maui.NetworkMonitor";
    public const string NetworkDiagnostics = "Plugin.Maui.NetworkDiagnostics";
    public const string JobQueue = "Plugin.Maui.JobQueue";
    public const string RetryQueue = "Plugin.Maui.RetryQueue";
    public const string OfflineSync = "Plugin.Maui.OfflineSync";
    public const string PermissionFlow = "Plugin.Maui.PermissionFlow";
    public const string AppHealth = "Plugin.Maui.AppHealth";
    public const string LeakAnalyser = "Plugin.Maui.LeakAnalyser";
    public const string Diagnostics = "Plugin.Maui.Diagnostics";
    public const string DeviceSession = "Plugin.Maui.DeviceSession";

    public const string JobQueueFile = "plugin.maui.jobqueue.db3";
    public const string RetryQueueFile = "plugin.maui.retryqueue.db3";
    public const string OfflineSyncFile = "offlinesync.db3";
    public const string DiagnosticsFolder = "maui-diagnostics";

    public static IReadOnlyList<PluginSource> Sources { get; } =
    [
        new(NetworkMonitor, PulseLane.Network, ""),
        new(NetworkDiagnostics, PulseLane.Api, ""),
        new(JobQueue, PulseLane.Queue, JobQueueFile),
        new(RetryQueue, PulseLane.Queue, RetryQueueFile),
        new(OfflineSync, PulseLane.Sync, OfflineSyncFile),
        new(PermissionFlow, PulseLane.Perms, ""),
        new(AppHealth, PulseLane.Health, ""),
        new(LeakAnalyser, PulseLane.Leak, ""),
        new(Diagnostics, PulseLane.Crash, DiagnosticsFolder),
        new(DeviceSession, PulseLane.Session, "")
    ];

    public static IReadOnlyList<PulseLane> Lanes { get; } =
    [
        PulseLane.Network, PulseLane.Api, PulseLane.Queue, PulseLane.Sync,
        PulseLane.Perms, PulseLane.Health, PulseLane.Leak, PulseLane.Crash, PulseLane.Session
    ];

    public static bool TryGet(string packageId, out PluginSource source)
    {
        foreach (var item in Sources)
        {
            if (string.Equals(item.PackageId, packageId, StringComparison.OrdinalIgnoreCase))
            {
                source = item;
                return true;
            }
        }

        source = Sources[0];
        return false;
    }

    public static bool TryParseLane(string? value, out PulseLane lane)
    {
        lane = PulseLane.Network;
        if (string.IsNullOrWhiteSpace(value))
            return false;
        return Enum.TryParse(value.Trim(), ignoreCase: true, out lane);
    }

    public static string LaneLabel(PulseLane lane) => lane switch
    {
        PulseLane.Network => "NETWORK",
        PulseLane.Api => "API",
        PulseLane.Queue => "QUEUE",
        PulseLane.Sync => "SYNC",
        PulseLane.Perms => "PERMS",
        PulseLane.Health => "HEALTH",
        PulseLane.Leak => "LEAK",
        PulseLane.Crash => "CRASH",
        PulseLane.Session => "SESSION",
        _ => lane.ToString().ToUpperInvariant()
    };

    public static string StateLabel(SourceState state) => state switch
    {
        SourceState.NotInstalled => "not_installed",
        SourceState.NotRegistered => "not_registered",
        SourceState.Quiet => "quiet",
        SourceState.Running => "running",
        SourceState.FromFile => "from_file",
        _ => "unknown"
    };

    /// <summary>
    /// Map Observability domain names onto allow-listed plugins. Unknown domains are dropped.
    /// </summary>
    public static bool TryMapObservabilityDomain(string? domain, out string packageId)
    {
        packageId = "";
        if (string.IsNullOrWhiteSpace(domain))
            return false;

        packageId = domain.Trim() switch
        {
            "Network" => NetworkMonitor,
            "Health" => AppHealth,
            "Sync" or "OfflineSync" => OfflineSync,
            "Session" or "Identity" or "DeviceSession" => DeviceSession,
            _ => ""
        };
        return packageId.Length > 0;
    }

    public static IReadOnlyList<PulseLane> ParseLaneFilter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Lanes;

        var list = new List<PulseLane>();
        foreach (var part in value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (TryParseLane(part, out var lane) && !list.Contains(lane))
                list.Add(lane);
        }

        return list.Count == 0 ? Lanes : list;
    }
}
