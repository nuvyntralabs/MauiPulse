namespace Pulse.Sample;

public static class FixtureSeeder
{
    static readonly string[] Files =
    [
        "plugin.maui.jobqueue.db3",
        "plugin.maui.retryqueue.db3",
        "offlinesync.db3",
        "maui-diagnostics/anr.txt",
        "maui-diagnostics/timeline.json"
    ];

    public static string Root => FileSystem.AppDataDirectory;

    public static async Task<string> SeedAsync()
    {
        foreach (var relative in Files)
        {
            var dest = Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            await using var source = await FileSystem.OpenAppPackageFileAsync(relative).ConfigureAwait(false);
            await using var target = File.Create(dest);
            await source.CopyToAsync(target).ConfigureAwait(false);
        }

        return Root;
    }
}
