using Billing.Domain.Entities;
using Billing.Domain.Services;
using Billing.Domain.Tests.Fakes;
using FluentAssertions;
using Xunit;

namespace Billing.Domain.Tests;

/// <summary>
/// TB-ENF-01 — full decision matrix for
/// <see cref="TenantBillingAccessPolicy"/>. Mirrors §5 / §7 of the report.
/// </summary>
public class TenantBillingAccessPolicyTests
{
    private static (TenantBillingProfileService p,
                    TenantBillingEntitlementService e,
                    TenantBillingEnablementResolver r) Build()
    {
        var profiles = new InMemoryTenantBillingProfileRepository();
        var snaps = new InMemoryTenantBillingEntitlementSnapshotRepository();
        var clock = TimeProvider.System;
        var pSvc = new TenantBillingProfileService(profiles, clock);
        var eSvc = new TenantBillingEntitlementService(profiles, snaps, clock);
        return (pSvc, eSvc, new TenantBillingEnablementResolver(eSvc));
    }

    private static async Task<Guid> SeedActiveAsync(
        TenantBillingProfileService p, Guid tenant, Guid account)
    {
        var draft = await p.CreateAsync(tenant, account, null, null,
            TenantBillingMode.InternalOnly, null);
        await p.ActivateAsync(tenant, draft.Id);
        return draft.Id;
    }

    private static ApplyEntitlementSnapshotRequest Req(Guid account, string status, string rec)
        => new(account, "commerce", status, rec, null, null, null, null, null, null, null, null);

    private static TenantBillingAccessPolicy NewPolicy(
        TenantBillingEnablementResolver r, EntitlementEnforcementOptions opts)
        => new(r, () => opts);

    // --- Master switch off ---------------------------------------------

    [Theory]
    [InlineData(TenantBillingOperationCategory.Read)]
    [InlineData(TenantBillingOperationCategory.CustomerWrite)]
    [InlineData(TenantBillingOperationCategory.InvoiceWrite)]
    [InlineData(TenantBillingOperationCategory.PaymentWrite)]
    [InlineData(TenantBillingOperationCategory.TemplateWrite)]
    [InlineData(TenantBillingOperationCategory.StatementGenerate)]
    [InlineData(TenantBillingOperationCategory.ExportWrite)]
    [InlineData(TenantBillingOperationCategory.EntitlementAdmin)]
    [InlineData(TenantBillingOperationCategory.ProfileAdmin)]
    public async Task Disabled_master_switch_allows_every_category(TenantBillingOperationCategory category)
    {
        var (_, _, r) = Build();
        var policy = NewPolicy(r, new EntitlementEnforcementOptions { Enabled = false });
        var d = await policy.AuthorizeAsync(Guid.CreateVersion7(), category);
        d.IsAllowed.Should().BeTrue();
        d.Reason.Should().Be("enforcement disabled");
    }

    // --- Always-allowed categories when enforcement is on -------------

    [Theory]
    [InlineData(TenantBillingOperationCategory.Read)]
    [InlineData(TenantBillingOperationCategory.EntitlementAdmin)]
    [InlineData(TenantBillingOperationCategory.ProfileAdmin)]
    public async Task Read_and_admin_categories_are_always_allowed_even_when_enabled(
        TenantBillingOperationCategory category)
    {
        var (_, _, r) = Build();
        var policy = NewPolicy(r, new EntitlementEnforcementOptions { Enabled = true });
        var d = await policy.AuthorizeAsync(Guid.CreateVersion7(), category);
        d.IsAllowed.Should().BeTrue();
    }

    // --- Allow / Enabled = full access --------------------------------

    [Theory]
    [InlineData(TenantBillingOperationCategory.CustomerWrite)]
    [InlineData(TenantBillingOperationCategory.InvoiceWrite)]
    [InlineData(TenantBillingOperationCategory.PaymentWrite)]
    [InlineData(TenantBillingOperationCategory.TemplateWrite)]
    [InlineData(TenantBillingOperationCategory.StatementGenerate)]
    [InlineData(TenantBillingOperationCategory.ExportWrite)]
    public async Task Active_profile_with_Allow_snapshot_passes_every_write_category(
        TenantBillingOperationCategory category)
    {
        var (p, e, r) = Build();
        var t = Guid.CreateVersion7(); var a = Guid.CreateVersion7();
        await SeedActiveAsync(p, t, a);
        await e.ApplySnapshotAsync(t, Req(a,
            TenantBillingEntitlementStatus.Enabled,
            TenantBillingAccessRecommendation.Allow));

        var policy = NewPolicy(r, new EntitlementEnforcementOptions { Enabled = true });
        var d = await policy.AuthorizeAsync(t, category);
        d.IsAllowed.Should().BeTrue();
        d.AccessRecommendation.Should().Be(TenantBillingAccessRecommendation.Allow);
    }

