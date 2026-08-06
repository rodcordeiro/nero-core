namespace Nero.Knowledge.Base.Mcp.Application.Services.Admin;

public interface IGitCommandRunner
{
    Task<GitCommandResult> RunAsync(
        string repositoryRoot,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null);
}
