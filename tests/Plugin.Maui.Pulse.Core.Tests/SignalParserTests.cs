using Plugin.Maui.Pulse;

namespace Plugin.Maui.Pulse.Core.Tests;

public sealed class SignalParserTests
{
    [Fact]
    public void AcceptsAllowListedSource()
    {
        var result = SignalParser.Parse(
            """{"source":"Plugin.Maui.NetworkMonitor","signal":"StatusChanged","summary":"captive portal"}""");
        Assert.Equal(0, result.Dropped);
        Assert.Single(result.Signals);
        Assert.Equal(PluginCatalog.NetworkMonitor, result.Signals[0].Source);
        Assert.Equal("captive portal", result.Signals[0].Summary);
    }

    [Fact]
    public void DropsUnknownSource()
    {
        var result = SignalParser.Parse(
            """{"source":"Firebase.Crashlytics","signal":"crash"}""");
        Assert.Equal(1, result.Dropped);
        Assert.Empty(result.Signals);
    }

    [Fact]
    public void DropsObservabilityDomainsOutsideAllowList()
    {
        var result = SignalParser.Parse(
            """{"signals":[{"domain":"SmartUpload","message":"chunk"},{"domain":"Network","message":"wifi"}]}""");
        Assert.Equal(1, result.Dropped);
        Assert.Single(result.Signals);
        Assert.Equal(PluginCatalog.NetworkMonitor, result.Signals[0].Source);
    }

    [Fact]
    public void PresenceIgnoresUnknownPlugins()
    {
        var result = SignalParser.Parse(
            """{"plugins":[{"id":"Plugin.Maui.OfflineSync","state":"running"},{"id":"Sentry","state":"running"}]}""");
        Assert.Single(result.Presence);
        Assert.Equal(PluginCatalog.OfflineSync, result.Presence[0].Id);
        Assert.Equal(1, result.Dropped);
    }

    [Fact]
    public void DropsSignalForAnotherAppPackage()
    {
        var result = SignalParser.Parse(
            """{"package":"com.other.app","source":"Plugin.Maui.NetworkMonitor","summary":"captive portal"}""",
            "com.test.androidapp");
        Assert.Equal(1, result.Dropped);
        Assert.Empty(result.Signals);
    }

    [Fact]
    public void KeepsSignalWhenPackageMatches()
    {
        var result = SignalParser.Parse(
            """{"package":"com.test.androidapp","source":"Plugin.Maui.NetworkMonitor","summary":"captive portal"}""",
            "com.test.androidapp");
        Assert.Single(result.Signals);
        Assert.Equal("com.test.androidapp", result.Signals[0].Package);
    }

    [Fact]
    public void RejectsLaneMismatch()
    {
        var result = SignalParser.Parse(
            """{"source":"Plugin.Maui.NetworkMonitor","lane":"sync","summary":"no"}""");
        Assert.Equal(1, result.Dropped);
        Assert.Empty(result.Signals);
    }
}
