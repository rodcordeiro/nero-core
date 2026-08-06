namespace Nero.Knowledge.Base.Mcp.Presentation.Mcp.Tools;

public sealed record NeroSupersededDecisionToolResult
{
    public required NeroProjectContextSectionToolResult Decision { get; init; }

    public required IReadOnlyList<NeroProjectContextSectionToolResult> SupersededBy { get; init; }
}
