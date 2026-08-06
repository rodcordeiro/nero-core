using Nero.Knowledge.Base.Mcp.Application.Services.Security;

namespace Nero.Knowledge.Base.Mcp.Infrastructure.Indexing;

public sealed class KnowledgeMarkdownReader(KnowledgeMarkdownParser? parser = null)
{
    private readonly KnowledgeMarkdownParser parser = parser ?? new KnowledgeMarkdownParser();

    /// <summary>
    /// Reads all Markdown notes below the knowledge root and parses them in deterministic path order.
    /// Skips paths that contain symlink/junction/reparse points (Marco 24) without reading content.
    /// </summary>
    public async Task<IReadOnlyList<KnowledgeMarkdownDocument>> ReadAsync(
        string knowledgeRootPath,
        CancellationToken cancellationToken = default)
    {
        KnowledgeRootOptions.ValidateRootExists(knowledgeRootPath);
        var resolvedKnowledgeRootPath = KnowledgeRootOptions.ResolveKnowledgeRootPath(knowledgeRootPath);

        var documents = new List<KnowledgeMarkdownDocument>();
        foreach (var markdownPath in Directory.EnumerateFiles(resolvedKnowledgeRootPath, "*.md", SearchOption.AllDirectories)
                     .Order(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                KnowledgePathSecurity.EnsureNoReparsePointsUnderRoot(resolvedKnowledgeRootPath, markdownPath);
            }
            catch (InvalidOperationException)
            {
                // Fail-closed indexer: skip unsafe paths; do not load content through aliases.
                continue;
            }

            var markdown = await File.ReadAllTextAsync(markdownPath, cancellationToken);
            documents.Add(parser.Parse(resolvedKnowledgeRootPath, markdownPath, markdown));
        }

        return documents;
    }
}
