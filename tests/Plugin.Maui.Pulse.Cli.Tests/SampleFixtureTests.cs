using Plugin.Maui.Pulse.Cli;

namespace Plugin.Maui.Pulse.Cli.Tests;

public sealed class SampleFixtureTests
{
    static string Fixtures => Path.Combine(AppContext.BaseDirectory, "fixtures");

    [Fact]
    public async Task ListenStdinUsesSampleJson()
    {
        var json = await File.ReadAllTextAsync(Path.Combine(Fixtures, "listen-network.json"));
        var stdout = new StringWriter();
        var code = await CliHost.RunAsync(
            ["listen", "--package", "com.test.androidapp", "--android", "--stdin", "--once", "--format", "json", "--no-update-check"],
            stdout,
            new StringWriter(),
            new StringReader(json));
        Assert.Equal(0, code);
        Assert.Contains("com.test.androidapp", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("captive portal", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task FileCommandsReadSampleFixtures()
    {
        var pulled = Path.Combine(Path.GetTempPath(), $"pulse-sample-{Guid.NewGuid():N}");
        var zip = Path.Combine(Path.GetTempPath(), $"pulse-sample-{Guid.NewGuid():N}.zip");
        try
        {
            var pull = await Run(
                ["pull", "--package", "com.test.androidapp", "--from", Fixtures, "--out", pulled, "--format", "json", "--no-update-check"]);
            Assert.Equal(0, pull.Code);
            Assert.Contains("plugin.maui.jobqueue.db3", pull.Stdout, StringComparison.Ordinal);
            Assert.DoesNotContain("logcat", pull.Stdout, StringComparison.Ordinal);

            var queues = await Run(
                ["queues", "--package", "com.test.androidapp", "--from", pulled, "--format", "json", "--no-update-check"]);
            Assert.Equal(0, queues.Code);
            Assert.Contains("payment.retry", queues.Stdout, StringComparison.Ordinal);

            var sync = await Run(
                ["sync", "--package", "com.test.androidapp", "--from", pulled, "--format", "json", "--no-update-check"]);
            Assert.Equal(0, sync.Code);
            Assert.Contains("visits#184", sync.Stdout, StringComparison.Ordinal);

            var incident = await Run(
                ["incident", "--package", "com.test.androidapp", "--from", pulled, "--out", zip, "--format", "json", "--no-update-check"]);
            Assert.Equal(0, incident.Code);
            Assert.True(File.Exists(zip));
            Assert.DoesNotContain("logcat", incident.Stdout, StringComparison.Ordinal);

            var attach = await Run(
                ["attach", "--package", "com.test.androidapp", "--android", "--from", pulled, "--once", "--format", "json", "--no-update-check"]);
            Assert.Equal(0, attach.Code);
            Assert.Contains("Plugin.Maui.RetryQueue", attach.Stdout, StringComparison.Ordinal);
            Assert.Contains("Plugin.Maui.OfflineSync", attach.Stdout, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(pulled))
                Directory.Delete(pulled, recursive: true);
            if (File.Exists(zip))
                File.Delete(zip);
        }
    }

    static async Task<(int Code, string Stdout)> Run(string[] args)
    {
        var stdout = new StringWriter();
        var code = await CliHost.RunAsync(args, stdout, new StringWriter(), new StringReader(""));
        return (code, stdout.ToString());
    }
}
