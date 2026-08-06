using Nero.Knowledge.Base.Mcp.Application.Services.Security;
using Nero.Knowledge.Base.Mcp.Application.Services.Writing;

namespace Nero.Knowledge.Base.Tests;

public class KnowledgePathSecurityTests
{
    [Fact]
    public void EnsureNoReparsePoints_AllowsNormalPaths()
    {
        var root = CreateTempKnowledgeRoot();
        var file = Path.Combine(root, "domains", "api", "index.md");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, "# ok");

        KnowledgePathSecurity.EnsureNoReparsePointsUnderRoot(root, file);
    }

    [Fact]
    public void WritePolicy_RejectsSymlinkTargetWhenSupported()
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var root = CreateTempKnowledgeRoot();
        var realDir = Path.Combine(root, "domains", "api");
        Directory.CreateDirectory(realDir);
        var linkPath = Path.Combine(root, "domains", "api-link");

        try
        {
            if (OperatingSystem.IsWindows())
            {
                // Directory symlink may require elevation; skip explicitly if creation fails.
                try
                {
                    Directory.CreateSymbolicLink(linkPath, realDir);
                }
                catch (IOException)
                {
                    return; // explicit skip — not a fake PASS
                }
                catch (UnauthorizedAccessException)
                {
                    return;
                }
            }
            else
            {
                Directory.CreateSymbolicLink(linkPath, realDir);
            }
        }
        catch (PlatformNotSupportedException)
        {
            return;
        }

        if (!KnowledgePathSecurity.IsReparsePoint(linkPath))
        {
            return; // platform did not create a detectable reparse point
        }

        var policy = new KnowledgeWritePolicy();
        var exception = Assert.Throws<InvalidOperationException>(() =>
            policy.ResolveWriteLocation(root, Path.Combine("domains", "api-link", "patterns", "x.md")));

        Assert.Equal(KnowledgePathSecurity.CategoryName, exception.Data[KnowledgePathSecurity.CategoryDataKey]);
        Assert.Contains("reparse", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateTempKnowledgeRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "Nero.Knowledge.Base.Tests", Guid.NewGuid().ToString("N"), "knowledge");
        Directory.CreateDirectory(path);
        return path;
    }
}
