using TenantBilling.Domain.Entities;
using TenantBilling.Domain.Repositories;
using TenantBilling.Domain.Services;

namespace TenantBilling.Domain.Rendering;

/// <summary>
/// INV-TPL-03 — Default <see cref="IInvoiceRenderService"/>. Pure
/// composition over existing repositories / services:
/// <list type="number">
///   <item>load the invoice (tenant-scoped) via the standard
///     <see cref="IInvoiceRepository.GetByIdForTenantAsync"/>
///     read path — already includes line items;</item>
///   <item>load the customer via
///     <see cref="ICustomerRepository.GetByIdAsync"/>;</item>
///   <item>load the money summary (totalPaid + balanceDue) via
///     <see cref="IPaymentService.GetInvoicePaymentSummaryAsync"/>
///     so the rendered "Amount Paid" / "Amount Due" lines stay in
///     lock-step with what the payment-summary API returns;</item>
///   <item>build the template snapshot block from the
///     <see cref="Invoice.Template*"/> fields (INV-TPL-02) — never
///     from a live <see cref="InvoiceTemplate"/> row;</item>
///   <item>build the issuer block from the
///     <see cref="Invoice.Issuer*"/> fields (INV-TPL-04) — also
///     snapshot-only.</item>
/// </list>
/// Returns <c>null</c> when the invoice does not exist or belongs
/// to a different tenant. Never throws on missing customer (a
/// soft-deleted customer still renders with a placeholder name).
/// </summary>
public sealed class InvoiceRenderService : IInvoiceRenderService
{
    private readonly IInvoiceRepository _invoices;
    private readonly ICustomerRepository _customers;
    private readonly IPaymentService _payments;
    private readonly IInvoiceHtmlRenderer _html;
    private readonly TimeProvider _time;

    public InvoiceRenderService(
        IInvoiceRepository invoices,
        ICustomerRepository customers,
        IPaymentService payments,
        IInvoiceHtmlRenderer html,
        TimeProvider? time = null)
    {
        _invoices = invoices;
        _customers = customers;
        _payments = payments;
        _html = html;
        _time = time ?? TimeProvider.System;
    }

    public async Task<InvoiceRenderDocument?> BuildRenderDocumentAsync(
        Guid tenantId, Guid invoiceId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (invoiceId == Guid.Empty)
            throw new ArgumentException("InvoiceId is required.", nameof(invoiceId));

        // Tenant-scoped read: a missing or cross-tenant id surfaces
        // as null here so the controller returns a clean 404 with
        // no existence leak.
        var invoice = await _invoices.GetByIdForTenantAsync(tenantId, invoiceId, ct);
        if (invoice is null) return null;

        // Customer load is best-effort: if the customer has been
        // soft-deleted (or, in tests, never existed) the document
        // still renders with placeholder identity rather than
        // throwing — rendering is a read path and must stay
        // resilient.
        var customer = await _customers.GetByIdAsync(tenantId, invoice.CustomerId, ct);

        // Money summary: prefer the live summary so AmountPaid /
        // AmountDue stay consistent with /payment-summary. Falls
        // back to (TotalAmount, 0, TotalAmount) if the summary
        // service somehow returns null (defensive — invoice exists
        // by this point).
        var summary = await _payments.GetInvoicePaymentSummaryAsync(tenantId, invoiceId, ct);
        var amountPaid = summary?.TotalPaid ?? 0m;
        var amountDue = summary?.BalanceDue ?? invoice.TotalAmount;

        var lines = invoice.LineItems
            .OrderBy(l => l.CreatedAt)
            .Select(l => new InvoiceRenderLine(
                Description: l.Description ?? string.Empty,
                Quantity: l.Quantity,
                UnitAmount: l.UnitPrice,
                LineTotal: l.LineTotal))
            .ToList();

        InvoiceRenderTemplateSnapshot? snapshot = invoice.InvoiceTemplateId.HasValue
            ? new InvoiceRenderTemplateSnapshot(
                TemplateId: invoice.InvoiceTemplateId,
                OwnerType: invoice.TemplateOwnerType,
                Name: invoice.TemplateName,
                LogoUrl: invoice.TemplateLogoUrl,
                AccentColor: invoice.TemplateAccentColor,
                HeaderText: invoice.TemplateHeaderText,
                FooterText: invoice.TemplateFooterText,
                PaymentInstructions: invoice.TemplatePaymentInstructions,
                TermsText: invoice.TemplateTermsText,
                MemoPlaceholder: invoice.TemplateMemoPlaceholder,
                DisplayBillingAddress: invoice.TemplateDisplayBillingAddress,
                DisplayPaymentInstructions: invoice.TemplateDisplayPaymentInstructions,
                DisplayTerms: invoice.TemplateDisplayTerms,
                StampedAtUtc: invoice.TemplateStampedAtUtc)
            : null;

        var customerAddress = BuildCustomerAddress(customer);
        var issuer = BuildIssuer(invoice);

        return new InvoiceRenderDocument(
            InvoiceId: invoice.Id,
            InvoiceNumber: invoice.InvoiceNumber,
            TenantId: invoice.TenantId,
            CustomerId: invoice.CustomerId,
            CustomerName: customer?.Name ?? string.Empty,
            CustomerEmail: customer?.Email,
            IssueDate: invoice.IssueDate,
            DueDate: invoice.DueDate,
            Status: invoice.Status,
            Currency: invoice.Currency,
            Subtotal: invoice.Subtotal,
            TaxAmount: invoice.TaxAmount,
            DiscountAmount: invoice.DiscountAmount,
            TotalAmount: invoice.TotalAmount,
            AmountPaid: amountPaid,
            AmountDue: amountDue,
            Notes: invoice.Notes,
            Lines: lines,
            TemplateSnapshot: snapshot,
            CustomerAddress: customerAddress,
            Issuer: issuer,
            GeneratedAtUtc: _time.GetUtcNow().UtcDateTime);
    }

