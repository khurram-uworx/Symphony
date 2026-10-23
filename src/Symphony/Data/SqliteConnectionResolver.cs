using Microsoft.Data.Sqlite;

namespace Symphony.Data;

public static class SqliteConnectionResolver
{
    static bool isMemory(string dataSource) =>
        string.Equals(dataSource, ":memory:", StringComparison.OrdinalIgnoreCase);

    public static string Resolve(string connectionString, string contentRoot)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (isMemory(builder.DataSource))
            return builder.ToString();

        if (!Path.IsPathRooted(builder.DataSource))
            builder.DataSource = Path.GetFullPath(Path.Combine(contentRoot, builder.DataSource));

        var directory = Path.GetDirectoryName(builder.DataSource);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        return builder.ToString();
    }
}
