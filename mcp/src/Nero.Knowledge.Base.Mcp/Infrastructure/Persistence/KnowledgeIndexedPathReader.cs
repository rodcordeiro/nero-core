namespace Nero.Knowledge.Base.Mcp.Infrastructure.Persistence;

public sealed class KnowledgeIndexedPathReader(KnowledgeDatabaseConnectionFactory connectionFactory)
{
    /// <summary>
    /// Reads normalized Markdown paths from the derived SQLite index.
    /// </summary>
    public async Task<IReadOnlySet<string>> ReadAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await KnowledgeDatabaseMigrator.MigrateAsync(connection, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT path FROM knowledge_nodes ORDER BY path;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync(cancellationToken))
        {
            var path = reader.GetString(0);
            paths.Add(path.StartsWith("knowledge/", StringComparison.OrdinalIgnoreCase)
                ? path["knowledge/".Length..].Replace('\\', '/')
                : path.Replace('\\', '/'));
        }

        return paths;
    }
}
