namespace Plugin.Maui.Pulse;

public interface IProcessRunner
{
    int Run(string fileName, IReadOnlyList<string> arguments, out string stdout, out string stderr);
}

public sealed class DevicePuller
{
    public const string JobQueueRemote = "files/plugin.maui.jobqueue.db3";
    public const string RetryQueueRemote = "files/plugin.maui.retryqueue.db3";
    public const string SyncRemote = "files/offlinesync.db3";
    public const string DiagnosticsRemote = "files/maui-diagnostics";

    readonly IProcessRunner _process;

    public DevicePuller(IProcessRunner process)
    {
        _process = process;
    }

    public PullResult PullFromDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        var copied = new List<string>();
        var skipped = new List<string>();
        var expected = new[]
        {
            (PluginCatalog.JobQueue, PluginCatalog.JobQueueFile, false),
            (PluginCatalog.RetryQueue, PluginCatalog.RetryQueueFile, false),
            (PluginCatalog.OfflineSync, PluginCatalog.OfflineSyncFile, false),
            (PluginCatalog.Diagnostics, PluginCatalog.DiagnosticsFolder, true)
        };

        foreach (var (package, name, directory) in expected)
        {
            var hits = ArtifactLocator.Find(source).Where(hit => hit.PackageId == package).ToArray();
            if (hits.Length == 0)
            {
                skipped.Add($"{package} ({name})");
                continue;
            }

            var hit = hits[0];
            var dest = Path.Combine(destination, name);
            if (directory)
                CopyDirectory(hit.FullPath, dest);
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(hit.FullPath, dest, overwrite: true);
            }

            copied.Add($"{package} → {dest}");
        }

        return new PullResult { Destination = destination, Copied = copied, Skipped = skipped };
    }

    public PullResult PullFromAdb(string destination, string packageId, string? serial)
    {
        Directory.CreateDirectory(destination);
        var copied = new List<string>();
        var skipped = new List<string>();

        var adb = ResolveAdb();
        if (adb is null)
            return new PullResult { Destination = destination, Copied = copied, Skipped = skipped, Error = "adb not found on PATH" };

        foreach (var (package, remote, local, directory) in RemoteFiles())
        {
            var dest = Path.Combine(destination, local);
            var args = new List<string>();
            if (!string.IsNullOrWhiteSpace(serial))
            {
                args.Add("-s");
                args.Add(serial);
            }

            if (directory)
            {
                args.AddRange(["exec-out", "run-as", packageId, "sh", "-c", $"tar -C {remote} -cf - . 2>/dev/null"]);
                var exit = _process.Run(adb, args, out var stdout, out _);
                if (exit != 0 || string.IsNullOrEmpty(stdout))
                {
                    skipped.Add($"{package} ({local})");
                    continue;
                }

                skipped.Add($"{package} ({local}) — directory pull requires --from");
                continue;
            }

            args.AddRange(["exec-out", "run-as", packageId, "cat", remote]);
            var code = _process.Run(adb, args, out var bytesAsText, out var stderr);
            if (code != 0 || string.IsNullOrEmpty(bytesAsText))
            {
                skipped.Add($"{package} ({local})");
                _ = stderr;
                continue;
            }

            // exec-out cat of a binary via text is unsafe; require the runner that writes files.
            skipped.Add($"{package} ({local}) — use --from after copying the db3");
        }

        return new PullResult
        {
            Destination = destination,
            Copied = copied,
            Skipped = skipped,
            Error = copied.Count == 0
                ? "adb run-as cannot stream binaries safely. Copy files off the device, then maui-pulse pull --from <dir>."
                : null
        };
    }

    public static string DefaultDestination() =>
        Path.Combine(Path.GetTempPath(), "maui-pulse", DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmss"));

    static string? ResolveAdb()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidates = new[]
        {
            "adb",
            Path.Combine(home, "Library", "Android", "sdk", "platform-tools", "adb"),
            Path.Combine(home, "AppData", "Local", "Android", "Sdk", "platform-tools", "adb.exe")
        };
        foreach (var candidate in candidates)
        {
            if (candidate == "adb")
                return candidate;
            if (File.Exists(candidate))
                return candidate;
        }

        return "adb";
    }

    static (string Package, string Remote, string Local, bool Directory)[] RemoteFiles() =>
    [
        (PluginCatalog.JobQueue, JobQueueRemote, PluginCatalog.JobQueueFile, false),
        (PluginCatalog.RetryQueue, RetryQueueRemote, PluginCatalog.RetryQueueFile, false),
        (PluginCatalog.OfflineSync, SyncRemote, PluginCatalog.OfflineSyncFile, false),
        (PluginCatalog.Diagnostics, DiagnosticsRemote, PluginCatalog.DiagnosticsFolder, true)
    ];

    static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var dest = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, overwrite: true);
        }
    }
}
