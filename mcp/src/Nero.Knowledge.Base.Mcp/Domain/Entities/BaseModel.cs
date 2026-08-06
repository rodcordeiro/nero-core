
namespace Nero.Knowledge.Base.Mcp.Domain;

public abstract record BaseModel
{
    public required string Id { get; init; }

    protected static void RequireNotBlank(string? value, string fieldName, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{fieldName} is required.");
        }
    }
}
