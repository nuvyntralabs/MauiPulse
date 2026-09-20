# Changelog

## 1.0.0

- Docs (submodule + hub): [README.md](README.md) is architecture and protocol; [USER-GUIDE.md](USER-GUIDE.md) is host + CLI integration only
- `UseMauiPulse()` host package (`Plugin.Maui.Pulse`) auto-forwards allow-listed plugin events — no per-event POST
- Sample MAUI host (`samples/Pulse.Sample`) calls `UseMauiPulse()` and raises demo plugin events; seeds plugin files for CLI tests
- `--package` is required on every session command (one applicationId / bundle id)
- `listen` / `attach` require exactly one of `--android` or `--ios`
- JSON for another app `package` is dropped
- `listen` / `attach` — HTTP or `--stdin` JSON sink; allow-listed `Plugin.Maui.*` sources only
- `pull` — copy known plugin files from `--from`
- `queues` / `sync` — read-only inspectors for JobQueue, RetryQueue, OfflineSync db3 files
- `incident` — zip allow-listed files plus `manifest.json`
- Missing plugins skip that lane. Unknown sources are dropped.
- Interactive nuget.org self-update check every 4 hours (`--no-update-check` to skip)
