using TenantBilling.Domain.Entities;
using TenantBilling.Domain.Rendering;
using TenantBilling.Domain.Services;
using Xunit;

namespace TenantBilling.Tests.Domain;

/// <summary>
/// INV-TPL-03 — Domain-level tests for <see cref="InvoiceRenderService"/>.
/// Exercises the snapshot-only rendering contract end-to-end against
/// real EF Core (InMemory) repositories so the read paths
/// (<c>GetByIdForTenantAsync</c>, customer lookup, payment summary)
/// stay honest.
/// </summary>
public class InvoiceRenderServiceTests
{
    private static readonly DateTime IssueDate = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime DueDate = IssueDate.AddDays(30);

    private static InvoiceRenderService BuildService(DomainTestHost host)
    {
        var renderer = new InvoiceHtmlRenderer();
        return new InvoiceRenderService(
            host.InvoiceRepo, host.CustomerRepo, host.Payments, renderer);
    }

    private static IReadOnlyList<NewInvoiceLine> Lines(decimal price = 50m, int qty = 2) =>
        new[] { new NewInvoiceLine("Consulting", qty, price) };

    private static NewInvoiceTemplate NewTemplate(
        string name, bool isDefault = false, string accent = "#10B981",
        string? header = "Thanks for your business",
        string? footer = "Acme Co",
        string? payInst = "Wire to ACH 123",
        string? terms = "Net 30") => new(
            Name: name,
            Description: null,
            Status: InvoiceTemplateStatus.Active,
            IsDefault: isDefault,
            LogoUrl: null,
            AccentColor: accent,
            HeaderText: header,
            FooterText: footer,
            PaymentInstructions: payInst,
            TermsText: terms,
            MemoPlaceholder: null,
            DefaultDueDays: 30,
            InvoiceNumberPrefix: null,
            InvoiceNumberFormat: null,
            DisplayBillingAddress: true,
            DisplayPaymentInstructions: true,
            DisplayTerms: true);

    private static async Task<(Guid tenantId, Guid customerId)> SeedTenantAsync(
        DomainTestHost host, string customerName = "Acme Co",
        string customerEmail = "billing@acme.test")
    {
        var tenantId = Guid.NewGuid();
        var customer = await host.Customers.CreateAsync(
            tenantId, customerName, customerEmail,
            phone: null, billingAddress: null, externalReference: null, notes: null);
        return (tenantId, customer.Id);
    }

    [Fact]
    public async Task BuildRenderDocument_UnstampedInvoice_ReturnsNullSnapshotAndPopulatesCore()
    {
        using var host = new DomainTestHost();
        var (tenantId, customerId) = await SeedTenantAsync(host);
        var inv = await host.Invoices.CreateAsync(
            tenantId, customerId, invoiceNumber: null,
            issueDate: IssueDate, dueDate: DueDate,
            currency: "USD", notes: null,
            lines: Lines(), taxAmount: 0m);
        var svc = BuildService(host);

        var doc = await svc.BuildRenderDocumentAsync(tenantId, inv.Id);

        Assert.NotNull(doc);
        Assert.Equal(inv.Id, doc!.InvoiceId);
        Assert.Equal(inv.InvoiceNumber, doc.InvoiceNumber);
        Assert.Equal("Acme Co", doc.CustomerName);
        Assert.Equal("billing@acme.test", doc.CustomerEmail);
        Assert.Equal("USD", doc.Currency);
        Assert.Equal(100m, doc.TotalAmount);
        Assert.Equal(0m, doc.AmountPaid);
        Assert.Equal(100m, doc.AmountDue);
        Assert.Single(doc.Lines);
        Assert.Equal("Consulting", doc.Lines[0].Description);
        Assert.Equal(2, doc.Lines[0].Quantity);
        Assert.Equal(50m, doc.Lines[0].UnitAmount);
        Assert.Equal(100m, doc.Lines[0].LineTotal);
        Assert.Null(doc.TemplateSnapshot);
    }

    [Fact]
    public async Task BuildRenderDocument_StampedInvoice_PopulatesSnapshotFromInvoiceColumns()
    {
        using var host = new DomainTestHost();
        var (tenantId, customerId) = await SeedTenantAsync(host);
        var tpl = await host.Templates.CreateAsync(tenantId, NewTemplate("Brand A"));

        var inv = await host.Invoices.CreateAsync(
            tenantId, customerId, invoiceNumber: null,
            issueDate: IssueDate, dueDate: DueDate,
            currency: "USD", notes: null,
            lines: Lines(), taxAmount: 0m,
            template: tpl);

        var svc = BuildService(host);
        var doc = await svc.BuildRenderDocumentAsync(tenantId, inv.Id);

        Assert.NotNull(doc!.TemplateSnapshot);
        var snap = doc.TemplateSnapshot!;
        Assert.Equal(tpl.Id, snap.TemplateId);
        Assert.Equal("Brand A", snap.Name);
        Assert.Equal("#10B981", snap.AccentColor);
        Assert.Equal("Thanks for your business", snap.HeaderText);
        Assert.Equal("Acme Co", snap.FooterText);
        Assert.Equal("Wire to ACH 123", snap.PaymentInstructions);
        Assert.Equal("Net 30", snap.TermsText);
        Assert.True(snap.DisplayPaymentInstructions);
        Assert.True(snap.DisplayTerms);
        Assert.NotNull(snap.StampedAtUtc);
    }

