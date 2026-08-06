using Nero.Knowledge.Base.Mcp.Application.Services.Security;

namespace Nero.Knowledge.Base.Mcp.Application.Services.Writing;

public sealed class KnowledgeWritePolicy(KnowledgeWriteOptions? options = null)
{
    private readonly KnowledgeWriteOptions options = options ?? new KnowledgeWriteOptions();

    public KnowledgeWriteLocation ResolveWriteLocation(string rootPath, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        var mode = ParseMode(options.Mode);
        if (mode == KnowledgeWriteMode.ReadOnly)
        {
            throw new InvalidOperationException("Knowledge write is blocked because KnowledgeWrite__Mode is read_only.");
        }

        var effectiveRelativePath = mode == KnowledgeWriteMode.Draft
            ? Path.Combine("_drafts", relativePath)
            : relativePath;

        var fullPath = ResolveSafeFullPath(rootPath, effectiveRelativePath);
        KnowledgePathSecurity.EnsureNoReparsePointsUnderRoot(rootPath, fullPath);
        var parent = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(parent))
        {
            KnowledgePathSecurity.EnsureNoReparsePointsUnderRoot(rootPath, parent);
        }

        return new KnowledgeWriteLocation
        {
            FullPath = fullPath,
            RelativePath = effectiveRelativePath.Replace('\\', '/')
        };
    }

    private static KnowledgeWriteMode ParseMode(string? mode)
    {
        var normalized = (mode ?? "direct").Replace("-", "_", StringComparison.Ordinal).Trim();
        return normalized.ToLowerInvariant() switch
        {
            "direct" => KnowledgeWriteMode.Direct,
            "draft" => KnowledgeWriteMode.Draft,
            "read_only" => KnowledgeWriteMode.ReadOnly,
            "readonly" => KnowledgeWriteMode.ReadOnly,
            _ => throw new InvalidOperationException(
                $"Unsupported knowledge write mode '{mode}'. Use direct, draft or read_only.")
        };
    }

    private static string ResolveSafeFullPath(string rootPath, string relativePath)
    {
        var resolvedRoot = Path.GetFullPath(rootPath);
        var fullPath = Path.GetFullPath(Path.Combine(resolvedRoot, relativePath));
        var rootPrefix = resolvedRoot.EndsWith(Path.DirectorySeparatorChar)
            ? resolvedRoot
            : resolvedRoot + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Resolved knowledge write path escapes the knowledge root.");
        }

        return fullPath;
    }

    private enum KnowledgeWriteMode
    {
        Direct,
        Draft,
        ReadOnly
    }
}
