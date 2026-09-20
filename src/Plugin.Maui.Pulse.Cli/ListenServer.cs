using System.Net;
using System.Text;
using Plugin.Maui.Pulse;

namespace Plugin.Maui.Pulse.Cli;

public sealed class ListenServer : IAsyncDisposable
{
    readonly HttpListener _listener = new();
    readonly SessionStore _store;
    readonly string _package;
    readonly Action<ParseResult> _onPayload;
    CancellationTokenSource? _cts;
    Task? _loop;

    public ListenServer(SessionStore store, string package, int port, Action<ParseResult> onPayload)
    {
        _store = store;
        _package = package;
        _onPayload = onPayload;
        Prefix = $"http://127.0.0.1:{port}/";
        _listener.Prefixes.Add(Prefix);
    }

    public string Prefix { get; }

    public void Start()
    {
        _listener.Start();
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => LoopAsync(_cts.Token));
    }

    async Task LoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().WaitAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (HttpListenerException)
            {
                break;
            }

            await HandleAsync(context).ConfigureAwait(false);
        }
    }

    async Task HandleAsync(HttpListenerContext context)
    {
        try
        {
            if (context.Request.HttpMethod == "GET")
            {
                await WriteAsync(context, 200, """{"ok":true,"tool":"maui-pulse"}""").ConfigureAwait(false);
                return;
            }

            if (context.Request.HttpMethod != "POST")
            {
                await WriteAsync(context, 405, """{"error":"POST JSON from an allow-listed Plugin.Maui.* source"}""").ConfigureAwait(false);
                return;
            }

            using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
            var body = await reader.ReadToEndAsync().ConfigureAwait(false);
            var parsed = SignalParser.Parse(body, _package);
            _store.Apply(parsed);
            _onPayload(parsed);
            await WriteAsync(context, 202, """{"accepted":true}""").ConfigureAwait(false);
        }
        catch
        {
            try { context.Response.Abort(); } catch { /* ignore */ }
        }
    }

    static async Task WriteAsync(HttpListenerContext context, int status, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        context.Response.Close();
    }

    public async ValueTask DisposeAsync()
    {
        if (_cts is not null)
            await _cts.CancelAsync().ConfigureAwait(false);
        try { _listener.Stop(); } catch { /* ignore */ }
        _listener.Close();
        if (_loop is not null)
        {
            try { await _loop.ConfigureAwait(false); } catch { /* ignore */ }
        }

        _cts?.Dispose();
    }
}
