using Reports.Contracts.Context;

namespace Reports.Contracts.Adapters;

public interface IAuditAdapter
{
    Task<AdapterResult<bool>> RecordEventAsync(RequestContext ctx, TenantContext tenant, string userId, string action, string description, CancellationToken ct = default);
}
