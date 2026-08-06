namespace Nero.Knowledge.Base.Mcp.Application.Services.Writing;

internal static class WriteFailureMetadata
{
    public const string TargetPathDataKey = "NeroKnowledgeTargetPath";
    public const string MarkdownWrittenDataKey = "NeroKnowledgeMarkdownWritten";
    public const string WrittenPathsDataKey = "NeroKnowledgeWrittenPaths";

    public static void Attach(
        Exception exception,
        string targetPath,
        bool markdownWritten,
        IReadOnlyCollection<string>? writtenPaths = null)
    {
        exception.Data[TargetPathDataKey] = targetPath;
        exception.Data[MarkdownWrittenDataKey] = markdownWritten;
        exception.Data[WrittenPathsDataKey] = writtenPaths?.ToArray()
            ?? (markdownWritten ? new[] { targetPath } : Array.Empty<string>());
    }
}
