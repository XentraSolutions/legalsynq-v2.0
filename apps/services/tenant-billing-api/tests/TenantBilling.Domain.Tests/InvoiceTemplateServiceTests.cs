using TenantBilling.Domain.Entities;
using TenantBilling.Domain.Services;
using TenantBilling.Domain.Tests.Fakes;
using Xunit;

namespace TenantBilling.Domain.Tests;

public class InvoiceTemplateServiceTests
{
    private static (InvoiceTemplateService svc, InMemoryInvoiceTemplateRepository repo) CreateService()
    {
        var repo = new InMemoryInvoiceTemplateRepository();
        var uow = new InMemoryUnitOfWork();
        return (new InvoiceTemplateService(repo, uow), repo);
    }

    private static NewInvoiceTemplate Sample(
        string? name = "Default",
        string? status = null,
        bool? isDefault = null,
        int? defaultDueDays = 30,
        string? accent = "#1F4FFF") => new(
            Name: name!,
            Description: "Brand A",
            Status: status,
            IsDefault: isDefault,
            LogoUrl: "/uploads/logo.png",
            AccentColor: accent,
            HeaderText: "Header",
            FooterText: "Footer",
            PaymentInstructions: "Wire to ...",
            TermsText: "Terms ...",
            MemoPlaceholder: "Memo",
            DefaultDueDays: defaultDueDays,
            InvoiceNumberPrefix: "INV",
            InvoiceNumberFormat: "INV-{YYYY}-{NNNNNN}",
            DisplayBillingAddress: true,
            DisplayPaymentInstructions: true,
            DisplayTerms: true);

    // -----------------------------------------------------------------
    // Create — basic shape, scoping, defaults
    // -----------------------------------------------------------------

    [Fact]
    public async Task Create_TenantScope_AssignsTenantOwnership()
    {
        var (svc, _) = CreateService();
        var tenantId = Guid.NewGuid();

        var t = await svc.CreateAsync(tenantId, Sample());

        Assert.Equal(InvoiceTemplateOwnerType.Tenant, t.OwnerType);
        Assert.Equal(tenantId, t.BillingAccountId);
        Assert.Null(t.TenantBillingProfileId);
        Assert.Equal(InvoiceTemplateStatus.Draft, t.Status); // default when omitted
        Assert.False(t.IsDefault);
    }

    [Fact]
    public async Task Create_PlatformScope_AssignsPlatformOwnership()
    {
        var (svc, _) = CreateService();

        var t = await svc.CreateAsync(tenantId: null, Sample());

        Assert.Equal(InvoiceTemplateOwnerType.Platform, t.OwnerType);
        Assert.Null(t.BillingAccountId);
    }

