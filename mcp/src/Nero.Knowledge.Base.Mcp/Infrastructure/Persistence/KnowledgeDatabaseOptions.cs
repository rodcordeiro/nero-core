namespace Nero.Knowledge.Base.Mcp.Infrastructure.Persistence;

public sealed class KnowledgeDatabaseOptions
{
    public const string SectionName = "KnowledgeDatabase";
    public const int DefaultBusyTimeoutMilliseconds = 5000;

    public string Path { get; init; } = "data/nero-knowledge.db";

    public int BusyTimeoutMilliseconds { get; init; } = DefaultBusyTimeoutMilliseconds;

    public bool Pooling { get; init; } = true;
}
