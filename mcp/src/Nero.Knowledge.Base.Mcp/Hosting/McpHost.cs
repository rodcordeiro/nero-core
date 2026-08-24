using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nero.Knowledge.Base.Mcp.Application.Services.Admin;
using Nero.Knowledge.Base.Mcp.Application.Services.BusinessRules;
using Nero.Knowledge.Base.Mcp.Application.Services.Decisions;
using Nero.Knowledge.Base.Mcp.Application.Services.Domains;
using Nero.Knowledge.Base.Mcp.Application.Services.Patterns;
using Nero.Knowledge.Base.Mcp.Application.Contracts.Domains;
using Nero.Knowledge.Base.Mcp.Application.Services.Graph;
using Nero.Knowledge.Base.Mcp.Application.Contracts.Graph;
using Nero.Knowledge.Base.Mcp.Application.Services.Links;
using Nero.Knowledge.Base.Mcp.Infrastructure.Indexing;
using Nero.Knowledge.Base.Mcp.Application.Services.Projects;
using Nero.Knowledge.Base.Mcp.Application.Contracts.Projects;
using Nero.Knowledge.Base.Mcp.Application.Services.Search;
using Nero.Knowledge.Base.Mcp.Application.Contracts.Search;
using Nero.Knowledge.Base.Mcp.Application.Services.Snapshots;
using Nero.Knowledge.Base.Mcp.Application.Services.Operations;
using Nero.Knowledge.Base.Mcp.Application.Services.Troubleshooting;
using Nero.Knowledge.Base.Mcp.Application.Services.ValidationRules;
using Nero.Knowledge.Base.Mcp.Application.Services.Writing;
using Nero.Knowledge.Base.Mcp.Infrastructure.Persistence;

namespace Nero.Knowledge.Base.Mcp.Hosting;

public static class McpHost
{
    public static void Configure(IHostApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(options =>
        {
            options.LogToStandardErrorThreshold = LogLevel.Trace;
        });

        var databaseSection = builder.Configuration.GetSection(KnowledgeDatabaseOptions.SectionName);
        var databaseOptions = new KnowledgeDatabaseOptions
        {
            Path = databaseSection[nameof(KnowledgeDatabaseOptions.Path)] ?? new KnowledgeDatabaseOptions().Path,
            BusyTimeoutMilliseconds = int.TryParse(
                databaseSection[nameof(KnowledgeDatabaseOptions.BusyTimeoutMilliseconds)],
                out var busyTimeoutMilliseconds)
                ? busyTimeoutMilliseconds
                : KnowledgeDatabaseOptions.DefaultBusyTimeoutMilliseconds,
            Pooling = bool.TryParse(
                databaseSection[nameof(KnowledgeDatabaseOptions.Pooling)],
                out var pooling)
                ? pooling
                : new KnowledgeDatabaseOptions().Pooling
        };
        var knowledgeRootSection = builder.Configuration.GetSection(KnowledgeRootOptions.SectionName);
        var knowledgeRootOptions = new KnowledgeRootOptions
        {
            Path = knowledgeRootSection[nameof(KnowledgeRootOptions.Path)] ?? new KnowledgeRootOptions().Path
        };
        var writeSection = builder.Configuration.GetSection(KnowledgeWriteOptions.SectionName);
        var writeOptions = new KnowledgeWriteOptions
        {
            Mode = writeSection[nameof(KnowledgeWriteOptions.Mode)] ?? new KnowledgeWriteOptions().Mode
        };
        var indexConsistencySection = builder.Configuration.GetSection(AdminIndexConsistencyOptions.SectionName);
        var indexConsistencyOptions = new AdminIndexConsistencyOptions
        {
            ThresholdMilliseconds = int.TryParse(
                indexConsistencySection[nameof(AdminIndexConsistencyOptions.ThresholdMilliseconds)],
                out var thresholdMilliseconds)
                ? thresholdMilliseconds
                : AdminIndexConsistencyOptions.DefaultThresholdMilliseconds
        };
        var projectFreshnessSection = builder.Configuration.GetSection(AdminProjectFreshnessOptions.SectionName);
        var projectFreshnessOptions = new AdminProjectFreshnessOptions
        {
            RecentSnapshotDays = int.TryParse(
                projectFreshnessSection[nameof(AdminProjectFreshnessOptions.RecentSnapshotDays)],
                out var recentSnapshotDays)
                ? recentSnapshotDays
                : AdminProjectFreshnessOptions.DefaultRecentSnapshotDays
        };

        builder.Services.AddSingleton(databaseOptions);
        builder.Services.AddSingleton(knowledgeRootOptions);
        builder.Services.AddSingleton(writeOptions);
        builder.Services.AddSingleton(indexConsistencyOptions);
        builder.Services.AddSingleton(projectFreshnessOptions);
        builder.Services.AddSingleton<KnowledgeWritePolicy>();
        builder.Services.AddSingleton<IGitCommandRunner, GitCommandRunner>();
        builder.Services.AddSingleton<AdminGitService>();
        builder.Services.AddSingleton<AdminStatusService>();
        builder.Services.AddSingleton<AdminKnowledgeMaintenanceService>();
        builder.Services.AddSingleton<KnowledgeIndexedPathReader>();
        builder.Services.AddSingleton<IAdminBatchOperations, AdminBatchOperations>();
        builder.Services.AddSingleton<AdminBatchFinalizationService>();
        builder.Services.AddSingleton<AdminTrustAuditService>();
        builder.Services.AddSingleton<KnowledgeDatabaseConnectionFactory>();
        builder.Services.AddSingleton<KnowledgeMarkdownReader>();
        builder.Services.AddSingleton<BusinessRuleWriterService>();
        builder.Services.AddSingleton<DecisionWriterService>();
        builder.Services.AddSingleton<PatternWriterService>();
        builder.Services.AddSingleton<SnapshotWriterService>();
        builder.Services.AddSingleton<TroubleshootingWriterService>();
        builder.Services.AddSingleton<ValidationRuleWriterService>();
        builder.Services.AddSingleton<KnowledgeIndexer>();
        builder.Services.AddSingleton<KnowledgeDomainContextService>();
        builder.Services.AddSingleton<KnowledgeLinkService>();
        builder.Services.AddSingleton<KnowledgeCliCommandRunner>();
        builder.Services.AddSingleton<ActiveDomainCatalog>();
        builder.Services.AddSingleton<DomainWriterService>();
        builder.Services.AddSingleton<ProjectWriterService>();
        builder.Services.AddSingleton<ProjectUpdateWriterService>();
        builder.Services.AddSingleton<KnowledgeProjectContextService>();
        builder.Services.AddSingleton<RelatedKnowledgeService>();
        builder.Services.AddSingleton<KnowledgeSearchService>();

        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly();
    }

    public static IHost Build(string[]? args = null)
    {
        var builder = Host.CreateApplicationBuilder(args ?? []);
        Configure(builder);
        return builder.Build();
    }
}
