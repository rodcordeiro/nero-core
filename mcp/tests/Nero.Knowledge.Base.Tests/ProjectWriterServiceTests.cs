using Nero.Knowledge.Base.Mcp.Application.Contracts.Projects;
using Nero.Knowledge.Base.Mcp.Application.Services.Projects;

namespace Nero.Knowledge.Base.Tests;

public class ProjectWriterServiceTests
{
    [Fact]
    public async Task WriteAsync_WhenContextWriteFailsAfterIndex_ReportsPartialWriteMetadata()
    {
        var root = CreateTempKnowledgeRoot();
        var projectDirectory = Path.Combine(root, "projects", "Acme.Metadata.Api");
        var indexPath = Path.Combine(projectDirectory, "index.md");
        var contextPath = Path.Combine(projectDirectory, "context.md");
        Directory.CreateDirectory(Path.Combine(root, "domains", "api"));
        await File.WriteAllTextAsync(
            Path.Combine(root, "domains", "api", "index.md"),
            "---\ntype: domain_index\nscope: domain\ndomain: api\nstatus: active\n---\n# api\n");
        Directory.CreateDirectory(contextPath);

        var exception = await Assert.ThrowsAnyAsync<Exception>(
            () => new ProjectWriterService().WriteAsync(
                root,
                new RegisterProjectRequest
                {
                    Project = "Acme.Metadata.Api",
                    Domain = "api",
                    Purpose = "Validar metadata de escrita parcial.",
                    Origin = "ProjectWriterServiceTests"
                }));

        Assert.True(File.Exists(indexPath));
        Assert.Equal(contextPath, exception.Data["NeroKnowledgeTargetPath"]);
        Assert.Equal(true, exception.Data["NeroKnowledgeMarkdownWritten"]);
        Assert.Equal([indexPath], Assert.IsType<string[]>(exception.Data["NeroKnowledgeWrittenPaths"]));
    }

    private static string CreateTempKnowledgeRoot()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "Nero.Knowledge.Base.Tests",
            Guid.NewGuid().ToString("N"),
            "knowledge");
        Directory.CreateDirectory(path);
        return path;
    }
}
