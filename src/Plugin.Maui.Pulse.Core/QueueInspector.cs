using Microsoft.Data.Sqlite;

namespace Plugin.Maui.Pulse;

public static class QueueInspector
{
    static readonly string[] JobStatusNames =
        ["Pending", "Running", "Succeeded", "Failed", "DeadLetter", "Cancelled"];

    public static bool TryInspectJobQueue(string path, out QueueCounts counts, out string? error)
    {
        counts = new QueueCounts();
        error = null;
        if (!File.Exists(path))
        {
            error = "not_installed";
            return false;
        }

        try
        {
            using var db = Open(path);
            if (!TableExists(db, "Jobs"))
            {
                error = "not a Plugin.Maui.JobQueue database";
                return false;
            }

            counts = CountRows(db, "Jobs", "JobType");
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool TryInspectRetryQueue(string path, out QueueCounts counts, out string? error)
    {
        counts = new QueueCounts();
        error = null;
        if (!File.Exists(path))
        {
            error = "not_installed";
            return false;
        }

        try
        {
            using var db = Open(path);
            if (!TableExists(db, "Operations"))
            {
                error = "not a Plugin.Maui.RetryQueue database";
                return false;
            }

            counts = CountRows(db, "Operations", "OperationName");
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    static QueueCounts CountRows(SqliteConnection db, string table, string nameColumn)
    {
        var pending = 0;
        var scheduled = 0;
        var running = 0;
        var failed = 0;
        var dead = 0;
        var succeeded = 0;
        var cancelled = 0;
        string? nextName = null;
        string? nextAt = null;
        string? deadName = null;
        var now = DateTimeOffset.UtcNow.ToString("O");

        using var command = db.CreateCommand();
        command.CommandText = $"SELECT {nameColumn}, Status, NextAttemptAtUtc, LastError FROM {table}";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var name = reader.IsDBNull(0) ? "" : reader.GetString(0);
            var status = reader.GetInt32(1);
            var next = reader.IsDBNull(2) ? "" : reader.GetString(2);

            switch (status)
            {
                case 0:
                    if (string.CompareOrdinal(next, now) > 0)
                    {
                        scheduled++;
                        if (nextName is null || string.CompareOrdinal(next, nextAt ?? next) < 0)
                        {
                            nextName = name;
                            nextAt = next;
                        }
                    }
                    else
                    {
                        pending++;
                    }

                    break;
                case 1:
                    running++;
                    break;
                case 2:
                    succeeded++;
                    break;
                case 3:
                    failed++;
                    if (nextName is null || string.CompareOrdinal(next, nextAt ?? next) < 0)
                    {
                        nextName = name;
                        nextAt = next;
                    }

                    break;
                case 4:
                    dead++;
                    deadName ??= name;
                    break;
                case 5:
                    cancelled++;
                    break;
            }
        }

        return new QueueCounts
        {
            Pending = pending,
            Scheduled = scheduled,
            Running = running,
            Failed = failed,
            DeadLetter = dead,
            Succeeded = succeeded,
            Cancelled = cancelled,
            NextName = nextName,
            NextAt = nextAt,
            DeadLetterName = deadName
        };
    }

    public static string Summarize(string plugin, QueueCounts counts)
    {
        var parts = new List<string> { $"{counts.Active} pending" };
        if (!string.IsNullOrWhiteSpace(counts.NextName))
            parts.Add(counts.NextName);
        if (counts.DeadLetter > 0)
            parts.Add($"{counts.DeadLetter} dead letter");
        return $"{plugin} {string.Join("  ·  ", parts)}";
    }

    public static string StatusName(int status) =>
        status >= 0 && status < JobStatusNames.Length ? JobStatusNames[status] : status.ToString();

    static SqliteConnection Open(string path)
    {
        var db = new SqliteConnection($"Data Source={path};Mode=ReadOnly");
        db.Open();
        return db;
    }

    static bool TableExists(SqliteConnection db, string name)
    {
        using var command = db.CreateCommand();
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name";
        command.Parameters.AddWithValue("$name", name);
        return command.ExecuteScalar() is not null;
    }
}
