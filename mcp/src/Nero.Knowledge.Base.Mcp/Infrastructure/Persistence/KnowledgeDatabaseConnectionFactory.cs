using Microsoft.Data.Sqlite;

namespace Nero.Knowledge.Base.Mcp.Infrastructure.Persistence;

public sealed class KnowledgeDatabaseConnectionFactory(KnowledgeDatabaseOptions options)
{
    /// <summary>
    /// Creates a SQLite connection for the configured local knowledge index.
    /// </summary>
    public SqliteConnection CreateConnection()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Path);
        if (options.BusyTimeoutMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.BusyTimeoutMilliseconds),
                options.BusyTimeoutMilliseconds,
                "SQLite busy timeout must be greater than zero milliseconds.");
        }

        var databasePath = ResolveDatabasePath(options.Path);
        var directoryPath = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            DefaultTimeout = checked((int)Math.Ceiling(options.BusyTimeoutMilliseconds / 1000d)),
            Pooling = options.Pooling
        }.ToString();

        return new SqliteConnection(connectionString);
    }

    /// <summary>
    /// Resolves relative database paths against the MCP application base directory.
    /// </summary>
    public static string ResolveDatabasePath(string configuredPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configuredPath);

        return Path.IsPathRooted(configuredPath)
            ? Path.GetFullPath(configuredPath)
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuredPath));
    }
}
