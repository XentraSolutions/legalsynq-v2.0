using BuildingBlocks.Commerce;
using Contracts.Commerce;
using Identity.Application.Interfaces;
using Identity.Domain;
using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Services;

/// <summary>
/// LS-COMMERCE-ECO-02: Commerce lifecycle notifications wired for product
/// enabled/disabled events.  Notifications are noop-first and never block
/// the primary product provisioning operation.
/// </summary>
public class ProductProvisioningService : IProductProvisioningService
{
    private readonly IdentityDbContext                         _db;
    private readonly IEnumerable<IProductProvisioningHandler> _handlers;
    private readonly IUserProductAccessService                _userProductAccessService;
    private readonly ILogger<ProductProvisioningService>      _logger;
    private readonly ICommerceLifecycleNotifier                _commerceNotifier;

    private const string HostPlatformKey = "legalsynq";

    public ProductProvisioningService(
        IdentityDbContext                         db,
        IEnumerable<IProductProvisioningHandler>  handlers,
        IUserProductAccessService                 userProductAccessService,
        ILogger<ProductProvisioningService>       logger,
        ICommerceLifecycleNotifier                commerceNotifier)
    {
        _db                       = db;
        _handlers                 = handlers;
        _userProductAccessService = userProductAccessService;
        _logger                   = logger;
        _commerceNotifier         = commerceNotifier;
    }

    public async Task<ProvisionProductResult> ProvisionAsync(
        ProvisionProductRequest request,
        CancellationToken ct = default)
    {
        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Code == request.ProductCode, ct)
            ?? throw new InvalidOperationException($"Product '{request.ProductCode}' not found.");

        var tenant = await _db.Tenants
            .Include(t => t.TenantProducts)
                .ThenInclude(tp => tp.Product)
            .FirstOrDefaultAsync(t => t.Id == request.TenantId, ct)
            ?? throw new InvalidOperationException($"Tenant '{request.TenantId}' not found.");

        var tenantProductChanged = HasTenantProductStateChanged(tenant, product.Id, request.Enabled);
        bool tpCreated = await ProvisionTenantProduct(tenant, product, request.Enabled, ct);

        var (orgCreated, orgUpdated, eligibleOrgs) =
            await ProvisionOrganizationProducts(request.TenantId, product, request.Enabled, ct);

        if (tenantProductChanged)
            await InvalidateTenantAccessAsync(request.TenantId, ct);

        await _db.SaveChangesAsync(ct);

        if (request.Enabled)
            await EnsureTenantOwnerProductAccessAsync(tenant, product.Code, ct);

        ProductProvisioningHandlerResult? handlerResult = null;
        if (request.Enabled && eligibleOrgs.Count > 0)
        {
            handlerResult = await ExecuteProvisioningHandlers(
                request.TenantId, product.Id, request.ProductCode, eligibleOrgs, ct);
        }

        _logger.LogInformation(
            "Product provisioning complete: Tenant={TenantId}, Product={ProductCode}, Enabled={Enabled}, " +
            "TenantProductCreated={TpCreated}, OrgProductsCreated={OrgCreated}, OrgProductsUpdated={OrgUpdated}",
            request.TenantId, request.ProductCode, request.Enabled, tpCreated, orgCreated, orgUpdated);

        // ── LS-COMMERCE-ECO-02: Notify Commerce of product lifecycle change ───
        var productEventType = request.Enabled
            ? CommerceEventTypes.ProductEnabled
            : CommerceEventTypes.ProductDisabled;

        await TryNotifyCommerceAsync(new CommerceLifecycleEvent(
            EventType:        productEventType,
            HostPlatformKey:  HostPlatformKey,
            ExternalTenantId: request.TenantId.ToString(),
            OccurredAtUtc:    DateTimeOffset.UtcNow,
            ProductKey:       request.ProductCode,
            Metadata:         new Dictionary<string, string>
            {
                ["productCode"]          = request.ProductCode,
                ["tenantProductCreated"] = tpCreated.ToString().ToLowerInvariant(),
                ["orgProductsCreated"]   = orgCreated.ToString(),
                ["orgProductsUpdated"]   = orgUpdated.ToString()
            }), ct);

