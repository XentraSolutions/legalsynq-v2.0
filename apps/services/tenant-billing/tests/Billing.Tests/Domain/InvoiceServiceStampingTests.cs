using Billing.Domain.Entities;
using Billing.Domain.Services;
using Xunit;

namespace Billing.Tests.Domain;

/// <summary>
/// INV-TPL-02: integration-style tests for InvoiceService against the
/// real EF-backed DomainTestHost. Verifies the create-path stamp and
/// the issue-path ensure-stamp interplay with the lifecycle engine.
/// </summary>
public class InvoiceServiceStampingTests
{
    private static async Task<(Guid TenantId, Guid CustomerId)> SeedAsync(DomainTestHost host)
    {
        var tenantId = Guid.NewGuid();
        var customer = await host.Customers.CreateAsync(
            tenantId, "Acme", $"billing+{Guid.NewGuid():N}@acme.test",
            phone: null, billingAddress: null, externalReference: null, notes: null);
        return (tenantId, customer.Id);
    }

    private static IReadOnlyList<NewInvoiceLine> Line(decimal price = 100m) =>
        new[] { new NewInvoiceLine("Consulting", 1, price) };

    private static NewInvoiceTemplate NewTemplate(
        string name, bool isDefault = true, bool withIssuer = false) => new(
        Name: name,
        Description: null,
        Status: InvoiceTemplateStatus.Active,
        IsDefault: isDefault,
        LogoUrl: null,
        AccentColor: "#10B981",
        HeaderText: $"Header for {name}",
        FooterText: $"Footer for {name}",
        PaymentInstructions: null,
        TermsText: null,
        MemoPlaceholder: null,
        DefaultDueDays: 30,
        InvoiceNumberPrefix: null,
        InvoiceNumberFormat: null,
        DisplayBillingAddress: null,
        DisplayPaymentInstructions: null,
        DisplayTerms: null,
        IssuerDisplayName: withIssuer ? $"{name} Display" : null,
        IssuerLegalName: withIssuer ? $"{name}, Inc." : null,
        IssuerAddressLine1: withIssuer ? "100 Market St" : null,
        IssuerAddressLine2: withIssuer ? "Suite 200" : null,
        IssuerCity: withIssuer ? "San Francisco" : null,
        IssuerStateRegion: withIssuer ? "CA" : null,
        IssuerPostalCode: withIssuer ? "94105" : null,
        IssuerCountry: withIssuer ? "USA" : null,
        IssuerEmail: withIssuer ? "ar@brand.test" : null,
        IssuerPhone: withIssuer ? "+1-415-555-0100" : null,
        IssuerTaxId: withIssuer ? "EIN-12-3456789" : null,
        IssuerWebsite: withIssuer ? "https://brand.test" : null);

    [Fact]
    public async Task CreateAsync_NoTemplate_LeavesSnapshotEmpty()
    {
        using var host = new DomainTestHost();
        var (tenantId, customerId) = await SeedAsync(host);

        var inv = await host.Invoices.CreateAsync(
            tenantId, customerId, invoiceNumber: null,
            issueDate: DateTime.UtcNow.Date,
            dueDate: DateTime.UtcNow.Date.AddDays(7),
            currency: "USD", notes: null,
            lines: Line(), taxAmount: 0m);

        Assert.Null(inv.InvoiceTemplateId);
        Assert.Null(inv.TemplateName);
        Assert.Null(inv.TemplateStampedAtUtc);
    }

    [Fact]
    public async Task CreateAsync_WithExplicitTemplate_StampsSnapshot()
    {
        using var host = new DomainTestHost();
        var (tenantId, customerId) = await SeedAsync(host);
        var tpl = await host.Templates.CreateAsync(tenantId, NewTemplate("Brand A"));

        var inv = await host.Invoices.CreateAsync(
            tenantId, customerId, invoiceNumber: null,
            issueDate: DateTime.UtcNow.Date,
            dueDate: DateTime.UtcNow.Date.AddDays(7),
            currency: "USD", notes: null,
            lines: Line(), taxAmount: 0m,
            template: tpl);

        Assert.Equal(tpl.Id, inv.InvoiceTemplateId);
        Assert.Equal("Brand A", inv.TemplateName);
        Assert.Equal("#10B981", inv.TemplateAccentColor);
        Assert.Equal("Header for Brand A", inv.TemplateHeaderText);
        Assert.NotNull(inv.TemplateStampedAtUtc);
    }

