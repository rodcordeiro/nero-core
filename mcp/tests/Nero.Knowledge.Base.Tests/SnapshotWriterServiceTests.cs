using Nero.Knowledge.Base.Mcp.Application.Contracts.Snapshots;
using Nero.Knowledge.Base.Mcp.Application.Services.Snapshots;
using Nero.Knowledge.Base.Mcp.Domain;

namespace Nero.Knowledge.Base.Tests;

public class SnapshotWriterServiceTests
{
    [Theory]
    [InlineData(KnowledgeScope.Global, null, null, "global/snapshots/")]
    [InlineData(KnowledgeScope.Domain, "api", null, "domains/api/snapshots/")]
    [InlineData(KnowledgeScope.Project, null, "Acme.Api", "projects/Acme.Api/snapshots/")]
    public async Task WriteAsync_ResolvesPathByScope(
        KnowledgeScope scope,
        string? domain,
        string? project,
        string expectedRelativePathPrefix)
    {
        var root = CreateTempKnowledgeRoot();
        var request = CreateRequest(scope, domain, project);

        var result = await new SnapshotWriterService().WriteAsync(root, request);

        Assert.StartsWith(expectedRelativePathPrefix, result.RelativePath, StringComparison.Ordinal);
        Assert.EndsWith("-snapshot-de-rotas.md", result.RelativePath, StringComparison.Ordinal);
        Assert.True(File.Exists(result.Path));
        var markdown = await File.ReadAllTextAsync(result.Path);
        Assert.Contains("type: snapshot", markdown);
        Assert.Contains("# Snapshot de rotas", markdown);
        Assert.Contains("## Contexto", markdown);
        Assert.Contains("## Evidencia", markdown);
        Assert.Contains("type: documents", markdown);
        Assert.Contains("type: evidences", markdown);
        Assert.Contains($"- Revisar ate: {DateOnly.FromDateTime(DateTime.UtcNow.AddDays(90)):yyyy-MM-dd}", markdown);
    }

    [Fact]
    public async Task WriteAsync_PreventsOverwrite()
    {
        var root = CreateTempKnowledgeRoot();
        var service = new SnapshotWriterService();
        var request = CreateRequest(KnowledgeScope.Domain, domain: "api");
        await service.WriteAsync(root, request);

        var exception = await Assert.ThrowsAsync<IOException>(() => service.WriteAsync(root, request));

        Assert.Equal(false, exception.Data["NeroKnowledgeMarkdownWritten"]);
        Assert.NotEqual("n/a", exception.Data["NeroKnowledgeTargetPath"]);
    }

    [Theory]
    [InlineData(nameof(RegisterSnapshotRequest.Context))]
    [InlineData(nameof(RegisterSnapshotRequest.Evidence))]
    [InlineData(nameof(RegisterSnapshotRequest.Origin))]
    public async Task WriteAsync_ValidatesRequiredFields(string field)
    {
        var root = CreateTempKnowledgeRoot();
        var request = field switch
        {
            nameof(RegisterSnapshotRequest.Context) => CreateRequest(KnowledgeScope.Global) with { Context = " " },
            nameof(RegisterSnapshotRequest.Evidence) => CreateRequest(KnowledgeScope.Global) with { Evidence = " " },
            nameof(RegisterSnapshotRequest.Origin) => CreateRequest(KnowledgeScope.Global) with { Origin = " " },
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unsupported test field.")
        };

        await Assert.ThrowsAnyAsync<ArgumentException>(() => new SnapshotWriterService().WriteAsync(root, request));
    }

    [Fact]
    public async Task WriteAsync_BlocksPathTraversalInContextSegment()
    {
        var root = CreateTempKnowledgeRoot();
        var request = CreateRequest(KnowledgeScope.Project, project: "../Acme.Api");

        await Assert.ThrowsAsync<ArgumentException>(() => new SnapshotWriterService().WriteAsync(root, request));
    }

    [Theory]
    [InlineData("domains/api/patterns")]
    [InlineData("projects/Acme.Api/decisions")]
    [InlineData("domains/api/index")]
    public async Task WriteAsync_RejectsEvidencesTargetingHub(string hubTarget)
    {
        var root = CreateTempKnowledgeRoot();
        var request = CreateRequest(KnowledgeScope.Project, project: "Acme.Api") with
        {
            Evidences = [hubTarget]
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => new SnapshotWriterService().WriteAsync(root, request));

        Assert.Equal(nameof(RegisterSnapshotRequest.Evidences), exception.ParamName);
        Assert.Contains("directory hub", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(hubTarget, exception.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task WriteAsync_LargeSnapshotBatch_WritesEveryFile()
    {
        const int snapshotCount = 8;
        var root = CreateTempKnowledgeRoot();
        var service = new SnapshotWriterService();
        var evidence = string.Concat(Enumerable.Repeat("Evidencia operacional para lote estavel. ", 512)).Trim();

        for (var index = 1; index <= snapshotCount; index++)
        {
            var request = CreateRequest(KnowledgeScope.Project, project: "Acme.Api") with
            {
                Title = $"Snapshot grande {index:D2}",
                Evidence = evidence
            };
            var result = await service.WriteAsync(root, request);
            Assert.True(File.Exists(result.Path));
        }

        Assert.Equal(snapshotCount, Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories).Count());
    }

    [Theory]
    [InlineData(nameof(RegisterSnapshotRequest.Context))]
    [InlineData(nameof(RegisterSnapshotRequest.Evidence))]
    public async Task WriteAsync_RejectsLongFieldsOver64KiBBeforeWriting(string field)
    {
        var root = CreateTempKnowledgeRoot();
        var oversizedValue = new string('x', SnapshotWriterService.MaximumLongFieldSizeBytes + 1);
        var request = field == nameof(RegisterSnapshotRequest.Context)
            ? CreateRequest(KnowledgeScope.Global) with { Context = oversizedValue }
            : CreateRequest(KnowledgeScope.Global) with { Evidence = oversizedValue };

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => new SnapshotWriterService().WriteAsync(root, request));

        Assert.Equal(field, exception.ParamName);
        Assert.Contains("64 KiB", exception.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories));
    }

    private static RegisterSnapshotRequest CreateRequest(
        KnowledgeScope scope,
        string? domain = null,
        string? project = null)
    {
        return new RegisterSnapshotRequest
        {
            Title = "Snapshot de rotas",
            Scope = scope,
            Domain = domain,
            Project = project,
            Context = "Inventario tecnico das rotas publicas revisadas.",
            Evidence = "Arquivos de controller e contratos analisados no checkout local.",
            Origin = "Teste automatizado",
            RelatesTo = ["projects/Acme.Api/index"],
            Evidences = ["domains/api/patterns/http-versioning"]
        };
    }

    private static string CreateTempKnowledgeRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "Nero.Knowledge.Base.Tests", Guid.NewGuid().ToString("N"), "knowledge");
        Directory.CreateDirectory(path);
        return path;
    }
}
