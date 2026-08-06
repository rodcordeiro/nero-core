namespace Nero.Knowledge.Base.Mcp.Presentation.Mcp.Tools;

public sealed record NeroGetProjectContextToolResult
{
    public required string Project { get; init; }

    public bool Exists { get; init; }

    public NeroProjectContextSectionToolResult? Index { get; init; }

    public NeroProjectContextSectionToolResult? Context { get; init; }

    public NeroProjectContextSectionToolResult? Patterns { get; init; }

    public NeroProjectContextSectionToolResult? BusinessRules { get; init; }

    public IReadOnlyList<NeroProjectContextSectionToolResult> Decisions { get; init; } = [];

    public IReadOnlyList<NeroProjectContextSectionToolResult> ActiveDecisions { get; init; } = [];

    public IReadOnlyList<NeroSupersededDecisionToolResult> SupersededDecisions { get; init; } = [];

    public bool HasSupersededDecisions { get; init; }

    public string? Recommendation { get; init; }

    public IReadOnlyList<NeroProjectContextSectionToolResult> Troubleshooting { get; init; } = [];
}