    [Fact]
    public async Task BuildRenderDocument_SnapshotWinsAfterTemplateMutation()
    {
        // Snapshot-only contract: even if the live template is mutated
        // after stamping, the rendered document must keep the original
        // values copied at create time.
        using var host = new DomainTestHost();
        var (tenantId, customerId) = await SeedTenantAsync(host);
        var tpl = await host.Templates.CreateAsync(
            tenantId, NewTemplate("Original", accent: "#10B981"));

        var inv = await host.Invoices.CreateAsync(
            tenantId, customerId, invoiceNumber: null,
            issueDate: IssueDate, dueDate: DueDate,
            currency: "USD", notes: null,
            lines: Lines(), taxAmount: 0m,
            template: tpl);

        // Mutate the live template after stamping.
        await host.Templates.UpdateAsync(tenantId, tpl.Id,
            new InvoiceTemplateUpdate(
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

        var svc = BuildService(host);
        var doc = await svc.BuildRenderDocumentAsync(tenantId, inv.Id);

        Assert.NotNull(doc!.TemplateSnapshot);
        Assert.Equal("Original", doc.TemplateSnapshot!.Name);
        Assert.Equal("#10B981", doc.TemplateSnapshot.AccentColor);
    }

    [Fact]
    public async Task BuildRenderDocument_CrossTenant_ReturnsNull()
    {
        using var host = new DomainTestHost();
        var (tenantA, customerId) = await SeedTenantAsync(host);
        var inv = await host.Invoices.CreateAsync(
            tenantA, customerId, invoiceNumber: null,
            issueDate: IssueDate, dueDate: DueDate,
            currency: "USD", notes: null,
            lines: Lines(), taxAmount: 0m);
        var tenantB = Guid.NewGuid();
        var svc = BuildService(host);

        var doc = await svc.BuildRenderDocumentAsync(tenantB, inv.Id);

        Assert.Null(doc);
    }

    [Fact]
    public async Task BuildRenderDocument_MissingInvoice_ReturnsNull()
    {
        using var host = new DomainTestHost();
        var svc = BuildService(host);

        var doc = await svc.BuildRenderDocumentAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(doc);
    }

    [Fact]
    public async Task BuildRenderDocument_EmptyTenantOrInvoice_Throws()
    {
        using var host = new DomainTestHost();
        var svc = BuildService(host);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.BuildRenderDocumentAsync(Guid.Empty, Guid.NewGuid()));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.BuildRenderDocumentAsync(Guid.NewGuid(), Guid.Empty));
    }

    [Fact]
    public async Task BuildRenderDocument_PaymentsApplied_ReflectedInAmountPaidAndDue()
    {
        using var host = new DomainTestHost();
        var (tenantId, customerId) = await SeedTenantAsync(host);
        var inv = await host.Invoices.CreateAsync(
            tenantId, customerId, invoiceNumber: null,
            issueDate: IssueDate, dueDate: DueDate,
            currency: "USD", notes: null,
            lines: Lines(), taxAmount: 0m);
        await host.Invoices.IssueAsync(tenantId, inv.Id);
        await host.Payments.CreateAsync(
            tenantId, inv.Id, amount: 30m, currency: "USD",
            method: "cash", status: "succeeded",
            transactionReference: null, paidAt: null);

        var svc = BuildService(host);
        var doc = await svc.BuildRenderDocumentAsync(tenantId, inv.Id);

        Assert.NotNull(doc);
        Assert.Equal(30m, doc!.AmountPaid);
        Assert.Equal(70m, doc.AmountDue);
    }

