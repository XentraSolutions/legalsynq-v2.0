namespace Billing.Domain.Accounting.Erp.Remediation;

/// <summary>
/// MS-BILL-ERP-005 — server-side, read-only QuickBooks Online
/// customer-lookup port. The remediation service depends on this
/// abstraction so the lookup adapter can live in
/// <c>Billing.Infrastructure</c> alongside the existing token
/// provider, while the domain stays free of any HTTP / Intuit
/// dependency.
///
/// <para>
/// Implementations MUST:
/// </para>
/// <list type="bullet">
///   <item>perform every QBO call server-side (no token leaves the
///   process);</item>
///   <item>return a deterministic <see cref="QuickBooksCustomerSearchResult"/>
///   shape — the controller never wraps these in HTTP exceptions;</item>
///   <item>cap result counts (the existing search already enforces
///   QBO MAXRESULTS 25);</item>
///   <item>NEVER fuzzy-match on the result set (the operator
///   explicitly chooses the QBO id);</item>
///   <item>NEVER auto-create a QBO customer.</item>
/// </list>
/// </summary>
public interface IQuickBooksCustomerLookup
{
    /// <summary>
    /// True when every required QuickBooks credential / account ref
    /// is configured. False short-circuits the search and the
    /// validation `QuickBooksCustomerNotFound` check with a
    /// deterministic <c>ConfigurationRequired</c> outcome.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Return up to 25 QBO customers whose <c>DisplayName</c>
    /// contains <paramref name="query"/> (QBO `LIKE '%q%'`). Empty
    /// or whitespace queries return an empty hit list with
    /// <c>Outcome = Ok</c> (no error).
    /// </summary>
    Task<QuickBooksCustomerSearchResult> SearchByDisplayNameAsync(
        string query,
        CancellationToken ct = default);

    /// <summary>
    /// Resolve a single QBO customer by its opaque id. Returns the
    /// hit on success; NULL when QBO returns 404. Other failure
    /// modes surface via the search-result shape on
    /// <see cref="QuickBooksCustomerLookupResult"/>; this overload
    /// throws <see cref="QuickBooksCustomerLookupException"/> for
    /// auth / transport / 5xx so the validation orchestrator can map
    /// to a deterministic issue code.
    /// </summary>
    Task<QuickBooksCustomerSearchHit?> GetByIdAsync(
        string quickBooksCustomerId,
        CancellationToken ct = default);
}

/// <summary>
/// Sentinel raised by <see cref="IQuickBooksCustomerLookup.GetByIdAsync"/>
/// for non-404 failure modes. <see cref="Outcome"/> is one of the
/// closed-enum values from
/// <see cref="QuickBooksCustomerLookupOutcome"/> and is safe to
/// surface verbatim to the operator UI.
/// </summary>
public sealed class QuickBooksCustomerLookupException : System.Exception
{
    public QuickBooksCustomerLookupOutcome Outcome { get; }

    public QuickBooksCustomerLookupException(QuickBooksCustomerLookupOutcome outcome, string message)
        : base(message)
    {
        Outcome = outcome;
    }
}
