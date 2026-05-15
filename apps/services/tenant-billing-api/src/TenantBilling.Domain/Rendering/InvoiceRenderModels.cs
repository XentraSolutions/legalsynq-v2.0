namespace TenantBilling.Domain.Rendering;

/// <summary>
/// INV-TPL-03 — Stable, deterministic, snapshot-only view of an
/// invoice plus its branding for downstream rendering (HTML, future
/// PDF, future email body, future preview UI). All template-related
/// data is sourced from the invoice's stamped snapshot columns
/// (INV-TPL-02), never from a live <see cref="Entities.InvoiceTemplate"/>
/// row, so a later template edit / retire / delete cannot alter how
/// a historical invoice renders.
///
/// INV-TPL-04: <see cref="CustomerAddress"/> exposes the structured
/// "Bill To" address sourced from the customer record; <see cref="Issuer"/>
/// exposes the "From" identity sourced exclusively from the invoice's
/// snapshot columns. Both are nullable — a render document for an
/// invoice whose customer has no address and whose template carried
/// no issuer fields renders without those blocks rather than empty.
/// </summary>
public sealed record InvoiceRenderDocument(
    Guid InvoiceId,
    string InvoiceNumber,
    Guid TenantId,
    Guid CustomerId,
    string CustomerName,
    string? CustomerEmail,
    DateTime IssueDate,
    DateTime DueDate,
    string Status,
    string Currency,
    decimal Subtotal,
    decimal TaxAmount,
    decimal DiscountAmount,
    decimal TotalAmount,
    decimal AmountPaid,
    decimal AmountDue,
    string? Notes,
    IReadOnlyList<InvoiceRenderLine> Lines,
    InvoiceRenderTemplateSnapshot? TemplateSnapshot,
    InvoiceRenderCustomerAddress? CustomerAddress,
    InvoiceRenderIssuer? Issuer,
    DateTime GeneratedAtUtc);

/// <summary>
/// One line on the invoice. Sourced verbatim from
/// <see cref="Entities.InvoiceLineItem"/>; <c>LineTotal</c> is
/// <c>Quantity × UnitAmount</c> (computed if not already persisted).
/// </summary>
public sealed record InvoiceRenderLine(
    string Description,
    int Quantity,
    decimal UnitAmount,
    decimal LineTotal);

/// <summary>
/// Snapshot of the invoice template's branding at the moment it was
/// stamped onto this invoice. Mirrors the
/// <c>Invoice.Template*</c> columns (INV-TPL-02). Null on the parent
/// document when the invoice was never stamped (no explicit
/// template id was passed AND no tenant default existed at create
/// or issue time).
/// </summary>
public sealed record InvoiceRenderTemplateSnapshot(
    Guid? TemplateId,
    string? OwnerType,
    string? Name,
    string? LogoUrl,
    string? AccentColor,
    string? HeaderText,
    string? FooterText,
    string? PaymentInstructions,
    string? TermsText,
    string? MemoPlaceholder,
    bool DisplayBillingAddress,
    bool DisplayPaymentInstructions,
    bool DisplayTerms,
    DateTime? StampedAtUtc);

/// <summary>
/// INV-TPL-04 — Structured "Bill To" address sourced from the
/// <see cref="Entities.Customer"/> record. All fields nullable so a
/// customer with only some address detail (e.g. just a country) still
/// produces a usable block. <see cref="Line1"/> may also carry the
/// legacy single-line <c>Customer.BillingAddress</c> text when no
/// structured fields are populated — see <c>InvoiceRenderService</c>
/// for the fallback rule. Returns <c>null</c> on the parent document
/// when the customer has no address data at all.
/// </summary>
public sealed record InvoiceRenderCustomerAddress(
    string? Line1,
    string? Line2,
    string? City,
    string? StateRegion,
    string? PostalCode,
    string? Country);

/// <summary>
/// INV-TPL-04 — "From" / issuer identity exposed in the render
/// document. Sourced EXCLUSIVELY from the invoice's snapshot columns
/// (<c>Invoice.Issuer*</c>) — never from a live template — so a later
/// template edit cannot rewrite a historical invoice's From block.
/// Returns <c>null</c> on the parent document when the snapshot's
/// <c>IssuerStampedAtUtc</c> is null OR every issuer text field is
/// null/blank (no point exposing an empty block).
/// </summary>
public sealed record InvoiceRenderIssuer(
    string? DisplayName,
    string? LegalName,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? StateRegion,
    string? PostalCode,
    string? Country,
    string? Email,
    string? Phone,
    string? TaxId,
    string? Website,
    DateTime? StampedAtUtc);
