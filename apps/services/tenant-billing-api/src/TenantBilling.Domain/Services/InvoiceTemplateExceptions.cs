using TenantBilling.Domain.Entities;

namespace TenantBilling.Domain.Services;

/// <summary>
/// Base for INV-TPL-01 lifecycle/validation exceptions. All derive
/// <see cref="InvalidOperationException"/> so existing controller code
/// that maps <c>InvalidOperationException</c> → 400 continues to work
/// without per-exception catch clauses.
/// </summary>
public abstract class InvoiceTemplateException : InvalidOperationException
{
    protected InvoiceTemplateException(string message) : base(message) { }
}

/// <summary>
/// Owner-scope rule violations. Examples: a Platform template with a
/// non-null tenant scope, a Tenant template with a null tenant scope,
/// or any non-null <c>TenantBillingProfileId</c> until that aggregate
/// is introduced in a later block.
/// </summary>
public sealed class InvalidInvoiceTemplateOwnerScopeException : InvoiceTemplateException
{
    public InvalidInvoiceTemplateOwnerScopeException(string message)
        : base(message) { }
}

/// <summary>
/// Status transition not permitted by
/// <see cref="InvoiceTemplateStatus"/> rules (e.g. trying to move a
/// Retired template back to Active, or editing a Retired template).
/// </summary>
public sealed class InvalidInvoiceTemplateStatusTransitionException : InvoiceTemplateException
{
    public string FromStatus { get; }
    public string ToStatus { get; }
    public InvalidInvoiceTemplateStatusTransitionException(string fromStatus, string toStatus)
        : base($"Invoice template transition '{fromStatus}' -> '{toStatus}' is not allowed.")
    {
        FromStatus = fromStatus;
        ToStatus = toStatus;
    }
}

/// <summary>
/// A retired template cannot be made or remain default. Surfaced when
/// <c>POST .../make-default</c> targets a retired template.
/// </summary>
public sealed class RetiredInvoiceTemplateCannotBeDefaultException : InvoiceTemplateException
{
    public Guid TemplateId { get; }
    public RetiredInvoiceTemplateCannotBeDefaultException(Guid templateId)
        : base($"Invoice template {templateId} is retired and cannot be made default.")
    {
        TemplateId = templateId;
    }
}

/// <summary>
/// Default-uniqueness violation. Indicates the service detected more
/// than one default template in the same scope at write time. Should
/// be impossible under normal operation because make-default unsets
/// peers in a single transaction; this exists so a logic regression
/// is surfaced loudly instead of silently leaving two defaults.
/// </summary>
public sealed class InvoiceTemplateDefaultConflictException : InvoiceTemplateException
{
    public InvoiceTemplateDefaultConflictException(string message) : base(message) { }
}

/// <summary>
/// A tenant-scoped read or write addressed a template owned by a
/// different tenant (or a Platform template). The repository surfaces
/// these as null reads to the controller (which becomes a 404 to
/// avoid existence leakage); the service layer raises this exception
/// only on the write/admin paths where mistaking ownership would
/// corrupt cross-scope state.
/// </summary>
public sealed class CrossTenantInvoiceTemplateAccessException : InvoiceTemplateException
{
    public CrossTenantInvoiceTemplateAccessException(string message) : base(message) { }
}

/// <summary>
/// INV-TPL-02: caller passed an explicit template id on the invoice
/// create path, but the id does not exist in the caller's scope (it
/// was never created, has been hard-deleted, or belongs to a
/// different tenant / the platform catalogue).
/// We deliberately surface this as 400 — not 404 — because the
/// failing resource is the invoice-create REQUEST, which itself does
/// not exist yet. Returning 404 would mis-imply that the invoice was
/// looked up and missing.
/// </summary>
public sealed class InvoiceTemplateNotFoundInScopeException : InvoiceTemplateException
{
    public Guid TemplateId { get; }
    public InvoiceTemplateNotFoundInScopeException(Guid templateId)
        : base($"Invoice template '{templateId}' does not exist in this scope.")
    {
        TemplateId = templateId;
    }
}

/// <summary>
/// INV-TPL-02: caller passed an explicit template id that exists in
/// the right scope, but its lifecycle status is not selectable for
/// stamping. Only Active templates may be stamped onto a new
/// invoice — Draft templates are still being authored and Retired
/// templates are soft-removed.
/// </summary>
public sealed class InvoiceTemplateNotSelectableException : InvoiceTemplateException
{
    public Guid TemplateId { get; }
    public string Status { get; }
    public InvoiceTemplateNotSelectableException(Guid templateId, string status)
        : base($"Invoice template '{templateId}' has status '{status}' and cannot be selected. " +
               $"Only '{InvoiceTemplateStatus.Active}' templates may be stamped onto a new invoice.")
    {
        TemplateId = templateId;
        Status = status;
    }
}
