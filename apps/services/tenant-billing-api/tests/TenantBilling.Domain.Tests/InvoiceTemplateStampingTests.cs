using TenantBilling.Domain.Entities;
using TenantBilling.Domain.Services;
using Xunit;

namespace TenantBilling.Domain.Tests;

/// <summary>
/// INV-TPL-02: pure-function unit tests for the stamping service.
/// Touches no database — verifies only that the right fields move from
/// template to invoice and that the idempotency guard fires.
/// </summary>
public class InvoiceTemplateStampingTests
{
    private static InvoiceTemplate FullyPopulatedTemplate(string name = "Brand A") => new()
    {
        Id = Guid.CreateVersion7(),
        OwnerType = InvoiceTemplateOwnerType.Tenant,
        BillingAccountId = Guid.CreateVersion7(),
        Name = name,
        Status = InvoiceTemplateStatus.Active,
        IsDefault = true,
        LogoUrl = "https://cdn.test/logo.png",
        AccentColor = "#10B981",
        HeaderText = "Header",
        FooterText = "Footer",
        PaymentInstructions = "Wire to ACH",
        TermsText = "Net 30",
        MemoPlaceholder = "memo",
        DefaultDueDays = 30,
        DisplayBillingAddress = true,
        DisplayPaymentInstructions = false,
        DisplayTerms = true,
    };

    private static Invoice BlankInvoice() => new()
    {
        Id = Guid.CreateVersion7(),
        TenantId = Guid.CreateVersion7(),
        CustomerId = Guid.CreateVersion7(),
        InvoiceNumber = "INV-2026-000001",
        Currency = "USD",
        Status = InvoiceStatus.Draft,
    };

    [Fact]
    public void StampInvoice_CopiesAllBrandingFields()
    {
        var sut = new InvoiceTemplateStampingService();
        var tpl = FullyPopulatedTemplate();
        var inv = BlankInvoice();
        var when = new DateTime(2026, 4, 24, 12, 0, 0, DateTimeKind.Utc);

        sut.StampInvoice(inv, tpl, when);

        Assert.Equal(tpl.Id, inv.InvoiceTemplateId);
        Assert.Equal(tpl.OwnerType, inv.TemplateOwnerType);
        Assert.Equal(tpl.Name, inv.TemplateName);
        Assert.Equal(tpl.LogoUrl, inv.TemplateLogoUrl);
        Assert.Equal(tpl.AccentColor, inv.TemplateAccentColor);
        Assert.Equal(tpl.HeaderText, inv.TemplateHeaderText);
        Assert.Equal(tpl.FooterText, inv.TemplateFooterText);
        Assert.Equal(tpl.PaymentInstructions, inv.TemplatePaymentInstructions);
        Assert.Equal(tpl.TermsText, inv.TemplateTermsText);
        Assert.Equal(tpl.MemoPlaceholder, inv.TemplateMemoPlaceholder);
        Assert.Equal(tpl.DisplayBillingAddress, inv.TemplateDisplayBillingAddress);
        Assert.Equal(tpl.DisplayPaymentInstructions, inv.TemplateDisplayPaymentInstructions);
        Assert.Equal(tpl.DisplayTerms, inv.TemplateDisplayTerms);
        Assert.Equal(when, inv.TemplateStampedAtUtc);
    }

    [Fact]
    public void StampInvoice_NullBrandingFields_AreCopiedThrough()
    {
        // A template with intentionally-null branding (e.g. no logo)
        // must propagate those nulls to the snapshot — we never want
        // the snapshot to silently invent a value the template did
        // not have.
        var sut = new InvoiceTemplateStampingService();
        var tpl = new InvoiceTemplate
        {
            Id = Guid.CreateVersion7(),
            OwnerType = InvoiceTemplateOwnerType.Tenant,
            BillingAccountId = Guid.CreateVersion7(),
            Name = "Minimal",
            Status = InvoiceTemplateStatus.Active,
            DisplayBillingAddress = false,
            DisplayPaymentInstructions = false,
            DisplayTerms = false,
        };
        var inv = BlankInvoice();

        sut.StampInvoice(inv, tpl, DateTime.UtcNow);

        Assert.Null(inv.TemplateLogoUrl);
        Assert.Null(inv.TemplateAccentColor);
        Assert.Null(inv.TemplateHeaderText);
        Assert.Null(inv.TemplateFooterText);
        Assert.Null(inv.TemplatePaymentInstructions);
        Assert.Null(inv.TemplateTermsText);
        Assert.Null(inv.TemplateMemoPlaceholder);
        Assert.False(inv.TemplateDisplayBillingAddress);
        Assert.False(inv.TemplateDisplayPaymentInstructions);
        Assert.False(inv.TemplateDisplayTerms);
    }

    [Fact]
    public void EnsureStampedInvoice_AlreadyStamped_NoOp()
    {
        var sut = new InvoiceTemplateStampingService();
        var first = FullyPopulatedTemplate("First");
        var second = FullyPopulatedTemplate("Second");
        var inv = BlankInvoice();
        var t0 = new DateTime(2026, 4, 24, 12, 0, 0, DateTimeKind.Utc);
        var t1 = t0.AddDays(7);

        sut.StampInvoice(inv, first, t0);
        var changed = sut.EnsureStampedInvoice(inv, second, t1);

        Assert.False(changed);
        Assert.Equal(first.Id, inv.InvoiceTemplateId);
        Assert.Equal("First", inv.TemplateName);
        Assert.Equal(t0, inv.TemplateStampedAtUtc);
    }

    [Fact]
    public void EnsureStampedInvoice_NotStamped_AppliesStamp()
    {
        var sut = new InvoiceTemplateStampingService();
        var tpl = FullyPopulatedTemplate();
        var inv = BlankInvoice();
        var when = new DateTime(2026, 4, 24, 13, 0, 0, DateTimeKind.Utc);

        var changed = sut.EnsureStampedInvoice(inv, tpl, when);

        Assert.True(changed);
        Assert.Equal(tpl.Id, inv.InvoiceTemplateId);
        Assert.Equal(when, inv.TemplateStampedAtUtc);
    }

    [Fact]
    public void StampInvoice_NullArgs_Throw()
    {
        var sut = new InvoiceTemplateStampingService();
        Assert.Throws<ArgumentNullException>(() =>
            sut.StampInvoice(null!, FullyPopulatedTemplate(), DateTime.UtcNow));
        Assert.Throws<ArgumentNullException>(() =>
            sut.StampInvoice(BlankInvoice(), null!, DateTime.UtcNow));
    }
}
