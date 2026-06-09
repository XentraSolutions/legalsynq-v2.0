using Billing.Domain.Accounting.Erp.QuickBooks;

namespace Billing.Domain.Accounting.Erp.Remediation;

/// <summary>
/// MS-BILL-ERP-005 — concrete remediation orchestrator. Pure
/// composition over the read-only repository, the QBO lookup port,
/// and the existing ERP-003 mapping service (used here only as a
/// READ probe — no mutation).
/// </summary>
public sealed class ErpRemediationService : IErpRemediationService
{
    /// <summary>
    /// Hard cap on the unmapped-customer projection. A tenant with
    /// more than this many unresolved customers is an operational
    /// red flag in its own right; the cap keeps the response
    /// bounded and predictable.
    /// </summary>
    public const int UnmappedCustomerHardCap = 100;

    /// <summary>
    /// Minimum query length for the QBO customer search. Anything
    /// shorter is treated as "empty" and returns an empty hit list
    /// with <c>Outcome = Ok</c> — preventing accidental open-ended
    /// `LIKE '%%'` scans against the QBO company.
    /// </summary>
    public const int MinSearchQueryLength = 2;

    private readonly IErpRemediationRepository _repo;
    private readonly IQuickBooksCustomerMappingRepository _mappings;
    private readonly IQuickBooksCustomerLookup _lookup;

    public ErpRemediationService(
        IErpRemediationRepository repo,
        IQuickBooksCustomerMappingRepository mappings,
        IQuickBooksCustomerLookup lookup)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _mappings = mappings ?? throw new ArgumentNullException(nameof(mappings));
        _lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
    }

    public Task<IReadOnlyList<UnmappedCustomerRow>> ListUnmappedCustomersAsync(
        Guid tenantId,
        CancellationToken ct = default)
        => _repo.ListUnmappedCustomersAsync(tenantId, UnmappedCustomerHardCap, ct);

    public Task<QuickBooksCustomerSearchResult> SearchQuickBooksCustomersAsync(
        string query,
        CancellationToken ct = default)
    {
        if (!_lookup.IsConfigured)
        {
            return Task.FromResult(QuickBooksCustomerSearchResult.ConfigurationRequired());
        }
        var trimmed = (query ?? string.Empty).Trim();
        if (trimmed.Length < MinSearchQueryLength)
        {
            return Task.FromResult(
                QuickBooksCustomerSearchResult.Ok(Array.Empty<QuickBooksCustomerSearchHit>()));
        }
        return _lookup.SearchByDisplayNameAsync(trimmed, ct);
    }

    public async Task<MappingValidationResult> ValidateMappingAsync(
        Guid tenantId,
        MappingValidationCommand command,
        CancellationToken ct = default)
    {
        if (command is null) throw new ArgumentNullException(nameof(command));

        var issues = new List<MappingValidationIssue>();

        // ----- 1. QBO customer id surface validation -----
        var qboId = (command.QuickBooksCustomerId ?? string.Empty).Trim();
        if (qboId.Length == 0 || qboId.Length > 100)
        {
            issues.Add(new MappingValidationIssue(
                MappingValidationIssueCode.InvalidQuickBooksCustomerId,
                "QuickBooks customer id is required and must be 1–100 characters."));
        }

        // ----- 2. Billing customer existence + tenant ownership -----
        var customer = await _repo.GetCustomerAsync(tenantId, command.BillingCustomerId, ct)
            .ConfigureAwait(false);
        if (customer is null)
        {
            issues.Add(new MappingValidationIssue(
                MappingValidationIssueCode.BillingCustomerNotFound,
                "Billing customer was not found in this tenant."));
        }

        // ----- 3. Mapping conflict (Billing side) -----
        var existingByBilling = await _mappings
            .GetByBillingCustomerAsync(tenantId, command.BillingCustomerId, ct)
            .ConfigureAwait(false);
        if (existingByBilling is not null
            && string.Equals(existingByBilling.MappingStatus,
                QuickBooksCustomerMappingStatus.Active,
                StringComparison.Ordinal))
        {
            issues.Add(new MappingValidationIssue(
                MappingValidationIssueCode.BillingCustomerAlreadyMapped,
                "An active mapping already exists for this Billing customer."));
        }

        // ----- 4. Mapping conflict (QBO side) -----
        // We probe the QBO-side uniqueness via the same repository
        // contract as ERP-003; a conflicting row blocks the
        // confirmation step before the controller's POST gets to
        // see a 409.
        if (issues.All(i => i.Code != MappingValidationIssueCode.InvalidQuickBooksCustomerId))
        {
            // A conflicting QBO row may belong to a DIFFERENT
            // BillingCustomerId; surface it as a structured issue.
            // Direct tenant-scoped lookup against the unique index
            // on (TenantId, QuickBooksCustomerId) — O(1) regardless
            // of how many mappings the tenant has.
            var existingByQbo = await _mappings
                .GetByQuickBooksCustomerIdAsync(tenantId, qboId, ct)
                .ConfigureAwait(false);
            if (existingByQbo is not null
                && existingByQbo.BillingCustomerId != command.BillingCustomerId
                && string.Equals(existingByQbo.MappingStatus,
                    QuickBooksCustomerMappingStatus.Active,
                    StringComparison.Ordinal))
            {
                issues.Add(new MappingValidationIssue(
                    MappingValidationIssueCode.QuickBooksCustomerAlreadyMapped,
                    "This QuickBooks customer is already mapped to a different Billing customer."));
            }
        }

        // ----- 5. QBO customer existence (server-side probe) -----
        string? qboDisplayName = null;
        if (issues.All(i => i.Code != MappingValidationIssueCode.InvalidQuickBooksCustomerId))
        {
            if (!_lookup.IsConfigured)
            {
                issues.Add(new MappingValidationIssue(
                    MappingValidationIssueCode.ProviderConfigurationRequired,
                    "QuickBooks provider configuration is incomplete."));
            }
            else
            {
                try
                {
                    var hit = await _lookup.GetByIdAsync(qboId, ct).ConfigureAwait(false);
                    if (hit is null)
                    {
                        issues.Add(new MappingValidationIssue(
                            MappingValidationIssueCode.QuickBooksCustomerNotFound,
                            "QuickBooks customer was not found in the configured realm."));
                    }
                    else
                    {
                        qboDisplayName = hit.DisplayName;
                    }
                }
                catch (QuickBooksCustomerLookupException lex)
                {
                    var code = lex.Outcome == QuickBooksCustomerLookupOutcome.ConfigurationRequired
                        ? MappingValidationIssueCode.ProviderConfigurationRequired
                        : MappingValidationIssueCode.ProviderUnavailable;
                    issues.Add(new MappingValidationIssue(code, lex.Message));
                }
            }
        }

        if (issues.Count > 0) return MappingValidationResult.WithIssues(issues);
        return MappingValidationResult.Ok(qboDisplayName);
    }

}
