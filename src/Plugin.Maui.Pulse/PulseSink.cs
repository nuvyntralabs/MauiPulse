using System.Net.Http;
using System.Text;

namespace Plugin.Maui.Pulse;

sealed class PulseSink
{
    readonly HttpClient _http;
    readonly string _endpoint;

    public PulseSink(string endpoint, HttpMessageHandler? handler = null)
    {
        _endpoint = endpoint.TrimEnd('/') + "/";
        _http = handler is null ? new HttpClient { Timeout = TimeSpan.FromSeconds(3) } : new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(3) };
    }

    public async Task PostAsync(string json, CancellationToken token = default)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        try
        {
            using var response = await _http.PostAsync(_endpoint, content, token).ConfigureAwait(false);
            _ = response;
        }
        catch (Exception)
        {
            // Attach is optional. A closed CLI must not crash the host.
        }
    }
}
