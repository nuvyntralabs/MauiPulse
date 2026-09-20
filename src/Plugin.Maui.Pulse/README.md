# Plugin.Maui.Pulse

Debug sink for [maui-pulse](https://www.nuget.org/packages/Plugin.Maui.Pulse.Cli). One line. The host does **not** write `HttpClient.PostAsync`.

```csharp
builder.UseMauiApp<App>().UseMauiPulse();
```

You still register the plugins you already use. On start, Pulse scans DI and loaded assemblies for those allow-listed types and forwards their events to `maui-pulse attach` (`http://127.0.0.1:7878/` on a device after `adb reverse`, `http://10.0.2.2:7878/` on an emulator). Missing plugins stay `not_installed`. Off in Release unless you set `Enabled = true`.

| Plugin | Host register | Events Pulse binds |
| --- | --- | --- |
| Plugin.Maui.NetworkMonitor | `AddNetworkMonitor` | `StatusChanged` |
| Plugin.Maui.NetworkDiagnostics | `UseNetworkDiagnostics` | `Completed` |
| Plugin.Maui.JobQueue | `UseMauiJobQueue` | `JobFailed` |
| Plugin.Maui.RetryQueue | `UseMauiRetryQueue` | `OperationFailed` |
| Plugin.Maui.OfflineSync | `UseOfflineSync` | `ConflictDetected`, `StatusChanged`, `SyncCompleted` |
| Plugin.Maui.PermissionFlow | `UsePermissionFlow` | `FlowCompleted`, `PermissionChanged` |
| Plugin.Maui.AppHealth | `UseAppHealth` | `HealthChanged` |
| Plugin.Maui.LeakAnalyser | `UseLeakAnalyser` | `OnLeaked` |
| Plugin.Maui.Diagnostics | `UseMauiDiagnostics` | `AnrDetected` |
| Plugin.Maui.DeviceSession | `UseDeviceSession` | `SessionStarted` |

Integrate: [USER-GUIDE.md](../../USER-GUIDE.md). Architecture: [README.md](../../README.md).

```csharp
builder.UseMauiPulse(options =>
{
    options.Endpoint = "http://192.168.1.10:7878/";
    options.Package = "com.myapp.android"; // optional; default AppInfo.PackageName
});
```

Then on the Mac:

```bash
adb reverse tcp:7878 tcp:7878   # USB Android
maui-pulse attach --package <applicationId> --android --port 7878
```

This is Niladri Padhy / Nuvyntra Labs work. The CLI package is `Plugin.Maui.Pulse.Cli`.
