using Nero.Knowledge.Base.Mcp.Application.Contracts.Admin;

namespace Nero.Knowledge.Base.Mcp.Application.Services.Admin;

public interface IAdminBatchOperations
{
    Task<AdminComplianceScanResult> ScanComplianceAsync(CancellationToken cancellationToken);

    Task<AdminReindexResult> ReindexAsync(CancellationToken cancellationToken);

    Task<AdminValidationResult> ValidateAsync(CancellationToken cancellationToken);

    Task<IReadOnlySet<string>> ReadIndexedPathsAsync(CancellationToken cancellationToken);
}