    [Fact]
    public async Task BuildRenderDocument_CustomerStructuredAddress_PopulatesCustomerAddressBlock()
    {
        using var host = new DomainTestHost();
        var tenantId = Guid.NewGuid();
        var customer = await host.Customers.CreateAsync(
            tenantId, "Acme Co", $"billing+{Guid.NewGuid():N}@acme.test",
            phone: null, billingAddress: null, externalReference: null, notes: null,
            billingAddressDetails: new CustomerBillingAddress(
                Line1: "100 Main St",
                Line2: "Suite 4",
                City: "Springfield",
                StateRegion: "IL",
                PostalCode: "62704",
                Country: "USA"));

        var inv = await host.Invoices.CreateAsync(
            tenantId, customer.Id, invoiceNumber: null,
            issueDate: IssueDate, dueDate: DueDate,
            currency: "USD", notes: null,
            lines: Lines(), taxAmount: 0m);

        var doc = await BuildService(host).BuildRenderDocumentAsync(tenantId, inv.Id);

        Assert.NotNull(doc!.CustomerAddress);
        Assert.Equal("100 Main St", doc.CustomerAddress!.Line1);
        Assert.Equal("Suite 4", doc.CustomerAddress.Line2);
        Assert.Equal("Springfield", doc.CustomerAddress.City);
        Assert.Equal("IL", doc.CustomerAddress.StateRegion);
        Assert.Equal("62704", doc.CustomerAddress.PostalCode);
        Assert.Equal("USA", doc.CustomerAddress.Country);
    }

    [Fact]
    public async Task BuildRenderDocument_LegacyBillingAddressOnly_FallsBackToLine1()
    {
        // Customer created via the legacy single-line BillingAddress
        // path with no structured fields → render service must pack
        // the bag-of-text into Line1 so the renderer still has
        // something to print.
        using var host = new DomainTestHost();
        var tenantId = Guid.NewGuid();
        var customer = await host.Customers.CreateAsync(
            tenantId, "Legacy Co", $"billing+{Guid.NewGuid():N}@legacy.test",
            phone: null,
            billingAddress: "5 Old Way, Suite Z, Old Town, OT 00000, USA",
            externalReference: null, notes: null);

        var inv = await host.Invoices.CreateAsync(
            tenantId, customer.Id, invoiceNumber: null,
            issueDate: IssueDate, dueDate: DueDate,
            currency: "USD", notes: null,
            lines: Lines(), taxAmount: 0m);

        var doc = await BuildService(host).BuildRenderDocumentAsync(tenantId, inv.Id);

        Assert.NotNull(doc!.CustomerAddress);
        Assert.Equal("5 Old Way, Suite Z, Old Town, OT 00000, USA",
            doc.CustomerAddress!.Line1);
        Assert.Null(doc.CustomerAddress.Line2);
        Assert.Null(doc.CustomerAddress.City);
        Assert.Null(doc.CustomerAddress.StateRegion);
        Assert.Null(doc.CustomerAddress.PostalCode);
        Assert.Null(doc.CustomerAddress.Country);
    }

    [Fact]
    public async Task BuildRenderDocument_CustomerWithNoAddress_OmitsCustomerAddressBlock()
    {
        using var host = new DomainTestHost();
        var (tenantId, customerId) = await SeedTenantAsync(host);
        var inv = await host.Invoices.CreateAsync(
            tenantId, customerId, invoiceNumber: null,
            issueDate: IssueDate, dueDate: DueDate,
            currency: "USD", notes: null,
            lines: Lines(), taxAmount: 0m);

        var doc = await BuildService(host).BuildRenderDocumentAsync(tenantId, inv.Id);

        Assert.Null(doc!.CustomerAddress);
    }

    private static NewInvoiceTemplate IssuerTemplate(string name) => new(
        Name: name, Description: null,
        Status: InvoiceTemplateStatus.Active, IsDefault: false,
        LogoUrl: null, AccentColor: null,
        HeaderText: null, FooterText: null,
        PaymentInstructions: null, TermsText: null, MemoPlaceholder: null,
        DefaultDueDays: 30, InvoiceNumberPrefix: null, InvoiceNumberFormat: null,
        DisplayBillingAddress: true,
        DisplayPaymentInstructions: null, DisplayTerms: null,
        IssuerDisplayName: $"{name} Display",
        IssuerLegalName: $"{name}, Inc.",
        IssuerAddressLine1: "100 Market St",
        IssuerAddressLine2: "Suite 200",
        IssuerCity: "San Francisco",
        IssuerStateRegion: "CA",
        IssuerPostalCode: "94105",
        IssuerCountry: "USA",
        IssuerEmail: "ar@brand.test",
        IssuerPhone: "+1-415-555-0100",
        IssuerTaxId: "EIN-12-3456789",
        IssuerWebsite: "https://brand.test");

