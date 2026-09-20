using Microsoft.Maui.Devices;

namespace Plugin.Maui.Pulse;

sealed class PulseHostRuntime : IDisposable
{
    readonly PulseHostOptions _options;
    readonly PluginHookBinder _binder = new();
    readonly List<IDisposable> _subscriptions = [];
    PulseSink? _sink;
    string _package = "";
    int _started;

    public PulseHostRuntime(PulseHostOptions options)
    {
        _options = options;
    }

    public void Start(IServiceProvider services)
    {
        if (!_options.Enabled || Interlocked.Exchange(ref _started, 1) == 1)
            return;

        _package = ResolvePackage();
        _sink = new PulseSink(ResolveEndpoint());
        var plugins = _binder.Bind(services);
        _ = _sink.PostAsync(PulseJson.Presence(_package, plugins.Select(plugin =>
            (plugin.Hook.PackageId, plugin.Hook.Lane, plugin.State))));
        _subscriptions.AddRange(_binder.Subscribe(plugins, OnPluginEvent));
    }

    void OnPluginEvent(PluginHook hook, string signal, object? args)
    {
        if (_sink is null)
            return;

        var summary = PluginHookBinder.Summarize(args);
        Dictionary<string, string>? data = null;
        var sessionId = PluginHookBinder.ReadSessionId(args);
        if (!string.IsNullOrWhiteSpace(sessionId))
            data = new Dictionary<string, string> { ["sessionId"] = sessionId };

        _ = _sink.PostAsync(PulseJson.Signal(_package, hook.PackageId, hook.Lane, signal, summary, data));
    }

    string ResolvePackage()
    {
        if (!string.IsNullOrWhiteSpace(_options.Package))
            return _options.Package.Trim();
        try
        {
            return AppInfo.PackageName;
        }
        catch (Exception)
        {
            return "unknown";
        }
    }

    string ResolveEndpoint()
    {
        if (!string.IsNullOrWhiteSpace(_options.Endpoint))
            return _options.Endpoint.Trim();
        try
        {
            return DeviceInfo.DeviceType == DeviceType.Virtual
                ? "http://10.0.2.2:7878/"
                : "http://127.0.0.1:7878/";
        }
        catch (Exception)
        {
            return "http://127.0.0.1:7878/";
        }
    }

    public void Dispose()
    {
        foreach (var subscription in _subscriptions)
            subscription.Dispose();
        _subscriptions.Clear();
    }
}
