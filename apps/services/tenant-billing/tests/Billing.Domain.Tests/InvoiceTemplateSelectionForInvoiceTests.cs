using Billing.Domain.Entities;
using Billing.Domain.Services;
using Billing.Domain.Tests.Fakes;
using Xunit;

namespace Billing.Domain.Tests;

/// <summary>
/// INV-TPL-02: tests the new SelectFor*Invoice methods on the
/// selection service — covers the explicit-id chain, the active/scope
/// validation, and the fallback to the tenant default.
/// </summary>
public class InvoiceTemplateSelectionForInvoiceTests
{
    private static (InvoiceTemplateService svc, InMemoryInvoiceTemplateRepository repo) Build()
    {
        var repo = new InMemoryInvoiceTemplateRepository();
        return (new InvoiceTemplateService(repo, new InMemoryUnitOfWork()), repo);
    }

    private static InvoiceTemplate Tenant(Guid tenantId, string status, bool isDefault = false, string name = "T") => new()
    {
        Id = Guid.NewGuid(),
        OwnerType = InvoiceTemplateOwnerType.Tenant,
        BillingAccountId = tenantId,
        Name = name,
        Status = status,
        IsDefault = isDefault,
    };

    [Fact]
    public async Task SelectForTenantInvoice_NullExplicitId_NoDefault_ReturnsNull()
    {
        var (svc, _) = Build();
        IInvoiceTemplateSelectionService sel = svc;
        Assert.Null(await sel.SelectForTenantInvoiceAsync(Guid.NewGuid(), explicitTemplateId: null));
    }

    [Fact]
    public async Task SelectForTenantInvoice_NullExplicitId_FallsBackToDefault()
    {
        var (svc, repo) = Build();
        IInvoiceTemplateSelectionService sel = svc;
        var tenantId = Guid.NewGuid();
        var d = Tenant(tenantId, InvoiceTemplateStatus.Active, isDefault: true, name: "Default");
        await repo.AddAsync(d);

        var picked = await sel.SelectForTenantInvoiceAsync(tenantId, explicitTemplateId: null);

        Assert.NotNull(picked);
        Assert.Equal(d.Id, picked!.Id);
    }

    [Fact]
    public async Task SelectForTenantInvoice_ExplicitId_Active_InScope_Selected()
    {
        var (svc, repo) = Build();
        IInvoiceTemplateSelectionService sel = svc;
        var tenantId = Guid.NewGuid();
        var t = Tenant(tenantId, InvoiceTemplateStatus.Active);
        await repo.AddAsync(t);

        var picked = await sel.SelectForTenantInvoiceAsync(tenantId, explicitTemplateId: t.Id);

        Assert.NotNull(picked);
        Assert.Equal(t.Id, picked!.Id);
    }

    [Fact]
    public async Task SelectForTenantInvoice_ExplicitId_DraftStatus_Throws()
    {
        var (svc, repo) = Build();
        IInvoiceTemplateSelectionService sel = svc;
        var tenantId = Guid.NewGuid();
        var t = Tenant(tenantId, InvoiceTemplateStatus.Draft);
        await repo.AddAsync(t);

        await Assert.ThrowsAsync<InvoiceTemplateNotSelectableException>(() =>
            sel.SelectForTenantInvoiceAsync(tenantId, explicitTemplateId: t.Id));
    }

    [Fact]
    public async Task SelectForTenantInvoice_ExplicitId_Retired_Throws()
    {
        var (svc, repo) = Build();
        IInvoiceTemplateSelectionService sel = svc;
        var tenantId = Guid.NewGuid();
        var t = Tenant(tenantId, InvoiceTemplateStatus.Retired);
        await repo.AddAsync(t);

        await Assert.ThrowsAsync<InvoiceTemplateNotSelectableException>(() =>
            sel.SelectForTenantInvoiceAsync(tenantId, explicitTemplateId: t.Id));
    }

    [Fact]
    public async Task SelectForTenantInvoice_ExplicitId_OtherTenant_Throws()
    {
        var (svc, repo) = Build();
        IInvoiceTemplateSelectionService sel = svc;
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var aTpl = Tenant(tenantA, InvoiceTemplateStatus.Active);
        await repo.AddAsync(aTpl);

        // Tenant B asks for tenant A's id — must not surface as
        // selected and must not leak existence either.
        await Assert.ThrowsAsync<InvoiceTemplateNotFoundInScopeException>(() =>
            sel.SelectForTenantInvoiceAsync(tenantB, explicitTemplateId: aTpl.Id));
    }

    [Fact]
    public async Task SelectForTenantInvoice_ExplicitId_PlatformOwned_Throws()
    {
        var (svc, repo) = Build();
        IInvoiceTemplateSelectionService sel = svc;
        var tenantId = Guid.NewGuid();
        var platform = new InvoiceTemplate
        {
            Id = Guid.NewGuid(),
            OwnerType = InvoiceTemplateOwnerType.Platform,
            BillingAccountId = null,
            Name = "Platform tpl",
            Status = InvoiceTemplateStatus.Active,
        };
        await repo.AddAsync(platform);

        // A tenant cannot stamp a platform template — it's outside
        // the tenant scope so it's invisible to the lookup.
        await Assert.ThrowsAsync<InvoiceTemplateNotFoundInScopeException>(() =>
            sel.SelectForTenantInvoiceAsync(tenantId, explicitTemplateId: platform.Id));
    }

    [Fact]
    public async Task SelectForPlatformInvoice_ExplicitId_TenantOwned_Throws()
    {
        var (svc, repo) = Build();
        IInvoiceTemplateSelectionService sel = svc;
        var tenantId = Guid.NewGuid();
        var t = Tenant(tenantId, InvoiceTemplateStatus.Active);
        await repo.AddAsync(t);

        // Mirror of the prior test — platform scope cannot reach
        // a tenant-owned template.
        await Assert.ThrowsAsync<InvoiceTemplateNotFoundInScopeException>(() =>
            sel.SelectForPlatformInvoiceAsync(explicitTemplateId: t.Id));
    }
}
