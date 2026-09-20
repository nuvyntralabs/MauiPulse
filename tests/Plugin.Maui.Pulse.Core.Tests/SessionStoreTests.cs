using Plugin.Maui.Pulse;

namespace Plugin.Maui.Pulse.Core.Tests;

public sealed class SessionStoreTests
{
    [Fact]
    public void MissingPluginsStayNotInstalled()
    {
        var store = new SessionStore();
        store.Apply(SignalParser.Parse(
            """{"source":"Plugin.Maui.NetworkMonitor","summary":"captive portal"}"""));
        var snapshot = store.Snapshot();
        var network = snapshot.Lanes.Single(lane => lane.Lane == PulseLane.Network);
        var sync = snapshot.Lanes.Single(lane => lane.Lane == PulseLane.Sync);
        Assert.Equal("captive portal", network.Headline);
        Assert.Equal("— not installed", sync.Headline);
        Assert.True(snapshot.HasAnyEvidence);
    }

    [Fact]
    public void QueueSourcesDegradeIndependently()
    {
        var store = new SessionStore();
        store.Apply(SignalParser.Parse(
            """{"plugins":[{"id":"Plugin.Maui.JobQueue","state":"running"},{"id":"Plugin.Maui.RetryQueue","state":"not_installed"}]}"""));
        store.Apply(SignalParser.Parse(
            """{"source":"Plugin.Maui.JobQueue","summary":"4 pending"}"""));
        var queue = store.Snapshot().Lanes.Single(lane => lane.Lane == PulseLane.Queue);
        Assert.Contains("4 pending", queue.Headline, StringComparison.Ordinal);
        Assert.Contains("RetryQueue — not installed", queue.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptySessionHasNoEvidence()
    {
        Assert.False(new SessionStore().Snapshot().HasAnyEvidence);
    }
}
