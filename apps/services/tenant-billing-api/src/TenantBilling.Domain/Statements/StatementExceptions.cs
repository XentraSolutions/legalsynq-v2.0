namespace TenantBilling.Domain.Statements;

/// <summary>
/// STAT-B01 — Thrown by <see cref="ICustomerStatementService"/> when
/// the request inputs themselves are invalid (date ordering, range
/// cap, multi-currency activity). Distinct from
/// <see cref="ArgumentException"/> so the HTTP layer can map this to
/// a 400 with the user-facing message intact, and so future callers
/// composing the service directly can pattern-match the validation
/// failure mode without sniffing exception messages.
/// </summary>
public sealed class StatementValidationException : Exception
{
    public StatementValidationException(string message) : base(message) { }
}
