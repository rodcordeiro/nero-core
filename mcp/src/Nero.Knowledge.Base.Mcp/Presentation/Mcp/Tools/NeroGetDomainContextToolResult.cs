namespace Nero.Knowledge.Base.Mcp.Presentation.Mcp.Tools;

public sealed record NeroGetDomainContextToolResult
{
    public required string Domain { get; init; }

    public bool Exists { get; init; }

    public NeroDomainContextSectionToolResult? Index { get; init; }

    public NeroDomainContextSectionToolResult? Patterns { get; init; }

    public NeroDomainContextSectionToolResult? BusinessRules { get; init; }

    public NeroDomainContextSectionToolResult? ValidationAndTests { get; init; }

    public IReadOnlyList<NeroDomainProjectToolResult> Projects { get; init; } = [];
}
