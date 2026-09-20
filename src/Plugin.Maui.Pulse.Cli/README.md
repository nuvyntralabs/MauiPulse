# maui-pulse

A [dotnet tool](https://learn.microsoft.com/dotnet/core/tools/global-tools) that listens **only** to Nuvyntra `Plugin.Maui.*` session data.

```bash
dotnet tool install -g Plugin.Maui.Pulse.Cli --source https://api.nuget.org/v3/index.json
adb reverse tcp:7878 tcp:7878   # USB Android
maui-pulse attach --package com.test.androidapp --android --port 7878 --no-update-check
maui-pulse attach --package com.test.ios --ios --port 7878 --no-update-check
maui-pulse listen --package com.test.androidapp --android --stdin --once --format json
maui-pulse pull --package com.test.androidapp --from ./device-files --out ./pulled
maui-pulse queues --package com.test.androidapp --from ./pulled
maui-pulse sync --package com.test.androidapp --from ./pulled
maui-pulse incident --package com.test.androidapp --from ./pulled --out incident.zip
maui-pulse version
```

Every session command requires `--package`. `listen` / `attach` also require `--android` or `--ios`.

A real host adds `Plugin.Maui.Pulse` and `builder.UseMauiPulse()`, and still registers the plugins it already uses (`AddNetworkMonitor`, `UseMauiJobQueue`, `UseOfflineSync`, …). The CLI only listens.

Unknown sources (logcat, Firebase, Sentry, MAUI `Connectivity`) are dropped. A missing plugin skips that lane.

Integrate: [USER-GUIDE.md](../../USER-GUIDE.md). Architecture and commands: [README.md](../../README.md).  
Docs: https://nuvyntralabs.github.io/toolkits/maui-pulse/
