# maui-pulse

[![NuGet](https://img.shields.io/nuget/v/Plugin.Maui.Pulse.Cli.svg?label=CLI)](https://www.nuget.org/packages/Plugin.Maui.Pulse.Cli)
[![NuGet](https://img.shields.io/nuget/v/Plugin.Maui.Pulse.svg?label=Host)](https://www.nuget.org/packages/Plugin.Maui.Pulse)

Live session viewer for **Nuvyntra `Plugin.Maui.*` data only**. Pulse does not scrape logcat, Charles, Firebase, Sentry, or MAUI framework APIs.

| | |
| --- | --- |
| **Integrate the app and CLI** | [USER-GUIDE.md](USER-GUIDE.md) |
| **Architecture and protocol** | this file |
| **GitHub** | https://github.com/nuvyntralabs/MauiPulse |
| **CLI NuGet** | https://www.nuget.org/packages/Plugin.Maui.Pulse.Cli |
| **Host NuGet** | https://www.nuget.org/packages/Plugin.Maui.Pulse |
| **Docs** | https://nuvyntralabs.github.io/toolkits/maui-pulse/ |
| **Author** | [Niladri Prasad Padhy](https://github.com/NiladriPadhy) / Nuvyntra Labs |
| **License / version** | MIT · 1.0.0 · `net10.0` |

Pulse is the **during-the-session** viewer. It does not replace [MauiDev](https://github.com/nuvyntralabs/MauiDev) (`maui-dev doctor`) or [maui-perf](https://www.nuget.org/packages/Plugin.Maui.Performance.Cli). Publishing is pipeline-only — never `dotnet nuget push` from a local clone.

---

## What Pulse is

Two packages, one job: show what allow-listed plugins are doing **right now**.

| Package | Where it runs | Role |
| --- | --- | --- |
| `Plugin.Maui.Pulse` | The MAUI host | `UseMauiPulse()` finds plugins already in DI, stays subscribed to their events, POSTs JSON to the CLI |
| `Plugin.Maui.Pulse.Cli` | The development machine | `maui-pulse attach` listens on port 7878 and draws a nine-lane table |

The person using the MAUI app does not tap anything for Pulse. The app already calls `EnqueueAsync()`, `SyncAsync()`, `StartWatching()`, and so on. Those plugins raise events. Pulse forwards them.

Usual alternatives (`adb logcat`, Charles / Proxyman, Firebase Performance, Sentry) are raw logs, HTTP only, or cloud-after-the-fact. They are not this live plugin-lane view.

---

## Architecture

```text
MAUI host                                      Development machine
─────────                                      ───────────────────
INetworkMonitor.StatusChanged  ─┐
IJobQueue.JobFailed            ─┤  events already on the plugin
IOfflineSyncEngine.SyncCompleted┤
…other allow-listed events     ─┘
        │
        ▼  reflection subscribe (once, stays for app life)
UseMauiPulse()
  PulseHostInitializer  →  PulseHostRuntime.Start
  PluginHookBinder.Bind + Subscribe
  PulseSink.PostAsync
        │
        │  HTTP POST  http://127.0.0.1:7878/  (or 10.0.2.2 on emulator)
        ▼
maui-pulse attach / listen
  ListenServer  →  SignalParser  →  SessionStore
        │
        ▼
Nine-lane table: NETWORK  API  QUEUE  SYNC  PERMS  HEALTH  LEAK  CRASH  SESSION

Optional second path (no live events):
  app data files  →  copy off device  →  pull / queues / sync / incident
```

Pulse never starts a plugin, never polls, and never adds the catalog for you. It only **listens** to instances the host already registered. A missing plugin skips that lane (`not_installed`). Pulse does not invent a substitute from the OS.

Supported plugins were **not** changed for Pulse. There is no `ProjectReference` to JobQueue, NetworkMonitor, or any sibling repo. The host package reflects types by name so this tree stays buildable after it is extracted to `nuvyntralabs/MauiPulse`.

---

## How `UseMauiPulse()` starts monitoring

`UseMauiPulse()` does **not** subscribe immediately. It only registers services. MAUI calls the initializer after `Build()`.

```text
MauiProgram
  builder.UseMauiApp<App>().UseMauiPulse()
        │
        │  AddSingleton PulseHostOptions
        │  AddSingleton PulseHostRuntime
        │  AddTransient IMauiInitializeService → PulseHostInitializer
        ▼
MauiApp.Build()
        │
        ▼
PulseHostInitializer.Initialize
        │
        ▼
PulseHostRuntime.Start   (once; no-op if Enabled is false)
   1. Resolve package   (options.Package or AppInfo.PackageName)
   2. Open PulseSink    (emulator 10.0.2.2:7878 / device 127.0.0.1:7878)
   3. Bind              walk PluginHookCatalog, find interface, GetService
   4. POST presence     which plugins are quiet / not_registered / not_installed
   5. Subscribe         AddEventHandler on each found instance
        │
        │  handlers stay attached until PulseHostRuntime.Dispose
        ▼
Later: JobFailed / StatusChanged / …  →  OnPluginEvent  →  POST signal
```

### Bind (find the plugin)

`PluginHookBinder.Bind` scans **loaded assemblies** for each catalog row:

1. Assembly hint present? (`Plugin.Maui.JobQueue`, …)
2. Interface type by full name? (`Plugin.Maui.JobQueue.IJobQueue`, …)
3. `services.GetService(type)` — the same singleton `UseMauiJobQueue()` already registered

| Outcome | State | CLI row |
| --- | --- | --- |
| Assembly not loaded | `not_installed` | `— not installed` |
| Type or assembly present, not in DI | `not_registered` | `— not registered` |
| Instance found, no event yet | `quiet` | `—` (still listening) |
| At least one allow-listed event arrived | `running` | headline from the event |
| Seeded from a copied file | `from_file` | file inspector text |

`Start` does not re-scan DI later. A plugin registered after initialize is missed.

### Subscribe (keep listening)

`PluginHookBinder.Subscribe` calls `AddEventHandler` for every catalog event on that instance. The handler stays for the app lifetime. It is **not** one-shot and it does not poll.

`OnPluginEvent` turns args into a short summary (`captive portal`, `visits#184`, first 8 of a session id) and POSTs JSON. `PulseSink` uses a 3-second HTTP timeout and swallows errors. A closed CLI must not crash the host.

Handlers come off only in `PulseHostRuntime.Dispose` (`RemoveEventHandler`).

Release builds set `Enabled = false` before your configure callback. Set `options.Enabled = true` if you need the sink in Release.

---

## Projects

| Project | Pack | Role |
| --- | --- | --- |
| `src/Plugin.Maui.Pulse` | yes | Host Debug sink (`UseMauiPulse`) |
| `src/Plugin.Maui.Pulse.Cli` | yes (`PackAsTool`) | `maui-pulse` command |
| `src/Plugin.Maui.Pulse.Core` | no (`IsPackable=false`) | Shared catalog, parser, session, file inspectors |
| `samples/Pulse.Sample` | no | Demo host. Not in `Pulse.sln`. No sibling `PackageReference` |
| `tests/Plugin.Maui.Pulse.Tests` | — | Host binder / JSON |
| `tests/Plugin.Maui.Pulse.Cli.Tests` | — | CLI invoke + sample fixtures |
| `tests/Plugin.Maui.Pulse.Core.Tests` | — | Parser / inspectors |

Pack the host and CLI with the **same** `Version`.

### Host source map

| Type | File | Job |
| --- | --- | --- |
| `UseMauiPulse` | `MauiAppBuilderExtensions.cs` | Register options, runtime, initializer |
| `PulseHostOptions` | `PulseHostOptions.cs` | `Enabled`, `Endpoint`, `Package` |
| `PulseHostInitializer` | `PulseHostInitializer.cs` | `IMauiInitializeService` → `Start` |
| `PulseHostRuntime` | `PulseHostRuntime.cs` | Bind, presence, subscribe, POST |
| `PluginHookCatalog` | `PluginHookCatalog.cs` | Closed list: package, lane, interface, events |
| `PluginHookBinder` | `PluginHookBinder.cs` | Reflection `GetService` + `AddEventHandler` |
| `PulseSink` | `PulseSink.cs` | `POST` JSON; swallow a closed CLI |
| `PulseJson` | `PulseJson.cs` | Presence and signal payloads |

### CLI / Core source map

| Type | File | Job |
| --- | --- | --- |
| `PluginCatalog` | `Plugin.Maui.Pulse.Core/PluginCatalog.cs` | Same allow-list + file names + Observability domain map |
| `SignalParser` | `SignalParser.cs` | Accept / drop JSON; filter `--package` |
| `SessionStore` | `SessionStore.cs` | Lane state for the attach table |
| `ListenServer` | `Plugin.Maui.Pulse.Cli/ListenServer.cs` | `GET /` health, `POST /` ingest |
| `CliHost` | `CliHost.cs` | `listen` `attach` `pull` `queues` `sync` `incident` `version` |

---

## Allow-list

Closed list. GeoLocator, PushRouter, SmartUpload, and every other plugin are never a Pulse lane.

| Lane | Package | Host register | DI type Pulse looks for | Events | Files |
| --- | --- | --- | --- | --- | --- |
| NETWORK | [Plugin.Maui.NetworkMonitor](https://www.nuget.org/packages/Plugin.Maui.NetworkMonitor) | `AddNetworkMonitor` | `Maui.NetworkMonitor.INetworkMonitor` | `StatusChanged` | — |
| API | [Plugin.Maui.NetworkDiagnostics](https://www.nuget.org/packages/Plugin.Maui.NetworkDiagnostics) | `UseNetworkDiagnostics` | `INetworkDiagnostics` | `Completed` | — |
| QUEUE | [Plugin.Maui.JobQueue](https://www.nuget.org/packages/Plugin.Maui.JobQueue) | `UseMauiJobQueue` | `IJobQueue` | `JobFailed` | `plugin.maui.jobqueue.db3` |
| QUEUE | [Plugin.Maui.RetryQueue](https://www.nuget.org/packages/Plugin.Maui.RetryQueue) | `UseMauiRetryQueue` | `IRetryQueue` | `OperationFailed` | `plugin.maui.retryqueue.db3` |
| SYNC | [Plugin.Maui.OfflineSync](https://www.nuget.org/packages/Plugin.Maui.OfflineSync) | `UseOfflineSync` | `IOfflineSyncEngine` (or `Abstractions.IOfflineSyncEngine`) | `ConflictDetected`, `StatusChanged`, `SyncCompleted` | `offlinesync.db3` |
| PERMS | [Plugin.Maui.PermissionFlow](https://www.nuget.org/packages/Plugin.Maui.PermissionFlow) | `UsePermissionFlow` | `IPermissionFlow` | `FlowCompleted`, `PermissionChanged` | — |
| HEALTH | [Plugin.Maui.AppHealth](https://www.nuget.org/packages/Plugin.Maui.AppHealth) | `UseAppHealth` | `IAppHealth` | `HealthChanged` | — |
| LEAK | [Plugin.Maui.LeakAnalyser](https://www.nuget.org/packages/Plugin.Maui.LeakAnalyser) | `UseLeakAnalyser` | `ILeakAnalyser` | `OnLeaked` | — |
| CRASH | [Plugin.Maui.Diagnostics](https://www.nuget.org/packages/Plugin.Maui.Diagnostics) | `UseMauiDiagnostics` | `IDiagnostics` | `AnrDetected` | `maui-diagnostics/` |
| SESSION | [Plugin.Maui.DeviceSession](https://www.nuget.org/packages/Plugin.Maui.DeviceSession) | `UseDeviceSession` | `IDeviceSession` | `SessionStarted` | — |

QUEUE is two plugins. They degrade independently (JobQueue only is valid).

How to register those plugins and attach the CLI: [USER-GUIDE.md](USER-GUIDE.md).

---

## CLI design

Every session command requires `--package` (one Android `applicationId` or iOS bundle id). `listen` and `attach` also require exactly one of `--android` or `--ios`. JSON whose `package` does not match is dropped. One Pulse process = one app.

| Command | Job |
| --- | --- |
| `listen` | HTTP sink on `--port` (default 7878) or `--stdin` JSON. No table. Agents / tests / Observability |
| `attach` | Same sink plus the nine-lane table. This is the live command |
| `pull` | Copy known plugin files from `--from`. `--adb` only prints how to copy |
| `queues` | Read-only JobQueue / RetryQueue `*.db3` inspector. Does not drain |
| `sync` | Read-only OfflineSync `offlinesync.db3` inspector |
| `incident` | Zip allow-listed files + `manifest.json` |
| `version` | Print `1.0.0` (no `--package`) |

```bash
maui-pulse attach --package com.test.androidapp --android --port 7878 --no-update-check
maui-pulse attach --package com.test.ios --ios --port 7878 --no-update-check
maui-pulse listen --package com.test.androidapp --android --stdin --once --format json
maui-pulse pull --package com.test.androidapp --from ./device-files --out ./pulled
maui-pulse queues --package com.test.androidapp --from ./pulled
maui-pulse sync --package com.test.androidapp --from ./pulled
maui-pulse incident --package com.test.androidapp --from ./pulled --out incident.zip
```

`--format human|json`. `--lanes network,sync,queue` hides rows; it does not widen the allow-list.

| Exit | Meaning |
| --- | --- |
| `0` | Success, including skipped lanes / missing queue files |
| `1` | No allow-listed evidence, nothing copied, or bind failed |
| `2` | Usage (missing `--package`, missing platform, unknown command) |

On an interactive terminal the CLI asks every 4 hours whether to update from nuget.org (`[y/N]`, default no). Skip with `--no-update-check` or `NUVYNTRA_NO_UPDATE_CHECK=1`. Cache: `~/.nuvyntra/cli-updates.json`. CI, `--format json`, and piped output skip the prompt. The CLI does not phone home.

Do not `dotnet add package Plugin.Maui.Pulse.Cli` into an app.

---

## Wire protocol

`UseMauiPulse()` writes this. Agents and tests may `POST http://127.0.0.1:7878/` or pipe `--stdin`. `GET /` returns `{ "ok": true, "tool": "maui-pulse" }`.

Unknown `source` / `id` values are dropped. If `lane` is present and does not match that package, the payload is dropped.

### Presence (host start)

```json
{
  "package": "com.test.androidapp",
  "plugins": [
    { "id": "Plugin.Maui.NetworkMonitor", "lane": "network", "state": "quiet" },
    { "id": "Plugin.Maui.OfflineSync", "lane": "sync", "state": "not_installed" }
  ]
}
```

### Signal (each plugin event)

```json
{
  "package": "com.test.androidapp",
  "source": "Plugin.Maui.NetworkMonitor",
  "lane": "network",
  "signal": "StatusChanged",
  "summary": "captive portal",
  "state": "running",
  "at": "2026-09-19T16:00:00Z"
}
```

DeviceSession may add `data.sessionId` so the attach header can show `session 8f2a`.

### Batch

```json
{
  "signals": [
    { "source": "Plugin.Maui.AppHealth", "signal": "HealthChanged", "summary": "battery 18%" },
    { "source": "Sentry", "summary": "ignored" }
  ]
}
```

Sentry is dropped. AppHealth is accepted.

### Observability pipe

[Plugin.Maui.Observability](https://www.nuget.org/packages/Plugin.Maui.Observability) is a **pipe**, not a tenth lane. Point `HttpEndpoint` at `http://127.0.0.1:7878/` if you already use it. Pulse still only **renders** the rows above.

| `domain` | Becomes |
| --- | --- |
| `Network` | Plugin.Maui.NetworkMonitor |
| `Health` | Plugin.Maui.AppHealth |
| `Sync` / `OfflineSync` | Plugin.Maui.OfflineSync |
| `Session` / `Identity` / `DeviceSession` | Plugin.Maui.DeviceSession |

`SmartUpload`, `ApiResilience`, `BackgroundTasks`, and every other domain are dropped.

---

## Files the CLI may read

Live events are optional for QUEUE, SYNC, and CRASH. Copy these off a debug device, then `--from`:

- `plugin.maui.jobqueue.db3`
- `plugin.maui.retryqueue.db3`
- `offlinesync.db3`
- `maui-diagnostics/`

`pull --adb` only prints how to copy. It does not scrape logcat. `logcat.txt` in the same folder is ignored.

---

## Sample host

[`samples/Pulse.Sample`](samples/Pulse.Sample) (`com.test.androidapp` / `com.test.ios`) calls `UseMauiPulse()` the same way a real host should. It cannot `ProjectReference` sibling plugin repos, so `DemoContracts.cs` / `DemoPlugins.cs` stand in for the real interfaces.

A real MAUI app does **not** copy those demo types. It PackageReferences the real plugins and calls `UseMauiJobQueue` / `UseOfflineSync` / … as in the [user guide](USER-GUIDE.md).

Do not add the sample to `Pulse.sln`.

---

## Tests and pack

```bash
dotnet test Pulse/Pulse.sln -c Release
dotnet pack Pulse/src/Plugin.Maui.Pulse/Plugin.Maui.Pulse.csproj -c Release
dotnet pack Pulse/src/Plugin.Maui.Pulse.Cli/Plugin.Maui.Pulse.Cli.csproj -c Release
```

CI (`Pulse/.github/workflows/ci.yml`) is the same fail-fast order as MauiDev / Nuvyn: version alignment → NuGet key / unpublished version → tests → pack both nupkgs → nuget.org and GitHub Packages. Never `dotnet nuget push` from a local clone.

---

## What it is not

| Tool | Job |
| --- | --- |
| [Nuvyn](https://www.nuget.org/packages/NuvyntraLabs.Nuvyn.Cli) | New-app scaffold |
| [maui-dev](https://www.nuget.org/packages/Plugin.Maui.MauiDev.Cli) | SDK / project doctor |
| [maui-perf](https://www.nuget.org/packages/Plugin.Maui.Performance.Cli) | EventPipe profile take |
| [Observability](https://www.nuget.org/packages/Plugin.Maui.Observability) | In-app exporter. It can push; Pulse listens |

Tizen is not a target. Pulse never installs workloads and never phones home.
