# Pulse.Sample

MAUI host that uses **`UseMauiPulse()`** the same way a real app should. Taps raise demo plugin events. This page does not POST JSON.

A real MAUI app does **not** copy `DemoContracts.cs` / `DemoPlugins.cs`. It PackageReferences the real plugins and calls `AddNetworkMonitor` / `UseMauiJobQueue` / `UseOfflineSync` / … then `UseMauiPulse()`. Integrate: [USER-GUIDE.md](../../USER-GUIDE.md). Architecture: [README.md](../../README.md).

| Platform | `--package` |
| --- | --- |
| Android | `com.test.androidapp` |
| iOS | `com.test.ios` |

Do not add this project to `Pulse.sln`. The demo types stand in for NetworkMonitor / JobQueue / … so Pulse can extract without those repos.

## Run the app

```bash
adb reverse tcp:7878 tcp:7878
ANDROID_SERIAL=<serial> dotnet build Pulse/samples/Pulse.Sample/Pulse.Sample.csproj -t:Run -f net10.0-android
```

Do not `adb install` the Debug APK (Fast Deploy needs `-t:Run`).

## attach

```bash
dotnet run --project Pulse/src/Plugin.Maui.Pulse.Cli -c Release -- --no-update-check \
  attach --package com.test.androidapp --android --port 7878
```

Open Pulse Sample and tap **Network** or **All lanes**. `UseMauiPulse()` forwards the event.

| Sample button | Demo raise | Lane |
| --- | --- | --- |
| Network | `RaiseNetwork()` → `StatusChanged` | NETWORK |
| API | `RaiseApi()` → `Completed` | API |
| Queue | `RaiseQueue()` → `JobFailed` | QUEUE |
| Sync | `RaiseSync()` → `ConflictDetected` | SYNC |
| Perms | `RaisePerms()` → `FlowCompleted` | PERMS |
| Health | `RaiseHealth()` → `HealthChanged` | HEALTH |
| Leak | `RaiseLeak()` → `OnLeaked` | LEAK |
| Crash | `RaiseCrash()` → `AnrDetected` | CRASH |
| Session | `RaiseSession()` → `SessionStarted` | SESSION |

## File commands

```bash
dotnet run --project Pulse/src/Plugin.Maui.Pulse.Cli -c Release -- --no-update-check \
  pull --package com.test.androidapp --from Pulse/samples/Pulse.Sample/fixtures --out ./pulled
```

Or tap **Seed plugin files** on the device, then `--from` a copied folder.

All other invokes (`listen`, `queues`, `sync`, `incident`, `version`): [README.md](../../README.md#cli-design).
