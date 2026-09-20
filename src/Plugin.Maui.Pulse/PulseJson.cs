using System.Text.Json;

namespace Plugin.Maui.Pulse;

static class PulseJson
{
    public static string Signal(
        string package,
        string source,
        string lane,
        string signal,
        string summary,
        IReadOnlyDictionary<string, string>? data = null)
    {
        var payload = new Dictionary<string, object?>
        {
            ["package"] = package,
            ["source"] = source,
            ["lane"] = lane,
            ["signal"] = signal,
            ["summary"] = summary,
            ["state"] = "running",
            ["at"] = DateTimeOffset.UtcNow
        };
        if (data is { Count: > 0 })
            payload["data"] = data;
        return JsonSerializer.Serialize(payload);
    }

    public static string Presence(string package, IEnumerable<(string Id, string Lane, string State)> plugins)
    {
        var rows = plugins.Select(plugin => new
        {
            id = plugin.Id,
            lane = plugin.Lane,
            state = plugin.State
        });
        return JsonSerializer.Serialize(new { package, plugins = rows });
    }
}