    // --- Block snapshot blocks every write category -------------------

    [Theory]
    [InlineData(TenantBillingOperationCategory.CustomerWrite)]
    [InlineData(TenantBillingOperationCategory.InvoiceWrite)]
    [InlineData(TenantBillingOperationCategory.PaymentWrite)]
    [InlineData(TenantBillingOperationCategory.TemplateWrite)]
    [InlineData(TenantBillingOperationCategory.StatementGenerate)]
    [InlineData(TenantBillingOperationCategory.ExportWrite)]
    public async Task Block_snapshot_blocks_every_write_category(TenantBillingOperationCategory category)
    {
        var (p, e, r) = Build();
        var t = Guid.CreateVersion7(); var a = Guid.CreateVersion7();
        await SeedActiveAsync(p, t, a);
        await e.ApplySnapshotAsync(t, Req(a,
            TenantBillingEntitlementStatus.Enabled,
            TenantBillingAccessRecommendation.Block));

        var policy = NewPolicy(r, new EntitlementEnforcementOptions { Enabled = true });
        var d = await policy.AuthorizeAsync(t, category);
        d.IsAllowed.Should().BeFalse();
        d.HttpStatus.Should().Be(403);
        d.ProblemTitle.Should().Be("Tenant Billing access is restricted");
    }

    // --- ReadOnly snapshot honours the per-category toggles -----------

    [Theory]
    [InlineData(TenantBillingOperationCategory.CustomerWrite, false)]
    [InlineData(TenantBillingOperationCategory.InvoiceWrite, false)]
    [InlineData(TenantBillingOperationCategory.TemplateWrite, false)]
    [InlineData(TenantBillingOperationCategory.PaymentWrite, true)]      // AllowPaymentsInReadOnly default true
    [InlineData(TenantBillingOperationCategory.StatementGenerate, true)] // AllowStatementsInReadOnly default true
    [InlineData(TenantBillingOperationCategory.ExportWrite, false)]      // AllowExportsInReadOnly default false
    public async Task ReadOnly_snapshot_uses_default_per_category_toggles(
        TenantBillingOperationCategory category, bool expectAllowed)
    {
        var (p, e, r) = Build();
        var t = Guid.CreateVersion7(); var a = Guid.CreateVersion7();
        await SeedActiveAsync(p, t, a);
        await e.ApplySnapshotAsync(t, Req(a,
            TenantBillingEntitlementStatus.Enabled,
            TenantBillingAccessRecommendation.ReadOnly));

        var policy = NewPolicy(r, new EntitlementEnforcementOptions { Enabled = true });
        var d = await policy.AuthorizeAsync(t, category);
        d.IsAllowed.Should().Be(expectAllowed);
    }

    [Fact]
    public async Task ReadOnly_payments_can_be_disabled_via_AllowPaymentsInReadOnly_false()
    {
        var (p, e, r) = Build();
        var t = Guid.CreateVersion7(); var a = Guid.CreateVersion7();
        await SeedActiveAsync(p, t, a);
        await e.ApplySnapshotAsync(t, Req(a,
            TenantBillingEntitlementStatus.Enabled,
            TenantBillingAccessRecommendation.ReadOnly));

        var policy = NewPolicy(r, new EntitlementEnforcementOptions
        {
            Enabled = true,
            AllowPaymentsInReadOnly = false,
        });
        var d = await policy.AuthorizeAsync(t, TenantBillingOperationCategory.PaymentWrite);
        d.IsAllowed.Should().BeFalse();
    }

    [Fact]
    public async Task ReadOnly_exports_can_be_enabled_via_AllowExportsInReadOnly_true()
    {
        var (p, e, r) = Build();
        var t = Guid.CreateVersion7(); var a = Guid.CreateVersion7();
        await SeedActiveAsync(p, t, a);
        await e.ApplySnapshotAsync(t, Req(a,
            TenantBillingEntitlementStatus.Enabled,
            TenantBillingAccessRecommendation.ReadOnly));

        var policy = NewPolicy(r, new EntitlementEnforcementOptions
        {
            Enabled = true,
            AllowExportsInReadOnly = true,
        });
        var d = await policy.AuthorizeAsync(t, TenantBillingOperationCategory.ExportWrite);
        d.IsAllowed.Should().BeTrue();
    }