    [Fact]
    public async Task Create_InvalidColor_Throws400()
    {
        var (svc, _) = CreateService();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CreateAsync(Guid.NewGuid(), Sample(accent: "blue")));
    }

    [Fact]
    public async Task Create_FirstActiveInScope_AutoBecomesDefault()
    {
        var (svc, _) = CreateService();
        var tenantId = Guid.NewGuid();

        var t = await svc.CreateAsync(tenantId, Sample(status: InvoiceTemplateStatus.Active));

        Assert.True(t.IsDefault);
    }

    [Fact]
    public async Task Create_SecondActiveInScope_DoesNotAutoSteal()
    {
        var (svc, _) = CreateService();
        var tenantId = Guid.NewGuid();

        await svc.CreateAsync(tenantId, Sample(name: "First", status: InvoiceTemplateStatus.Active));
        var second = await svc.CreateAsync(tenantId, Sample(name: "Second", status: InvoiceTemplateStatus.Active));

        Assert.False(second.IsDefault);
    }

    [Fact]
    public async Task Create_ExplicitIsDefaultOnDraft_Throws()
    {
        var (svc, _) = CreateService();
        await Assert.ThrowsAsync<InvalidInvoiceTemplateStatusTransitionException>(() =>
            svc.CreateAsync(Guid.NewGuid(),
                Sample(status: InvoiceTemplateStatus.Draft, isDefault: true)));
    }

    [Fact]
    public async Task Create_PlatformAndTenantWithSameId_AreDistinct()
    {
        var (svc, _) = CreateService();
        var tenantId = Guid.NewGuid();

        var pt = await svc.CreateAsync(tenantId: null, Sample(status: InvoiceTemplateStatus.Active));
        var tt = await svc.CreateAsync(tenantId, Sample(status: InvoiceTemplateStatus.Active));

        // Each scope has its own default; the platform default is
        // independent of the tenant default.
        Assert.True(pt.IsDefault);
        Assert.True(tt.IsDefault);
    }

    // -----------------------------------------------------------------
    // Tenant isolation
    // -----------------------------------------------------------------

    [Fact]
    public async Task GetAsync_FromOtherTenant_ReturnsNull()
    {
        var (svc, _) = CreateService();
        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        var created = await svc.CreateAsync(t1, Sample());

        var fromOther = await svc.GetAsync(t2, created.Id);

        Assert.Null(fromOther);
    }

    [Fact]
    public async Task UpdateAsync_FromOtherTenant_ReturnsNull()
    {
        var (svc, _) = CreateService();
        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        var created = await svc.CreateAsync(t1, Sample());

        var result = await svc.UpdateAsync(t2, created.Id,
            new InvoiceTemplateUpdate("Hacked", null, null, null, null, null, null, null, null, null, null, null, null, null, null));

        Assert.Null(result);
    }

    // -----------------------------------------------------------------
    // Update / lifecycle
    // -----------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_OnRetired_Throws()
    {
        var (svc, _) = CreateService();
        var tenantId = Guid.NewGuid();
        var t = await svc.CreateAsync(tenantId, Sample(status: InvoiceTemplateStatus.Active));
        await svc.RetireAsync(tenantId, t.Id);

        await Assert.ThrowsAsync<InvalidInvoiceTemplateStatusTransitionException>(() =>
            svc.UpdateAsync(tenantId, t.Id,
                new InvoiceTemplateUpdate("New", null, null, null, null, null, null, null, null, null, null, null, null, null, null)));
    }

    [Fact]
    public async Task ActivateAsync_FromRetired_Throws()
    {
        var (svc, _) = CreateService();
        var tenantId = Guid.NewGuid();
        var t = await svc.CreateAsync(tenantId, Sample(status: InvoiceTemplateStatus.Active));
        await svc.RetireAsync(tenantId, t.Id);

        await Assert.ThrowsAsync<InvalidInvoiceTemplateStatusTransitionException>(() =>
            svc.ActivateAsync(tenantId, t.Id));
    }

    [Fact]
    public async Task RetireAsync_DefaultTemplate_ClearsDefaultFlag()
    {
        var (svc, _) = CreateService();
        var tenantId = Guid.NewGuid();
        var t = await svc.CreateAsync(tenantId, Sample(status: InvoiceTemplateStatus.Active));

        var retired = await svc.RetireAsync(tenantId, t.Id);

        Assert.NotNull(retired);
        Assert.Equal(InvoiceTemplateStatus.Retired, retired!.Status);
        Assert.False(retired.IsDefault);
        Assert.Null(await svc.GetDefaultAsync(tenantId));
    }

    // -----------------------------------------------------------------
    // MakeDefault — atomic + retired-rejection
    // -----------------------------------------------------------------

    [Fact]
    public async Task MakeDefault_PromotesAndUnsetsPrevious()
    {
        var (svc, _) = CreateService();
        var tenantId = Guid.NewGuid();
        var first = await svc.CreateAsync(tenantId, Sample(name: "First", status: InvoiceTemplateStatus.Active));
        var second = await svc.CreateAsync(tenantId, Sample(name: "Second", status: InvoiceTemplateStatus.Active));

        var promoted = await svc.MakeDefaultAsync(tenantId, second.Id);

        Assert.NotNull(promoted);
        Assert.True(promoted!.IsDefault);
        // The previous default is now NOT default.
        var firstAfter = await svc.GetAsync(tenantId, first.Id);
        Assert.False(firstAfter!.IsDefault);
        var current = await svc.GetDefaultAsync(tenantId);
        Assert.Equal(second.Id, current!.Id);
    }

    [Fact]
    public async Task MakeDefault_OnRetired_Throws()
    {
        var (svc, _) = CreateService();
        var tenantId = Guid.NewGuid();
        var t = await svc.CreateAsync(tenantId, Sample(status: InvoiceTemplateStatus.Active));
        await svc.RetireAsync(tenantId, t.Id);

        await Assert.ThrowsAsync<RetiredInvoiceTemplateCannotBeDefaultException>(() =>
            svc.MakeDefaultAsync(tenantId, t.Id));
    }

    [Fact]
    public async Task MakeDefault_OnDraft_Throws()
    {
        var (svc, _) = CreateService();
        var tenantId = Guid.NewGuid();
        var t = await svc.CreateAsync(tenantId, Sample(status: InvoiceTemplateStatus.Draft));

        await Assert.ThrowsAsync<InvalidInvoiceTemplateStatusTransitionException>(() =>
            svc.MakeDefaultAsync(tenantId, t.Id));
    }

    [Fact]
    public async Task MakeDefault_Idempotent()
    {
        var (svc, _) = CreateService();
        var tenantId = Guid.NewGuid();
        var t = await svc.CreateAsync(tenantId, Sample(status: InvoiceTemplateStatus.Active));
        // Already default from auto-default rule; second call no-ops.
        var again = await svc.MakeDefaultAsync(tenantId, t.Id);
        Assert.True(again!.IsDefault);
    }

    [Fact]
    public async Task UniqueDefaultPerScope_HoldsAfterPromotion()
    {
        var (svc, repo) = CreateService();
        var tenantId = Guid.NewGuid();
        var a = await svc.CreateAsync(tenantId, Sample(name: "A", status: InvoiceTemplateStatus.Active));
        var b = await svc.CreateAsync(tenantId, Sample(name: "B", status: InvoiceTemplateStatus.Active));
        var c = await svc.CreateAsync(tenantId, Sample(name: "C", status: InvoiceTemplateStatus.Active));

        await svc.MakeDefaultAsync(tenantId, b.Id);
        await svc.MakeDefaultAsync(tenantId, c.Id);

        var all = await svc.ListAsync(tenantId);
        Assert.Equal(1, all.Count(t => t.IsDefault));
        Assert.Equal(c.Id, all.Single(t => t.IsDefault).Id);
    }
}
