using Microsoft.Data.Sqlite;

namespace Plugin.Maui.Pulse;

public static class SyncInspector
{
    public static bool TryInspect(string path, out SyncSnapshot snapshot, out string? error)
    {
        snapshot = new SyncSnapshot();
        error = null;
        if (!File.Exists(path))
        {
            error = "not_installed";
            return false;
        }

        try
        {
            using var db = new SqliteConnection($"Data Source={path};Mode=ReadOnly");
            db.Open();
            if (!TableExists(db, "SyncChangeRecord") && !TableExists(db, "SyncDocumentRecord"))
            {
                error = "not a Plugin.Maui.OfflineSync database";
                return false;
            }

            var pending = Scalar(db, "SELECT COUNT(*) FROM SyncChangeRecord");
            var conflicts = Scalar(db, "SELECT COUNT(*) FROM SyncDocumentRecord WHERE SyncStateValue = 4");
            var failed = Scalar(db, "SELECT COUNT(*) FROM SyncDocumentRecord WHERE SyncStateValue = 5");
            var collections = QueryStrings(db, "SELECT DISTINCT Collection FROM SyncDocumentRecord ORDER BY Collection");
            var sample = QueryString(db,
                "SELECT Collection || '#' || EntityId FROM SyncDocumentRecord WHERE SyncStateValue = 4 LIMIT 1");

            snapshot = new SyncSnapshot
            {
                PendingChanges = pending,
                Conflicts = conflicts,
                Failed = failed,
                Collections = collections,
                SampleConflict = sample
            };
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static string Summarize(SyncSnapshot snapshot)
    {
        if (snapshot.Conflicts > 0)
            return $"{snapshot.Conflicts} conflict{(snapshot.Conflicts == 1 ? "" : "s")}  ·  {snapshot.SampleConflict ?? "offlinesync.db3"}";
        if (snapshot.PendingChanges > 0)
            return $"{snapshot.PendingChanges} pending";
        return "idle";
    }

    static bool TableExists(SqliteConnection db, string name)
    {
        using var command = db.CreateCommand();
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name";
        command.Parameters.AddWithValue("$name", name);
        return command.ExecuteScalar() is not null;
    }

    static int Scalar(SqliteConnection db, string sql)
    {
        try
        {
            using var command = db.CreateCommand();
            command.CommandText = sql;
            var value = command.ExecuteScalar();
            return value is long number ? (int)number : Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (SqliteException)
        {
            return 0;
        }
    }

    static string? QueryString(SqliteConnection db, string sql)
    {
        try
        {
            using var command = db.CreateCommand();
            command.CommandText = sql;
            return command.ExecuteScalar() as string;
        }
        catch (SqliteException)
        {
            return null;
        }
    }

    static IReadOnlyList<string> QueryStrings(SqliteConnection db, string sql)
    {
        try
        {
            using var command = db.CreateCommand();
            command.CommandText = sql;
            using var reader = command.ExecuteReader();
            var list = new List<string>();
            while (reader.Read())
            {
                if (!reader.IsDBNull(0))
                    list.Add(reader.GetString(0));
            }

            return list;
        }
        catch (SqliteException)
        {
            return [];
        }
    }
}
