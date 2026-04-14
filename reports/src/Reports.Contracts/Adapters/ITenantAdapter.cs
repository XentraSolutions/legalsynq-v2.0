namespace Reports.Contracts.Adapters;

public interface ITenantAdapter
{
    Task<string?> ResolveTenantIdAsync(string tenantCode, CancellationToken ct = default);
    Task<bool> IsTenantActiveAsync(string tenantId, CancellationToken ct = default);
}
