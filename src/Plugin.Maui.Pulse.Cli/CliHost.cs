using System.CommandLine;
using System.Net.Sockets;
using Plugin.Maui.Pulse;
using Spectre.Console;

namespace Plugin.Maui.Pulse.Cli;

public static class CliHost
{
    public const string Usage =
        """
        maui-pulse — live session viewer for Nuvyntra Plugin.Maui.* data only

          maui-pulse listen      HTTP / stdin sink (Observability or Pulse JSON)
          maui-pulse attach      Live lane table (same sink, plus --from files)
          maui-pulse pull        Copy known plugin files from --from (or hint adb)
          maui-pulse queues      Inspect JobQueue / RetryQueue db3 files
          maui-pulse sync        Inspect offlinesync.db3
          maui-pulse incident    Zip allow-listed plugin files + manifest
          maui-pulse version     Print the CLI version

        Options
          --package <id>         Required. One applicationId / bundle id per session
          --android|--ios        Required on listen / attach (not both)
          --format human|json    Output format
          --lanes <list>         network,api,queue,sync,perms,health,leak,crash,session
          --no-update-check      Skip the nuget.org self-update prompt

        listen / attach
          --port <n>             HTTP bind (default 7878)
          --stdin                Read one JSON payload from stdin
          --once                 Exit after the first allow-listed payload
          --from <dir>           Seed lanes from pulled plugin files

        pull / queues / sync / incident
          --from <dir>           Directory that already has plugin files
          --out <path>           Destination folder or zip
          --db <path>            One db3 (queues uses JobQueue; sync uses OfflineSync)
          --job-db / --retry-db  Explicit queue files
          --serial <id>          adb serial

        Examples
          maui-pulse attach --package com.test.androidapp --android --port 7878 --no-update-check
          maui-pulse attach --package com.test.ios --ios --port 7878 --no-update-check

        Pulse drops unknown sources. Missing plugins skip that lane.

        Docs: https://nuvyntralabs.github.io/toolkits/maui-pulse/
        """;

    public static Task<int> RunAsync(IReadOnlyList<string> args, TextWriter stdout, TextWriter stderr) =>
        RunAsync(args, stdout, stderr, Console.In);

    public static async Task<int> RunAsync(
        IReadOnlyList<string> args,
        TextWriter stdout,
        TextWriter stderr,
        TextReader stdin)
    {
        var remaining = ToolUpdateCheck.ConsumeNoUpdateCheckFlag(args, out var noUpdateCheck);
        var update = ToolUpdateCheck.Run(new UpdateCheckOptions
        {
            ToolKey = "maui-pulse",
            PackageId = "Plugin.Maui.Pulse.Cli",
            CurrentVersion = ToolUpdateCheck.ReadAssemblyVersion(typeof(CliHost)),
            Args = remaining,
            Stdout = stdout,
            Stderr = stderr,
            Stdin = stdin,
            AllowPrompt = ToolUpdateCheck.IsInteractive(stdout) && !noUpdateCheck,
        });
        if (update == UpdateCheckOutcome.UpdatedExit)
            return ExitCodes.Success;

        if (remaining.Length == 0 || IsHelp(remaining[0]))
        {
            stdout.WriteLine(Usage);
            return ExitCodes.Success;
        }

        var root = BuildRoot(stdout, stderr, stdin);
        var parse = root.Parse(remaining);
        if (parse.Errors.Count > 0 && !LooksLikeKnownCommand(remaining[0]))
        {
            stderr.WriteLine($"Unknown command '{remaining[0]}'.");
            stderr.WriteLine();
            stderr.WriteLine(Usage);
            return ExitCodes.Usage;
        }

        if (parse.Errors.Count > 0 && LooksLikeKnownCommand(remaining[0]) && remaining[0] is not "version" && !HasFlag(remaining, "--package"))
        {
            stderr.WriteLine("maui-pulse requires --package <applicationId> (one app per session).");
            stderr.WriteLine("  maui-pulse attach --package com.test.androidapp --android --port 7878");
            stderr.WriteLine("  maui-pulse attach --package com.test.ios --ios --port 7878");
            return ExitCodes.Usage;
        }

        return await parse.InvokeAsync(new InvocationConfiguration
        {
            Output = stdout,
            Error = stderr
        }).ConfigureAwait(false);
    }