    [Fact]
    public async Task IssueAsync_DraftWithoutSnapshot_AppliesCurrentDefault()
    {
        using var host = new DomainTestHost();
        var (tenantId, customerId) = await SeedAsync(host);

        // 1) Create a Draft invoice with NO template configured.
        var draft = await host.Invoices.CreateAsync(
            tenantId, customerId, invoiceNumber: null,
            issueDate: DateTime.UtcNow.Date,
            dueDate: DateTime.UtcNow.Date.AddDays(7),
            currency: "USD", notes: null,
            lines: Line(), taxAmount: 0m);
        Assert.Null(draft.InvoiceTemplateId);

        // 2) Tenant configures a default AFTER the invoice was created.
        var tpl = await host.Templates.CreateAsync(tenantId, NewTemplate("Late default"));

        // 3) Issue — the ensure-stamp path should pick up the default
        //    that exists at issue time.
        var issued = await host.Invoices.IssueAsync(tenantId, draft.Id);
        Assert.NotNull(issued);
        Assert.Equal(InvoiceStatus.Issued, issued!.Status);
        Assert.Equal(tpl.Id, issued.InvoiceTemplateId);
        Assert.Equal("Late default", issued.TemplateName);
    }

    [Fact]
    public async Task IssueAsync_AlreadyStamped_KeepsOriginalSnapshot()
    {
        using var host = new DomainTestHost();
        var (tenantId, customerId) = await SeedAsync(host);

        // Two templates exist — original gets stamped at create time;
        // a NEW default is then promoted before the issue.
        var original = await host.Templates.CreateAsync(tenantId, NewTemplate("Original"));
        var draft = await host.Invoices.CreateAsync(
            tenantId, customerId, invoiceNumber: null,
            issueDate: DateTime.UtcNow.Date,
            dueDate: DateTime.UtcNow.Date.AddDays(7),
            currency: "USD", notes: null,
            lines: Line(), taxAmount: 0m,
            template: original);

        // New default is promoted; issue must NOT re-stamp.
        await host.Templates.CreateAsync(tenantId, NewTemplate("Newer", isDefault: true));

        var issued = await host.Invoices.IssueAsync(tenantId, draft.Id);
        Assert.NotNull(issued);
        Assert.Equal(original.Id, issued!.InvoiceTemplateId);
        Assert.Equal("Original", issued.TemplateName);
    }

    [Fact]
    public async Task IssueAsync_NoTemplateAndNoDefault_LeavesUnstamped()
    {
        using var host = new DomainTestHost();
        var (tenantId, customerId) = await SeedAsync(host);

        var draft = await host.Invoices.CreateAsync(
            tenantId, customerId, invoiceNumber: null,
            issueDate: DateTime.UtcNow.Date,
            dueDate: DateTime.UtcNow.Date.AddDays(7),
            currency: "USD", notes: null,
            lines: Line(), taxAmount: 0m);

        var issued = await host.Invoices.IssueAsync(tenantId, draft.Id);
        Assert.NotNull(issued);
        Assert.Equal(InvoiceStatus.Issued, issued!.Status);
        Assert.Null(issued.InvoiceTemplateId);
    }

    [Fact]
    public async Task CreateAsync_WithIssuerTemplate_StampsIssuerSnapshotAndTimestamp()
    {
        using var host = new DomainTestHost();
        var (tenantId, customerId) = await SeedAsync(host);
        var tpl = await host.Templates.CreateAsync(
            tenantId, NewTemplate("Brand A", withIssuer: true));

        var inv = await host.Invoices.CreateAsync(
            tenantId, customerId, invoiceNumber: null,
            issueDate: DateTime.UtcNow.Date,
            dueDate: DateTime.UtcNow.Date.AddDays(7),
            currency: "USD", notes: null,
            lines: Line(), taxAmount: 0m,
            template: tpl);

        Assert.Equal("Brand A Display", inv.IssuerDisplayName);
        Assert.Equal("Brand A, Inc.", inv.IssuerLegalName);
        Assert.Equal("100 Market St", inv.IssuerAddressLine1);
        Assert.Equal("Suite 200", inv.IssuerAddressLine2);
        Assert.Equal("San Francisco", inv.IssuerCity);
        Assert.Equal("CA", inv.IssuerStateRegion);
        Assert.Equal("94105", inv.IssuerPostalCode);
        Assert.Equal("USA", inv.IssuerCountry);
        Assert.Equal("ar@brand.test", inv.IssuerEmail);
        Assert.Equal("+1-415-555-0100", inv.IssuerPhone);
        Assert.Equal("EIN-12-3456789", inv.IssuerTaxId);
        Assert.Equal("https://brand.test", inv.IssuerWebsite);
        Assert.NotNull(inv.IssuerStampedAtUtc);
    }

