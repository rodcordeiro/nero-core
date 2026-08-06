using Microsoft.Data.Sqlite;
using Nero.Knowledge.Base.Mcp.Application.Contracts.Admin;
using Nero.Knowledge.Base.Mcp.Application.Services.Writing;
using Nero.Knowledge.Base.Mcp.Infrastructure.Indexing;
using Nero.Knowledge.Base.Mcp.Infrastructure.Persistence;

namespace Nero.Knowledge.Base.Mcp.Application.Services.Admin;

public sealed class AdminStatusService(
    KnowledgeDatabaseOptions databaseOptions,
    KnowledgeRootOptions knowledgeRootOptions,
    KnowledgeWriteOptions writeOptions,
    IGitCommandRunner gitCommandRunner)
{
    public async Task<AdminStatusResult> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var knowledgeRootPath = knowledgeRootOptions.ResolvePath();
        var repositoryRoot = FindRepositoryRoot(knowledgeRootPath) ?? FindRepositoryRoot(Environment.CurrentDirectory) ?? Environment.CurrentDirectory;
        var databasePath = KnowledgeDatabaseConnectionFactory.ResolveDatabasePath(databaseOptions.Path);
        var databaseExists = File.Exists(databasePath);
        var modifiedFiles = await GetModifiedFilesAsync(repositoryRoot, cancellationToken);

        return new AdminStatusResult
        {
            Server = "nero-knowledge-base",
            RepositoryRoot = repositoryRoot,
            Branch = await GetBranchAsync(repositoryRoot, cancellationToken),
            HasModifiedFiles = modifiedFiles.Count > 0,
            ModifiedFiles = modifiedFiles,
            IndexDatabaseExists = databaseExists,
            IndexDatabasePath = databasePath,
            LastIndexedUtc = databaseExists
                ? await GetLastIndexedUtcAsync(databasePath, cancellationToken)
                : null,
            WriteMode = writeOptions.Mode
        };
    }

    private async Task<string?> GetBranchAsync(string repositoryRoot, CancellationToken cancellationToken)
    {
        var result = await gitCommandRunner.RunAsync(
            repositoryRoot,
            ["branch", "--show-current"],
            cancellationToken);

        return result.ExitCode == 0
            ? NullIfWhiteSpace(result.Output.Trim())
            : null;
    }

    private async Task<IReadOnlyList<string>> GetModifiedFilesAsync(
        string repositoryRoot,
        CancellationToken cancellationToken)
    {
        var result = await gitCommandRunner.RunAsync(
            repositoryRoot,
            ["status", "--porcelain", "--untracked-files=no"],
            cancellationToken);

        if (result.ExitCode != 0)
        {
            return [];
        }

        return result.Output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Length > 3 ? line[3..].Trim() : line.Trim())
            .ToList();
    }

    private static async Task<string?> GetLastIndexedUtcAsync(
        string databasePath,
        CancellationToken cancellationToken)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT MAX(updated_utc)
            FROM knowledge_nodes;
            """;

        return NullIfWhiteSpace((string?)await command.ExecuteScalarAsync(cancellationToken));
    }

    private static string? FindRepositoryRoot(string startPath)
    {
        var directory = Directory.Exists(startPath)
            ? new DirectoryInfo(startPath)
            : Directory.GetParent(startPath);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git"))
                || File.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
