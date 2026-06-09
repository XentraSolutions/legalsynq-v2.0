namespace Billing.Api.Tenancy;

/// <summary>
/// Resolves the tenant identifier for the current request. Implementations
/// must throw if no tenant has been resolved so callers cannot accidentally
/// query without a tenant filter.
/// </summary>
public interface ITenantContext
{
    Guid TenantId { get; }
}
