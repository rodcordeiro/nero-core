using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Nero.Knowledge.Base.Mcp.Application.Contracts.Admin;
using Nero.Knowledge.Base.Mcp.Application.Services.Admin;

namespace Nero.Knowledge.Base.Mcp.Presentation.Mcp.Tools;

[McpServerToolType]
public sealed class NeroAdminTools(
    AdminStatusService adminStatusService,
    AdminGitService adminGitService,
    AdminKnowledgeMaintenanceService adminKnowledgeMaintenanceService,
    ILogger<NeroAdminTools>? logger = null)
{
    [McpServerTool]
    [Description("Returns local administrative status for the Nero knowledge MCP server, including git branch, modified files, SQLite index state and write mode.")]
    public async Task<NeroAdminStatusToolResult> nero_admin_status(CancellationToken cancellationToken = default)
    {
        return await ExecuteAdminAsync("nero_admin_status", async () =>
        {
            var result = await adminStatusService.GetStatusAsync(cancellationToken);

            return new NeroAdminStatusToolResult
            {
                Server = result.Server, RepositoryRoot = result.RepositoryRoot, Branch = result.Branch,
                HasModifiedFiles = result.HasModifiedFiles, ModifiedFiles = result.ModifiedFiles,
                IndexDatabaseExists = result.IndexDatabaseExists, IndexDatabasePath = result.IndexDatabasePath,
                LastIndexedUtc = result.LastIndexedUtc, WriteMode = result.WriteMode
            };
        });
    }

    [McpServerTool]
    [Description("Validates Nero knowledge structure/semantics (IsValid) and independently scans for active P0 compliance hits (IsCompliant). Readiness requires both true.")]
    public async Task<NeroAdminValidationToolResult> nero_admin_validate(CancellationToken cancellationToken = default)
    {
        return await ExecuteAdminAsync("nero_admin_validate", async () =>
        {
            var result = await adminKnowledgeMaintenanceService.ValidateAsync(cancellationToken);
            var actionable = result.Errors
                .Concat(result.ComplianceGaps.Select(gap => $"compliance: {gap}"))
                .ToArray();
            return new NeroAdminValidationToolResult
            {
                IsValid = result.IsValid,
                IsCompliant = result.IsCompliant,
                NodeCount = result.NodeCount,
                EdgeCount = result.EdgeCount,
                Errors = result.Errors,
                ComplianceGaps = result.ComplianceGaps,
                ActionableGaps = actionable,
                Recommendation = KnowledgeBatchHints.RecommendValidation(result.IsValid, result.IsCompliant)
            };
        });
    }

    [McpServerTool]
    [Description("Scans all Markdown under the Nero knowledge root for secrets/PII using the versioned compliance taxonomy. Returns masked hits only; does not rewrite files or commit. Quarantined notes appear under quarantinedHits and do not fail IsCompliant.")]
    public async Task<NeroAdminComplianceScanToolResult> nero_admin_compliance_scan(CancellationToken cancellationToken = default)
    {
        return await ExecuteAdminAsync("nero_admin_compliance_scan", async () =>
        {
            var result = await adminKnowledgeMaintenanceService.ScanComplianceAsync(cancellationToken);
            return new NeroAdminComplianceScanToolResult
            {
                IsCompliant = result.IsCompliant,
                TaxonomyVersion = result.TaxonomyVersion,
                ScannedFileCount = result.ScannedFileCount,
                ActiveBlockingHitCount = result.ActiveBlockingHitCount,
                QuarantinedBlockingHitCount = result.QuarantinedBlockingHitCount,
                WarningHitCount = result.WarningHitCount,
                ActiveHits = result.ActiveHits.Select(MapComplianceIssue).ToArray(),
                QuarantinedHits = result.QuarantinedHits.Select(MapComplianceIssue).ToArray(),
                Warnings = result.Warnings.Select(MapComplianceIssue).ToArray(),
                Recommendation = result.IsCompliant
                    ? KnowledgeBatchHints.CompliantRecommendation
                    : KnowledgeBatchHints.NonCompliantRecommendation
            };
        });
    }

    private static NeroAdminComplianceScanIssueToolResult MapComplianceIssue(AdminComplianceScanIssue issue) =>
        new()
        {
            Path = issue.Path,
            RuleId = issue.RuleId,
            Severity = issue.Severity,
            Line = issue.Line,
            MaskedExcerpt = issue.MaskedExcerpt,
            Quarantined = issue.Quarantined,
            ComplianceReason = issue.ComplianceReason
        };

    [McpServerTool]
    [Description("Reindexes the Nero knowledge Markdown tree into the configured SQLite index and returns the indexed node count.")]
    public async Task<NeroAdminReindexToolResult> nero_admin_reindex(CancellationToken cancellationToken = default)
    {
        return await ExecuteAdminAsync("nero_admin_reindex", async () =>
        {
            var result = await adminKnowledgeMaintenanceService.ReindexAsync(cancellationToken);
            return new NeroAdminReindexToolResult
            {
                IndexedNodes = result.IndexedNodes, KnowledgeRootPath = result.KnowledgeRootPath,
                IndexDatabasePath = result.IndexDatabasePath
            };
        });
    }

    [McpServerTool]
    [Description("Checks consistency between the configured SQLite index and the Markdown files in the Nero knowledge filesystem without reindexing.")]
    public async Task<NeroAdminIndexConsistencyToolResult> nero_admin_check_index_consistency(CancellationToken cancellationToken = default)
    {
        return await ExecuteAdminAsync("nero_admin_check_index_consistency", async () =>
        {
            var result = await adminKnowledgeMaintenanceService.CheckIndexConsistencyAsync(cancellationToken);
            return new NeroAdminIndexConsistencyToolResult
            {
                IsConsistent = result.IsConsistent, KnowledgeRootPath = result.KnowledgeRootPath,
                IndexDatabasePath = result.IndexDatabasePath, IndexedNodeCount = result.IndexedNodeCount,
                MarkdownFileCount = result.MarkdownFileCount, ElapsedMilliseconds = result.ElapsedMilliseconds,
                ThresholdMilliseconds = result.ThresholdMilliseconds, ExceededThreshold = result.ExceededThreshold,
                Issues = result.Issues.Select(issue => new NeroAdminIndexConsistencyIssueToolResult
                {
                    Type = issue.Type, Id = issue.Id, Path = issue.Path,
                    IndexedUpdatedUtc = issue.IndexedUpdatedUtc, FileLastWriteUtc = issue.FileLastWriteUtc,
                    Recommendation = issue.Recommendation
                }).ToList()
            };
        });
    }

    [McpServerTool]
    [Description("Diagnoses whether a Nero project exists in the knowledge filesystem and SQLite index, has base files and belongs_to_domain links, and returns the next recommended action.")]
    public async Task<NeroAdminProjectHealthToolResult> nero_admin_project_health(
        [Description("Project name, for example Acme.Api.")]
        string project,
        [Description("Optional primary domain expected in the belongs_to_domain link, for example api, mobile, front or integracoes.")]
        string? primaryDomain = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAdminAsync("nero_admin_project_health", async () =>
        {
            var result = await adminKnowledgeMaintenanceService.CheckProjectHealthAsync(project, primaryDomain, cancellationToken);
            return new NeroAdminProjectHealthToolResult
            {
                Project = result.Project, PrimaryDomain = result.PrimaryDomain,
                KnowledgeRootPath = result.KnowledgeRootPath, ProjectDirectoryPath = result.ProjectDirectoryPath,
                ExistsInFilesystem = result.ExistsInFilesystem, ExistsInIndex = result.ExistsInIndex,
                HasIndex = result.HasIndex, HasContext = result.HasContext, HasBaseStructure = result.HasBaseStructure,
                HasNotesWithoutBaseStructure = result.HasNotesWithoutBaseStructure,
                HasBelongsToDomain = result.HasBelongsToDomain, LastIndexedUtc = result.LastIndexedUtc,
                RecentSnapshotDays = result.RecentSnapshotDays, HasRecentSnapshot = result.HasRecentSnapshot,
                LatestSnapshotPath = result.LatestSnapshotPath, LatestSnapshotDate = result.LatestSnapshotDate,
                LatestSnapshotAgeDays = result.LatestSnapshotAgeDays, LatestSnapshotOrigin = result.LatestSnapshotOrigin,
                Recommendation = result.Recommendation,
                Issues = result.Issues.Select(issue => new NeroAdminProjectHealthIssueToolResult
                {
                    Type = issue.Type, Path = issue.Path, Recommendation = issue.Recommendation
                }).ToList()
            };
        });
    }

    [McpServerTool]
    [Description("Checks all Nero knowledge projects and domains in one Markdown tree read and one SQLite index query, returning aggregate health counts and details only for scopes with issues.")]
    public async Task<NeroAdminEcosystemHealthToolResult> nero_admin_ecosystem_health(
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAdminAsync("nero_admin_ecosystem_health", async () =>
        {
            var result = await adminKnowledgeMaintenanceService.CheckEcosystemHealthAsync(cancellationToken);
            return new NeroAdminEcosystemHealthToolResult
            {
                KnowledgeRootPath = result.KnowledgeRootPath,
                IndexDatabasePath = result.IndexDatabasePath,
                ProjectCount = result.ProjectCount,
                DomainCount = result.DomainCount,
                HealthyProjectCount = result.HealthyProjectCount,
                ProjectsWithIssuesCount = result.ProjectsWithIssuesCount,
                HealthyDomainCount = result.HealthyDomainCount,
                DomainsWithIssuesCount = result.DomainsWithIssuesCount,
                ElapsedMilliseconds = result.ElapsedMilliseconds,
                ThresholdMilliseconds = result.ThresholdMilliseconds,
                ExceededThreshold = result.ExceededThreshold,
                Recommendation = result.Recommendation,
                ProjectsWithIssues = MapEcosystemScopes(result.ProjectsWithIssues),
                DomainsWithIssues = MapEcosystemScopes(result.DomainsWithIssues)
            };
        });
    }

    [McpServerTool]
    [Description("Returns read-only git synchronization status, including remote presence and local/remote pending commit counts without fetching or merging.")]
    public async Task<NeroAdminGitStatusToolResult> nero_admin_git_status(CancellationToken cancellationToken = default)
    {
        return await ExecuteAdminAsync("nero_admin_git_status", async () =>
        {
            var result = await adminGitService.GetStatusAsync(cancellationToken);
            return new NeroAdminGitStatusToolResult
            {
                RepositoryRoot = result.RepositoryRoot, Branch = result.Branch, HasRemote = result.HasRemote,
                Remote = result.Remote, Upstream = result.Upstream, Ahead = result.Ahead, Behind = result.Behind,
                LocalHead = result.LocalHead, RemoteHead = result.RemoteHead,
                HasModifiedFiles = result.HasModifiedFiles, ModifiedFiles = result.ModifiedFiles
            };
        });
    }

    [McpServerTool]
    [Description("Runs git fetch --prune for the configured remote and returns the result without pull, merge or checkout.")]
    public async Task<NeroAdminGitFetchToolResult> nero_admin_git_fetch(CancellationToken cancellationToken = default)
    {
        return await ExecuteAdminAsync("nero_admin_git_fetch", async () =>
        {
            var result = await adminGitService.FetchAsync(cancellationToken);
            return new NeroAdminGitFetchToolResult
            {
                Success = result.Success, RepositoryRoot = result.RepositoryRoot, Remote = result.Remote,
                Message = result.Message, Output = result.Output, Error = result.Error
            };
        });
    }

    [McpServerTool]
    [Description("Fast-forward-only git pull for the resolved remote/branch. Blocked when KnowledgeWrite__Mode=read_only, when the worktree is dirty (including untracked), or when histories diverge. Never merges or rebases.")]
    public async Task<NeroAdminGitPullToolResult> nero_admin_git_pull(
        [Description("Optional remote name. Defaults to preferred remote (origin when present).")]
        string? remote = null,
        [Description("Optional branch name. Defaults to the current branch.")]
        string? branch = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAdminAsync("nero_admin_git_pull", async () =>
        {
            var result = await adminGitService.PullAsync(remote, branch, cancellationToken);
            return new NeroAdminGitPullToolResult
            {
                Success = result.Success,
                RepositoryRoot = result.RepositoryRoot,
                Remote = result.Remote,
                Branch = result.Branch,
                Message = result.Message,
                Output = result.Output,
                Error = result.Error
            };
        });
    }

    [McpServerTool]
    [Description("Creates a git commit for explicit allowlisted paths only (global/**, domains/**, projects/**, data/** relative to the Knowledge Repo). Requires a clean index, stages exactly paths[], scans the staged diff for any compliance Blocking/Warning hit, and never uses --no-verify/amend/force. Blocked when KnowledgeWrite__Mode=read_only.")]
    public async Task<NeroAdminGitCreateCommitToolResult> nero_admin_create_commit(
        [Description("Commit message.")]
        string message,
        [Description("Repo-relative paths to stage and commit. Must be inside the hard allowlist.")]
        string[] paths,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAdminAsync("nero_admin_create_commit", async () =>
        {
            var result = await adminGitService.CreateCommitAsync(message, paths, cancellationToken);
            return new NeroAdminGitCreateCommitToolResult
            {
                Success = result.Success,
                RepositoryRoot = result.RepositoryRoot,
                CommitSha = result.CommitSha,
                Paths = result.Paths,
                Message = result.Message,
                Output = result.Output,
                Error = result.Error
            };
        });
    }

    [McpServerTool]
    [Description("Pushes the resolved remote/branch without force. Requires confirm=true and confirmPhrase exactly 'PUSH <remote> <branch>' for the resolved target. Uses environment/SSH credentials only. Blocked when KnowledgeWrite__Mode=read_only.")]
    public async Task<NeroAdminGitPushToolResult> nero_admin_git_push(
        [Description("Must be true to authorize the push.")]
        bool confirm,
        [Description("Exact confirmation phrase: PUSH <remote> <branch> using the resolved remote and branch names.")]
        string confirmPhrase,
        [Description("Optional remote name. Defaults to preferred remote (origin when present).")]
        string? remote = null,
        [Description("Optional branch name. Defaults to the current branch.")]
        string? branch = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAdminAsync("nero_admin_git_push", async () =>
        {
            var result = await adminGitService.PushAsync(confirm, confirmPhrase, remote, branch, cancellationToken);
            return new NeroAdminGitPushToolResult
            {
                Success = result.Success,
                RepositoryRoot = result.RepositoryRoot,
                Remote = result.Remote,
                Branch = result.Branch,
                Message = result.Message,
                Output = result.Output,
                Error = result.Error
            };
        });
    }

    private async Task<T> ExecuteAdminAsync<T>(string toolName, Func<Task<T>> operation)
    {
        var startedTimestamp = Stopwatch.GetTimestamp();
        try
        {
            return await operation();
        }
        catch (Exception exception) when (ToolFailureDiagnostics.IsActionableWriteException(exception))
        {
            ToolFailureDiagnostics.LogFailure(logger, toolName, exception, startedTimestamp);
            throw ToolFailureDiagnostics.CreateActionableWriteException(toolName, exception);
        }
        catch (Exception exception)
        {
            ToolFailureDiagnostics.LogFailure(logger, toolName, exception, startedTimestamp);
            throw;
        }
    }

    private static IReadOnlyList<NeroAdminEcosystemScopeHealthToolResult> MapEcosystemScopes(
        IReadOnlyCollection<AdminEcosystemScopeHealthResult> scopes)
    {
        return scopes.Select(scope => new NeroAdminEcosystemScopeHealthToolResult
        {
            Name = scope.Name,
            Issues = scope.Issues.Select(issue => new NeroAdminProjectHealthIssueToolResult
            {
                Type = issue.Type,
                Path = issue.Path,
                Recommendation = issue.Recommendation
            }).ToList()
        }).ToList();
    }
}
