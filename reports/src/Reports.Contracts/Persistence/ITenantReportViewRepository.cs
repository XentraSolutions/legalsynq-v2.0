using Reports.Domain.Entities;

namespace Reports.Contracts.Persistence;

public interface ITenantReportViewRepository
{
    Task<TenantReportView?> GetByIdAsync(Guid viewId, CancellationToken ct = default);
    Task<IReadOnlyList<TenantReportView>> ListByTenantAndTemplateAsync(string tenantId, Guid templateId, CancellationToken ct = default);
    Task<TenantReportView?> GetDefaultViewAsync(string tenantId, Guid templateId, CancellationToken ct = default);
    Task<bool> HasDefaultViewAsync(string tenantId, Guid templateId, Guid? excludeViewId = null, CancellationToken ct = default);
    Task<TenantReportView> CreateAsync(TenantReportView entity, CancellationToken ct = default);
    Task<TenantReportView> UpdateAsync(TenantReportView entity, CancellationToken ct = default);
    Task DeleteAsync(Guid viewId, CancellationToken ct = default);
}
