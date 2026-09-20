namespace Plugin.Maui.Pulse;

public sealed class SessionStore
{
    readonly Dictionary<string, SourceSnapshot> _sources;
    readonly object _gate = new();
    int _accepted;
    int _dropped;

    public SessionStore(string? app = null, string? device = null, string? package = null, DateTimeOffset? startedAt = null)
    {
        App = string.IsNullOrWhiteSpace(app) ? "maui-pulse" : app;
        Device = device;
        Package = string.IsNullOrWhiteSpace(package) ? null : package.Trim();
        StartedAt = startedAt ?? DateTimeOffset.UtcNow;
        _sources = PluginCatalog.Sources.ToDictionary(
            source => source.PackageId,
            source => new SourceSnapshot { PackageId = source.PackageId, Lane = source.Lane },
            StringComparer.OrdinalIgnoreCase);
    }

    public string App { get; }

    public string? Device { get; set; }

    public string? Package { get; }

    public string? SessionId { get; private set; }

    public DateTimeOffset StartedAt { get; }

    public void Apply(ParseResult result)
    {
        lock (_gate)
        {
            _dropped += result.Dropped;
            foreach (var row in result.Presence)
                ApplyPresence(row);
            foreach (var signal in result.Signals)
                ApplySignal(signal);
        }
    }

    public SessionSnapshot Snapshot(IReadOnlyList<PulseLane>? lanes = null)
    {
        lock (_gate)
        {
            var filter = lanes ?? PluginCatalog.Lanes;
            var built = new List<LaneSnapshot>(filter.Count);
            foreach (var lane in filter)
            {
                var sources = _sources.Values
                    .Where(source => source.Lane == lane)
                    .OrderBy(source => source.PackageId, StringComparer.Ordinal)
                    .ToArray();
                built.Add(new LaneSnapshot
                {
                    Lane = lane,
                    Sources = sources,
                    Headline = Headline(lane, sources),
                    Detail = Detail(lane, sources)
                });
            }

            return new SessionSnapshot
            {
                App = App,
                Package = Package,
                Device = Device,
                SessionId = SessionId,
                StartedAt = StartedAt,
                Lanes = built,
                AcceptedSignals = _accepted,
                DroppedSignals = _dropped
            };
        }
    }

    void ApplyPresence(PluginPresence row)
    {
        if (!_sources.TryGetValue(row.Id, out var current))
        {
            _dropped++;
            return;
        }

        var state = SignalParser.ParseState(row.State);
        _sources[row.Id] = current with { State = state };
        if (row.Id == PluginCatalog.DeviceSession && SessionId is null)
            SessionId = "present";
    }

    void ApplySignal(PulseSignal signal)
    {
        if (!PluginCatalog.TryGet(signal.Source, out _))
        {
            _dropped++;
            return;
        }

        var current = _sources[signal.Source];
        var state = string.IsNullOrWhiteSpace(signal.State)
            ? SourceState.Running
            : SignalParser.ParseState(signal.State);
        if (state == SourceState.NotInstalled)
            state = SourceState.Running;

        _sources[signal.Source] = current with
        {
            State = state,
            Signal = signal.Signal,
            Summary = signal.Summary,
            At = signal.At ?? DateTimeOffset.UtcNow
        };
        _accepted++;

        if (signal.Source == PluginCatalog.DeviceSession)
        {
            if (signal.Data is not null && signal.Data.TryGetValue("sessionId", out var id) && !string.IsNullOrWhiteSpace(id))
                SessionId = id;
            else if (!string.IsNullOrWhiteSpace(signal.Summary))
                SessionId = signal.Summary;
        }
    }

    public void MarkFromFile(string packageId, string? summary)
    {
        lock (_gate)
        {
            if (!_sources.TryGetValue(packageId, out var current))
                return;

            _sources[packageId] = current with
            {
                State = SourceState.FromFile,
                Summary = summary ?? current.Summary,
                At = DateTimeOffset.UtcNow
            };
        }
    }

    static string Headline(PulseLane lane, IReadOnlyList<SourceSnapshot> sources)
    {
        var live = sources.Where(IsPresent).ToArray();
        if (live.Length == 0)
            return "— not installed";

        var registered = live.FirstOrDefault(source => source.State == SourceState.NotRegistered);
        if (registered is not null && live.All(source => source.State is SourceState.NotRegistered or SourceState.NotInstalled))
            return "— not registered";

        var quiet = live.All(source => source.State is SourceState.Quiet);
        if (quiet)
            return "—";

        var withText = live.FirstOrDefault(source => !string.IsNullOrWhiteSpace(source.Summary));
        if (withText?.Summary is { Length: > 0 } summary)
            return summary;

        if (lane == PulseLane.Queue)
            return string.Join(" · ", live.Select(DescribeQueueSource));

        return live[0].State == SourceState.FromFile ? "from file" : "running";
    }

    static string Detail(PulseLane lane, IReadOnlyList<SourceSnapshot> sources)
    {
        if (lane == PulseLane.Queue)
        {
            return string.Join("  ·  ", sources.Select(source =>
                source.State == SourceState.NotInstalled
                    ? $"{ShortName(source.PackageId)} — not installed"
                    : $"{ShortName(source.PackageId)} {source.Summary ?? PluginCatalog.StateLabel(source.State)}"));
        }

        var present = sources.Where(IsPresent).ToArray();
        if (present.Length == 0)
            return sources[0].PackageId;

        var first = present[0];
        var bits = new List<string> { first.PackageId };
        if (!string.IsNullOrWhiteSpace(first.Signal))
            bits.Add(first.Signal);
        return string.Join("  ·  ", bits);
    }

    static string DescribeQueueSource(SourceSnapshot source) =>
        source.Summary ?? ShortName(source.PackageId);

    static string ShortName(string packageId) =>
        packageId.Replace("Plugin.Maui.", "", StringComparison.Ordinal);

    static bool IsPresent(SourceSnapshot source) =>
        source.State is not SourceState.NotInstalled;
}
