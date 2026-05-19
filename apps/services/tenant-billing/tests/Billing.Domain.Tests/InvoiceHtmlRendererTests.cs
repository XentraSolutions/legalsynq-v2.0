using Billing.Domain.Rendering;
using Xunit;

namespace Billing.Domain.Tests;

/// <summary>
/// INV-TPL-03 — Pure-function tests for <see cref="InvoiceHtmlRenderer"/>.
/// Verifies escaping, snapshot inclusion, fallback layout, and the
/// "no script" guarantee.
/// </summary>
public class InvoiceHtmlRendererTests
{
    private static InvoiceRenderDocument MakeDoc(
        InvoiceRenderTemplateSnapshot? snap = null,
        string customerName = "Acme",
        string? notes = null,
        IReadOnlyList<InvoiceRenderLine>? lines = null)
        => new(
            InvoiceId: Guid.CreateVersion7(),
            InvoiceNumber: "INV-0001",
            TenantId: Guid.CreateVersion7(),
            CustomerId: Guid.CreateVersion7(),
            CustomerName: customerName,
            CustomerEmail: "billing@acme.test",
            IssueDate: new DateTime(2026, 4, 1),
            DueDate: new DateTime(2026, 5, 1),
            Status: "Issued",
            Currency: "USD",
            Subtotal: 100m,
            TaxAmount: 0m,
            DiscountAmount: 0m,
            TotalAmount: 100m,
            AmountPaid: 0m,
            AmountDue: 100m,
            Notes: notes,
            Lines: lines ?? new List<InvoiceRenderLine>
            {
                new("Consulting", 2, 50m, 100m),
            },
            TemplateSnapshot: snap,
            CustomerAddress: null,
            Issuer: null,
            GeneratedAtUtc: new DateTime(2026, 4, 24, 12, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void Render_EmitsCoreInvoiceFields()
    {
        var html = new InvoiceHtmlRenderer().Render(MakeDoc());

        Assert.Contains("<!doctype html>", html);
        Assert.Contains("Invoice INV-0001", html);
        Assert.Contains("Acme", html);
        Assert.Contains("billing@acme.test", html);
        Assert.Contains("2026-04-01", html);
        Assert.Contains("2026-05-01", html);
        Assert.Contains("Consulting", html);
        Assert.Contains("100.00 USD", html);
    }

    [Fact]
    public void Render_NullSnapshot_FallsBackToDefaultLayout()
    {
        var html = new InvoiceHtmlRenderer().Render(MakeDoc(snap: null));

        // Default accent (#1f2937) is used; no logo, no
        // header/footer, no payment-instructions/terms blocks.
        Assert.Contains("#1f2937", html);
        Assert.DoesNotContain("<img class=\"logo\"", html);
        Assert.DoesNotContain("Payment instructions", html);
        Assert.DoesNotContain("Terms</h3>", html);
    }

    [Fact]
    public void Render_WithSnapshot_EmitsLogoHeaderFooter()
    {
        var snap = new InvoiceRenderTemplateSnapshot(
            TemplateId: Guid.CreateVersion7(), OwnerType: "Tenant",
            Name: "Brand A", LogoUrl: "https://cdn.example/logo.png",
            AccentColor: "#10B981",
            HeaderText: "Thanks for your business",
            FooterText: "Acme Co",
            PaymentInstructions: "Wire to ACH 123",
            TermsText: "Net 30",
            MemoPlaceholder: null,
            DisplayBillingAddress: true,
            DisplayPaymentInstructions: true,
            DisplayTerms: true,
            StampedAtUtc: DateTime.UtcNow);

        var html = new InvoiceHtmlRenderer().Render(MakeDoc(snap: snap));

        Assert.Contains("https://cdn.example/logo.png", html);
        Assert.Contains("#10B981", html);
        Assert.Contains("Thanks for your business", html);
        Assert.Contains("Acme Co", html);
        Assert.Contains("Payment instructions", html);
        Assert.Contains("Wire to ACH 123", html);
        Assert.Contains("Terms", html);
        Assert.Contains("Net 30", html);
    }

    [Fact]
    public void Render_DisplayFlagsFalse_OmitOptionalSections()
    {
        var snap = new InvoiceRenderTemplateSnapshot(
            TemplateId: Guid.CreateVersion7(), OwnerType: "Tenant",
            Name: "Brand A", LogoUrl: null, AccentColor: null,
            HeaderText: null, FooterText: null,
            PaymentInstructions: "Wire here",
            TermsText: "Net 30",
            MemoPlaceholder: null,
            DisplayBillingAddress: false,
            DisplayPaymentInstructions: false,
            DisplayTerms: false,
            StampedAtUtc: DateTime.UtcNow);

        var html = new InvoiceHtmlRenderer().Render(MakeDoc(snap: snap));

        Assert.DoesNotContain("Payment instructions", html);
        Assert.DoesNotContain("Wire here", html);
        Assert.DoesNotContain("Net 30", html);
    }

    [Fact]
    public void Render_EscapesScriptInUserText_NeverEmitsExecutableScriptTag()
    {
        var malicious = "<script>alert('xss')</script>";
        var html = new InvoiceHtmlRenderer().Render(MakeDoc(
            customerName: malicious,
            notes: malicious,
            lines: new List<InvoiceRenderLine> { new(malicious, 1, 1m, 1m) }));

        // The literal text never appears as an executable tag in
        // the output — it is entity-encoded. Match on the
        // canonical encoded sequence and confirm the raw form is
        // absent.
        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("<script>alert", html);
    }

    [Fact]
    public void Render_EscapesScriptInSnapshotText()
    {
        var malicious = "<script>alert('xss')</script>";
        var snap = new InvoiceRenderTemplateSnapshot(
            TemplateId: Guid.CreateVersion7(), OwnerType: "Tenant",
            Name: malicious, LogoUrl: null, AccentColor: null,
            HeaderText: malicious, FooterText: malicious,
            PaymentInstructions: malicious, TermsText: malicious,
            MemoPlaceholder: malicious,
            DisplayBillingAddress: true,
            DisplayPaymentInstructions: true,
            DisplayTerms: true,
            StampedAtUtc: DateTime.UtcNow);

        var html = new InvoiceHtmlRenderer().Render(MakeDoc(snap: snap));

        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("<script>alert", html);
    }

    [Fact]
    public void Render_EscapesAccentColor_PreventsCssBreakOut()
    {
        var snap = new InvoiceRenderTemplateSnapshot(
            TemplateId: Guid.CreateVersion7(), OwnerType: "Tenant",
            Name: "X", LogoUrl: null,
            AccentColor: "red;}</style><script>alert(1)</script><style>",
            HeaderText: null, FooterText: null,
            PaymentInstructions: null, TermsText: null, MemoPlaceholder: null,
            DisplayBillingAddress: false,
            DisplayPaymentInstructions: false,
            DisplayTerms: false,
            StampedAtUtc: DateTime.UtcNow);

        var html = new InvoiceHtmlRenderer().Render(MakeDoc(snap: snap));

        // The closing </style> + opening <script> in the accent
        // payload must NOT appear unencoded — they'd terminate
        // the inline <style> block and inject script.
        Assert.DoesNotContain("</style><script>", html);
        Assert.DoesNotContain("<script>alert(1)", html);
        Assert.Contains("&lt;/style&gt;", html);
    }

    [Fact]
    public void Render_EmptyLines_ShowsPlaceholderRow()
    {
        var html = new InvoiceHtmlRenderer().Render(
            MakeDoc(lines: new List<InvoiceRenderLine>()));

        Assert.Contains("No line items.", html);
    }

    [Fact]
    public void Render_NullDocument_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new InvoiceHtmlRenderer().Render(null!));
    }

