namespace Identity.Application.Interfaces;

public interface IDnsService
{
    string BaseDomain { get; }
    string BuildHostname(string tenantSlug);
    Task<bool> CreateSubdomainAsync(string subdomain, CancellationToken ct = default);
    Task<bool> DeleteSubdomainAsync(string subdomain, CancellationToken ct = default);
}
