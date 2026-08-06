using Microsoft.Data.Sqlite;
using Nero.Knowledge.Base.Mcp.Application.Services.Admin;
using Nero.Knowledge.Base.Mcp.Infrastructure.Indexing;
using Nero.Knowledge.Base.Mcp.Infrastructure.Persistence;

namespace Nero.Knowledge.Base.Mcp.Application.Services.Operations;

public sealed class KnowledgeCliCommandRunner(
    KnowledgeDatabaseConnectionFactory connectionFactory,
    KnowledgeRootOptions knowledgeRootOptions,
    KnowledgeIndexer knowledgeIndexer,
    AdminKnowledgeMaintenanceService adminKnowledgeMaintenanceService)
{
    private static readonly HashSet<string> KnownCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "reindex",
        "validate",
        "dump-graph",
        "check-orphans"
    };

    public static bool IsCommand(IReadOnlyList<string> args)
    {
        return args.Count > 0 && KnownCommands.Contains(args[0]);
    }

    public async Task<int> ExecuteAsync(
        IReadOnlyList<string> args,
        TextWriter output,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);

        if (args.Count == 0)
        {
            await output.WriteLineAsync("Missing command. Use reindex, validate, dump-graph or check-orphans.");
            return 1;
        }

        return args[0].ToLowerInvariant() switch
        {
            "reindex" => await ReindexAsync(output, cancellationToken),
            "validate" => await ValidateAsync(output, cancellationToken),
            "dump-graph" => await DumpGraphAsync(output, cancellationToken),
            "check-orphans" => await CheckOrphansAsync(output, cancellationToken),
            _ => await UnknownCommandAsync(args[0], output)
        };
    }

    private async Task<int> ReindexAsync(TextWriter output, CancellationToken cancellationToken)
    {
        var knowledgeRootPath = knowledgeRootOptions.ResolvePath();
        await using var connection = connectionFactory.CreateConnection();
        var result = await knowledgeIndexer.ReindexAsync(connection, knowledgeRootPath, cancellationToken);

        await output.WriteLineAsync($"Reindexed {result.NodeCount} knowledge nodes.");
        return 0;
    }

    private async Task<int> ValidateAsync(TextWriter output, CancellationToken cancellationToken)
    {
        var result = await adminKnowledgeMaintenanceService.ValidateAsync(cancellationToken);
        if (!result.IsValid)
        {
            foreach (var error in result.Errors)
            {
                await output.WriteLineAsync(error);
            }

            return 1;
        }

        await output.WriteLineAsync($"Validated {result.NodeCount} nodes and {result.EdgeCount} edges.");
        return 0;
    }

    private async Task<int> DumpGraphAsync(TextWriter output, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await KnowledgeDatabaseMigrator.MigrateAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT source_node_id, relation, target_node_id, evidence
            FROM knowledge_edges
            ORDER BY source_node_id, relation, target_node_id;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var count = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            count++;
            await output.WriteLineAsync(
                $"{reader.GetString(0)} --{reader.GetString(1)}--> {reader.GetString(2)} | {reader.GetString(3)}");
        }

        await output.WriteLineAsync($"Dumped {count} knowledge edges.");
        return 0;
    }

    private async Task<int> CheckOrphansAsync(TextWriter output, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await KnowledgeDatabaseMigrator.MigrateAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT node.id, node.title
            FROM knowledge_nodes node
            WHERE NOT EXISTS (
                SELECT 1 FROM knowledge_edges edge
                WHERE edge.source_node_id = node.id OR edge.target_node_id = node.id
            )
            ORDER BY node.id;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var count = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            count++;
            await output.WriteLineAsync($"{reader.GetString(0)} | {reader.GetString(1)}");
        }

        await output.WriteLineAsync($"Found {count} orphan knowledge nodes.");
        return 0;
    }

    private static async Task<int> UnknownCommandAsync(string command, TextWriter output)
    {
        await output.WriteLineAsync($"Unknown command '{command}'. Use reindex, validate, dump-graph or check-orphans.");
        return 1;
    }
}
