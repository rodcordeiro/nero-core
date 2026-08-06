using System.Diagnostics;
using System.Globalization;
using Nero.Knowledge.Base.Mcp.Application.Contracts.Admin;
using Nero.Knowledge.Base.Mcp.Application.Services.Compliance;
using Nero.Knowledge.Base.Mcp.Application.Services.Domains;
using Nero.Knowledge.Base.Mcp.Domain;
using Nero.Knowledge.Base.Mcp.Infrastructure.Indexing;
using Nero.Knowledge.Base.Mcp.Infrastructure.Persistence;

namespace Nero.Knowledge.Base.Mcp.Application.Services.Admin;

public sealed class AdminKnowledgeMaintenanceService(
    KnowledgeDatabaseConnectionFactory connectionFactory,
    KnowledgeDatabaseOptions databaseOptions,
    KnowledgeRootOptions knowledgeRootOptions,
    KnowledgeIndexer knowledgeIndexer,
    KnowledgeMarkdownReader markdownReader,
    AdminIndexConsistencyOptions indexConsistencyOptions,
    AdminProjectFreshnessOptions projectFreshnessOptions)
{
    private static readonly TimeSpan TimestampTolerance = TimeSpan.FromSeconds(1);

    public async Task<AdminValidationResult> ValidateAsync(CancellationToken cancellationToken = default)
    {
        var knowledgeRootPath = knowledgeRootOptions.ResolvePath();
        var errors = ValidateRequiredStructure(knowledgeRootPath);
        IReadOnlyList<KnowledgeMarkdownDocument> documents = [];
        var nodes = new List<KnowledgeNode>();
        var edges = new List<KnowledgeEdge>();

        try
        {
            documents = await markdownReader.ReadAsync(knowledgeRootPath, cancellationToken);
            errors.AddRange(ValidateFrontmatter(documents));
            // Semantic vocabulary + orphans (G2), dependency direction (G3), evidences hubs (G4).
            errors.AddRange(KnowledgeSemanticValidation.Validate(documents));
            nodes = documents.Select(KnowledgeIndexer.ToKnowledgeNode).ToList();
            edges = KnowledgeIndexer.ToKnowledgeEdges(documents, nodes).ToList();
        }
        catch (Exception exception) when (exception is InvalidOperationException or DirectoryNotFoundException)
        {
            errors.Add(exception.Message);
        }

        foreach (var node in nodes)
        {
            var validation = node.Validate();
            if (!validation.IsValid)
            {
                errors.Add($"Node '{node.Id}': {string.Join(" ", validation.Errors)}");
            }
        }

        foreach (var edge in edges)
        {
            var validation = edge.Validate();
            if (!validation.IsValid)
            {
                errors.Add($"Edge '{edge.Id}': {string.Join(" ", validation.Errors)}");
            }
        }

        var compliance = await ScanComplianceCoreAsync(documents, cancellationToken);
        var complianceGaps = compliance.ActiveHits
            .Select(hit => $"{hit.Path}:{hit.Line} {hit.RuleId} {hit.MaskedExcerpt}")
            .ToArray();

        return new AdminValidationResult
        {
            IsValid = errors.Count == 0,
            IsCompliant = compliance.IsCompliant,
            NodeCount = nodes.Count,
            EdgeCount = edges.Count,
            Errors = errors,
            ComplianceGaps = complianceGaps
        };
    }

    public async Task<AdminComplianceScanResult> ScanComplianceAsync(CancellationToken cancellationToken = default)
    {
        var knowledgeRootPath = knowledgeRootOptions.ResolvePath();
        IReadOnlyList<KnowledgeMarkdownDocument> documents = [];
        if (Directory.Exists(knowledgeRootPath))
        {
            documents = await markdownReader.ReadAsync(knowledgeRootPath, cancellationToken);
        }

        return await ScanComplianceCoreAsync(documents, cancellationToken);
    }

    private static Task<AdminComplianceScanResult> ScanComplianceCoreAsync(
        IReadOnlyList<KnowledgeMarkdownDocument> documents,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var activeHits = new List<AdminComplianceScanIssue>();
        var quarantinedHits = new List<AdminComplianceScanIssue>();
        var warnings = new List<AdminComplianceScanIssue>();

        foreach (var document in documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var quarantined = ComplianceFrontmatter.IsQuarantined(document.Frontmatter);
            document.Frontmatter.TryGetValue(ComplianceFrontmatter.ReasonKey, out var reason);
            var scanText = string.IsNullOrEmpty(document.Content)
                ? document.Title
                : $"{document.Title}\n{document.Content}";
            // Also scan raw frontmatter values (excluding reason text that may quote masked placeholders).
            foreach (var pair in document.Frontmatter)
            {
                if (string.Equals(pair.Key, ComplianceFrontmatter.ReasonKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                scanText += "\n" + pair.Value;
            }

            foreach (var hit in ComplianceScanner.Scan(scanText))
            {
                var issue = new AdminComplianceScanIssue
                {
                    Path = document.Path,
                    RuleId = hit.RuleId,
                    Severity = hit.Severity.ToString(),
                    Line = hit.Line,
                    MaskedExcerpt = hit.MaskedExcerpt,
                    Quarantined = quarantined,
                    ComplianceReason = quarantined ? reason : null
                };

                if (hit.Severity == ComplianceSeverity.Warning)
                {
                    warnings.Add(issue);
                    continue;
                }

                if (quarantined)
                {
                    quarantinedHits.Add(issue);
                }
                else
                {
                    activeHits.Add(issue);
                }
            }
        }

        return Task.FromResult(new AdminComplianceScanResult
        {
            IsCompliant = activeHits.Count == 0,
            TaxonomyVersion = ComplianceTaxonomy.Version,
            ScannedFileCount = documents.Count,
            ActiveBlockingHitCount = activeHits.Count,
            QuarantinedBlockingHitCount = quarantinedHits.Count,
            WarningHitCount = warnings.Count,
            ActiveHits = activeHits,
            QuarantinedHits = quarantinedHits,
            Warnings = warnings
        });
    }

    public async Task<AdminReindexResult> ReindexAsync(CancellationToken cancellationToken = default)
    {
        var knowledgeRootPath = knowledgeRootOptions.ResolvePath();
        await using var connection = connectionFactory.CreateConnection();
        var result = await knowledgeIndexer.ReindexAsync(connection, knowledgeRootPath, cancellationToken);

        return new AdminReindexResult
        {
            IndexedNodes = result.NodeCount,
            KnowledgeRootPath = knowledgeRootPath,
            IndexDatabasePath = KnowledgeDatabaseConnectionFactory.ResolveDatabasePath(databaseOptions.Path)
        };
    }

    public async Task<AdminIndexConsistencyResult> CheckIndexConsistencyAsync(
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var thresholdMilliseconds = Math.Max(0, indexConsistencyOptions.ThresholdMilliseconds);
        var knowledgeRootPath = knowledgeRootOptions.ResolvePath();
        var databasePath = KnowledgeDatabaseConnectionFactory.ResolveDatabasePath(databaseOptions.Path);
        var issues = new List<AdminIndexConsistencyIssue>();
        var documents = Directory.Exists(knowledgeRootPath)
            ? await markdownReader.ReadAsync(knowledgeRootPath, cancellationToken)
            : [];
        var documentsById = documents.ToDictionary(document => document.Id, StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(databasePath))
        {
            issues.Add(new AdminIndexConsistencyIssue
            {
                Type = "MissingIndexDatabase",
                Id = databasePath,
                Path = databasePath,
                Recommendation = "Run nero_admin_reindex to create the SQLite index from the Markdown filesystem."
            });

            return BuildConsistencyResult(
                stopwatch,
                thresholdMilliseconds,
                isConsistent: false,
                knowledgeRootPath,
                databasePath,
                indexedNodeCount: 0,
                markdownFileCount: documents.Count,
                issues);
        }

        await using var connection = connectionFactory.CreateConnection();
        await KnowledgeDatabaseMigrator.MigrateAsync(connection, cancellationToken);
        var indexedNodes = await ReadIndexedNodesAsync(connection, cancellationToken);
        var indexedNodesById = indexedNodes.ToDictionary(node => node.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var indexedNode in indexedNodes)
        {
            if (!documentsById.TryGetValue(indexedNode.Id, out var document))
            {
                issues.Add(new AdminIndexConsistencyIssue
                {
                    Type = "IndexedNodeMissingFile",
                    Id = indexedNode.Id,
                    Path = indexedNode.Path,
                    IndexedUpdatedUtc = indexedNode.UpdatedUtc,
                    Recommendation = "Run nero_admin_reindex if the Markdown file was intentionally removed, or restore the missing file."
                });
                continue;
            }

            var filePath = ResolveMarkdownFilePath(knowledgeRootPath, document.Path);
            var fileLastWriteUtc = File.GetLastWriteTimeUtc(filePath);
            if (TryParseSqliteUtc(indexedNode.UpdatedUtc, out var indexedUpdatedUtc)
                && fileLastWriteUtc > indexedUpdatedUtc.Add(TimestampTolerance))
            {
                issues.Add(new AdminIndexConsistencyIssue
                {
                    Type = "MarkdownNewerThanIndex",
                    Id = indexedNode.Id,
                    Path = document.Path,
                    IndexedUpdatedUtc = indexedUpdatedUtc.ToString("O"),
                    FileLastWriteUtc = fileLastWriteUtc.ToString("O"),
                    Recommendation = "Run nero_admin_reindex so SQLite reflects the latest Markdown file content."
                });
            }
        }

        foreach (var document in documents)
        {
            if (!indexedNodesById.ContainsKey(document.Id))
            {
                issues.Add(new AdminIndexConsistencyIssue
                {
                    Type = "MarkdownMissingIndexedNode",
                    Id = document.Id,
                    Path = document.Path,
                    FileLastWriteUtc = File.GetLastWriteTimeUtc(ResolveMarkdownFilePath(knowledgeRootPath, document.Path)).ToString("O"),
                    Recommendation = "Run nero_admin_reindex to add this Markdown file to the SQLite index."
                });
            }
        }

        return BuildConsistencyResult(
            stopwatch,
            thresholdMilliseconds,
            isConsistent: issues.Count == 0,
            knowledgeRootPath,
            databasePath,
            indexedNodeCount: indexedNodes.Count,
            markdownFileCount: documents.Count,
            issues);
    }

    private static AdminIndexConsistencyResult BuildConsistencyResult(
        Stopwatch stopwatch,
        int thresholdMilliseconds,
        bool isConsistent,
        string knowledgeRootPath,
        string databasePath,
        int indexedNodeCount,
        int markdownFileCount,
        IReadOnlyList<AdminIndexConsistencyIssue> issues)
    {
        stopwatch.Stop();
        var elapsedMilliseconds = stopwatch.ElapsedMilliseconds;

        return new AdminIndexConsistencyResult
        {
            IsConsistent = isConsistent,
            KnowledgeRootPath = knowledgeRootPath,
            IndexDatabasePath = databasePath,
            IndexedNodeCount = indexedNodeCount,
            MarkdownFileCount = markdownFileCount,
            ElapsedMilliseconds = elapsedMilliseconds,
            ThresholdMilliseconds = thresholdMilliseconds,
            ExceededThreshold = elapsedMilliseconds > thresholdMilliseconds,
            Issues = issues
        };
    }

    /// <summary>
    /// Checks all project and domain knowledge scopes from one Markdown read and one SQLite node query.
    /// Healthy scopes are summarized; only scopes with issues are detailed to keep the MCP payload bounded.
    /// </summary>
    public async Task<AdminEcosystemHealthResult> CheckEcosystemHealthAsync(
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var thresholdMilliseconds = Math.Max(0, indexConsistencyOptions.ThresholdMilliseconds);
        var knowledgeRootPath = knowledgeRootOptions.ResolvePath();
        var databasePath = KnowledgeDatabaseConnectionFactory.ResolveDatabasePath(databaseOptions.Path);
        var documents = Directory.Exists(knowledgeRootPath)
            ? await markdownReader.ReadAsync(knowledgeRootPath, cancellationToken)
            : [];
        IReadOnlyList<IndexedNodeRow> indexedNodes = [];

        if (File.Exists(databasePath))
        {
            await using var connection = connectionFactory.CreateConnection();
            await KnowledgeDatabaseMigrator.MigrateAsync(connection, cancellationToken);
            indexedNodes = await ReadIndexedNodesAsync(connection, cancellationToken);
        }

        var projectNames = DiscoverScopeNames(knowledgeRootPath, "projects", documents, indexedNodes);
        var domainNames = DiscoverScopeNames(knowledgeRootPath, "domains", documents, indexedNodes);
        var projectsWithIssues = new List<AdminEcosystemScopeHealthResult>();
        var domainsWithIssues = new List<AdminEcosystemScopeHealthResult>();
        var recentSnapshotDays = Math.Max(1, projectFreshnessOptions.RecentSnapshotDays);

        foreach (var project in projectNames)
        {
            var projectDirectoryPath = Path.Combine(knowledgeRootPath, "projects", project);
            var projectDocuments = documents
                .Where(document => string.Equals(document.Project, project, StringComparison.OrdinalIgnoreCase)
                    || document.Id.StartsWith($"projects/{project}/", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var projectIndexedNodes = indexedNodes
                .Where(node => node.Id.StartsWith($"projects/{project}/", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var existsInFilesystem = Directory.Exists(projectDirectoryPath);
            var hasIndex = File.Exists(Path.Combine(projectDirectoryPath, "index.md"));
            var hasContext = File.Exists(Path.Combine(projectDirectoryPath, "context.md"));
            var hasBaseStructure = hasIndex && hasContext;
            var hasProjectNotes = projectDocuments.Any(document =>
                !document.Id.Equals($"projects/{project}/index", StringComparison.OrdinalIgnoreCase)
                && !document.Id.Equals($"projects/{project}/context", StringComparison.OrdinalIgnoreCase));
            var latestSnapshot = FindLatestSnapshot(projectDocuments);
            var latestSnapshotAgeDays = latestSnapshot is null
                ? (int?)null
                : Math.Max(0, DateOnly.FromDateTime(DateTime.UtcNow).DayNumber - latestSnapshot.Date.DayNumber);
            var issues = BuildProjectHealthIssues(
                project,
                primaryDomain: null,
                projectDirectoryPath,
                databasePath,
                existsInFilesystem,
                existsInIndex: projectIndexedNodes.Count > 0,
                hasIndex,
                hasContext,
                hasNotesWithoutBaseStructure: hasProjectNotes && !hasBaseStructure,
                hasBelongsToDomain: HasBelongsToDomain(projectDocuments, primaryDomain: null)).ToList();
            AddSnapshotFreshnessIssue(
                issues,
                project,
                primaryDomain: null,
                projectDirectoryPath,
                latestSnapshot,
                latestSnapshotAgeDays,
                recentSnapshotDays);

            if (issues.Count > 0)
            {
                projectsWithIssues.Add(new AdminEcosystemScopeHealthResult
                {
                    Name = project,
                    Issues = issues
                });
            }
        }

        foreach (var domain in domainNames)
        {
            var domainDirectoryPath = Path.Combine(knowledgeRootPath, "domains", domain);
            var existsInFilesystem = Directory.Exists(domainDirectoryPath);
            var hasIndex = File.Exists(Path.Combine(domainDirectoryPath, "index.md"));
            var status = hasIndex
                ? ActiveDomainCatalog.ReadStatusFromIndex(Path.Combine(domainDirectoryPath, "index.md"))
                : null;
            var existsInIndex = indexedNodes.Any(
                node => node.Id.StartsWith($"domains/{domain}/", StringComparison.OrdinalIgnoreCase));
            var issues = BuildDomainHealthIssues(
                domainDirectoryPath,
                databasePath,
                existsInFilesystem,
                existsInIndex,
                hasIndex,
                status);

            if (issues.Count > 0)
            {
                domainsWithIssues.Add(new AdminEcosystemScopeHealthResult
                {
                    Name = domain,
                    Issues = issues
                });
            }
        }

        stopwatch.Stop();
        var elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
        var issueCount = projectsWithIssues.Count + domainsWithIssues.Count;

        return new AdminEcosystemHealthResult
        {
            KnowledgeRootPath = knowledgeRootPath,
            IndexDatabasePath = databasePath,
            ProjectCount = projectNames.Count,
            DomainCount = domainNames.Count,
            HealthyProjectCount = projectNames.Count - projectsWithIssues.Count,
            ProjectsWithIssuesCount = projectsWithIssues.Count,
            HealthyDomainCount = domainNames.Count - domainsWithIssues.Count,
            DomainsWithIssuesCount = domainsWithIssues.Count,
            ElapsedMilliseconds = elapsedMilliseconds,
            ThresholdMilliseconds = thresholdMilliseconds,
            ExceededThreshold = elapsedMilliseconds > thresholdMilliseconds,
            Recommendation = issueCount == 0
                ? "Ecosystem knowledge health is healthy."
                : "Resolve the detailed project and domain issues; run nero_admin_reindex when filesystem/index drift is reported.",
            ProjectsWithIssues = projectsWithIssues,
            DomainsWithIssues = domainsWithIssues
        };
    }

    public async Task<AdminProjectHealthResult> CheckProjectHealthAsync(
        string project,
        string? primaryDomain = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(project);

        var knowledgeRootPath = knowledgeRootOptions.ResolvePath();
        var projectRelativePath = Path.Combine("projects", project);
        var projectDirectoryPath = Path.Combine(knowledgeRootPath, projectRelativePath);
        var existsInFilesystem = Directory.Exists(projectDirectoryPath);
        var hasIndex = File.Exists(Path.Combine(projectDirectoryPath, "index.md"));
        var hasContext = File.Exists(Path.Combine(projectDirectoryPath, "context.md"));
        var hasBaseStructure = hasIndex && hasContext;
        var documents = Directory.Exists(knowledgeRootPath)
            ? await markdownReader.ReadAsync(knowledgeRootPath, cancellationToken)
            : [];
        var projectDocuments = documents
            .Where(document => string.Equals(document.Project, project, StringComparison.OrdinalIgnoreCase)
                || document.Id.StartsWith($"projects/{project}/", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var hasProjectNotes = projectDocuments.Any(document =>
            !document.Id.Equals($"projects/{project}/index", StringComparison.OrdinalIgnoreCase)
            && !document.Id.Equals($"projects/{project}/context", StringComparison.OrdinalIgnoreCase));
        var recentSnapshotDays = Math.Max(1, projectFreshnessOptions.RecentSnapshotDays);
        var latestSnapshot = FindLatestSnapshot(projectDocuments);
        var latestSnapshotAgeDays = latestSnapshot is null
            ? (int?)null
            : Math.Max(0, DateOnly.FromDateTime(DateTime.UtcNow).DayNumber - latestSnapshot.Date.DayNumber);
        var hasRecentSnapshot = latestSnapshotAgeDays is not null
            && latestSnapshotAgeDays <= recentSnapshotDays;
        var hasNotesWithoutBaseStructure = hasProjectNotes && !hasBaseStructure;
        var hasBelongsToDomain = HasBelongsToDomain(projectDocuments, primaryDomain);
        var databasePath = KnowledgeDatabaseConnectionFactory.ResolveDatabasePath(databaseOptions.Path);
        var existsInIndex = File.Exists(databasePath)
            && await ProjectExistsInIndexAsync(project, cancellationToken);
        var lastIndexedUtc = File.Exists(databasePath)
            ? await GetProjectLastIndexedUtcAsync(project, cancellationToken)
            : null;
        var issues = BuildProjectHealthIssues(
            project,
            primaryDomain,
            projectDirectoryPath,
            databasePath,
            existsInFilesystem,
            existsInIndex,
            hasIndex,
            hasContext,
            hasNotesWithoutBaseStructure,
            hasBelongsToDomain).ToList();
        AddSnapshotFreshnessIssue(
            issues,
            project,
            primaryDomain,
            projectDirectoryPath,
            latestSnapshot,
            latestSnapshotAgeDays,
            recentSnapshotDays);

        return new AdminProjectHealthResult
        {
            Project = project,
            PrimaryDomain = string.IsNullOrWhiteSpace(primaryDomain) ? null : primaryDomain,
            KnowledgeRootPath = knowledgeRootPath,
            ProjectDirectoryPath = projectDirectoryPath,
            ExistsInFilesystem = existsInFilesystem,
            ExistsInIndex = existsInIndex,
            HasIndex = hasIndex,
            HasContext = hasContext,
            HasBaseStructure = hasBaseStructure,
            HasNotesWithoutBaseStructure = hasNotesWithoutBaseStructure,
            HasBelongsToDomain = hasBelongsToDomain,
            LastIndexedUtc = lastIndexedUtc,
            RecentSnapshotDays = recentSnapshotDays,
            HasRecentSnapshot = hasRecentSnapshot,
            LatestSnapshotPath = latestSnapshot?.Document.Path,
            LatestSnapshotDate = latestSnapshot?.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            LatestSnapshotAgeDays = latestSnapshotAgeDays,
            LatestSnapshotOrigin = latestSnapshot?.Origin,
            Recommendation = RecommendProjectHealthAction(issues, existsInFilesystem, existsInIndex),
            Issues = issues
        };
    }

    private static async Task<IReadOnlyList<IndexedNodeRow>> ReadIndexedNodesAsync(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, path, updated_utc
            FROM knowledge_nodes
            ORDER BY id;
            """;

        var nodes = new List<IndexedNodeRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            nodes.Add(new IndexedNodeRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2)));
        }

        return nodes;
    }

    private async Task<bool> ProjectExistsInIndexAsync(
        string project,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await KnowledgeDatabaseMigrator.MigrateAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM knowledge_nodes
            WHERE project = $project
               OR id LIKE $project_prefix;
            """;
        command.Parameters.AddWithValue("$project", project);
        command.Parameters.AddWithValue("$project_prefix", $"projects/{project}/%");

        return (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L) > 0;
    }

    private async Task<string?> GetProjectLastIndexedUtcAsync(
        string project,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await KnowledgeDatabaseMigrator.MigrateAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT MAX(updated_utc)
            FROM knowledge_nodes
            WHERE project = $project
               OR id LIKE $project_prefix;
            """;
        command.Parameters.AddWithValue("$project", project);
        command.Parameters.AddWithValue("$project_prefix", $"projects/{project}/%");

        return NullIfWhiteSpace((string?)await command.ExecuteScalarAsync(cancellationToken));
    }

    private static bool HasBelongsToDomain(
        IReadOnlyCollection<KnowledgeMarkdownDocument> projectDocuments,
        string? primaryDomain)
    {
        return projectDocuments.Any(document => document.Links.Any(link =>
            link.Type.Equals("belongs_to_domain", StringComparison.OrdinalIgnoreCase)
            && MatchesDomainTarget(link.Target, primaryDomain)));
    }

    private static bool MatchesDomainTarget(string target, string? primaryDomain)
    {
        if (string.IsNullOrWhiteSpace(primaryDomain))
        {
            return target.StartsWith("domains/", StringComparison.OrdinalIgnoreCase);
        }

        return target.Equals($"domains/{primaryDomain}", StringComparison.OrdinalIgnoreCase)
            || target.Equals($"domains/{primaryDomain}/index", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<AdminProjectHealthIssue> BuildProjectHealthIssues(
        string project,
        string? primaryDomain,
        string projectDirectoryPath,
        string databasePath,
        bool existsInFilesystem,
        bool existsInIndex,
        bool hasIndex,
        bool hasContext,
        bool hasNotesWithoutBaseStructure,
        bool hasBelongsToDomain)
    {
        var issues = new List<AdminProjectHealthIssue>();
        if (!existsInFilesystem && !existsInIndex)
        {
            issues.Add(new AdminProjectHealthIssue
            {
                Type = "ProjectMissing",
                Path = projectDirectoryPath,
                Recommendation = "Run nero_register_project before registering project decisions, patterns, rules, validations or troubleshooting notes."
            });
        }

        if (!hasIndex)
        {
            issues.Add(new AdminProjectHealthIssue
            {
                Type = "MissingIndex",
                Path = Path.Combine(projectDirectoryPath, "index.md"),
                Recommendation = "Run nero_register_project to create the missing project index.md."
            });
        }

        if (!hasContext)
        {
            issues.Add(new AdminProjectHealthIssue
            {
                Type = "MissingContext",
                Path = Path.Combine(projectDirectoryPath, "context.md"),
                Recommendation = "Run nero_register_project to create the missing project context.md."
            });
        }

        if (hasNotesWithoutBaseStructure)
        {
            issues.Add(new AdminProjectHealthIssue
            {
                Type = "NotesWithoutBaseStructure",
                Path = projectDirectoryPath,
                Recommendation = "Run nero_register_project to create missing index.md/context.md before adding more project notes."
            });
        }

        if (hasIndex && !hasBelongsToDomain)
        {
            issues.Add(new AdminProjectHealthIssue
            {
                Type = "MissingBelongsToDomain",
                Path = Path.Combine(projectDirectoryPath, "index.md"),
                Recommendation = string.IsNullOrWhiteSpace(primaryDomain)
                    ? "Run nero_update_project_index with dominio and arquivos so index.md includes belongs_to_domain."
                    : $"Run nero_update_project_index with dominio={primaryDomain} so index.md includes belongs_to_domain -> domains/{primaryDomain}."
            });
        }

        if (existsInFilesystem && !existsInIndex)
        {
            issues.Add(new AdminProjectHealthIssue
            {
                Type = "ProjectNotIndexed",
                Path = databasePath,
                Recommendation = "Run nero_admin_reindex so SQLite reflects the project filesystem."
            });
        }

        return issues;
    }

    private static IReadOnlyList<AdminProjectHealthIssue> BuildDomainHealthIssues(
        string domainDirectoryPath,
        string databasePath,
        bool existsInFilesystem,
        bool existsInIndex,
        bool hasIndex,
        string? status)
    {
        var issues = new List<AdminProjectHealthIssue>();
        if (!existsInFilesystem)
        {
            issues.Add(new AdminProjectHealthIssue
            {
                Type = "DomainMissing",
                Path = domainDirectoryPath,
                Recommendation = "Run nero_register_domain to create the domain directory and index.md."
            });
        }

        if (!hasIndex)
        {
            issues.Add(new AdminProjectHealthIssue
            {
                Type = "MissingIndex",
                Path = Path.Combine(domainDirectoryPath, "index.md"),
                Recommendation = "Run nero_register_domain or create the missing domain index.md."
            });
        }

        if (hasIndex
            && string.Equals(status, ActiveDomainCatalog.StatusInactive, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new AdminProjectHealthIssue
            {
                Type = "DomainInactive",
                Path = Path.Combine(domainDirectoryPath, "index.md"),
                Recommendation = "Domain is inactive. Reactivate with nero_update_domain (reativar=true) or migrate projects off belongs_to_domain before relying on it."
            });
        }

        if (existsInFilesystem && !existsInIndex)
        {
            issues.Add(new AdminProjectHealthIssue
            {
                Type = "DomainNotIndexed",
                Path = databasePath,
                Recommendation = "Run nero_admin_reindex so SQLite reflects the domain filesystem."
            });
        }

        return issues;
    }

    private static IReadOnlyList<string> DiscoverScopeNames(
        string knowledgeRootPath,
        string scopeDirectory,
        IReadOnlyCollection<KnowledgeMarkdownDocument> documents,
        IReadOnlyCollection<IndexedNodeRow> indexedNodes)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var scopePath = Path.Combine(knowledgeRootPath, scopeDirectory);
        if (Directory.Exists(scopePath))
        {
            foreach (var directory in Directory.EnumerateDirectories(scopePath, "*", SearchOption.TopDirectoryOnly))
            {
                names.Add(Path.GetFileName(directory));
            }
        }

        foreach (var id in documents.Select(document => document.Id).Concat(indexedNodes.Select(node => node.Id)))
        {
            var name = ExtractScopeName(id, scopeDirectory);
            if (name is not null)
            {
                names.Add(name);
            }
        }

        return names.Order(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string? ExtractScopeName(string id, string scopeDirectory)
    {
        var prefix = $"{scopeDirectory}/";
        if (!id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var remainder = id[prefix.Length..];
        var separatorIndex = remainder.IndexOf('/');
        return separatorIndex <= 0 ? null : remainder[..separatorIndex];
    }

    private static SnapshotFreshness? FindLatestSnapshot(
        IReadOnlyCollection<KnowledgeMarkdownDocument> projectDocuments)
    {
        return projectDocuments
            .Where(document => document.Type == KnowledgeNodeType.Snapshot)
            .Select(document => TryReadSnapshotFreshness(document, out var snapshot) ? snapshot : null)
            .OfType<SnapshotFreshness>()
            .OrderByDescending(snapshot => snapshot.Date)
            .FirstOrDefault();
    }

    private static bool TryReadSnapshotFreshness(
        KnowledgeMarkdownDocument document,
        out SnapshotFreshness? snapshot)
    {
        var fileName = Path.GetFileName(document.Path);
        if (fileName.Length < 10
            || !DateOnly.TryParseExact(
                fileName[..10],
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            snapshot = null;
            return false;
        }

        document.Frontmatter.TryGetValue("origin", out var origin);
        snapshot = new SnapshotFreshness(
            document,
            date,
            string.IsNullOrWhiteSpace(origin) ? null : origin);
        return true;
    }

    public const string KnowledgeReviewPromptPath = "skills/nero/prompts/knowledge-review-app-mcp.txt";

    private static void AddSnapshotFreshnessIssue(
        ICollection<AdminProjectHealthIssue> issues,
        string project,
        string? primaryDomain,
        string projectDirectoryPath,
        SnapshotFreshness? latestSnapshot,
        int? latestSnapshotAgeDays,
        int recentSnapshotDays)
    {
        if (latestSnapshot is null)
        {
            issues.Add(new AdminProjectHealthIssue
            {
                Type = "MissingRecentSnapshot",
                Path = Path.Combine(projectDirectoryPath, "snapshots"),
                Recommendation = BuildKnowledgeReviewRecommendation(
                    project,
                    primaryDomain,
                    $"hasRecentSnapshot=false; no dated snapshot within the {recentSnapshotDays}-day freshness window.")
            });
            return;
        }

        if (latestSnapshotAgeDays > recentSnapshotDays)
        {
            issues.Add(new AdminProjectHealthIssue
            {
                Type = "StaleSnapshot",
                Path = latestSnapshot.Document.Path,
                Recommendation = BuildKnowledgeReviewRecommendation(
                    project,
                    primaryDomain,
                    $"hasRecentSnapshot=false; latest dated snapshot is {latestSnapshotAgeDays} days old (threshold: {recentSnapshotDays}).")
            });
        }
    }

    private static string BuildKnowledgeReviewRecommendation(
        string project,
        string? primaryDomain,
        string reason)
    {
        var domainPart = string.IsNullOrWhiteSpace(primaryDomain)
            ? "primaryDomain=<api|front|mobile|integracoes>"
            : $"primaryDomain={primaryDomain.Trim()}";

        return $"Run knowledge review with {KnowledgeReviewPromptPath} for project {project} ({domainPart}). Reason: {reason}";
    }

    private static string RecommendProjectHealthAction(
        IReadOnlyCollection<AdminProjectHealthIssue> issues,
        bool existsInFilesystem,
        bool existsInIndex)
    {
        if (issues.Count == 0)
        {
            return "Project knowledge structure is healthy.";
        }

        if (!existsInFilesystem && !existsInIndex)
        {
            return "Register the project with nero_register_project.";
        }

        return issues.First().Recommendation;
    }

    private static string ResolveMarkdownFilePath(string knowledgeRootPath, string knowledgePath)
    {
        var relativePath = knowledgePath.StartsWith("knowledge/", StringComparison.OrdinalIgnoreCase)
            ? knowledgePath["knowledge/".Length..]
            : knowledgePath;

        return Path.Combine(
            KnowledgeRootOptions.ResolveKnowledgeRootPath(knowledgeRootPath),
            relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static bool TryParseSqliteUtc(string value, out DateTime utc)
    {
        if (DateTime.TryParse(
                value,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            utc = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            return true;
        }

        utc = default;
        return false;
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static List<string> ValidateRequiredStructure(string knowledgeRootPath)
    {
        var errors = new List<string>();
        if (!Directory.Exists(knowledgeRootPath))
        {
            errors.Add($"Knowledge root '{knowledgeRootPath}' does not exist.");
            return errors;
        }

        foreach (var requiredDirectory in new[] { "domains", "global", "projects" })
        {
            var path = Path.Combine(knowledgeRootPath, requiredDirectory);
            if (!Directory.Exists(path))
            {
                errors.Add($"Required knowledge directory '{requiredDirectory}' was not found.");
            }
        }

        return errors;
    }

    private static IEnumerable<string> ValidateFrontmatter(IReadOnlyCollection<KnowledgeMarkdownDocument> documents)
    {
        foreach (var document in documents.Where(document => document.Frontmatter.Count > 0))
        {
            if (!document.Frontmatter.ContainsKey("type"))
            {
                yield return $"Markdown '{document.Id}' has frontmatter but is missing 'type'.";
            }

            if (!document.Frontmatter.TryGetValue("scope", out var scope) || string.IsNullOrWhiteSpace(scope))
            {
                yield return $"Markdown '{document.Id}' has frontmatter but is missing 'scope'.";
                continue;
            }

            if (scope.Equals("domain", StringComparison.OrdinalIgnoreCase)
                && !document.Frontmatter.ContainsKey("domain"))
            {
                yield return $"Markdown '{document.Id}' has domain scope but is missing 'domain'.";
            }

            if (scope.Equals("project", StringComparison.OrdinalIgnoreCase)
                && !document.Frontmatter.ContainsKey("project"))
            {
                yield return $"Markdown '{document.Id}' has project scope but is missing 'project'.";
            }
        }
    }

    private sealed record IndexedNodeRow(string Id, string Path, string UpdatedUtc);

    private sealed record SnapshotFreshness(
        KnowledgeMarkdownDocument Document,
        DateOnly Date,
        string? Origin);
}
