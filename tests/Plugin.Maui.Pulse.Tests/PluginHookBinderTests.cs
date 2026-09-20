using Microsoft.Extensions.DependencyInjection;
using Plugin.Maui.AppHealth;

namespace Plugin.Maui.Pulse.Tests
{
    public sealed class PluginHookBinderTests
    {
        [Fact]
        public void BindMarksMissingPluginsNotInstalled()
        {
            var services = new ServiceCollection().BuildServiceProvider();
            var bound = new PluginHookBinder().Bind(services);
            Assert.Contains(bound, plugin => plugin.Hook.PackageId == "Plugin.Maui.NetworkMonitor" && plugin.State == "not_installed");
        }

        [Fact]
        public void BindAndSubscribeForwardsHealthChanged()
        {
            var health = new FakeAppHealth();
            var services = new ServiceCollection()
                .AddSingleton<IAppHealth>(health)
                .BuildServiceProvider();
            var binder = new PluginHookBinder();
            var bound = binder.Bind(services);
            var row = Assert.Single(bound, plugin => plugin.Hook.PackageId == "Plugin.Maui.AppHealth");
            Assert.Equal("quiet", row.State);

            PluginHook? seen = null;
            string? signal = null;
            object? args = null;
            var subscriptions = binder.Subscribe(bound, (hook, name, value) =>
            {
                seen = hook;
                signal = name;
                args = value;
            });

            health.Raise();
            Assert.Equal("Plugin.Maui.AppHealth", seen?.PackageId);
            Assert.Equal("HealthChanged", signal);
            Assert.Equal("battery 18%", PluginHookBinder.Summarize(args));
            foreach (var subscription in subscriptions)
                subscription.Dispose();
        }

        [Fact]
        public void PresenceJsonListsStates()
        {
            var json = PulseJson.Presence("com.myapp.android",
            [
                ("Plugin.Maui.AppHealth", "health", "quiet"),
                ("Plugin.Maui.OfflineSync", "sync", "not_installed")
            ]);
            Assert.Contains("com.myapp.android", json, StringComparison.Ordinal);
            Assert.Contains("Plugin.Maui.AppHealth", json, StringComparison.Ordinal);
            Assert.Contains("not_installed", json, StringComparison.Ordinal);
        }
    }

    public sealed class FakeAppHealth : IAppHealth
    {
        public event EventHandler<HealthChangedEventArgs>? HealthChanged;

        public void Raise() => HealthChanged?.Invoke(this, new HealthChangedEventArgs(new HealthReport()));
    }
}

namespace Plugin.Maui.AppHealth
{
    public sealed class HealthReport
    {
        public override string ToString() => "battery 18%";
    }

    public sealed class HealthChangedEventArgs(HealthReport current) : EventArgs
    {
        public HealthReport Current { get; } = current;
    }

    public interface IAppHealth
    {
        event EventHandler<HealthChangedEventArgs>? HealthChanged;
    }
}