    static RootCommand BuildRoot(TextWriter stdout, TextWriter stderr, TextReader stdin)
    {
        var format = new Option<string>("--format")
        {
            Description = "human | json",
            DefaultValueFactory = _ => "human"
        };
        var lanes = new Option<string?>("--lanes") { Description = "Comma-separated lanes" };
        var port = new Option<int>("--port")
        {
            Description = "HTTP port",
            DefaultValueFactory = _ => 7878
        };
        var stdinOption = new Option<bool>("--stdin") { Description = "Read JSON from stdin" };
        var once = new Option<bool>("--once") { Description = "Exit after the first allow-listed payload" };
        var from = new Option<string?>("--from") { Description = "Directory of pulled plugin files" };
        var android = new Option<bool>("--android") { Description = "Android session" };
        var ios = new Option<bool>("--ios") { Description = "iOS session" };
        ios.Aliases.Add("--iOS");
        var output = new Option<string?>("--out");
        var db = new Option<string?>("--db");
        var jobDb = new Option<string?>("--job-db");
        var retryDb = new Option<string?>("--retry-db");
        var package = new Option<string>("--package")
        {
            Description = "App applicationId / bundle id. One session per app.",
            Required = true
        };
        var serial = new Option<string?>("--serial");
        var adb = new Option<bool>("--adb");

        var listen = new Command("listen", "HTTP / stdin sink for allow-listed Plugin.Maui.* JSON");
        listen.Options.Add(package);
        listen.Options.Add(android);
        listen.Options.Add(ios);
        listen.Options.Add(format);
        listen.Options.Add(lanes);
        listen.Options.Add(port);
        listen.Options.Add(stdinOption);
        listen.Options.Add(once);
        listen.Options.Add(from);
        listen.SetAction(async (parse, token) =>
            await RunSinkAsync(parse, stdout, stderr, stdin, package, format, lanes, port, stdinOption, once, from, android, ios, attach: false, token).ConfigureAwait(false));

        var attach = new Command("attach", "Live lane table from plugin JSON and/or --from files");
        attach.Options.Add(package);
        attach.Options.Add(android);
        attach.Options.Add(ios);
        attach.Options.Add(format);
        attach.Options.Add(lanes);
        attach.Options.Add(port);
        attach.Options.Add(stdinOption);
        attach.Options.Add(once);
        attach.Options.Add(from);
        attach.SetAction(async (parse, token) =>
            await RunSinkAsync(parse, stdout, stderr, stdin, package, format, lanes, port, stdinOption, once, from, android, ios, attach: true, token).ConfigureAwait(false));

        var pull = new Command("pull", "Copy known plugin files from --from");
        pull.Options.Add(package);
        pull.Options.Add(format);
        pull.Options.Add(from);
        pull.Options.Add(output);
        pull.Options.Add(adb);
        pull.Options.Add(serial);
        pull.SetAction(parse => RunPull(parse, stdout, stderr, package, format, from, output, adb, serial));

        var queues = new Command("queues", "Inspect JobQueue and RetryQueue db3 files");
        queues.Options.Add(package);
        queues.Options.Add(format);
        queues.Options.Add(from);
        queues.Options.Add(db);
        queues.Options.Add(jobDb);
        queues.Options.Add(retryDb);
        queues.SetAction(parse => RunQueues(parse, stdout, stderr, package, format, from, db, jobDb, retryDb));

        var sync = new Command("sync", "Inspect offlinesync.db3");
        sync.Options.Add(package);
        sync.Options.Add(format);
        sync.Options.Add(from);
        sync.Options.Add(db);
        sync.SetAction(parse => RunSync(parse, stdout, stderr, package, format, from, db));

        var incident = new Command("incident", "Zip allow-listed plugin files");
        incident.Options.Add(package);
        incident.Options.Add(format);
        incident.Options.Add(from);
        incident.Options.Add(output);
        incident.SetAction(parse => RunIncident(parse, stdout, stderr, package, format, from, output));

        var version = new Command("version", "Print the CLI version");
        version.SetAction(_ =>
        {
            stdout.WriteLine($"maui-pulse {ToolUpdateCheck.ReadAssemblyVersion(typeof(CliHost))}");
            return ExitCodes.Success;
        });

        var root = new RootCommand("maui-pulse — Plugin.Maui.* session viewer");
        root.Subcommands.Add(listen);
        root.Subcommands.Add(attach);
        root.Subcommands.Add(pull);
        root.Subcommands.Add(queues);
        root.Subcommands.Add(sync);
        root.Subcommands.Add(incident);
        root.Subcommands.Add(version);
        root.SetAction(_ =>
        {
            stdout.WriteLine(Usage);
            return ExitCodes.Success;
        });
        return root;
    }

