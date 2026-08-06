namespace Nero.Knowledge.Base.Mcp.Application.Services.Admin;

public sealed record GitCommandResult(int ExitCode, string Output, string Error);
