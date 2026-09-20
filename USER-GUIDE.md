# Integrate maui-pulse

Add Pulse to a .NET MAUI app, then watch it from the CLI. Architecture and protocol live in [README.md](README.md).

**Host:** `Plugin.Maui.Pulse` → `UseMauiPulse()`  
**CLI:** `Plugin.Maui.Pulse.Cli` → `maui-pulse`  
**Version:** 1.0.1 · .NET 10

These packages are [Niladri Prasad Padhy](https://github.com/NiladriPadhy) / Nuvyntra Labs work.

---

## 1. Add Pulse to the app

```bash
dotnet add package Plugin.Maui.Pulse
```

```csharp
using Plugin.Maui.Pulse;

builder.UseMauiApp<App>().UseMauiPulse();
```

That is the only Pulse line. Do not add `Plugin.Maui.Pulse.Cli` to the app. Do not POST from a page.

Release is **off** unless you turn it on. Defaults are enough for Debug.

```csharp
builder.UseMauiPulse(options =>
{
    options.Enabled = true;                          // optional; already true in Debug
    options.Package = "com.myapp.android";           // optional; default AppInfo.PackageName
    options.Endpoint = "http://192.168.1.10:7878/";  // optional; see step 4
});
```

If `maui-pulse` is not running, the app still works.

---

## 2. Register the plugins you already use

Pulse does not install NetworkMonitor, JobQueue, or any other plugin. Add **only** what the product needs. A missing plugin is skipped.

| If the app uses | Register it with |
| --- | --- |
| [Plugin.Maui.NetworkMonitor](https://www.nuget.org/packages/Plugin.Maui.NetworkMonitor) | `builder.Services.AddNetworkMonitor(...)` |
| [Plugin.Maui.NetworkDiagnostics](https://www.nuget.org/packages/Plugin.Maui.NetworkDiagnostics) | `builder.UseNetworkDiagnostics(...)` |
| [Plugin.Maui.JobQueue](https://www.nuget.org/packages/Plugin.Maui.JobQueue) | `builder.UseMauiJobQueue(...)` |
| [Plugin.Maui.RetryQueue](https://www.nuget.org/packages/Plugin.Maui.RetryQueue) | `builder.UseMauiRetryQueue(...)` |
| [Plugin.Maui.OfflineSync](https://www.nuget.org/packages/Plugin.Maui.OfflineSync) | `builder.UseOfflineSync(...)` |
| [Plugin.Maui.PermissionFlow](https://www.nuget.org/packages/Plugin.Maui.PermissionFlow) | `builder.UsePermissionFlow(...)` |
| [Plugin.Maui.AppHealth](https://www.nuget.org/packages/Plugin.Maui.AppHealth) | `builder.UseAppHealth(...)` |
| [Plugin.Maui.LeakAnalyser](https://www.nuget.org/packages/Plugin.Maui.LeakAnalyser) | `builder.UseLeakAnalyser(...)` (Debug) |
| [Plugin.Maui.Diagnostics](https://www.nuget.org/packages/Plugin.Maui.Diagnostics) | `builder.UseMauiDiagnostics(...)` |
| [Plugin.Maui.DeviceSession](https://www.nuget.org/packages/Plugin.Maui.DeviceSession) | `builder.UseDeviceSession(...)` |

```csharp
builder.Services.AddNetworkMonitor(options =>
{
    options.EnableHttpProbe = true;
    options.EnableCaptivePortalDetection = true;
});

builder
    .UseMauiApp<App>()
    .UseOfflineSync(options =>
    {
        options.RemoteBaseAddress = new Uri("https://api.example.com/sync/");
        options.AutoSync = true;
    })
    .UseMauiPulse();
```

The app keeps calling `StartWatching()`, `EnqueueAsync()`, `SyncAsync()`, and so on. Pulse forwards those events. The person using the app does not tap anything for Pulse.

---

## 3. Install the CLI

```bash
dotnet tool install -g Plugin.Maui.Pulse.Cli --source https://api.nuget.org/v3/index.json
maui-pulse version
```

Until nuget.org has the package, run it from this repo:

```bash
dotnet run --project Pulse/src/Plugin.Maui.Pulse.Cli -c Release -- --no-update-check attach --package com.myapp.android --android --port 7878
```

---

## 4. Let the phone reach the Mac

`UseMauiPulse()` POSTs to port **7878**. The device must be able to open that URL.

| Host | What you do |
| --- | --- |
| USB Android | `adb reverse tcp:7878 tcp:7878` (repeat after unplug) |
| Android emulator | nothing — Pulse uses `10.0.2.2` |
| iOS simulator | nothing — Pulse uses `127.0.0.1` |
| Physical iPhone | same Wi-Fi; set `options.Endpoint` to the Mac IP if needed |

---

## 5. Attach and use the app

Every command needs `--package` (the Android `applicationId` or iOS bundle id). Live commands also need `--android` or `--ios`.

```bash
adb reverse tcp:7878 tcp:7878
maui-pulse attach --package com.myapp.android --android --port 7878 --no-update-check
```

Launch the **Debug** host (`dotnet build -t:Run`). Use the app the way a tester would.

| You do this | Lane that should move |
| --- | --- |
| Toggle Wi-Fi / hotel portal | NETWORK |
| Run a “diagnose API” action | API |
| A queued upload fails | QUEUE |
| Two devices edit the same row | SYNC |
| Camera / location permission | PERMS |
| Battery watch fires | HEALTH |
| Leave a page that still holds bindings | LEAK |
| Main thread freeze | CRASH |
| Cold start | SESSION |

A quiet row (`—`) means Pulse is subscribed and waiting. That is not a failure.

Two apps need two Pulse windows (two `--package` values).

---

## 6. Optional: inspect files

Live events are not required for queues, sync, or crash files. Copy these off a debug device, then:

```bash
maui-pulse pull --package com.myapp.android --from ./device-files --out ./pulled --no-update-check
maui-pulse queues --package com.myapp.android --from ./pulled --no-update-check
maui-pulse sync --package com.myapp.android --from ./pulled --no-update-check
maui-pulse incident --package com.myapp.android --from ./pulled --out incident.zip --no-update-check
```

Known names: `plugin.maui.jobqueue.db3`, `plugin.maui.retryqueue.db3`, `offlinesync.db3`, `maui-diagnostics/`.

---

## If something is wrong

| What you see | What to do |
| --- | --- |
| Every lane is `— not installed` | Add `UseMauiPulse()` and register at least one plugin from step 2 |
| Lane is `— not registered` | The package is referenced; the `UseX()` / `AddX()` call is missing |
| Lane stays `—` | Use that feature in the app (`SyncAsync()`, `StartWatching()`, …) |
| Attach never changes | Run Debug; on USB run `adb reverse` again |
| `requires --package` | Pass the same id as the app (`com.myapp.android`) |
| Port already in use | `--port 7879` and matching `options.Endpoint` |

More commands, JSON, and how the sink works: [README.md](README.md).

---

## Sample

[`samples/Pulse.Sample`](samples/Pulse.Sample) is a demo host (`com.test.androidapp` / `com.test.ios`). A real app PackageReferences the real plugins — do not copy the sample’s demo types.
