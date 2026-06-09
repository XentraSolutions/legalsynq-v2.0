namespace Billing.Domain.Entities;

/// <summary>
/// STAT-B02 — Lifecycle status for a <see cref="StatementTemplate"/>.
/// Mirrors <see cref="InvoiceTemplateStatus"/>:
/// <list type="bullet">
///   <item><c>Draft</c> — fully editable; cannot be selected for
///     statement generation and cannot be made default.</item>
///   <item><c>Active</c> — selectable and may be made default. Still
///     editable so brand updates land without forcing operators to
///     create a new template.</item>
///   <item><c>Retired</c> — soft-removed. Cannot be selected, cannot
///     be made default, and is locked against branding/text edits.
///     Retiring the current default also clears the default flag.</item>
/// </list>
/// </summary>
public static class StatementTemplateStatus
{
    public const string Draft = "Draft";
    public const string Active = "Active";
    public const string Retired = "Retired";

    public static bool IsValid(string? value) =>
        value is Draft or Active or Retired;
}

/// <summary>
/// STAT-B02 — Tenant-scoped template configuring the branding and
/// presentation of a customer statement. Unlike
/// <see cref="InvoiceTemplate"/> there is no platform tier — the
/// platform never issues statements to its tenants in this service.
///
/// Template state is *configuration*: it never directly references
/// individual statements, and editing a template never rewrites a
/// historical <see cref="CustomerStatement"/>. The persistence
/// service snapshots the template into the statement at generation
/// time, so a later edit cannot change what was sent.
/// </summary>
public sealed class StatementTemplate
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>One of <see cref="StatementTemplateStatus"/> values.</summary>
    public string Status { get; set; } = StatementTemplateStatus.Draft;

    public bool IsDefault { get; set; }

    // ---- Branding ----
    public string? LogoUrl { get; set; }
    public string? AccentColor { get; set; }
    public string? HeaderText { get; set; }
    public string? FooterText { get; set; }

    // ---- Body content ----
    public string? PaymentInstructions { get; set; }
    public string? TermsText { get; set; }
    public string? MemoPlaceholder { get; set; }

    // ---- Presentation toggles ----
    public bool DisplayOutstandingTable { get; set; } = true;
    public bool DisplayPaymentInstructions { get; set; } = true;
    public bool DisplayTransactionMemos { get; set; } = true;

    /// <summary>
    /// Optional override for the leading prefix of the statement
    /// number. When null the persistence layer uses the canonical
    /// <c>STMT</c> prefix. The number generator does NOT consume
    /// this value in STAT-B02 (so the unique index remains stable
    /// across tenants); it is reserved for a future renderer that
    /// wants to display the configured prefix.
    /// </summary>
    public string? StatementNumberPrefix { get; set; }

    // ---- Issuer / sender identity ----
    public string? IssuerDisplayName { get; set; }
    public string? IssuerLegalName { get; set; }
    public string? IssuerAddressLine1 { get; set; }
    public string? IssuerAddressLine2 { get; set; }
    public string? IssuerCity { get; set; }
    public string? IssuerStateRegion { get; set; }
    public string? IssuerPostalCode { get; set; }
    public string? IssuerCountry { get; set; }
    public string? IssuerEmail { get; set; }
    public string? IssuerPhone { get; set; }
    public string? IssuerTaxId { get; set; }
    public string? IssuerWebsite { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
