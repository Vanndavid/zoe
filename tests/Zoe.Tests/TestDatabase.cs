using Microsoft.Data.Sqlite;

namespace Zoe.Tests;

internal static class TestDatabase
{
    /// <summary>
    /// Deletes a test database file. Pooled SQLite connections keep the file open after
    /// their context is disposed, which Windows refuses to delete, so the pool is cleared first.
    /// </summary>
    public static void Delete(string path)
    {
        SqliteConnection.ClearAllPools();

        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
