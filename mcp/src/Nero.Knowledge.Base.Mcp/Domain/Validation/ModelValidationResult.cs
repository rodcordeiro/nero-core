namespace Nero.Knowledge.Base.Mcp.Domain;

public sealed record ModelValidationResult(IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;

    public static ModelValidationResult Valid { get; } = new([]);
}