    static async Task<int> RunSinkAsync(
        System.CommandLine.ParseResult parse,
        TextWriter stdout,
        TextWriter stderr,
        TextReader stdin,
        Option<string> packageOption,
        Option<string> formatOption,
        Option<string?> lanesOption,
        Option<int> portOption,
        Option<bool> stdinOption,
        Option<bool> onceOption,
        Option<string?> fromOption,
        Option<bool> androidOption,
        Option<bool> iosOption,
        bool attach,
        CancellationToken token)
    {
        if (!TryReadPackage(parse, packageOption, stderr, out var package))
            return ExitCodes.Usage;
        if (!TryReadPlatform(parse, androidOption, iosOption, stderr, out var device))
            return ExitCodes.Usage;

        var format = ParseFormat(parse.GetValue(formatOption));
        var lanes = PluginCatalog.ParseLaneFilter(parse.GetValue(lanesOption));
        var store = new SessionStore(
            app: attach ? "maui-pulse attach" : "maui-pulse listen",
            device: device,
            package: package);
        var from = parse.GetValue(fromOption);
        if (!string.IsNullOrWhiteSpace(from))
            FileSessionLoader.Apply(store, from);

        if (parse.GetValue(stdinOption))
        {
            var json = await stdin.ReadToEndAsync(token).ConfigureAwait(false);
            store.Apply(SignalParser.Parse(json, package));
            return WriteSnapshot(store.Snapshot(lanes), format, stdout, requireEvidence: true);
        }

        if (parse.GetValue(onceOption) && !string.IsNullOrWhiteSpace(from))
            return WriteSnapshot(store.Snapshot(lanes), format, stdout, requireEvidence: true);

        var port = parse.GetValue(portOption);
        if (port <= 0)
            port = FreePort();

        await using var server = new ListenServer(store, package, port, _ => { });
        try
        {
            server.Start();
        }
        catch (Exception ex)
        {
            stderr.WriteLine($"Could not bind {server.Prefix}: {ex.Message}");
            return ExitCodes.Issues;
        }

        var snapshot = store.Snapshot(lanes);
        if (format == "human")
        {
            stdout.WriteLine($"Listening on {server.Prefix}  (POST allow-listed Plugin.Maui.* JSON only)");
            stdout.WriteLine("Waiting for Pulse Sample. Tap Network on the phone.");
            stdout.WriteLine();
            stdout.WriteLine(SessionFormatter.Human(snapshot));
        }
        else
        {
            stdout.WriteLine(SessionFormatter.Json(snapshot));
        }

        if (parse.GetValue(onceOption))
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
            while (DateTime.UtcNow < deadline && !token.IsCancellationRequested)
            {
                if (store.Snapshot(lanes).AcceptedSignals > 0 || store.Snapshot(lanes).HasAnyEvidence)
                    return WriteSnapshot(store.Snapshot(lanes), format, stdout, requireEvidence: true);
                await Task.Delay(50, token).ConfigureAwait(false);
            }

            return WriteSnapshot(store.Snapshot(lanes), format, stdout, requireEvidence: true);
        }

        var lastAccepted = snapshot.AcceptedSignals;
        var lastDropped = snapshot.DroppedSignals;
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(200, token).ConfigureAwait(false);
                snapshot = store.Snapshot(lanes);
                if (snapshot.AcceptedSignals == lastAccepted && snapshot.DroppedSignals == lastDropped)
                    continue;

                lastAccepted = snapshot.AcceptedSignals;
                lastDropped = snapshot.DroppedSignals;
                stdout.WriteLine();
                if (format == "human")
                {
                    stdout.WriteLine($"accepted {snapshot.AcceptedSignals}  ·  dropped {snapshot.DroppedSignals}");
                    stdout.WriteLine(SessionFormatter.Human(snapshot));
                }
                else
                {
                    stdout.WriteLine(SessionFormatter.Json(snapshot));
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Ctrl+C
        }

