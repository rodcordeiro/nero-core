using Nero.Knowledge.Base.Mcp.Application.Contracts.Admin;
using Nero.Knowledge.Base.Mcp.Application.Services.Admin;

namespace Nero.Knowledge.Base.Mcp.Infrastructure.Persistence;

public sealed class AdminBatchOperations(
    AdminKnowledgeMaintenanceService maintenanceService,
    KnowledgeIndexedPathReader indexedPathReader) : IAdminBatchOperations
{
    public Task<AdminComplianceScanResult> ScanComplianceAsync(CancellationToken cancellationToken) =>
        maintenanceService.ScanComplianceAsync(cancellationToken);

    public Task<AdminReindexResult> ReindexAsync(CancellationToken cancellationToken) =>
        maintenanceService.ReindexAsync(cancellationToken);

    public Task<AdminValidationResult> ValidateAsync(CancellationToken cancellationToken) =>
        maintenanceService.ValidateAsync(cancellationToken);

    public Task<IReadOnlySet<string>> ReadIndexedPathsAsync(CancellationToken cancellationToken) =>
        indexedPathReader.ReadAsync(cancellationToken);
}
