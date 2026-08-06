namespace Nero.Knowledge.Base.Mcp.Infrastructure.Indexing;

public sealed class KnowledgeRootOptions
{
    public const string SectionName = "KnowledgeRoot";

    public string Path { get; init; } = "examples/knowledge-scaffold";

    public string ResolvePath()
    {
        return ResolveKnowledgeRootPath(Path);
    }

    public static string ResolveKnowledgeRootPath(string configuredPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configuredPath);

        return System.IO.Path.IsPathRooted(configuredPath)
            ? System.IO.Path.GetFullPath(configuredPath)
            : System.IO.Path.GetFullPath(System.IO.Path.Combine(Directory.GetCurrentDirectory(), configuredPath));
    }

    public static void ValidateRootExists(string knowledgeRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(knowledgeRootPath);

        var resolvedPath = ResolveKnowledgeRootPath(knowledgeRootPath);
        if (!Directory.Exists(resolvedPath))
        {
            throw new DirectoryNotFoundException(
                $"Knowledge root not found at '{resolvedPath}'. Configure '{SectionName}:Path' or environment variable '{SectionName}__Path' to point to your Knowledge Repo root (see examples/knowledge-scaffold).");
        }
    }
}
