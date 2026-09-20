using System.Text.Json;

namespace Plugin.Maui.Pulse;

public static class SignalParser
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static ParseResult Parse(string json, string? expectedPackage = null)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new ParseResult();

        try
        {
            using var doc = JsonDocument.Parse(json);
            return ForPackage(Parse(doc.RootElement), expectedPackage);
        }
        catch (JsonException)
        {
            return new ParseResult { Dropped = 1 };
        }
    }

    public static ParseResult ForPackage(ParseResult result, string? expectedPackage)
    {
        if (string.IsNullOrWhiteSpace(expectedPackage))
            return result;

        var keptSignals = new List<PulseSignal>();
        var dropped = result.Dropped;
        foreach (var signal in result.Signals)
        {
            if (Mismatches(signal.Package, expectedPackage))
            {
                dropped++;
                continue;
            }

            keptSignals.Add(signal);
        }

        return new ParseResult
        {
            Signals = keptSignals,
            Presence = result.Presence,
            Dropped = dropped
        };
    }

    public static bool SamePackage(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left)
        && !string.IsNullOrWhiteSpace(right)
        && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    static bool Mismatches(string? actual, string expected) =>
        !string.IsNullOrWhiteSpace(actual) && !SamePackage(actual, expected);

    public static ParseResult Parse(JsonElement root)
    {
        var signals = new List<PulseSignal>();
        var presence = new List<PluginPresence>();
        var dropped = 0;

        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
                Ingest(item, signals, presence, ref dropped, null);
            return new ParseResult { Signals = signals, Presence = presence, Dropped = dropped };
        }

        if (root.ValueKind != JsonValueKind.Object)
            return new ParseResult { Dropped = 1 };

        if (TryReadPlugins(root, out var listed))
        {
            foreach (var item in listed)
            {
                if (!AcceptPresence(item, out var row))
                {
                    dropped++;
                    continue;
                }

                presence.Add(row);
            }
        }

        var batchPackage = ReadAppPackage(root);

        if (root.TryGetProperty("signals", out var batch) && batch.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in batch.EnumerateArray())
                Ingest(item, signals, presence, ref dropped, batchPackage);
            return new ParseResult { Signals = signals, Presence = presence, Dropped = dropped };
        }

        if (LooksLikeSignal(root))
            Ingest(root, signals, presence, ref dropped, batchPackage);

        return new ParseResult { Signals = signals, Presence = presence, Dropped = dropped };
    }

    static void Ingest(JsonElement item, List<PulseSignal> signals, List<PluginPresence> presence, ref int dropped, string? batchPackage)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            dropped++;
            return;
        }

        if (item.TryGetProperty("id", out _) && item.TryGetProperty("state", out _) && !item.TryGetProperty("source", out _))
        {
            var raw = item.Deserialize<PluginPresence>(JsonOptions);
            if (raw is null || !AcceptPresence(raw, out var row))
            {
                dropped++;
                return;
            }

            presence.Add(row);
            return;
        }

        if (!TryMaterialize(item, out var signal, batchPackage))
        {
            dropped++;
            return;
        }

        signals.Add(signal);
    }

    static bool TryMaterialize(JsonElement item, out PulseSignal signal, string? batchPackage)
    {
        signal = new PulseSignal();
        var source = ReadString(item, "source") ?? "";
        if (string.IsNullOrWhiteSpace(source) && item.TryGetProperty("domain", out var domain))
            PluginCatalog.TryMapObservabilityDomain(domain.GetString(), out source);

        if (string.IsNullOrWhiteSpace(source) || !PluginCatalog.TryGet(source, out var known))
            return false;

        var lane = ReadString(item, "lane");
        if (!string.IsNullOrWhiteSpace(lane)
            && PluginCatalog.TryParseLane(lane, out var parsed)
            && parsed != known.Lane)
        {
            return false;
        }

        signal = new PulseSignal
        {
            Package = ReadAppPackage(item) ?? batchPackage,
            Source = known.PackageId,
            Lane = PluginCatalog.LaneLabel(known.Lane).ToLowerInvariant(),
            Signal = ReadString(item, "signal") ?? ReadString(item, "name"),
            State = ReadString(item, "state"),
            Summary = ReadString(item, "summary") ?? ReadString(item, "message"),
            At = ReadTime(item),
            Data = ReadData(item)
        };
        return true;
    }

    static bool AcceptPresence(PluginPresence item, out PluginPresence accepted)
    {
        accepted = item;
        if (!PluginCatalog.TryGet(item.Id, out var known))
            return false;

        accepted = new PluginPresence
        {
            Id = known.PackageId,
            Lane = known.Lane.ToString().ToLowerInvariant(),
            State = NormalizeState(item.State)
        };
        return true;
    }

    static bool TryReadPlugins(JsonElement root, out List<PluginPresence> items)
    {
        items = [];
        if (!root.TryGetProperty("plugins", out var plugins) || plugins.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var item in plugins.EnumerateArray())
        {
            var row = item.Deserialize<PluginPresence>(JsonOptions);
            if (row is not null)
                items.Add(row);
        }

        return true;
    }

    static bool LooksLikeSignal(JsonElement root) =>
        root.TryGetProperty("source", out _) || root.TryGetProperty("domain", out _);

    static string? ReadAppPackage(JsonElement item) =>
        ReadString(item, "package") ?? ReadString(item, "applicationId") ?? ReadString(item, "app");

    static string? ReadString(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    static DateTimeOffset? ReadTime(JsonElement item)
    {
        if (!item.TryGetProperty("at", out var value))
            return null;
        if (value.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(value.GetString(), out var parsed))
            return parsed;
        return null;
    }

    static Dictionary<string, string>? ReadData(JsonElement item)
    {
        if (!item.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            return null;

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in data.EnumerateObject())
        {
            map[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString() ?? "",
                JsonValueKind.Number => property.Value.ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => property.Value.GetRawText()
            };
        }

        return map;
    }

    public static string NormalizeState(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return PluginCatalog.StateLabel(SourceState.NotInstalled);

        return value.Trim().ToLowerInvariant() switch
        {
            "not_installed" or "notinstalled" or "missing" => PluginCatalog.StateLabel(SourceState.NotInstalled),
            "not_registered" or "notregistered" => PluginCatalog.StateLabel(SourceState.NotRegistered),
            "quiet" or "idle" => PluginCatalog.StateLabel(SourceState.Quiet),
            "running" or "active" or "ok" => PluginCatalog.StateLabel(SourceState.Running),
            "from_file" or "fromfile" or "file" => PluginCatalog.StateLabel(SourceState.FromFile),
            _ => PluginCatalog.StateLabel(SourceState.NotInstalled)
        };
    }

    public static SourceState ParseState(string? value)
    {
        return NormalizeState(value) switch
        {
            "not_registered" => SourceState.NotRegistered,
            "quiet" => SourceState.Quiet,
            "running" => SourceState.Running,
            "from_file" => SourceState.FromFile,
            _ => SourceState.NotInstalled
        };
    }
}