    [Fact]
    public async Task BuildRenderDocument_StampedIssuer_PopulatesIssuerBlockFromInvoiceColumns()
    {
        using var host = new DomainTestHost();
        var (tenantId, customerId) = await SeedTenantAsync(host);
        var tpl = await host.Templates.CreateAsync(tenantId, IssuerTemplate("Brand A"));

        var inv = await host.Invoices.CreateAsync(
            tenantId, customerId, invoiceNumber: null,
            issueDate: IssueDate, dueDate: DueDate,
            currency: "USD", notes: null,
            lines: Lines(), taxAmount: 0m,
            template: tpl);

        var doc = await BuildService(host).BuildRenderDocumentAsync(tenantId, inv.Id);

        Assert.NotNull(doc!.Issuer);
        var issuer = doc.Issuer!;
        Assert.Equal("Brand A Display", issuer.DisplayName);
        Assert.Equal("Brand A, Inc.", issuer.LegalName);
        Assert.Equal("100 Market St", issuer.AddressLine1);
        Assert.Equal("Suite 200", issuer.AddressLine2);
        Assert.Equal("San Francisco", issuer.City);
        Assert.Equal("CA", issuer.StateRegion);
        Assert.Equal("94105", issuer.PostalCode);
        Assert.Equal("USA", issuer.Country);
        Assert.Equal("ar@brand.test", issuer.Email);
        Assert.Equal("+1-415-555-0100", issuer.Phone);
        Assert.Equal("EIN-12-3456789", issuer.TaxId);
        Assert.Equal("https://brand.test", issuer.Website);
        Assert.NotNull(issuer.StampedAtUtc);
    }

    [Fact]
    public async Task BuildRenderDocument_NoStampedIssuer_OmitsIssuerBlock()
    {
        using var host = new DomainTestHost();
        var (tenantId, customerId) = await SeedTenantAsync(host);
        // Template without any issuer info.
        var tpl = await host.Templates.CreateAsync(tenantId, NewTemplate("No-Issuer"));

        var inv = await host.Invoices.CreateAsync(
            tenantId, customerId, invoiceNumber: null,
            issueDate: IssueDate, dueDate: DueDate,
            currency: "USD", notes: null,
            lines: Lines(), taxAmount: 0m,
            template: tpl);

        var doc = await BuildService(host).BuildRenderDocumentAsync(tenantId, inv.Id);

        Assert.Null(doc!.Issuer);
    }

    [Fact]
    public async Task BuildRenderDocument_IssuerSnapshot_SurvivesTemplateMutation()
    {
        // Identical to the template-snapshot survival test but for the
        // INV-TPL-04 issuer block: render must read snapshot columns,
        // never the live template row.
        using var host = new DomainTestHost();
        var (tenantId, customerId) = await SeedTenantAsync(host);
        var tpl = await host.Templates.CreateAsync(tenantId, IssuerTemplate("Original"));

        var inv = await host.Invoices.CreateAsync(
            tenantId, customerId, invoiceNumber: null,
            issueDate: IssueDate, dueDate: DueDate,
            currency: "USD", notes: null,
            lines: Lines(), taxAmount: 0m,
            template: tpl);

        await host.Templates.UpdateAsync(tenantId, tpl.Id, new InvoiceTemplateUpdate(
            Name: null, Description: null, LogoUrl: null, AccentColor: null,
            HeaderText: null, FooterText: null, PaymentInstructions: null,
            TermsText: null, MemoPlaceholder: null, DefaultDueDays: null,
            InvoiceNumberPrefix: null, InvoiceNumberFormat: null,
            DisplayBillingAddress: null, DisplayPaymentInstructions: null,
            DisplayTerms: null,
            IssuerDisplayName: "Renamed",
            IssuerEmail: "new@brand.test",
            IssuerWebsite: "https://renamed.test"));

        var doc = await BuildService(host).BuildRenderDocumentAsync(tenantId, inv.Id);

        Assert.NotNull(doc!.Issuer);
        Assert.Equal("Original Display", doc.Issuer!.DisplayName);
        Assert.Equal("ar@brand.test", doc.Issuer.Email);
        Assert.Equal("https://brand.test", doc.Issuer.Website);
    }

    [Fact]
    public async Task RenderHtml_DelegatesToRenderer_ReturnsHtml()
    {
        using var host = new DomainTestHost();
        var (tenantId, customerId) = await SeedTenantAsync(host);
        var inv = await host.Invoices.CreateAsync(
            tenantId, customerId, invoiceNumber: null,
            issueDate: IssueDate, dueDate: DueDate,
            currency: "USD", notes: null,
            lines: Lines(), taxAmount: 0m);
        var svc = BuildService(host);

        var html = await svc.RenderHtmlAsync(tenantId, inv.Id);

        Assert.NotNull(html);
        Assert.StartsWith("<!doctype html>", html);
        Assert.Contains(inv.InvoiceNumber, html);
    }

    [Fact]
    public async Task RenderHtml_MissingInvoice_ReturnsNull()
    {
        using var host = new DomainTestHost();
        var svc = BuildService(host);

        var html = await svc.RenderHtmlAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(html);
    }
}
