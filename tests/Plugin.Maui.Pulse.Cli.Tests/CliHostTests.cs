using Plugin.Maui.Pulse;
using Plugin.Maui.Pulse.Cli;

namespace Plugin.Maui.Pulse.Cli.Tests;

public sealed class CliHostTests
{
    [Fact]
    public async Task NoArgsPrintsUsage()
    {
        var stdout = new StringWriter();
        var code = await CliHost.RunAsync([], stdout, new StringWriter(), new StringReader(""));
        Assert.Equal(0, code);
        Assert.Contains("maui-pulse listen", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("maui-pulse incident", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("maui-pulse attach --package com.test.androidapp --android --port 7878 --no-update-check", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("maui-pulse attach --package com.test.ios --ios --port 7878 --no-update-check", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownCommandIsUsageError()
    {
        var stderr = new StringWriter();
        var code = await CliHost.RunAsync(["doctor"], new StringWriter(), stderr, new StringReader(""));
        Assert.Equal(ExitCodes.Usage, code);
        Assert.Contains("Unknown command", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListenStdinAcceptsAllowListedJson()
    {
        var stdout = new StringWriter();
        var code = await CliHost.RunAsync(
            ["listen", "--package", "com.test.androidapp", "--android", "--stdin", "--once", "--format", "json", "--no-update-check"],
            stdout,
            new StringWriter(),
            new StringReader("""{"source":"Plugin.Maui.AppHealth","summary":"battery 18%"}"""));
        Assert.Equal(0, code);
        var json = stdout.ToString();
        Assert.Contains("Plugin.Maui.AppHealth", json, StringComparison.Ordinal);
        Assert.Contains("battery 18%", json, StringComparison.Ordinal);
        Assert.Contains("not_installed", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListenStdinDropsUnknownAndExitsIssuesWhenEmpty()
    {
        var stdout = new StringWriter();
        var code = await CliHost.RunAsync(
            ["listen", "--package", "com.test.androidapp", "--android", "--stdin", "--once", "--format", "json", "--no-update-check"],
            stdout,
            new StringWriter(),
            new StringReader("""{"source":"Sentry","summary":"boom"}"""));
        Assert.Equal(ExitCodes.Issues, code);
        Assert.DoesNotContain("Sentry", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueuesMissingFileIsNotInstalled()
    {
        var stdout = new StringWriter();
        var code = await CliHost.RunAsync(
            ["queues", "--package", "com.test.androidapp", "--format", "json", "--no-update-check"],
            stdout,
            new StringWriter(),
            new StringReader(""));
        Assert.Equal(0, code);
        Assert.Contains("not_installed", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AttachFromDirectorySeedsFileLanes()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pulse-cli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var dbPath = Path.Combine(root, PluginCatalog.RetryQueueFile);
            using (var db = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}"))
            {
                db.Open();
                using var command = db.CreateCommand();
                command.CommandText =
                    """
                    CREATE TABLE Operations (
                      Id TEXT PRIMARY KEY,
                      OperationName TEXT,
                      Status INTEGER,
                      NextAttemptAtUtc TEXT,
                      LastError TEXT
                    );
                    """;
                command.ExecuteNonQuery();
                command.CommandText = "INSERT INTO Operations VALUES ('1','payment.retry',3,'2099-01-01T00:00:00.0000000+00:00',null);";
                command.ExecuteNonQuery();
            }

            var stdout = new StringWriter();
            var code = await CliHost.RunAsync(
                ["attach", "--package", "com.test.androidapp", "--android", "--from", root, "--once", "--format", "json", "--no-update-check"],
                stdout,
                new StringWriter(),
                new StringReader(""));
            Assert.Equal(0, code);
            Assert.Contains("com.test.androidapp", stdout.ToString(), StringComparison.Ordinal);
            Assert.Contains("Plugin.Maui.RetryQueue", stdout.ToString(), StringComparison.Ordinal);
            Assert.Contains("payment.retry", stdout.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("logcat", stdout.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task VersionPrintsSemVer()
    {
        var stdout = new StringWriter();
        var code = await CliHost.RunAsync(["version", "--no-update-check"], stdout, new StringWriter(), new StringReader(""));
        Assert.Equal(0, code);
        Assert.Contains("maui-pulse 1.0.0", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PullRequiresFrom()
    {
        var stderr = new StringWriter();
        var code = await CliHost.RunAsync(
            ["pull", "--package", "com.test.androidapp", "--no-update-check"],
            new StringWriter(),
            stderr,
            new StringReader(""));
        Assert.Equal(ExitCodes.Usage, code);
        Assert.Contains("--from", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AttachRequiresPackage()
    {
        var stderr = new StringWriter();
        var code = await CliHost.RunAsync(
            ["attach", "--android", "--port", "7878", "--no-update-check"],
            new StringWriter(),
            stderr,
            new StringReader(""));
        Assert.Equal(ExitCodes.Usage, code);
        Assert.Contains("--package", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AttachRequiresAndroidOrIos()
    {
        var stderr = new StringWriter();
        var code = await CliHost.RunAsync(
            ["attach", "--package", "com.test.ios", "--port", "7878", "--no-update-check"],
            new StringWriter(),
            stderr,
            new StringReader(""));
        Assert.Equal(ExitCodes.Usage, code);
        Assert.Contains("--android or --ios", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AttachAcceptsIosAlias()
    {
        var stdout = new StringWriter();
        var code = await CliHost.RunAsync(
            ["attach", "--package", "com.test.ios", "--iOS", "--stdin", "--once", "--format", "json", "--no-update-check"],
            stdout,
            new StringWriter(),
            new StringReader("""{"package":"com.test.ios","source":"Plugin.Maui.AppHealth","summary":"battery 18%"}"""));
        Assert.Equal(0, code);
        Assert.Contains("com.test.ios", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"device\": \"ios\"", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListenDropsJsonForAnotherPackage()
    {
        var stdout = new StringWriter();
        var code = await CliHost.RunAsync(
            ["listen", "--package", "com.test.androidapp", "--android", "--stdin", "--once", "--format", "json", "--no-update-check"],
            stdout,
            new StringWriter(),
            new StringReader("""{"package":"com.other.app","source":"Plugin.Maui.AppHealth","summary":"battery 18%"}"""));
        Assert.Equal(ExitCodes.Issues, code);
        Assert.DoesNotContain("battery 18%", stdout.ToString(), StringComparison.Ordinal);
    }
}
