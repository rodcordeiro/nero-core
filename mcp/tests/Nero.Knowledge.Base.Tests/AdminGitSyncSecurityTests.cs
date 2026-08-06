using Nero.Knowledge.Base.Mcp.Application.Services.Admin;
using Nero.Knowledge.Base.Mcp.Application.Services.Security;

namespace Nero.Knowledge.Base.Tests;

public class AdminGitSyncSecurityTests
{
    [Theory]
    [InlineData("global/index.md")]
    [InlineData("data/nero-knowledge.db")]
    [InlineData("projects/Acme.Api/index.md")]
    [InlineData(@"domains\api\index.md")]
    public void NormalizeAndValidateAllowlistedPaths_AcceptsAllowlistedPaths(string path)
    {
        var normalized = GitSyncSecurity.NormalizeAndValidateAllowlistedPaths([path]);

        Assert.Single(normalized);
        Assert.DoesNotContain('\\', normalized[0]);
    }

    [Theory]
    [InlineData("../secrets.env")]
    [InlineData("/etc/passwd")]
    [InlineData("C:/Windows/system32")]
    [InlineData("mcp/src/Program.cs")]
    [InlineData("skills/nero/SKILL.md")]
    public void NormalizeAndValidateAllowlistedPaths_RejectsUnsafePaths(string path)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            GitSyncSecurity.NormalizeAndValidateAllowlistedPaths([path]));

        Assert.Equal(KnowledgePathSecurity.CategoryName, exception.Data[KnowledgePathSecurity.CategoryDataKey]);
    }

    [Fact]
    public void EnsureSafeGitArguments_RejectsForceAndRebase()
    {
        Assert.Throws<InvalidOperationException>(() =>
            GitSyncSecurity.EnsureSafeGitArguments(["push", "--force", "origin", "main"]));
        Assert.Throws<InvalidOperationException>(() =>
            GitSyncSecurity.EnsureSafeGitArguments(["push", "--force-with-lease", "origin", "main"]));
        Assert.Throws<InvalidOperationException>(() =>
            GitSyncSecurity.EnsureSafeGitArguments(["commit", "--no-verify", "-m", "x"]));
        Assert.Throws<InvalidOperationException>(() =>
            GitSyncSecurity.EnsureSafeGitArguments(["commit", "--amend", "-m", "x"]));
        Assert.Throws<InvalidOperationException>(() =>
            GitSyncSecurity.EnsureSafeGitArguments(["rebase", "origin/main"]));
    }

    [Fact]
    public void EnsureSafeGitArguments_AllowsControlledSyncShape()
    {
        GitSyncSecurity.EnsureSafeGitArguments(["pull", "--ff-only", "origin", "main"]);
        GitSyncSecurity.EnsureSafeGitArguments(["push", "origin", "main"]);
        GitSyncSecurity.EnsureSafeGitArguments(["commit", "-m", "message"]);
        GitSyncSecurity.EnsureSafeGitArguments(["add", "--", "global/a.md"]);
    }

    [Fact]
    public void RejectSecretParamNames_RejectsKnownCredentialKeys()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            GitSyncSecurity.RejectSecretParamNames(new Dictionary<string, string?>
            {
                ["token"] = "should-not-matter"
            }));

        Assert.Equal(KnowledgePathSecurity.CategoryName, exception.Data[KnowledgePathSecurity.CategoryDataKey]);
    }

    [Fact]
    public void EnsureSafeRefName_RejectsForceRefspecAndRetarget()
    {
        Assert.Throws<InvalidOperationException>(() => GitSyncSecurity.EnsureSafeRefName("+main", "branch"));
        Assert.Throws<InvalidOperationException>(() => GitSyncSecurity.EnsureSafeRefName("main:production", "branch"));
        Assert.Throws<InvalidOperationException>(() => GitSyncSecurity.EnsureSafeRefName("HEAD^{}", "branch"));
        Assert.Throws<InvalidOperationException>(() => GitSyncSecurity.EnsureSafeRefName("-main", "branch"));
        Assert.Throws<InvalidOperationException>(() => GitSyncSecurity.EnsureSafeRefName("foo/../bar", "branch"));
        GitSyncSecurity.EnsureSafeRefName("feature/x", "branch");
        GitSyncSecurity.EnsureSafeRefName("origin", "remote");
    }

    [Fact]
    public void SanitizeGitText_StripsUrlUserInfo()
    {
        var withPassword = GitSyncSecurity.SanitizeGitText(
            "fatal: could not read from https://user:secret@github.com/org/repo.git");
        Assert.DoesNotContain("secret", withPassword, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]@", withPassword, StringComparison.Ordinal);
        Assert.Contains("github.com/org/repo.git", withPassword, StringComparison.Ordinal);

        var tokenOnly = GitSyncSecurity.SanitizeGitText(
            "fatal: Authentication failed for 'https://oauth-fixture-token@github.com/org/repo.git'");
        Assert.DoesNotContain("oauth-fixture-token", tokenOnly, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]@", tokenOnly, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildPushConfirmPhrase_UsesExactResolvedNames()
    {
        Assert.Equal("PUSH origin main", GitSyncSecurity.BuildPushConfirmPhrase("origin", "main"));
        Assert.Equal("PUSH upstream feature/x", GitSyncSecurity.BuildPushConfirmPhrase("upstream", "feature/x"));
    }

    [Fact]
    public void EnsurePushConfirmation_RequiresExactPhrase()
    {
        GitSyncSecurity.EnsurePushConfirmation(true, "PUSH origin main", "origin", "main");

        Assert.Throws<InvalidOperationException>(() =>
            GitSyncSecurity.EnsurePushConfirmation(false, "PUSH origin main", "origin", "main"));
        Assert.Throws<InvalidOperationException>(() =>
            GitSyncSecurity.EnsurePushConfirmation(true, "PUSH origin MAIN", "origin", "main"));
    }
}
