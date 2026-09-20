using System.Text;
using System.Text.Json;

namespace Plugin.Maui.Pulse;

public static class SessionFormatter
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static string Human(SessionSnapshot snapshot)
    {
        var elapsed = DateTimeOffset.UtcNow - snapshot.StartedAt;
        if (elapsed < TimeSpan.Zero)
            elapsed = TimeSpan.Zero;
        var header = $"{snapshot.Package ?? snapshot.App}  ·  {snapshot.Device ?? "local"}  ·  session {snapshot.SessionId ?? "—"}  ·  {elapsed:hh\\:mm\\:ss}";
        var builder = new StringBuilder();
        builder.AppendLine(header);
        builder.AppendLine();
        foreach (var lane in snapshot.Lanes)
        {
            builder.Append(PluginCatalog.LaneLabel(lane.Lane).PadRight(9));
            builder.Append(lane.Headline.PadRight(18));
            builder.Append(lane.Detail);
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    public static string Json(SessionSnapshot snapshot)
    {
        var payload = new
        {
            snapshot.App,
            snapshot.Package,
            snapshot.Device,
            snapshot.SessionId,
            startedAt = snapshot.StartedAt,
            acceptedSignals = snapshot.AcceptedSignals,
            droppedSignals = snapshot.DroppedSignals,
            hasEvidence = snapshot.HasAnyEvidence,
            lanes = snapshot.Lanes.Select(lane => new
            {
                id = lane.Lane.ToString().ToLowerInvariant(),
                label = PluginCatalog.LaneLabel(lane.Lane),
                headline = lane.Headline,
                detail = lane.Detail,
                sources = lane.Sources.Select(source => new
                {
                    source = source.PackageId,
                    state = PluginCatalog.StateLabel(source.State),
                    signal = source.Signal,
                    summary = source.Summary,
                    at = source.At
                })
            })
        };
        return JsonSerializer.Serialize(payload, JsonOptions);
    }
}
