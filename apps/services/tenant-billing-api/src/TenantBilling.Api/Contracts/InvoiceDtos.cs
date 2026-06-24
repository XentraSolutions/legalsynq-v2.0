using System.ComponentModel.DataAnnotations;
using TenantBilling.Domain.Entities;
using TenantBilling.Domain.Services;

namespace TenantBilling.Api.Contracts;

public sealed class CreateInvoiceLineRequest
{
    [Required, MaxLength(500)] public string Description { get; set; } = string.Empty;
    [Range(1, int.MaxValue)] public int Quantity { get; set; } = 1;
    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal UnitPrice { get; set; }
}

public sealed class CreateInvoiceRequest
{
    // TenantId is sourced from the X-Tenant-Id request header. Kept in the
    // body shape for backwards compatibility with earlier clients/tests; the
    // controller ignores this field in favor of the tenant context.
    public Guid TenantId { get; set; }
    [Required] public Guid CustomerId { get; set; }

    /// <summary>
    /// Optional. When null/blank the service auto-generates an
    /// <c>INV-YYYY-NNNNNN</c> sequence number scoped to the tenant.
    /// </summary>
    [MaxLength(64)] public string? InvoiceNumber { get; set; }

    [Required] public DateTime IssueDate { get; set; }

    /// <summary>
    /// Optional. When omitted, the controller applies the calling
    /// tenant's default invoice template's <c>DefaultDueDays</c>:
    /// <c>DueDate = IssueDate + DefaultDueDays</c>. Returns 400 when
    /// neither a value nor a tenant default is available.
    /// </summary>
    public DateTime? DueDate { get; set; }
    [Required, MaxLength(3)] public string Currency { get; set; } = "USD";
    [MaxLength(2000)] public string? Notes { get; set; }
    public decimal TaxAmount { get; set; }

    /// <summary>Optional discount applied to the invoice total. Defaults to 0.</summary>
    public decimal DiscountAmount { get; set; }

    [Required, MinLength(1)]
    public List<CreateInvoiceLineRequest> Lines { get; set; } = new();

    /// <summary>
    /// INV-TPL-02: optional invoice template id. When provided, the
    /// controller validates it (must exist in the calling tenant's
    /// scope and be Active) and stamps its branding snapshot onto the
    /// new invoice. Returns 400 when the id is missing from the
    /// tenant's scope or is not Active. When omitted, the controller
    /// falls back to the tenant's default template; if no default
    /// is configured, the invoice is created with no snapshot — a
    /// fully supported state.
    /// </summary>
    public Guid? InvoiceTemplateId { get; set; }
}

public sealed record InvoiceLineItemResponse(
    Guid Id,
    string Description,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    DateTime CreatedAt);

/// <summary>
/// INV-TPL-02: nested view of the branding snapshot persisted on an
/// invoice. Null on the parent <see cref="InvoiceResponse"/> when the
/// invoice was created without an effective template (no explicit id
/// AND no tenant default configured at the time). Once present, it
/// reflects the template's appearance at stamp time and never tracks
/// later edits to the template itself.
/// </summary>
public sealed record InvoiceTemplateSnapshotResponse(
    Guid Id,
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

public sealed record InvoiceResponse(
    Guid Id,
    Guid TenantId,
    Guid CustomerId,
    string InvoiceNumber,
    DateTime IssueDate,
    DateTime DueDate,
    string Status,
    decimal Subtotal,
    decimal TaxAmount,
    decimal DiscountAmount,
    decimal TotalAmount,
    string Currency,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? IssuedAt,
    IReadOnlyList<InvoiceLineItemResponse> Lines,
    InvoiceTemplateSnapshotResponse? TemplateSnapshot)
{
    public static InvoiceResponse From(Invoice i) => new(
        i.Id, i.TenantId, i.CustomerId, i.InvoiceNumber, i.IssueDate, i.DueDate,
        i.Status, i.Subtotal, i.TaxAmount, i.DiscountAmount, i.TotalAmount, i.Currency, i.Notes,
        i.CreatedAt, i.UpdatedAt, i.IssuedAt,
        i.LineItems
            .Select(l => new InvoiceLineItemResponse(l.Id, l.Description, l.Quantity, l.UnitPrice, l.LineTotal, l.CreatedAt))
            .ToList(),
        // Snapshot is present only when the invoice was stamped.
        // Existence is keyed off the InvoiceTemplateId column, which
        // is the only field guaranteed to be non-null on every stamp
        // (Name/LogoUrl/etc may legitimately be null on the source
        // template).
        i.InvoiceTemplateId is { } templateId
            ? new InvoiceTemplateSnapshotResponse(
                templateId,
                i.TemplateOwnerType,
                i.TemplateName,
                i.TemplateLogoUrl,
                i.TemplateAccentColor,
                i.TemplateHeaderText,
                i.TemplateFooterText,
                i.TemplatePaymentInstructions,
                i.TemplateTermsText,
                i.TemplateMemoPlaceholder,
                i.TemplateDisplayBillingAddress,
                i.TemplateDisplayPaymentInstructions,
                i.TemplateDisplayTerms,
                i.TemplateStampedAtUtc)
            : null);
}

/// <summary>
/// Paginated invoice list response. Wraps a page of invoices plus the
/// pagination metadata callers need to render a UI pager.
/// </summary>
public sealed record InvoiceListResponse(
    IReadOnlyList<InvoiceResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages)
{
    public static InvoiceListResponse From(InvoicePage page, int requestedPage, int pageSize)
    {
        var totalPages = pageSize <= 0 ? 0 : (int)Math.Ceiling(page.TotalCount / (double)pageSize);
        return new InvoiceListResponse(
            page.Items.Select(InvoiceResponse.From).ToList(),
            requestedPage, pageSize, page.TotalCount, totalPages);
    }
}

/// <summary>
/// Slim response returned by <c>POST /api/invoices/{id}/issue</c>. Contains
/// just the fields callers care about for the transition (no need to re-ship
/// the whole invoice with line items every time).
/// </summary>
public sealed record IssueInvoiceResponse(
    Guid Id,
    string Status,
    DateTime? IssuedAt,
    DateTime UpdatedAt)
{
    public static IssueInvoiceResponse From(Invoice i)
        => new(i.Id, i.Status, i.IssuedAt, i.UpdatedAt);
}
