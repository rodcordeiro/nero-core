using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nero.Knowledge.Base.Mcp.Hosting;
using Nero.Knowledge.Base.Mcp.Application.Services.Writing;
using Nero.Knowledge.Base.Mcp.Infrastructure.Indexing;
using Nero.Knowledge.Base.Mcp.Infrastructure.Persistence;

namespace Nero.Knowledge.Base.Tests;

public class KnowledgeDatabaseConnectionFactoryTests
{
    [Fact]
    public async Task CreateConnection_CreatesDirectoryAndOpensFileDatabase()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            "Nero.Knowledge.Base.Tests",
            Guid.NewGuid().ToString("N"),
            "index",
            "knowledge.db");
        var factory = new KnowledgeDatabaseConnectionFactory(new KnowledgeDatabaseOptions
        {
            Path = databasePath
        });

        await using var connection = factory.CreateConnection();
        await connection.OpenAsync();

        Assert.True(Directory.Exists(Path.GetDirectoryName(databasePath)));
        Assert.Equal(System.Data.ConnectionState.Open, connection.State);
    }

    [Fact]
    public void CreateConnection_ConfiguresBusyTimeoutAndPooling()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "knowledge.db");
        var factory = new KnowledgeDatabaseConnectionFactory(new KnowledgeDatabaseOptions
        {
            Path = databasePath,
            BusyTimeoutMilliseconds = 7500,
            Pooling = false
        });

        using var connection = factory.CreateConnection();
        var connectionString = new SqliteConnectionStringBuilder(connection.ConnectionString);
        Assert.Equal(8, connectionString.DefaultTimeout);
        Assert.False(connectionString.Pooling);
    }

    [Fact]
    public void Build_RegistersDatabaseOptionsFromEnvironmentOverride()
    {
        const string pathVariableName = "KnowledgeDatabase__Path";
        const string timeoutVariableName = "KnowledgeDatabase__BusyTimeoutMilliseconds";
        const string poolingVariableName = "KnowledgeDatabase__Pooling";
        var previousPath = Environment.GetEnvironmentVariable(pathVariableName);
        var previousTimeout = Environment.GetEnvironmentVariable(timeoutVariableName);
        var previousPooling = Environment.GetEnvironmentVariable(poolingVariableName);
        var databasePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "knowledge.db");

        try
        {
            Environment.SetEnvironmentVariable(pathVariableName, databasePath);
            Environment.SetEnvironmentVariable(timeoutVariableName, "9000");
            Environment.SetEnvironmentVariable(poolingVariableName, "false");

            using var host = McpHost.Build();
            var options = host.Services.GetRequiredService<KnowledgeDatabaseOptions>();

            Assert.Equal(databasePath, options.Path);
            Assert.Equal(9000, options.BusyTimeoutMilliseconds);
            Assert.False(options.Pooling);
            Assert.NotNull(host.Services.GetRequiredService<KnowledgeDatabaseConnectionFactory>());
            Assert.Contains(
                host.Services.GetServices<ILoggerProvider>(),
                provider => provider.GetType().Name == "ConsoleLoggerProvider");
        }
        finally
        {
            Environment.SetEnvironmentVariable(pathVariableName, previousPath);
            Environment.SetEnvironmentVariable(timeoutVariableName, previousTimeout);
            Environment.SetEnvironmentVariable(poolingVariableName, previousPooling);
        }
    }

    [Fact]
    public void ResolveDatabasePath_UsesApplicationBaseDirectoryForRelativePath()
    {
        var resolvedPath = KnowledgeDatabaseConnectionFactory.ResolveDatabasePath("data/nero-knowledge.db");

        Assert.Equal(
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "data", "nero-knowledge.db")),
            resolvedPath);
    }

    [Fact]
    public void Build_RegistersKnowledgeRootOptionsFromEnvironmentOverride()
    {
        const string variableName = "KnowledgeRoot__Path";
        var previousValue = Environment.GetEnvironmentVariable(variableName);
        var knowledgeRootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "knowledge");

        try
        {
            Environment.SetEnvironmentVariable(variableName, knowledgeRootPath);

            using var host = McpHost.Build();
            var options = host.Services.GetRequiredService<KnowledgeRootOptions>();

            Assert.Equal(knowledgeRootPath, options.Path);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variableName, previousValue);
        }
    }

    [Fact]
    public void Build_RegistersKnowledgeWriteOptionsFromEnvironmentOverride()
    {
        const string variableName = "KnowledgeWrite__Mode";
        var previousValue = Environment.GetEnvironmentVariable(variableName);

        try
        {
            Environment.SetEnvironmentVariable(variableName, "read_only");

            using var host = McpHost.Build();
            var options = host.Services.GetRequiredService<KnowledgeWriteOptions>();

            Assert.Equal("read_only", options.Mode);
            Assert.NotNull(host.Services.GetRequiredService<KnowledgeWritePolicy>());
        }
        finally
        {
            Environment.SetEnvironmentVariable(variableName, previousValue);
        }
    }
}
