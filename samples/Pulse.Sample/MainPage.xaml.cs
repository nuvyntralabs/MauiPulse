using Microsoft.Extensions.DependencyInjection;

namespace Pulse.Sample;

public partial class MainPage : ContentPage
{
    readonly DemoPlugins _plugins;

    public MainPage()
    {
        _plugins = IPlatformApplication.Current?.Services.GetRequiredService<DemoPlugins>()
            ?? throw new InvalidOperationException("DemoPlugins is not registered.");
        InitializeComponent();
        PackageLabel.Text = $"{AppInfo.PackageName}  ·  {DeviceInfo.Platform}";
        EndpointLabel.Text = DeviceInfo.DeviceType == DeviceType.Virtual
            ? "Pulse endpoint: http://10.0.2.2:7878/"
            : "Pulse endpoint: http://127.0.0.1:7878/  (adb reverse tcp:7878 tcp:7878)";
        FilesLabel.Text = $"App data: {FixtureSeeder.Root}";
    }

    void OnNetworkClicked(object? sender, EventArgs e) => Append(_plugins.RaiseNetwork());

    void OnApiClicked(object? sender, EventArgs e) => Append(_plugins.RaiseApi());

    void OnQueueClicked(object? sender, EventArgs e) => Append(_plugins.RaiseQueue());

    void OnSyncClicked(object? sender, EventArgs e) => Append(_plugins.RaiseSync());

    void OnPermsClicked(object? sender, EventArgs e) => Append(_plugins.RaisePerms());

    void OnHealthClicked(object? sender, EventArgs e) => Append(_plugins.RaiseHealth());

    void OnLeakClicked(object? sender, EventArgs e) => Append(_plugins.RaiseLeak());

    void OnCrashClicked(object? sender, EventArgs e) => Append(_plugins.RaiseCrash());

    void OnSessionClicked(object? sender, EventArgs e) => Append(_plugins.RaiseSession());

    void OnAllLanesClicked(object? sender, EventArgs e)
    {
        foreach (var name in _plugins.RaiseAll())
            Append(name);
    }

    async void OnSeedClicked(object? sender, EventArgs e)
    {
        try
        {
            var root = await FixtureSeeder.SeedAsync();
            FilesLabel.Text =
                $"Seeded {root}{Environment.NewLine}" +
                $"Copy those files off the device, then:{Environment.NewLine}" +
                $"maui-pulse pull --package {AppInfo.PackageName} --from <dir> --out ./pulled";
            Append($"seeded {root}");
        }
        catch (Exception ex)
        {
            Append($"seed failed: {ex.Message}");
        }
    }

    void Append(string line)
    {
        var next = $"{DateTime.Now:HH:mm:ss}  raised {line}{Environment.NewLine}{LogLabel.Text}";
        LogLabel.Text = next.Length > 4000 ? next[..4000] : next;
    }
}
