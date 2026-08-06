namespace Nero.Knowledge.Base.Mcp.Domain;

public sealed record KnowledgeNode : BaseModel
{
    public required string Title { get; init; }

    public required string Path { get; init; }

    public required KnowledgeScope Scope { get; init; }

    public required KnowledgeNodeType Type { get; init; }

    public string? Domain { get; init; }

    public string? Project { get; init; }

    public string Content { get; init; } = string.Empty;

    public ModelValidationResult Validate()
    {
        var errors = new List<string>();

        RequireNotBlank(Id, nameof(Id), errors);
        RequireNotBlank(Title, nameof(Title), errors);
        RequireNotBlank(Path, nameof(Path), errors);

        if (!Enum.IsDefined(Scope))
        {
            errors.Add($"{nameof(Scope)} must be a supported value.");
        }

        if (!Enum.IsDefined(Type))
        {
            errors.Add($"{nameof(Type)} must be a supported value.");
        }

        if (Scope == KnowledgeScope.Domain)
        {
            RequireNotBlank(Domain, nameof(Domain), errors);
        }

        if (Scope == KnowledgeScope.Project)
        {
            RequireNotBlank(Project, nameof(Project), errors);
        }

        return errors.Count == 0
            ? ModelValidationResult.Valid
            : new ModelValidationResult(errors);
    }


}
