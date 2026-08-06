namespace Nero.Knowledge.Base.Mcp.Domain;

public sealed record KnowledgeEdge : BaseModel
{ 

    public required string SourceNodeId { get; init; }

    public required string TargetNodeId { get; init; }

    public required KnowledgeRelationType Relation { get; init; }

    public decimal Confidence { get; init; } = 1m;

    public string Evidence { get; init; } = string.Empty;

    public ModelValidationResult Validate()
    {
        var errors = new List<string>();

        RequireNotBlank(Id, nameof(Id), errors);
        RequireNotBlank(SourceNodeId, nameof(SourceNodeId), errors);
        RequireNotBlank(TargetNodeId, nameof(TargetNodeId), errors);

        if (!Enum.IsDefined(Relation))
        {
            errors.Add($"{nameof(Relation)} must be a supported value.");
        }

        if (Confidence is < 0m or > 1m)
        {
            errors.Add($"{nameof(Confidence)} must be between 0 and 1.");
        }

        return errors.Count == 0
            ? ModelValidationResult.Valid
            : new ModelValidationResult(errors);
    }

 
}