        return WriteSnapshot(store.Snapshot(lanes), format, stdout, requireEvidence: !store.Snapshot(lanes).HasAnyEvidence);
    }

    static int RunPull(
        System.CommandLine.ParseResult parse,
        TextWriter stdout,
        TextWriter stderr,
        Option<string> packageOption,
        Option<string> formatOption,
        Option<string?> fromOption,
        Option<string?> outOption,
        Option<bool> adbOption,
        Option<string?> serialOption)
    {
        if (!TryReadPackage(parse, packageOption, stderr, out var package))
            return ExitCodes.Usage;

        var format = ParseFormat(parse.GetValue(formatOption));
        var dest = parse.GetValue(outOption) ?? DevicePuller.DefaultDestination();
        var puller = new DevicePuller(new SystemProcessRunner());
        PullResult result;
        var from = parse.GetValue(fromOption);
        if (!string.IsNullOrWhiteSpace(from))
        {
            result = puller.PullFromDirectory(from, dest);
        }
        else if (parse.GetValue(adbOption))
        {
            result = puller.PullFromAdb(dest, package, parse.GetValue(serialOption));
        }
        else
        {
            stderr.WriteLine("pull requires --from <dir> (copy plugin files off the device first).");
            return ExitCodes.Usage;
        }

        if (format == "json")
        {
            stdout.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
            {
                package,
                result.Destination,
                result.Copied,
                result.Skipped,
                result.Error
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase }));
        }
        else
        {
            stdout.WriteLine($"Package {package}");
            stdout.WriteLine($"Destination {result.Destination}");
            foreach (var item in result.Copied)
                stdout.WriteLine($"copied  {item}");
            foreach (var item in result.Skipped)
                stdout.WriteLine($"skipped {item}");
            if (!string.IsNullOrWhiteSpace(result.Error))
                stdout.WriteLine(result.Error);
        }

        return result.Copied.Count == 0 ? ExitCodes.Issues : ExitCodes.Success;
    }

    static int RunQueues(
        System.CommandLine.ParseResult parse,
        TextWriter stdout,
        TextWriter stderr,
        Option<string> packageOption,
        Option<string> formatOption,
        Option<string?> fromOption,
        Option<string?> dbOption,
        Option<string?> jobDbOption,
        Option<string?> retryDbOption)
    {
        if (!TryReadPackage(parse, packageOption, stderr, out var package))
            return ExitCodes.Usage;

        var format = ParseFormat(parse.GetValue(formatOption));
        var from = parse.GetValue(fromOption);
        var jobPath = parse.GetValue(jobDbOption) ?? parse.GetValue(dbOption) ?? Find(from, PluginCatalog.JobQueueFile);
        var retryPath = parse.GetValue(retryDbOption) ?? Find(from, PluginCatalog.RetryQueueFile);

        QueueInspector.TryInspectJobQueue(jobPath ?? "", out var jobs, out var jobError);
        QueueInspector.TryInspectRetryQueue(retryPath ?? "", out var retries, out var retryError);

        if (format == "json")
        {
            stdout.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
            {
                package,
                jobQueue = new { state = jobError ?? "from_file", path = jobPath, counts = jobError is null ? jobs : null },
                retryQueue = new { state = retryError ?? "from_file", path = retryPath, counts = retryError is null ? retries : null }
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase }));
        }
        else
        {
            stdout.WriteLine($"Package {package}");
            stdout.WriteLine(jobError is null
                ? QueueInspector.Summarize("JobQueue", jobs)
                : $"JobQueue — {jobError}");
            stdout.WriteLine(retryError is null
                ? QueueInspector.Summarize("RetryQueue", retries)
                : $"RetryQueue — {retryError}");
        }

        return ExitCodes.Success;
    }

    static int RunSync(
        System.CommandLine.ParseResult parse,
        TextWriter stdout,
        TextWriter stderr,
        Option<string> packageOption,
        Option<string> formatOption,
        Option<string?> fromOption,
        Option<string?> dbOption)
    {
        if (!TryReadPackage(parse, packageOption, stderr, out var package))
            return ExitCodes.Usage;

        var format = ParseFormat(parse.GetValue(formatOption));
        var path = parse.GetValue(dbOption) ?? Find(parse.GetValue(fromOption), PluginCatalog.OfflineSyncFile);
        var ok = SyncInspector.TryInspect(path ?? "", out var snapshot, out var error);
        if (format == "json")
        {
            stdout.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
            {
                package,
                source = PluginCatalog.OfflineSync,
                state = ok ? "from_file" : error,
                path,
                snapshot = ok ? snapshot : null
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase }));
        }
        else
        {
            stdout.WriteLine(ok ? $"{package}  ·  {SyncInspector.Summarize(snapshot)}" : $"{package}  ·  OfflineSync — {error}");
        }

        return ExitCodes.Success;
    }

    static int RunIncident(
        System.CommandLine.ParseResult parse,
        TextWriter stdout,
        TextWriter stderr,
        Option<string> packageOption,
        Option<string> formatOption,
        Option<string?> fromOption,
        Option<string?> outOption)
    {
        if (!TryReadPackage(parse, packageOption, stderr, out var package))
            return ExitCodes.Usage;

        var from = parse.GetValue(fromOption);
        if (string.IsNullOrWhiteSpace(from) || !Directory.Exists(from))
        {
            stderr.WriteLine("incident requires --from <dir> with plugin files.");
            return ExitCodes.Usage;
        }

        var hits = ArtifactLocator.Find(from);
        if (hits.Count == 0)
        {
            stderr.WriteLine("No allow-listed plugin files found.");
            return ExitCodes.Issues;
        }

        var zip = IncidentPacker.Pack(from, parse.GetValue(outOption));
        if (ParseFormat(parse.GetValue(formatOption)) == "json")
            stdout.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { package, zip, files = hits.Select(hit => hit.PackageId) }));
        else
            stdout.WriteLine($"{package}  ·  {zip}");
        return ExitCodes.Success;
    }

    static int WriteSnapshot(SessionSnapshot snapshot, string format, TextWriter stdout, bool requireEvidence)
    {
        if (format == "json")
            stdout.WriteLine(SessionFormatter.Json(snapshot));
        else if (ReferenceEquals(stdout, Console.Out) && !Console.IsOutputRedirected)
            WriteSpectre(snapshot);
        else
            stdout.WriteLine(SessionFormatter.Human(snapshot));

        if (requireEvidence && !snapshot.HasAnyEvidence)
            return ExitCodes.Issues;
        return ExitCodes.Success;
    }

    static void WriteSpectre(SessionSnapshot snapshot)
    {
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("Lane");
        table.AddColumn("Headline");
        table.AddColumn("Detail");
        foreach (var lane in snapshot.Lanes)
            table.AddRow(PluginCatalog.LaneLabel(lane.Lane), Markup.Escape(lane.Headline), Markup.Escape(lane.Detail));
        AnsiConsole.Write(table);
    }

    static string? Find(string? root, string name)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            return null;
        return Directory.EnumerateFiles(root, name, SearchOption.AllDirectories).FirstOrDefault();
    }

    static bool TryReadPackage(
        System.CommandLine.ParseResult parse,
        Option<string> packageOption,
        TextWriter stderr,
        out string package)
    {
        package = (parse.GetValue(packageOption) ?? "").Trim();
        if (package.Length > 0)
            return true;

        stderr.WriteLine("maui-pulse requires --package <applicationId> (one app per session).");
        stderr.WriteLine("  maui-pulse attach --package com.test.androidapp --android --port 7878");
        stderr.WriteLine("  maui-pulse attach --package com.test.ios --ios --port 7878");
        return false;
    }

    static bool TryReadPlatform(
        System.CommandLine.ParseResult parse,
        Option<bool> androidOption,
        Option<bool> iosOption,
        TextWriter stderr,
        out string device)
    {
        var android = parse.GetValue(androidOption);
        var ios = parse.GetValue(iosOption);
        if (android == ios)
        {
            stderr.WriteLine("listen / attach require exactly one of --android or --ios.");
            device = "";
            return false;
        }

        device = android ? "android" : "ios";
        return true;
    }

    static string ParseFormat(string? value) =>
        string.Equals(value, "json", StringComparison.OrdinalIgnoreCase) ? "json" : "human";

    static bool IsInteractive(TextWriter stdout) =>
        ReferenceEquals(stdout, Console.Out) && !Console.IsInputRedirected && !Console.IsOutputRedirected;

    static bool IsHelp(string value) => value is "-h" or "--help" or "-?" or "help";

    static bool LooksLikeKnownCommand(string value) =>
        value is "listen" or "attach" or "pull" or "queues" or "sync" or "incident" or "version";

    static bool HasFlag(IReadOnlyList<string> args, string flag)
    {
        foreach (var arg in args)
        {
            if (string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase)
                || arg.StartsWith(flag + "=", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    static int FreePort()
    {
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