    // --- GraceLimited preserves payments + statements but blocks new writes

    [Theory]
    [InlineData(TenantBillingOperationCategory.PaymentWrite, true)]
    [InlineData(TenantBillingOperationCategory.StatementGenerate, true)]
    [InlineData(TenantBillingOperationCategory.CustomerWrite, false)]
    [InlineData(TenantBillingOperationCategory.InvoiceWrite, false)]
    [InlineData(TenantBillingOperationCategory.TemplateWrite, false)]
    [InlineData(TenantBillingOperationCategory.ExportWrite, false)]
    public async Task GraceLimited_snapshot_preserves_payment_and_statement_only(
        TenantBillingOperationCategory category, bool expectAllowed)
    {
        var (p, e, r) = Build();
        var t = Guid.CreateVersion7(); var a = Guid.CreateVersion7();
        await SeedActiveAsync(p, t, a);
        await e.ApplySnapshotAsync(t, Req(a,
            TenantBillingEntitlementStatus.Enabled,
            TenantBillingAccessRecommendation.GraceLimited));

        var policy = NewPolicy(r, new EntitlementEnforcementOptions { Enabled = true });
        var d = await policy.AuthorizeAsync(t, category);
        d.IsAllowed.Should().Be(expectAllowed);
    }

    [Fact]
    public async Task GraceLimitedMode_Block_blocks_everything_including_payments()
    {
        var (p, e, r) = Build();
        var t = Guid.CreateVersion7(); var a = Guid.CreateVersion7();
        await SeedActiveAsync(p, t, a);
        await e.ApplySnapshotAsync(t, Req(a,
            TenantBillingEntitlementStatus.Enabled,
            TenantBillingAccessRecommendation.GraceLimited));

        var policy = NewPolicy(r, new EntitlementEnforcementOptions
        {
            Enabled = true,
            GraceLimitedMode = "Block",
        });
        var d = await policy.AuthorizeAsync(t, TenantBillingOperationCategory.PaymentWrite);
        d.IsAllowed.Should().BeFalse();
    }

    // --- Unknown / missing-snapshot / missing-profile -----------------

    [Fact]
    public async Task Missing_profile_under_default_UnknownMode_is_ReadOnly_so_payments_pass()
    {
        var (_, _, r) = Build();
        var policy = NewPolicy(r, new EntitlementEnforcementOptions { Enabled = true });

        var pay = await policy.AuthorizeAsync(Guid.CreateVersion7(),
            TenantBillingOperationCategory.PaymentWrite);
        pay.IsAllowed.Should().BeTrue();

        var inv = await policy.AuthorizeAsync(Guid.CreateVersion7(),
            TenantBillingOperationCategory.InvoiceWrite);
        inv.IsAllowed.Should().BeFalse();
    }

    [Fact]
    public async Task UnknownMode_Block_blocks_payments_too_for_missing_profile()
    {
        var (_, _, r) = Build();
        var policy = NewPolicy(r, new EntitlementEnforcementOptions
        {
            Enabled = true,
            UnknownMode = "Block",
        });
        var d = await policy.AuthorizeAsync(Guid.CreateVersion7(),
            TenantBillingOperationCategory.PaymentWrite);
        d.IsAllowed.Should().BeFalse();
    }

    [Fact]
    public async Task Empty_tenant_id_blocks_writes_when_enabled()
    {
        var (_, _, r) = Build();
        var policy = NewPolicy(r, new EntitlementEnforcementOptions { Enabled = true });
        var d = await policy.AuthorizeAsync(Guid.Empty,
            TenantBillingOperationCategory.InvoiceWrite);
        d.IsAllowed.Should().BeFalse();
        d.Reason.Should().Be("missing tenant context");
    }

    [Fact]
    public async Task Suspended_profile_does_not_short_circuit_to_Allow_even_with_Allow_snapshot()
    {
        var (p, e, r) = Build();
        var t = Guid.CreateVersion7(); var a = Guid.CreateVersion7();
        var pid = await SeedActiveAsync(p, t, a);
        await e.ApplySnapshotAsync(t, Req(a,
            TenantBillingEntitlementStatus.Enabled,
            TenantBillingAccessRecommendation.Allow));
        await p.SuspendAsync(t, pid);

        var policy = NewPolicy(r, new EntitlementEnforcementOptions { Enabled = true });
        var d = await policy.AuthorizeAsync(t, TenantBillingOperationCategory.InvoiceWrite);
        d.IsAllowed.Should().BeFalse();
    }
}
