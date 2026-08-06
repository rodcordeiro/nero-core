namespace Nero.Knowledge.Base.Mcp.Application.Services.Admin;

public sealed class AdminIndexConsistencyOptions
{
    public const string SectionName = "AdminIndexConsistency";

    /// <summary>
    /// Soft UX stretch target for Marco 18 (baseline ~200ms on ~476 files; hard SLO ≤60s).
    /// 2000ms surfaces degradation early without treating the hard MCP timeout as the only signal.
    /// Override via <c>AdminIndexConsistency:ThresholdMilliseconds</c> or env
    /// <c>AdminIndexConsistency__ThresholdMilliseconds</c>.
    /// </summary>
    public const int DefaultThresholdMilliseconds = 2000;

    public int ThresholdMilliseconds { get; init; } = DefaultThresholdMilliseconds;
}
