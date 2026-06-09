using Billing.Domain.Entities;

namespace Billing.Domain.StatementTemplates;

/// <summary>
/// STAT-B02 — Base for <see cref="StatementTemplate"/> lifecycle /
/// validation exceptions. All derive
/// <see cref="InvalidOperationException"/> so existing controller
/// code that maps <c>InvalidOperationException</c> → 400 continues
/// to work without per-exception catch clauses.
/// </summary>
public abstract class StatementTemplateException : InvalidOperationException
{
    protected StatementTemplateException(string message) : base(message) { }
}

/// <summary>
/// Status transition not permitted by
/// <see cref="StatementTemplateStatus"/> rules (e.g. trying to
/// move a Retired template back to Active, or editing a Retired
/// template).
/// </summary>
public sealed class InvalidStatementTemplateStatusTransitionException : StatementTemplateException
{
    public string FromStatus { get; }
    public string ToStatus { get; }
    public InvalidStatementTemplateStatusTransitionException(string fromStatus, string toStatus)
        : base($"Statement template transition '{fromStatus}' -> '{toStatus}' is not allowed.")
    {
        FromStatus = fromStatus;
        ToStatus = toStatus;
    }
}

/// <summary>
/// A retired template cannot be made or remain default. Surfaced
/// when <c>POST .../make-default</c> targets a retired template.
/// </summary>
public sealed class RetiredStatementTemplateCannotBeDefaultException : StatementTemplateException
{
    public Guid TemplateId { get; }
    public RetiredStatementTemplateCannotBeDefaultException(Guid templateId)
        : base($"Statement template {templateId} is retired and cannot be made default.")
    {
        TemplateId = templateId;
    }
}

/// <summary>
/// Default-uniqueness violation — surfaced when the
/// <c>UX_statement_templates_DefaultScopeKey</c> unique index
/// rejects a write because another template was concurrently
/// promoted to default in the same tenant scope. Controllers map
/// to 409 Conflict; clients should refetch and retry.
/// </summary>
public sealed class StatementTemplateDefaultConflictException : StatementTemplateException
{
    public StatementTemplateDefaultConflictException(string message) : base(message) { }
}

/// <summary>
/// Caller passed an explicit template id at generate time but the
/// id does not exist in the tenant's scope (or belongs to a
/// different tenant). Maps to 400 because the failing resource is
/// the generate REQUEST itself, not the statement we never created.
/// </summary>
public sealed class StatementTemplateNotFoundInScopeException : StatementTemplateException
{
    public Guid TemplateId { get; }
    public StatementTemplateNotFoundInScopeException(Guid templateId)
        : base($"Statement template '{templateId}' does not exist in this scope.")
    {
        TemplateId = templateId;
    }
}

/// <summary>
/// Caller passed an explicit template id that exists in scope but
/// is not selectable (Draft or Retired). Only Active templates may
/// be stamped onto a new statement.
/// </summary>
public sealed class StatementTemplateNotSelectableException : StatementTemplateException
{
    public Guid TemplateId { get; }
    public string Status { get; }
    public StatementTemplateNotSelectableException(Guid templateId, string status)
        : base($"Statement template '{templateId}' has status '{status}' and cannot be selected. " +
               $"Only '{StatementTemplateStatus.Active}' templates may be stamped onto a new statement.")
    {
        TemplateId = templateId;
        Status = status;
    }
}