    [Fact]
    public async Task CreateAsync_TemplateWithoutIssuer_StampsNullsButRecordsTimestamp()
    {
        using var host = new DomainTestHost();
        var (tenantId, customerId) = await SeedAsync(host);
        // withIssuer: false → all 12 issuer fields null on the template.
        var tpl = await host.Templates.CreateAsync(
            tenantId, NewTemplate("No-Issuer", withIssuer: false));

        var inv = await host.Invoices.CreateAsync(
            tenantId, customerId, invoiceNumber: null,
            issueDate: DateTime.UtcNow.Date,
            dueDate: DateTime.UtcNow.Date.AddDays(7),
            currency: "USD", notes: null,
            lines: Line(), taxAmount: 0m,
            template: tpl);

        // Per stamping contract: every issuer text column copies the
        // template's null verbatim, and IssuerStampedAtUtc is still
        // set so the renderer can distinguish "stamped, no issuer"
        // from "never stamped".
        Assert.Equal("No-Issuer", inv.TemplateName);
        Assert.Null(inv.IssuerDisplayName);
        Assert.Null(inv.IssuerLegalName);
        Assert.Null(inv.IssuerAddressLine1);
        Assert.Null(inv.IssuerEmail);
        Assert.Null(inv.IssuerWebsite);
        Assert.NotNull(inv.IssuerStampedAtUtc);
    }

    [Fact]
    public async Task IssuerSnapshot_SurvivesTemplateIssuerEdit()
    {
        // Snapshot-only contract for issuer block: a later template
        // edit must NOT rewrite a historical invoice's From identity.
        using var host = new DomainTestHost();
        var (tenantId, customerId) = await SeedAsync(host);
        var tpl = await host.Templates.CreateAsync(
            tenantId, NewTemplate("Original Brand", withIssuer: true));

        var inv = await host.Invoices.CreateAsync(
            tenantId, customerId, invoiceNumber: null,
            issueDate: DateTime.UtcNow.Date,
            dueDate: DateTime.UtcNow.Date.AddDays(7),
            currency: "USD", notes: null,
            lines: Line(), taxAmount: 0m,
            template: tpl);

        // Mutate every issuer field on the live template after stamping.
        await host.Templates.UpdateAsync(tenantId, tpl.Id, new InvoiceTemplateUpdate(
            Name: null, Description: null, LogoUrl: null, AccentColor: null,
            HeaderText: null, FooterText: null, PaymentInstructions: null,
            TermsText: null, MemoPlaceholder: null, DefaultDueDays: null,
            InvoiceNumberPrefix: null, InvoiceNumberFormat: null,
            DisplayBillingAddress: null, DisplayPaymentInstructions: null,
            DisplayTerms: null,
            IssuerDisplayName: "Renamed Display",
            IssuerLegalName: "Renamed, LLC",
            IssuerAddressLine1: "999 Other St",
            IssuerAddressLine2: null,
            IssuerCity: "Boston",
            IssuerStateRegion: "MA",
            IssuerPostalCode: "02110",
            IssuerCountry: "USA",
            IssuerEmail: "new@brand.test",
            IssuerPhone: null,
            IssuerTaxId: null,
            IssuerWebsite: "https://renamed.test"));

        var reloaded = await host.Invoices.GetAsync(tenantId, inv.Id);
        Assert.NotNull(reloaded);
        Assert.Equal("Original Brand Display", reloaded!.IssuerDisplayName);
        Assert.Equal("Original Brand, Inc.", reloaded.IssuerLegalName);
        Assert.Equal("100 Market St", reloaded.IssuerAddressLine1);
        Assert.Equal("ar@brand.test", reloaded.IssuerEmail);
        Assert.Equal("https://brand.test", reloaded.IssuerWebsite);
    }

    [Fact]
    public async Task HistoricalSnapshot_SurvivesTemplateRename()
    {
        using var host = new DomainTestHost();
        var (tenantId, customerId) = await SeedAsync(host);

        var tpl = await host.Templates.CreateAsync(tenantId, NewTemplate("Original Name"));
        var inv = await host.Invoices.CreateAsync(
            tenantId, customerId, invoiceNumber: null,
            issueDate: DateTime.UtcNow.Date,
            dueDate: DateTime.UtcNow.Date.AddDays(7),
            currency: "USD", notes: null,
            lines: Line(), taxAmount: 0m,
            template: tpl);

        // Edit the template's name and accent — historical invoice
        // must keep its original snapshot.
        await host.Templates.UpdateAsync(tenantId, tpl.Id, new InvoiceTemplateUpdate(
            Name: "Renamed",
            Description: null,
            LogoUrl: null,
            AccentColor: "#FF0000",
            HeaderText: null,
            FooterText: null,
            PaymentInstructions: null,
            TermsText: null,
            MemoPlaceholder: null,
            DefaultDueDays: null,
            InvoiceNumberPrefix: null,
            InvoiceNumberFormat: null,
            DisplayBillingAddress: null,
            DisplayPaymentInstructions: null,
            DisplayTerms: null));

        var reloaded = await host.Invoices.GetAsync(tenantId, inv.Id);
        Assert.NotNull(reloaded);
        Assert.Equal("Original Name", reloaded!.TemplateName);
        Assert.Equal("#10B981", reloaded.TemplateAccentColor);
    }
}