    public async Task<string?> RenderHtmlAsync(
        Guid tenantId, Guid invoiceId, CancellationToken ct = default)
    {
        var doc = await BuildRenderDocumentAsync(tenantId, invoiceId, ct);
        return doc is null ? null : _html.Render(doc);
    }

    /// <summary>
    /// INV-TPL-04 — Build the structured "Bill To" address from the
    /// customer record. Prefers structured fields when any are set;
    /// otherwise falls back to the legacy single-line
    /// <see cref="Customer.BillingAddress"/> as <c>Line1</c> so old
    /// rows still render usefully. Returns <c>null</c> when the
    /// customer is null or has no address data at all (the renderer
    /// then omits the address sub-block entirely).
    /// </summary>
    private static InvoiceRenderCustomerAddress? BuildCustomerAddress(Customer? customer)
    {
        if (customer is null) return null;

        var hasStructured =
            !string.IsNullOrWhiteSpace(customer.BillingAddressLine1)
            || !string.IsNullOrWhiteSpace(customer.BillingAddressLine2)
            || !string.IsNullOrWhiteSpace(customer.BillingCity)
            || !string.IsNullOrWhiteSpace(customer.BillingStateRegion)
            || !string.IsNullOrWhiteSpace(customer.BillingPostalCode)
            || !string.IsNullOrWhiteSpace(customer.BillingCountry);

        if (hasStructured)
        {
            return new InvoiceRenderCustomerAddress(
                Line1: customer.BillingAddressLine1,
                Line2: customer.BillingAddressLine2,
                City: customer.BillingCity,
                StateRegion: customer.BillingStateRegion,
                PostalCode: customer.BillingPostalCode,
                Country: customer.BillingCountry);
        }

        if (!string.IsNullOrWhiteSpace(customer.BillingAddress))
        {
            // Legacy fallback: drop the entire bag-of-text into
            // Line1 so the renderer can present it as a single
            // address paragraph. We do not try to parse it because
            // the format was never constrained.
            return new InvoiceRenderCustomerAddress(
                Line1: customer.BillingAddress,
                Line2: null,
                City: null,
                StateRegion: null,
                PostalCode: null,
                Country: null);
        }

        return null;
    }

    /// <summary>
    /// INV-TPL-04 — Build the From / issuer block from the invoice's
    /// snapshot columns ONLY. Returns <c>null</c> when the snapshot
    /// has no issuer fields (every column null/blank), which
    /// happens for invoices stamped before issuer fields existed
    /// or stamped from a template that had no issuer info.
    /// </summary>
    private static InvoiceRenderIssuer? BuildIssuer(Invoice invoice)
    {
        var hasAny =
            !string.IsNullOrWhiteSpace(invoice.IssuerDisplayName)
            || !string.IsNullOrWhiteSpace(invoice.IssuerLegalName)
            || !string.IsNullOrWhiteSpace(invoice.IssuerAddressLine1)
            || !string.IsNullOrWhiteSpace(invoice.IssuerAddressLine2)
            || !string.IsNullOrWhiteSpace(invoice.IssuerCity)
            || !string.IsNullOrWhiteSpace(invoice.IssuerStateRegion)
            || !string.IsNullOrWhiteSpace(invoice.IssuerPostalCode)
            || !string.IsNullOrWhiteSpace(invoice.IssuerCountry)
            || !string.IsNullOrWhiteSpace(invoice.IssuerEmail)
            || !string.IsNullOrWhiteSpace(invoice.IssuerPhone)
            || !string.IsNullOrWhiteSpace(invoice.IssuerTaxId)
            || !string.IsNullOrWhiteSpace(invoice.IssuerWebsite);

        if (!hasAny) return null;

        return new InvoiceRenderIssuer(
            DisplayName: invoice.IssuerDisplayName,
            LegalName: invoice.IssuerLegalName,
            AddressLine1: invoice.IssuerAddressLine1,
            AddressLine2: invoice.IssuerAddressLine2,
            City: invoice.IssuerCity,
            StateRegion: invoice.IssuerStateRegion,
            PostalCode: invoice.IssuerPostalCode,
            Country: invoice.IssuerCountry,
            Email: invoice.IssuerEmail,
            Phone: invoice.IssuerPhone,
            TaxId: invoice.IssuerTaxId,
            Website: invoice.IssuerWebsite,
            StampedAtUtc: invoice.IssuerStampedAtUtc);
    }
}
