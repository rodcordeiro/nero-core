using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Nero.Knowledge.Base.Mcp.Application.Services.Compliance;
using Nero.Knowledge.Base.Mcp.Application.Services.Security;
using Nero.Knowledge.Base.Mcp.Application.Services.Writing;

namespace Nero.Knowledge.Base.Mcp.Presentation.Mcp.Tools;

internal static class ToolFailureDiagnostics
{
    private const int MaxReasonLength = 240;

    public static bool IsActionableWriteException(Exception exception) =>
        exception is ArgumentException
            or IOException
            or InvalidOperationException
            or SqliteException
            or UnauthorizedAccessException;

    public static InvalidOperationException CreateActionableWriteException(
        string toolName,
        Exception exception)
    {
        var category = Classify(exception);
        var field = exception is ArgumentException argumentException
            ? argumentException.ParamName ?? "n/a"
            : "n/a";
        var targetPath = exception.Data[WriteFailureMetadata.TargetPathDataKey] as string ?? "n/a";
        var markdownWritten = exception.Data[WriteFailureMetadata.MarkdownWrittenDataKey] as bool? ?? false;
        var writtenPaths = GetWrittenPaths(exception);
        var ruleId = exception.Data[ComplianceViolationException.RuleIdDataKey] as string
            ?? (exception is ComplianceViolationException compliance ? compliance.RuleId : null);
        var ruleSegment = string.IsNullOrWhiteSpace(ruleId) ? string.Empty : $" RuleId: {ruleId}.";
        var reason = SanitizeReason(exception);

        return new InvalidOperationException(
            $"Tool '{toolName}' failed. Category: {category}. Field: {field}.{ruleSegment} Reason: {reason}. "
            + $"TargetPath: {targetPath}. MarkdownWritten: {markdownWritten.ToString().ToLowerInvariant()}. "
            + $"WrittenPaths: {writtenPaths}. Recommendation: {RecommendRecovery(category)}",
            exception);
    }

    public static void LogFailure(
        ILogger? logger,
        string toolName,
        Exception exception,
        long startedTimestamp)
    {
        logger?.LogError(
            "MCP tool failure Tool={Tool} Category={Category} Field={Field} Reason={Reason} "
            + "TargetPath={TargetPath} DurationMilliseconds={DurationMilliseconds} MarkdownWritten={MarkdownWritten} "
            + "WrittenPaths={WrittenPaths}",
            toolName,
            Classify(exception),
            exception is ArgumentException argumentException ? argumentException.ParamName ?? "n/a" : "n/a",
            SanitizeReason(exception),
            exception.Data[WriteFailureMetadata.TargetPathDataKey] as string ?? "n/a",
            Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds,
            exception.Data[WriteFailureMetadata.MarkdownWrittenDataKey] as bool? ?? false,
            GetWrittenPaths(exception));
    }

    private static string SanitizeReason(Exception exception)
    {
        // Never echo long payloads or secret-shaped match text. Prefer RuleId for compliance.
        if (exception is ComplianceViolationException compliance)
        {
            return $"Compliance rule '{compliance.RuleId}' blocked the write.";
        }

        var message = exception.Message ?? string.Empty;
        if (message.Length > MaxReasonLength)
        {
            message = message[..MaxReasonLength] + "...";
        }

        // Strip accidental secret-shaped spans from non-compliance messages.
        return ComplianceReadRedactor.Redact(message, dataClass: ComplianceFrontmatter.DefaultDataClass);
    }

    private static string Classify(Exception exception)
    {
        if (exception is SqliteException sqliteException)
        {
            return sqliteException.SqliteErrorCode is 5 or 6 ? "SqliteBusy" : "Sqlite";
        }

        if (exception is ComplianceViolationException
            || string.Equals(
                exception.Data[ComplianceViolationException.CategoryDataKey] as string,
                ComplianceViolationException.CategoryName,
                StringComparison.Ordinal))
        {
            return ComplianceViolationException.CategoryName;
        }

        if (string.Equals(
                exception.Data[KnowledgePathSecurity.CategoryDataKey] as string,
                KnowledgePathSecurity.CategoryName,
                StringComparison.Ordinal)
            || exception.Message.Contains("reparse point", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("symlink", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("junction", StringComparison.OrdinalIgnoreCase))
        {
            return KnowledgePathSecurity.CategoryName;
        }

        if (exception is ArgumentException) return "InvalidInput";
        if (exception is UnauthorizedAccessException) return "UnauthorizedWrite";
        if (exception is IOException) return "FileWrite";

        var message = exception.Message;
        if (message.Contains("read_only", StringComparison.OrdinalIgnoreCase)) return "ReadOnly";
        if (message.Contains("escapes the knowledge root", StringComparison.OrdinalIgnoreCase)
            || message.Contains("path traversal", StringComparison.OrdinalIgnoreCase)) return "InvalidPath";
        if (message.Contains("Broken knowledge link", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Unsupported knowledge relation", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Invalid depends_on relation", StringComparison.OrdinalIgnoreCase)) return "ReindexOrGraph";
        return "WriteOperation";
    }

    private static string GetWrittenPaths(Exception exception)
    {
        if (exception.Data[WriteFailureMetadata.WrittenPathsDataKey] is not IEnumerable<string> writtenPaths)
        {
            return "none";
        }

        var paths = writtenPaths.ToArray();
        return paths.Length == 0 ? "none" : string.Join(", ", paths);
    }

    private static string RecommendRecovery(string category) => category switch
    {
        "Compliance" => "Remove the sensitive value or replace it with an exact allowlisted placeholder, then retry. Never paste real tokens, keys, cookies or verifiable PII.",
        "Security" => "Use a real filesystem path under the knowledge root without symlinks/junctions, keep Git sync inside the allowlist, and never force-push or bypass hooks. Do not bypass via shell writes.",
        "InvalidInput" => "Review required fields, payload limits and enum values in the tool input.",
        "UnauthorizedWrite" => "Check filesystem permissions for the configured knowledge root.",
        "FileWrite" => "Check whether the calculated target file already exists or is locked, then retry with a different title if needed.",
        "ReadOnly" => "Set KnowledgeWrite__Mode to direct or draft before using write or git pull/commit/push tools.",
        "InvalidPath" => "Use a valid domain/project path segment without traversal, separators or invalid characters.",
        "ReindexOrGraph" => "Validate frontmatter links and run nero_admin_validate or nero_admin_check_index_consistency before retrying.",
        "SqliteBusy" => "Serialize reindex and SQLite reads/writes, wait briefly, then retry after the active operation completes.",
        "Sqlite" => "Inspect the SQLite error and configured database path, then retry after correcting the database condition.",
        _ => "Inspect the reported reason, validate the knowledge tree and retry after correcting the cause."
    };
}