        return new ProvisionProductResult(
            request.TenantId,
            request.ProductCode,
            request.Enabled,
            tpCreated,
            orgCreated,
            orgUpdated,
            handlerResult);
    }

    private static bool HasTenantProductStateChanged(Tenant tenant, Guid productId, bool enabled)
    {
        var existing = tenant.TenantProducts.FirstOrDefault(tp => tp.ProductId == productId);
        return existing is null ? enabled : existing.IsEnabled != enabled;
    }

    private async Task EnsureTenantOwnerProductAccessAsync(
        Tenant tenant,
        string productCode,
        CancellationToken ct)
    {
        if (!tenant.OwnerUserId.HasValue)
            return;

        try
        {
            await _userProductAccessService.GrantAsync(
                tenant.Id,
                tenant.OwnerUserId.Value,
                productCode,
                actorUserId: tenant.OwnerUserId.Value,
                ct);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                ex,
                "Tenant owner product access grant skipped: TenantId={TenantId}, OwnerUserId={OwnerUserId}, ProductCode={ProductCode}",
                tenant.Id,
                tenant.OwnerUserId.Value,
                productCode);
        }
    }

    private async Task InvalidateTenantAccessAsync(Guid tenantId, CancellationToken ct)
    {
        var userIds = await _db.UserTenants
            .Where(ut => ut.TenantId == tenantId)
            .Select(ut => ut.UserId)
            .Distinct()
            .ToListAsync(ct);

        if (userIds.Count == 0)
            return;

        var users = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToListAsync(ct);

        foreach (var user in users)
            user.IncrementAccessVersion();
    }

    // ── LS-COMMERCE-ECO-02: Safe Commerce notification helper ─────────────────

    /// <summary>
    /// Sends a Commerce lifecycle event without blocking or throwing into the
    /// caller.  The <see cref="ICommerceLifecycleNotifier"/> contract already
    /// requires implementations to swallow delivery errors; this wrapper adds a
    /// second safety net at the call-site level.
    /// </summary>
    private async Task TryNotifyCommerceAsync(CommerceLifecycleEvent ev, CancellationToken ct)
    {
        try
        {
            await _commerceNotifier.NotifyAsync(ev, ct);
            _logger.LogDebug(
                "Commerce lifecycle notification dispatched: EventType={EventType}, TenantId={TenantId}, ProductKey={ProductKey}",
                ev.EventType, ev.ExternalTenantId, ev.ProductKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Commerce lifecycle notification failed (non-blocking): EventType={EventType}, TenantId={TenantId}, ProductKey={ProductKey}",
                ev.EventType, ev.ExternalTenantId, ev.ProductKey);
        }
    }

    private async Task<bool> ProvisionTenantProduct(
        Tenant tenant, Product product, bool enabled, CancellationToken ct)
    {
        var existing = tenant.TenantProducts.FirstOrDefault(tp => tp.ProductId == product.Id);

        if (existing is null)
        {
            if (enabled)
            {
                var tp = TenantProduct.Create(tenant.Id, product.Id);
                _db.Set<TenantProduct>().Add(tp);
                return true;
            }
            return false;
        }

        if (!enabled && existing.IsEnabled)
        {
            await _db.Database.ExecuteSqlRawAsync(
                "UPDATE idt_TenantProducts SET IsEnabled = 0 WHERE TenantId = {0} AND ProductId = {1}",
                tenant.Id, product.Id);
            return false;
        }

        if (enabled && !existing.IsEnabled)
        {
            await _db.Database.ExecuteSqlRawAsync(
                "UPDATE idt_TenantProducts SET IsEnabled = 1, EnabledAtUtc = {0} WHERE TenantId = {1} AND ProductId = {2}",
                DateTime.UtcNow, tenant.Id, product.Id);
            return true;
        }

        return false;
    }

    private async Task<(int Created, int Updated, List<Organization> EligibleOrgs)>
        ProvisionOrganizationProducts(
            Guid tenantId, Product product, bool enabled, CancellationToken ct)
    {
        var tenantOrgs = await _db.Organizations
            .Include(o => o.OrganizationProducts)
            .Where(o => o.TenantId == tenantId && o.IsActive)
            .ToListAsync(ct);

        int created = 0;
        int updated = 0;
        var eligibleOrgs = new List<Organization>();

        foreach (var org in tenantOrgs)
        {
            var orgProduct = org.OrganizationProducts
                .FirstOrDefault(op => op.ProductId == product.Id);

            if (enabled)
            {
                if (!ProductEligibilityConfig.IsEligible(org.OrgType, product.Code))
                {
                    _logger.LogDebug(
                        "Skipping org {OrgId} ({OrgType}): not eligible for {ProductCode}",
                        org.Id, org.OrgType, product.Code);
                    continue;
                }

                eligibleOrgs.Add(org);

                if (orgProduct is null)
                {
                    _db.OrganizationProducts.Add(
                        OrganizationProduct.Create(org.Id, product.Id));
                    created++;
                }
                else if (!orgProduct.IsEnabled)
                {
                    orgProduct.Enable();
                    updated++;
                }
            }
            else
            {
                if (orgProduct is not null && orgProduct.IsEnabled)
                {
                    orgProduct.Disable();
                    updated++;
                }
            }
        }

        return (created, updated, eligibleOrgs);
    }

    private async Task<ProductProvisioningHandlerResult?> ExecuteProvisioningHandlers(
        Guid tenantId, Guid productId, string productCode,
        List<Organization> eligibleOrgs, CancellationToken ct)
    {
        var handler = _handlers.FirstOrDefault(
            h => string.Equals(h.ProductCode, productCode, StringComparison.OrdinalIgnoreCase));

        if (handler is null)
        {
            _logger.LogDebug("No provisioning handler registered for product {ProductCode}", productCode);
            return null;
        }

        try
        {
            var context = new ProductProvisioningContext(tenantId, productId, productCode, eligibleOrgs);
            var result = await handler.HandleAsync(context, ct);

            _logger.LogInformation(
                "Product handler {ProductCode} completed: Processed={Processed}, Created={Created}, Linked={Linked}, Warnings={WarningCount}",
                productCode, result.OrganizationsProcessed, result.ProvidersCreated,
                result.ProvidersLinked, result.Warnings.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Product handler {ProductCode} failed for tenant {TenantId}. " +
                "Identity-side provisioning is complete; product-specific setup may be incomplete.",
                productCode, tenantId);

            return new ProductProvisioningHandlerResult(
                productCode,
                eligibleOrgs.Count,
                ProvidersCreated: 0,
                ProvidersLinked: 0,
                Warnings: [$"Handler failed: {ex.Message}"]);
        }
    }
}
