namespace Billing.Domain.Accounting.Erp.QuickBooks;

/// <summary>
/// Allow-listed lifecycle states for a QuickBooks customer mapping
/// row. Persisted verbatim on
/// <c>quickbooks_customer_mappings.MappingStatus</c>; the BFF / UI
/// render badges off these literal strings.
/// </summary>
public static class QuickBooksCustomerMappingStatus
{
    /// <summary>Mapping is active and used by the customer resolver.</summary>
    public const string Active = "Active";

    /// <summary>
    /// Mapping is preserved for audit / history but the resolver
    /// MUST treat the Billing customer as if no mapping exists
    /// (so the configured fallback path applies, or the export
    /// fails deterministically).
    /// </summary>
    public const string Disabled = "Disabled";
}

/// <summary>
/// Allow-listed export-mode override for a single mapping. NULL on
/// the row means "inherit the provider-wide
/// <c>QuickBooksOptions.ExportMode</c>". Populated values let an
/// operator opt a single Billing customer onto the InvoiceFirst
/// path while leaving the rest of the tenant on JournalEntry.
/// </summary>
public static class QuickBooksCustomerMappingExportMode
{
    public const string JournalEntry = "JournalEntry";
    public const string InvoiceFirst = "InvoiceFirst";
}

/// <summary>
/// MS-BILL-ERP-003 — Tenant-scoped, operator-curated link from a
/// Billing customer (<see cref="BillingCustomerId"/>) to a
/// QuickBooks Online customer (<see cref="QuickBooksCustomerId"/>).
///
/// <para>
/// One row per (TenantId, BillingCustomerId) AND per
/// (TenantId, QuickBooksCustomerId) — both pairs are enforced by
/// unique indexes at the SQL level so a Billing customer cannot
/// silently double-map and a QBO customer cannot be linked to two
/// distinct Billing customers within the same tenant.
/// </para>
///
/// <para>
/// All fields are server-authoritative. The browser never supplies
/// <see cref="TenantId"/>; it is injected from the IDM session via
/// the existing <c>X-Tenant-Id</c> header convention.
/// </para>
/// </summary>
public sealed class QuickBooksCustomerMapping
{
    /// <summary>Surrogate primary key (GUID).</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tenant. Required for every read/write.</summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Billing-side <see cref="Billing.Domain.Entities.Customer.Id"/>.
    /// Unique within the tenant.
    /// </summary>
    public Guid BillingCustomerId { get; set; }

    /// <summary>
    /// QBO-side customer id (the value of <c>Customer.Id</c> in QBO
    /// REST responses). String because Intuit treats it as an
    /// opaque scalar and we never do arithmetic on it. Capped to
    /// 100 chars on the SQL side.
    /// </summary>
    public string QuickBooksCustomerId { get; set; } = string.Empty;

    /// <summary>
    /// Operator-supplied display label for the QBO customer (e.g.
    /// "Acme Corp"). Surfaced read-only in the admin UI; the
    /// resolver does NOT match on this field — only
    /// <see cref="QuickBooksCustomerId"/> is authoritative.
    /// Optional.
    /// </summary>
    public string? QuickBooksDisplayName { get; set; }

    /// <summary>
    /// One of <see cref="QuickBooksCustomerMappingStatus"/>.
    /// </summary>
    public string MappingStatus { get; set; } = QuickBooksCustomerMappingStatus.Active;

    /// <summary>
    /// Optional per-mapping override. NULL means "inherit
    /// provider-wide ExportMode". Allowed values are listed in
    /// <see cref="QuickBooksCustomerMappingExportMode"/>.
    /// </summary>
    public string? ExportMode { get; set; }

    /// <summary>
    /// Display name of the operator who created the row. Sourced
    /// from the BFF-injected <c>X-User-DisplayName</c> header;
    /// never browser-trusted for authorization.
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>
    /// Stamp updated by the QB provider after a successful Invoice
    /// or JournalEntry post that resolved through this mapping.
    /// Read-only signal for operators. NULL until first use.
    /// </summary>
    public DateTime? LastExportedAtUtc { get; set; }
}
