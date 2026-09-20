namespace Plugin.Maui.Pulse;

public sealed record ArtifactHit(string PackageId, string RelativeName, string FullPath, bool IsDirectory);

public static class ArtifactLocator
{
    public static IReadOnlyList<ArtifactHit> Find(string root)
    {
        if (!Directory.Exists(root))
            return [];

        var hits = new List<ArtifactHit>();
        TryFile(root, PluginCatalog.JobQueue, PluginCatalog.JobQueueFile, hits);
        TryFile(root, PluginCatalog.RetryQueue, PluginCatalog.RetryQueueFile, hits);
        TryFile(root, PluginCatalog.OfflineSync, PluginCatalog.OfflineSyncFile, hits);
        TryDirectory(root, PluginCatalog.Diagnostics, PluginCatalog.DiagnosticsFolder, hits);
        return hits;
    }

    static void TryFile(string root, string packageId, string name, List<ArtifactHit> hits)
    {
        var matches = Directory.EnumerateFiles(root, name, SearchOption.AllDirectories).Take(4).ToArray();
        foreach (var path in matches)
            hits.Add(new ArtifactHit(packageId, name, path, false));
    }

    static void TryDirectory(string root, string packageId, string name, List<ArtifactHit> hits)
    {
        var matches = Directory.EnumerateDirectories(root, name, SearchOption.AllDirectories).Take(4).ToArray();
        foreach (var path in matches)
            hits.Add(new ArtifactHit(packageId, name, path, true));
    }
}
