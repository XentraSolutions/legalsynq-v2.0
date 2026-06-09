using TenantBilling.Domain.Entities;
using TenantBilling.Domain.Services;
using TenantBilling.Domain.Tests.Fakes;
using Xunit;

namespace TenantBilling.Domain.Tests;

public class InvoiceTemplateSelectionTests
{
    private static (InvoiceTemplateService svc, InMemoryInvoiceTemplateRepository repo) Build()
    {
        var repo = new InMemoryInvoiceTemplateRepository();
        return (new InvoiceTemplateService(repo, new InMemoryUnitOfWork()), repo);
    }

    [Fact]
    public async Task GetDefaultForTenantAsync_NoTemplates_ReturnsNull()
    {
        var (svc, _) = Build();
        IInvoiceTemplateSelectionService sel = svc;
        Assert.Null(await sel.GetDefaultForTenantAsync(Guid.CreateVersion7()));
    }

    [Fact]
    public async Task GetDefaultForTenantAsync_DraftDefault_NotReturned()
    {
        var (svc, repo) = Build();
        IInvoiceTemplateSelectionService sel = svc;
        var tenantId = Guid.CreateVersion7();

        // Bypass the service to construct an "impossible" Draft+IsDefault
        // row directly so we can prove the selection service filters
        // out non-Active defaults defensively.
        await repo.AddAsync(new InvoiceTemplate
        {
            Id = Guid.CreateVersion7(),
            OwnerType = InvoiceTemplateOwnerType.Tenant,
            BillingAccountId = tenantId,
            Name = "Draft default",
            Status = InvoiceTemplateStatus.Draft,
            IsDefault = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        });

        Assert.Null(await sel.GetDefaultForTenantAsync(tenantId));
    }

    [Fact]
    public async Task GetDefaultForTenantAsync_ReturnsActiveDefault()
    {
        var (svc, _) = Build();
        IInvoiceTemplateSelectionService sel = svc;
        var tenantId = Guid.CreateVersion7();

        var created = await svc.CreateAsync(tenantId, new NewInvoiceTemplate(
            "Default", null, InvoiceTemplateStatus.Active, true,
            null, null, null, null, null, null, null,
            DefaultDueDays: 14,
            null, null, null, null, null));

        var hit = await sel.GetDefaultForTenantAsync(tenantId);

        Assert.NotNull(hit);
        Assert.Equal(created.Id, hit!.Id);
        Assert.Equal(14, hit.DefaultDueDays);
    }

    [Fact]
    public async Task GetDefaultForTenantAsync_DoesNotLeakAcrossTenants()
    {
        var (svc, _) = Build();
        IInvoiceTemplateSelectionService sel = svc;
        var t1 = Guid.CreateVersion7();
        var t2 = Guid.CreateVersion7();

        await svc.CreateAsync(t1, new NewInvoiceTemplate(
            "T1 default", null, InvoiceTemplateStatus.Active, true,
            null, null, null, null, null, null, null, 30, null, null, null, null, null));

        Assert.Null(await sel.GetDefaultForTenantAsync(t2));
    }

    [Fact]
    public async Task GetDefaultPlatformAsync_IndependentFromTenant()
    {
        var (svc, _) = Build();
        IInvoiceTemplateSelectionService sel = svc;
        var tenantId = Guid.CreateVersion7();

        await svc.CreateAsync(tenantId, new NewInvoiceTemplate(
            "Tenant default", null, InvoiceTemplateStatus.Active, true,
            null, null, null, null, null, null, null, 7, null, null, null, null, null));

        // Tenant has a default, platform does not — selection service
        // must not "fall back" silently to the tenant default for a
        // platform query.
        Assert.Null(await sel.GetDefaultPlatformAsync());

        var platformDefault = await svc.CreateAsync(tenantId: null, new NewInvoiceTemplate(
            "Platform default", null, InvoiceTemplateStatus.Active, true,
            null, null, null, null, null, null, null, 21, null, null, null, null, null));

        var hit = await sel.GetDefaultPlatformAsync();
        Assert.Equal(platformDefault.Id, hit!.Id);
        Assert.Equal(21, hit.DefaultDueDays);
    }
}
