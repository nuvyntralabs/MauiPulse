namespace Plugin.Maui.Pulse;

public static class FileSessionLoader
{
    public static SessionStore Load(string root, string? app = null, string? device = null)
    {
        var store = new SessionStore(app, device);
        Apply(store, root);
        return store;
    }

    public static void Apply(SessionStore store, string root)
    {
        foreach (var hit in ArtifactLocator.Find(root))
        {
            if (hit.PackageId == PluginCatalog.JobQueue
                && QueueInspector.TryInspectJobQueue(hit.FullPath, out var jobs, out _))
            {
                store.MarkFromFile(hit.PackageId, QueueInspector.Summarize("JobQueue", jobs));
                continue;
            }

            if (hit.PackageId == PluginCatalog.RetryQueue
                && QueueInspector.TryInspectRetryQueue(hit.FullPath, out var retries, out _))
            {
                store.MarkFromFile(hit.PackageId, QueueInspector.Summarize("RetryQueue", retries));
                continue;
            }

            if (hit.PackageId == PluginCatalog.OfflineSync
                && SyncInspector.TryInspect(hit.FullPath, out var sync, out _))
            {
                store.MarkFromFile(hit.PackageId, SyncInspector.Summarize(sync));
                continue;
            }

            if (hit.PackageId == PluginCatalog.Diagnostics)
            {
                var files = Directory.Exists(hit.FullPath)
                    ? Directory.EnumerateFiles(hit.FullPath, "*", SearchOption.AllDirectories).Count()
                    : 0;
                store.MarkFromFile(hit.PackageId, files == 0 ? "empty" : $"{files} file{(files == 1 ? "" : "s")}");
            }
        }
    }
}
