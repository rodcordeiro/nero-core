using System.Text;

namespace Nero.Knowledge.Base.Mcp.Application.Services.Writing;

internal static class KnowledgeMarkdownFileWriter
{
    public static async Task WriteNewAsync(
        string fullPath,
        string markdown,
        CancellationToken cancellationToken)
    {
        var markdownWritten = false;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await using var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            await writer.WriteAsync(markdown.AsMemory(), cancellationToken);
            await writer.FlushAsync(cancellationToken);
            markdownWritten = true;
        }
        catch (Exception exception)
        {
            WriteFailureMetadata.Attach(exception, fullPath, markdownWritten);
            throw;
        }
    }

    /// <summary>
    /// Overwrites an existing file or creates it when missing (upsert).
    /// </summary>
    public static async Task WriteReplaceAsync(
        string fullPath,
        string markdown,
        CancellationToken cancellationToken)
    {
        var markdownWritten = false;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await using var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            await writer.WriteAsync(markdown.AsMemory(), cancellationToken);
            await writer.FlushAsync(cancellationToken);
            markdownWritten = true;
        }
        catch (Exception exception)
        {
            WriteFailureMetadata.Attach(exception, fullPath, markdownWritten);
            throw;
        }
    }
}