    private static InvoiceRenderIssuer SampleIssuer(
        string display = "Brand A Display",
        string website = "https://brand.test") =>
        new(
            DisplayName: display,
            LegalName: "Brand A, Inc.",
            AddressLine1: "100 Market St",
            AddressLine2: "Suite 200",
            City: "San Francisco",
            StateRegion: "CA",
            PostalCode: "94105",
            Country: "USA",
            Email: "ar@brand.test",
            Phone: "+1-415-555-0100",
            TaxId: "EIN-12-3456789",
            Website: website,
            StampedAtUtc: new DateTime(2026, 4, 24, 12, 0, 0, DateTimeKind.Utc));

    private static InvoiceRenderCustomerAddress SampleAddress() => new(
        Line1: "100 Main St",
        Line2: "Suite 4",
        City: "Springfield",
        StateRegion: "IL",
        PostalCode: "62704",
        Country: "USA");

    private static InvoiceRenderDocument MakeDocWithBlocks(
        InvoiceRenderTemplateSnapshot? snap = null,
        InvoiceRenderCustomerAddress? customerAddress = null,
        InvoiceRenderIssuer? issuer = null)
        => new(
            InvoiceId: Guid.CreateVersion7(),
            InvoiceNumber: "INV-0001",
            TenantId: Guid.CreateVersion7(),
            CustomerId: Guid.CreateVersion7(),
            CustomerName: "Acme",
            CustomerEmail: "billing@acme.test",
            IssueDate: new DateTime(2026, 4, 1),
            DueDate: new DateTime(2026, 5, 1),
            Status: "Issued",
            Currency: "USD",
            Subtotal: 100m,
            TaxAmount: 0m,
            DiscountAmount: 0m,
            TotalAmount: 100m,
            AmountPaid: 0m,
            AmountDue: 100m,
            Notes: null,
            Lines: new List<InvoiceRenderLine> { new("Consulting", 2, 50m, 100m) },
            TemplateSnapshot: snap,
            CustomerAddress: customerAddress,
            Issuer: issuer,
            GeneratedAtUtc: new DateTime(2026, 4, 24, 12, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void Render_WithIssuer_EmitsFromBlock()
    {
        var html = new InvoiceHtmlRenderer().Render(
            MakeDocWithBlocks(issuer: SampleIssuer()));

        Assert.Contains(">From<", html);
        Assert.Contains("Brand A Display", html);
        Assert.Contains("Brand A, Inc.", html);
        Assert.Contains("100 Market St", html);
        Assert.Contains("Suite 200", html);
        Assert.Contains("San Francisco, CA 94105", html);
        Assert.Contains("USA", html);
        Assert.Contains("ar@brand.test", html);
        Assert.Contains("+1-415-555-0100", html);
        Assert.Contains("EIN-12-3456789", html);
        Assert.Contains("href=\"https://brand.test\"", html);
        Assert.Contains("rel=\"noopener noreferrer\"", html);
    }

    [Fact]
    public void Render_NullIssuer_OmitsFromBlock()
    {
        var html = new InvoiceHtmlRenderer().Render(
            MakeDocWithBlocks(issuer: null));

        Assert.DoesNotContain(">From<", html);
    }

    [Fact]
    public void Render_BillTo_IncludesAddressWhenNoSnapshot()
    {
        // No snapshot ⇒ default behaviour is to show the customer
        // address (matches the historical INV-TPL-03 fallback).
        var html = new InvoiceHtmlRenderer().Render(
            MakeDocWithBlocks(snap: null, customerAddress: SampleAddress()));

        Assert.Contains("100 Main St", html);
        Assert.Contains("Suite 4", html);
        Assert.Contains("Springfield, IL 62704", html);
    }

    [Fact]
    public void Render_BillTo_IncludesAddressWhenSnapshotEnablesFlag()
    {
        var snap = new InvoiceRenderTemplateSnapshot(
            TemplateId: Guid.CreateVersion7(), OwnerType: "Tenant",
            Name: "Brand", LogoUrl: null, AccentColor: null,
            HeaderText: null, FooterText: null,
            PaymentInstructions: null, TermsText: null, MemoPlaceholder: null,
            DisplayBillingAddress: true,
            DisplayPaymentInstructions: false, DisplayTerms: false,
            StampedAtUtc: DateTime.UtcNow);

        var html = new InvoiceHtmlRenderer().Render(
            MakeDocWithBlocks(snap: snap, customerAddress: SampleAddress()));

        Assert.Contains("100 Main St", html);
        Assert.Contains("Springfield, IL 62704", html);
    }

    [Fact]
    public void Render_BillTo_OmitsAddressWhenSnapshotDisablesFlag()
    {
        var snap = new InvoiceRenderTemplateSnapshot(
            TemplateId: Guid.CreateVersion7(), OwnerType: "Tenant",
            Name: "Brand", LogoUrl: null, AccentColor: null,
            HeaderText: null, FooterText: null,
            PaymentInstructions: null, TermsText: null, MemoPlaceholder: null,
            DisplayBillingAddress: false,
            DisplayPaymentInstructions: false, DisplayTerms: false,
            StampedAtUtc: DateTime.UtcNow);

        var html = new InvoiceHtmlRenderer().Render(
            MakeDocWithBlocks(snap: snap, customerAddress: SampleAddress()));

        // Bill-To header still present (name + email always render),
        // but the address lines must be absent.
        Assert.Contains(">Bill to<", html);
        Assert.DoesNotContain("100 Main St", html);
        Assert.DoesNotContain("Springfield, IL 62704", html);
    }

    [Fact]
    public void Render_EscapesIssuerFields_NoExecutableScriptInFromBlock()
    {
        var malicious = "<script>alert('iss')</script>";
        var issuer = new InvoiceRenderIssuer(
            DisplayName: malicious,
            LegalName: malicious,
            AddressLine1: malicious,
            AddressLine2: null,
            City: malicious,
            StateRegion: null,
            PostalCode: null,
            Country: malicious,
            Email: malicious,
            Phone: malicious,
            TaxId: malicious,
            // Website is itself escaped in attribute context. The
            // renderer never validates the URL — that's the
            // template-creation layer's job — so a hostile string
            // must still come out entity-encoded both in href and
            // in the visible text.
            Website: malicious,
            StampedAtUtc: DateTime.UtcNow);

        var html = new InvoiceHtmlRenderer().Render(
            MakeDocWithBlocks(issuer: issuer));

        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("<script>alert('iss')", html);
    }

    [Fact]
    public void Render_NoExternalScriptTags_EverEmitted()
    {
        // Even with a maximally hostile snapshot + invoice we never
        // emit a literal <script ...> opening tag (anything from
        // input is entity-encoded). The static <style> block is
        // the only inline content.
        var malicious = "</style><script src=\"http://evil/x.js\"></script>";
        var snap = new InvoiceRenderTemplateSnapshot(
            TemplateId: Guid.CreateVersion7(), OwnerType: "Tenant",
            Name: malicious, LogoUrl: malicious, AccentColor: malicious,
            HeaderText: malicious, FooterText: malicious,
            PaymentInstructions: malicious, TermsText: malicious,
            MemoPlaceholder: malicious,
            DisplayBillingAddress: true,
            DisplayPaymentInstructions: true,
            DisplayTerms: true,
            StampedAtUtc: DateTime.UtcNow);

        var html = new InvoiceHtmlRenderer().Render(MakeDoc(
            snap: snap, customerName: malicious, notes: malicious));

        Assert.DoesNotContain("<script", html);
    }
}
