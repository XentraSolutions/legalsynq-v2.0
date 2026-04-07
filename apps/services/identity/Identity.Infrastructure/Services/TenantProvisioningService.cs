using Identity.Application.Interfaces;
using Identity.Domain;
using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Services;

public sealed class TenantProvisioningService : ITenantProvisioningService
{
    private readonly IdentityDbContext _db;
    private readonly IDnsService _dns;
    private readonly ILogger<TenantProvisioningService> _log;

    public TenantProvisioningService(
        IdentityDbContext db,
        IDnsService dns,
        ILogger<TenantProvisioningService> log)
    {
        _db = db;
        _dns = dns;
        _log = log;
    }

    public async Task<ProvisioningResult> ProvisionAsync(Tenant tenant, CancellationToken ct = default)
    {
        return await RunProvisioningAsync(tenant, isRetry: false, ct);
    }

    public async Task<ProvisioningResult> RetryProvisioningAsync(Tenant tenant, CancellationToken ct = default)
    {
        return await RunProvisioningAsync(tenant, isRetry: true, ct);
    }

    private async Task<ProvisioningResult> RunProvisioningAsync(Tenant tenant, bool isRetry, CancellationToken ct)
    {
        var slug = tenant.Subdomain
            ?? tenant.PreferredSubdomain
            ?? SlugGenerator.Generate(tenant.Name);

        var (isValid, validationError) = SlugGenerator.Validate(slug);
        if (!isValid)
            return new ProvisioningResult(false, null, validationError);

        slug = await ResolveUniqueSlugAsync(slug, tenant.Id, ct);
        if (slug != tenant.Subdomain)
            tenant.SetSubdomain(slug);

        tenant.MarkProvisioningInProgress();
        await _db.SaveChangesAsync(ct);

        _log.LogInformation(
            "Provisioning {Action} for tenant {TenantCode} with subdomain {Slug}",
            isRetry ? "retry" : "started", tenant.Code, slug);

        try
        {
            var dnsCreated = await _dns.CreateSubdomainAsync(slug, ct);
            if (!dnsCreated)
            {
                var msg = "DNS record creation returned failure.";
                tenant.MarkProvisioningFailed(msg);
                await _db.SaveChangesAsync(ct);
                _log.LogWarning("Provisioning failed for tenant {TenantCode}: {Reason}", tenant.Code, msg);
                return new ProvisioningResult(false, null, msg);
            }

            var hostname = $"{slug}.{_dns.BaseDomain}";

            var existingDomain = await _db.TenantDomains
                .FirstOrDefaultAsync(d => d.TenantId == tenant.Id && d.DomainType == "SUBDOMAIN", ct);

            if (existingDomain is null)
            {
                var tenantDomain = TenantDomain.Create(
                    tenantId: tenant.Id,
                    domain: hostname,
                    domainType: "SUBDOMAIN",
                    isPrimary: true);
                _db.TenantDomains.Add(tenantDomain);
            }

            tenant.MarkProvisioningActive();
            await _db.SaveChangesAsync(ct);

            _log.LogInformation(
                "Provisioning succeeded for tenant {TenantCode}: hostname={Hostname}",
                tenant.Code, hostname);

            return new ProvisioningResult(true, hostname, null);
        }
        catch (Exception ex)
        {
            var msg = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
            tenant.MarkProvisioningFailed(msg);
            await _db.SaveChangesAsync(ct);
            _log.LogError(ex, "Provisioning exception for tenant {TenantCode}", tenant.Code);
            return new ProvisioningResult(false, null, msg);
        }
    }

    private async Task<string> ResolveUniqueSlugAsync(string slug, Guid tenantId, CancellationToken ct)
    {
        var exists = await _db.Tenants.AnyAsync(
            t => t.Subdomain == slug && t.Id != tenantId, ct);

        if (!exists) return slug;

        for (var i = 2; i <= 99; i++)
        {
            var candidate = SlugGenerator.AppendSuffix(slug, i);
            var taken = await _db.Tenants.AnyAsync(
                t => t.Subdomain == candidate && t.Id != tenantId, ct);
            if (!taken) return candidate;
        }

        return $"{slug}-{Guid.NewGuid().ToString("N")[..6]}";
    }
}
