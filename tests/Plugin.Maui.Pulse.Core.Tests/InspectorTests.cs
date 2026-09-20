using Microsoft.Data.Sqlite;
using Plugin.Maui.Pulse;

namespace Plugin.Maui.Pulse.Core.Tests;

public sealed class InspectorTests
{
    [Fact]
    public void MissingQueueFileIsNotInstalled()
    {
        Assert.False(QueueInspector.TryInspectJobQueue(Path.Combine(Path.GetTempPath(), "no-such-jobqueue.db3"), out _, out var error));
        Assert.Equal("not_installed", error);
    }

    [Fact]
    public void JobQueueCountsPendingAndDeadLetter()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pulse-jobs-{Guid.NewGuid():N}.db3");
        try
        {
            using (var db = new SqliteConnection($"Data Source={path}"))
            {
                db.Open();
                Exec(db, """
                    CREATE TABLE Jobs (
                      Id TEXT PRIMARY KEY,
                      JobType TEXT,
                      Status INTEGER,
                      NextAttemptAtUtc TEXT,
                      LastError TEXT
                    );
                    """);
                Exec(db, "INSERT INTO Jobs VALUES ('1','payment.retry',0,'2020-01-01T00:00:00.0000000+00:00',null);");
                Exec(db, "INSERT INTO Jobs VALUES ('2','upload',4,'2020-01-01T00:00:00.0000000+00:00','boom');");
            }

            Assert.True(QueueInspector.TryInspectJobQueue(path, out var counts, out var error));
            Assert.Null(error);
            Assert.Equal(1, counts.Pending);
            Assert.Equal(1, counts.DeadLetter);
            Assert.Equal("upload", counts.DeadLetterName);
            Assert.Contains("1 pending", QueueInspector.Summarize("JobQueue", counts), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SyncInspectorReadsConflicts()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pulse-sync-{Guid.NewGuid():N}.db3");
        try
        {
            using (var db = new SqliteConnection($"Data Source={path}"))
            {
                db.Open();
                Exec(db, """
                    CREATE TABLE SyncDocumentRecord (
                      Key TEXT PRIMARY KEY,
                      Collection TEXT,
                      EntityId TEXT,
                      SyncStateValue INTEGER
                    );
                    """);
                Exec(db, """
                    CREATE TABLE SyncChangeRecord (
                      Id INTEGER PRIMARY KEY,
                      Collection TEXT,
                      EntityId TEXT
                    );
                    """);
                Exec(db, "INSERT INTO SyncDocumentRecord VALUES ('visits:184','visits','184',4);");
                Exec(db, "INSERT INTO SyncChangeRecord VALUES (1,'visits','184');");
            }

            Assert.True(SyncInspector.TryInspect(path, out var snapshot, out var error));
            Assert.Null(error);
            Assert.Equal(1, snapshot.Conflicts);
            Assert.Equal(1, snapshot.PendingChanges);
            Assert.Equal("visits#184", snapshot.SampleConflict);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void IncidentPackIncludesOnlyAllowListedFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pulse-inc-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, PluginCatalog.OfflineSyncFile), "not-a-real-db");
        File.WriteAllText(Path.Combine(root, "logcat.txt"), "should not be packed");
        var zip = Path.Combine(root, "incident.zip");
        try
        {
            // Packer copies files even if they are not valid sqlite.
            var created = IncidentPacker.Pack(root, zip);
            Assert.True(File.Exists(created));
            using var archive = System.IO.Compression.ZipFile.OpenRead(created);
            Assert.Contains(archive.Entries, entry => entry.Name == PluginCatalog.OfflineSyncFile);
            Assert.Contains(archive.Entries, entry => entry.Name == "manifest.json");
            Assert.DoesNotContain(archive.Entries, entry => entry.Name == "logcat.txt");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    static void Exec(SqliteConnection db, string sql)
    {
        using var command = db.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
