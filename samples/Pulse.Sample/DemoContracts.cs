namespace Maui.NetworkMonitor
{
    public interface INetworkMonitor
    {
        event EventHandler<NetworkStatusChangedEventArgs>? StatusChanged;
    }

    public sealed class NetworkStatus(bool Captive)
    {
        public bool IsCaptivePortal { get; } = Captive;
        public bool HasInternet { get; } = !Captive;
    }

    public sealed class NetworkStatusChangedEventArgs(NetworkStatus current) : EventArgs
    {
        public NetworkStatus Current { get; } = current;
    }
}

namespace Plugin.Maui.NetworkDiagnostics
{
    public interface INetworkDiagnostics
    {
        event EventHandler<DiagnosticsCompletedEventArgs>? Completed;
    }

    public sealed class DiagnosticsCompletedEventArgs(string result) : EventArgs
    {
        public string Result { get; } = result;
    }
}

namespace Plugin.Maui.JobQueue
{
    public interface IJobQueue
    {
        event EventHandler<JobFailedEventArgs>? JobFailed;
    }

    public sealed class JobFailedEventArgs(string result) : EventArgs
    {
        public string Result { get; } = result;
    }
}

namespace Plugin.Maui.OfflineSync
{
    public interface IOfflineSyncEngine
    {
        event EventHandler<ConflictDetectedEventArgs>? ConflictDetected;
        event EventHandler<EventArgs>? StatusChanged;
        event EventHandler<EventArgs>? SyncCompleted;
    }

    public sealed class ConflictDetectedEventArgs(string collection, string entityId) : EventArgs
    {
        public string Collection { get; } = collection;
        public string EntityId { get; } = entityId;
    }
}

namespace Plugin.Maui.PermissionFlow
{
    public interface IPermissionFlow
    {
        event EventHandler<FlowCompletedEventArgs>? FlowCompleted;
        event EventHandler<EventArgs>? PermissionChanged;
    }

    public sealed class FlowCompletedEventArgs(string result) : EventArgs
    {
        public string Result { get; } = result;
    }
}

namespace Plugin.Maui.AppHealth
{
    public interface IAppHealth
    {
        event EventHandler<HealthChangedEventArgs>? HealthChanged;
    }

    public sealed class HealthReport(string text)
    {
        public override string ToString() => text;
    }

    public sealed class HealthChangedEventArgs(HealthReport current) : EventArgs
    {
        public HealthReport Current { get; } = current;
    }
}

namespace Plugin.Maui.LeakAnalyser
{
    public interface ILeakAnalyser
    {
        event EventHandler<LeakedEventArgs>? OnLeaked;
    }

    public sealed class LeakedEventArgs(string result) : EventArgs
    {
        public string Result { get; } = result;
    }
}

namespace Plugin.Maui.Diagnostics
{
    public interface IDiagnostics
    {
        event EventHandler<AnrDetectedEventArgs>? AnrDetected;
    }

    public sealed class AnrDetectedEventArgs(string result) : EventArgs
    {
        public string Result { get; } = result;
    }
}

namespace Plugin.Maui.DeviceSession
{
    public interface IDeviceSession
    {
        event EventHandler<SessionStartedEventArgs>? SessionStarted;
    }

    public sealed class SessionInfo(string sessionId)
    {
        public string SessionId { get; } = sessionId;
    }

    public sealed class SessionStartedEventArgs(SessionInfo session) : EventArgs
    {
        public SessionInfo Session { get; } = session;
    }
}
