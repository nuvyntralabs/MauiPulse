using System.Text.Json.Serialization;

namespace Plugin.Maui.Pulse;

public sealed class PulseSignal
{
    [JsonPropertyName("package")]
    public string? Package { get; set; }

    [JsonPropertyName("source")]
    public string Source { get; set; } = "";

    [JsonPropertyName("lane")]
    public string? Lane { get; set; }

    [JsonPropertyName("signal")]
    public string? Signal { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("at")]
    public DateTimeOffset? At { get; set; }

    [JsonPropertyName("data")]
    public Dictionary<string, string>? Data { get; set; }
}

public sealed class PluginPresence
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("lane")]
    public string? Lane { get; set; }

    [JsonPropertyName("state")]
    public string State { get; set; } = SourceState.NotInstalled.ToString();
}

public sealed record SourceSnapshot
{
    public required string PackageId { get; init; }
    public required PulseLane Lane { get; init; }
    public SourceState State { get; init; } = SourceState.NotInstalled;
    public string? Signal { get; init; }
    public string? Summary { get; init; }
    public DateTimeOffset? At { get; init; }
}

public sealed class LaneSnapshot
{
    public required PulseLane Lane { get; init; }
    public required IReadOnlyList<SourceSnapshot> Sources { get; init; }
    public string Headline { get; init; } = "— not installed";
    public string Detail { get; init; } = "";
}

public sealed class SessionSnapshot
{
    public string App { get; init; } = "maui-pulse";
    public string? Package { get; init; }
    public string? Device { get; init; }
    public string? SessionId { get; init; }
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
    public required IReadOnlyList<LaneSnapshot> Lanes { get; init; }
    public int AcceptedSignals { get; init; }
    public int DroppedSignals { get; init; }

    public bool HasAnyEvidence =>
        Lanes.Any(lane => lane.Sources.Any(source =>
            source.State is SourceState.Running or SourceState.FromFile or SourceState.Quiet or SourceState.NotRegistered));
}

public sealed class ParseResult
{
    public IReadOnlyList<PulseSignal> Signals { get; init; } = [];
    public IReadOnlyList<PluginPresence> Presence { get; init; } = [];
    public int Dropped { get; init; }
}

public sealed class QueueCounts
{
    public int Pending { get; init; }
    public int Scheduled { get; init; }
    public int Running { get; init; }
    public int Failed { get; init; }
    public int DeadLetter { get; init; }
    public int Succeeded { get; init; }
    public int Cancelled { get; init; }
    public string? NextName { get; init; }
    public string? NextAt { get; init; }
    public string? DeadLetterName { get; init; }

    public int Active => Pending + Scheduled + Running + Failed;
}

public sealed class SyncSnapshot
{
    public int PendingChanges { get; init; }
    public int Conflicts { get; init; }
    public int Failed { get; init; }
    public IReadOnlyList<string> Collections { get; init; } = [];
    public string? SampleConflict { get; init; }
}

public sealed class PullResult
{
    public required string Destination { get; init; }
    public IReadOnlyList<string> Copied { get; init; } = [];
    public IReadOnlyList<string> Skipped { get; init; } = [];
    public string? Error { get; init; }
}

public static class ExitCodes
{
    public const int Success = 0;
    public const int Issues = 1;
    public const int Usage = 2;
}
