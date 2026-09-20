using System.IO.Compression;
using System.Text.Json;

namespace Plugin.Maui.Pulse;

public static class IncidentPacker
{
    public static string Pack(string source, string? zipPath = null)
    {
        var hits = ArtifactLocator.Find(source);
        var dest = string.IsNullOrWhiteSpace(zipPath)
            ? Path.Combine(Path.GetTempPath(), "maui-pulse", $"incident-{DateTimeOffset.UtcNow:yyyyMMddTHHmmss}.zip")
            : zipPath;
        var folder = Path.GetDirectoryName(dest);
        if (!string.IsNullOrEmpty(folder))
            Directory.CreateDirectory(folder);

        if (File.Exists(dest))
            File.Delete(dest);

        using var zip = ZipFile.Open(dest, ZipArchiveMode.Create);
        var copied = new List<string>();
        var skipped = new List<string>();

        foreach (var sourcePlugin in PluginCatalog.Sources.Where(item => !string.IsNullOrEmpty(item.FileHint)))
        {
            var match = hits.FirstOrDefault(hit => hit.PackageId == sourcePlugin.PackageId);
            if (match is null)
            {
                skipped.Add(sourcePlugin.PackageId);
                continue;
            }

            if (match.IsDirectory)
            {
                foreach (var file in Directory.EnumerateFiles(match.FullPath, "*", SearchOption.AllDirectories))
                {
                    var relative = Path.Combine(match.RelativeName, Path.GetRelativePath(match.FullPath, file));
                    zip.CreateEntryFromFile(file, relative.Replace('\\', '/'));
                }
            }
            else
            {
                zip.CreateEntryFromFile(match.FullPath, match.RelativeName);
            }

            copied.Add(sourcePlugin.PackageId);
        }

        var manifest = JsonSerializer.Serialize(new
        {
            createdAt = DateTimeOffset.UtcNow,
            sources = copied,
            skipped,
            rule = "allow-listed Plugin.Maui.* files only"
        }, new JsonSerializerOptions { WriteIndented = true });
        var entry = zip.CreateEntry("manifest.json");
        using (var stream = entry.Open())
        using (var writer = new StreamWriter(stream))
            writer.Write(manifest);

        return dest;
    }
}
