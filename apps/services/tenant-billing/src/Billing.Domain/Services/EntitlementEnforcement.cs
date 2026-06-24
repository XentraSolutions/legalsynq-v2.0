using Billing.Domain.Entities;

namespace Billing.Domain.Services;

/// <summary>
/// TB-ENF-01 — operation categories used by the soft-enforcement policy.
/// Maps directly to the per-endpoint risk classes documented in the
/// TB-ENF-01 report §5.
/// </summary>
public enum TenantBillingOperationCategory
{
    /// <summary>Read-only access. Always allowed.</summary>
    Read = 0,

    /// <summary>Customer create / update / delete.</summary>
    CustomerWrite = 1,

    /// <summary>Invoice create / issue / void / overdue / refund / adjust.</summary>
    InvoiceWrite = 2,

    /// <summary>Payment recording / reversal / notes.</summary>
    PaymentWrite = 3,

    /// <summary>Invoice/statement template create / update / activate / retire / make-default.</summary>
    TemplateWrite = 4,

    /// <summary>Statement generate / persist / send / void.</summary>
    StatementGenerate = 5,

    /// <summary>Accounting/ERP export create / run / mapping import-commit.</summary>
    ExportWrite = 6,

    /// <summary>Internal admin: apply entitlement snapshot.</summary>
    EntitlementAdmin = 7,

    /// <summary>Internal admin: profile create / activate / suspend / close.</summary>
    ProfileAdmin = 8,
}

/// <summary>
/// TB-ENF-01 — bound configuration for the soft-enforcement policy. Section
/// <c>Billing:EntitlementEnforcement</c>. All defaults match the safe-rollout
/// posture documented in the report:
///   - Enforcement OFF (Enabled=false)
///   - Unknown / GraceLimited treated as ReadOnly when enabled
///   - Read-only payment recording + statement reads still allowed
///   - Read-only export creation blocked
/// </summary>
public sealed class EntitlementEnforcementOptions
{
    public const string SectionName = "Billing:EntitlementEnforcement";

    /// <summary>Master switch. When false, every category is allowed.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// How to treat <c>Unknown</c> / missing-snapshot / missing-profile
    /// states when <see cref="Enabled"/> is true. Allowed values are
    /// <c>ReadOnly</c> (default) and <c>Block</c>. Unknown literal values
    /// fall back to <c>ReadOnly</c>.
    /// </summary>
    public string UnknownMode { get; set; } = "ReadOnly";

    /// <summary>
    /// How to treat <c>GraceLimited</c> snapshots. Allowed values are
    /// <c>ReadOnly</c> (default) and <c>Block</c>. The default is the
    /// "soft" interpretation: reads + payment recovery + statement reads
    /// continue, new write-expansion is blocked.
    /// </summary>
    public string GraceLimitedMode { get; set; } = "ReadOnly";

    /// <summary>
    /// When true, <see cref="TenantBillingOperationCategory.PaymentWrite"/>
    /// is allowed under <c>ReadOnly</c> (and under
    /// <see cref="GraceLimitedMode"/>=ReadOnly). Operationally important —
    /// payment recovery should usually continue even when the tenant
    /// cannot expand state.
    /// </summary>
    public bool AllowPaymentsInReadOnly { get; set; } = true;

    /// <summary>
    /// When true, <see cref="TenantBillingOperationCategory.StatementGenerate"/>
    /// is allowed under <c>ReadOnly</c> / <c>GraceLimited</c>=ReadOnly.
    /// Statement generation does not change the AR ledger, only renders /
    /// persists a snapshot of it, so most deployments leave this on.
    /// </summary>
    public bool AllowStatementsInReadOnly { get; set; } = true;

    /// <summary>
    /// When true, <see cref="TenantBillingOperationCategory.ExportWrite"/>
    /// is allowed under <c>ReadOnly</c>. Default is <c>false</c> — exports
    /// are external side-effects (QuickBooks, etc.) that operators usually
    /// want pinned off when entitlement is degraded.
    /// </summary>
    public bool AllowExportsInReadOnly { get; set; } = false;
}

/// <summary>
/// TB-ENF-01 — final policy decision returned by
/// <see cref="ITenantBillingAccessPolicy"/>. Carries everything the action
/// filter needs to either pass through or write a ProblemDetails 403.
/// </summary>
public sealed record TenantBillingEnforcementDecision(
    bool IsAllowed,
    TenantBillingOperationCategory Category,
    string AccessRecommendation,
    string EntitlementStatus,
    string Reason,
    int HttpStatus,
    string ProblemTitle,
    string ProblemDetail);

/// <summary>
/// TB-ENF-01 — applies the soft-enforcement matrix to one
/// (tenantId, category) pair. Pure read seam over the existing
/// <see cref="ITenantBillingEnablementResolver"/>; never mutates state and
/// never reaches Commerce.
/// </summary>
public interface ITenantBillingAccessPolicy
{
    /// <summary>Returns the decision without throwing.</summary>
    Task<TenantBillingEnforcementDecision> EvaluateAsync(
        Guid tenantId,
        TenantBillingOperationCategory category,
        CancellationToken ct = default);

    /// <summary>
    /// Convenience wrapper used by the action filter — same as
    /// <see cref="EvaluateAsync"/> but exposed under an explicit
    /// "authorize" name so the call site reads as an authorization check.
    /// </summary>
    Task<TenantBillingEnforcementDecision> AuthorizeAsync(
        Guid tenantId,
        TenantBillingOperationCategory category,
        CancellationToken ct = default);
}

/// <summary>
/// TB-ENF-01 — concrete policy. Reads the bound
/// <see cref="EntitlementEnforcementOptions"/> on every call (so a
/// runtime config reload through <see cref="Microsoft.Extensions.Options.IOptionsMonitor{T}"/>
/// would take effect without a restart, though the production path uses
/// IOptions today which is also live-bound).
/// </summary>
public sealed class TenantBillingAccessPolicy : ITenantBillingAccessPolicy
{
    private readonly ITenantBillingEnablementResolver _resolver;
    private readonly Func<EntitlementEnforcementOptions> _optionsAccessor;

    public TenantBillingAccessPolicy(
        ITenantBillingEnablementResolver resolver,
        Func<EntitlementEnforcementOptions> optionsAccessor)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _optionsAccessor = optionsAccessor ?? throw new ArgumentNullException(nameof(optionsAccessor));
    }

    public Task<TenantBillingEnforcementDecision> AuthorizeAsync(
        Guid tenantId, TenantBillingOperationCategory category, CancellationToken ct = default)
        => EvaluateAsync(tenantId, category, ct);

    public async Task<TenantBillingEnforcementDecision> EvaluateAsync(
        Guid tenantId, TenantBillingOperationCategory category, CancellationToken ct = default)
    {
        var opts = _optionsAccessor() ?? new EntitlementEnforcementOptions();

        // Master switch: enforcement OFF → allow everything. This is the
        // safe-rollout default and the path every regression test runs on.
        if (!opts.Enabled)
        {
            return Allow(category,
                accessRecommendation: TenantBillingAccessRecommendation.Allow,
                entitlementStatus: TenantBillingEntitlementStatus.Enabled,
                reason: "enforcement disabled");
        }

        // Always-allowed categories. Reads must never be blocked, and
        // internal admin endpoints (apply snapshot, profile lifecycle)
        // must remain reachable so an operator can recover from a
        // bad / missing entitlement state.
        switch (category)
        {
            case TenantBillingOperationCategory.Read:
            case TenantBillingOperationCategory.EntitlementAdmin:
            case TenantBillingOperationCategory.ProfileAdmin:
                return Allow(category,
                    accessRecommendation: TenantBillingAccessRecommendation.Allow,
                    entitlementStatus: TenantBillingEntitlementStatus.Enabled,
                    reason: "category always allowed");
        }

        // Tenant context is required for write evaluation. A missing
        // tenant id would only occur on an unscoped route that someone
        // accidentally attributed; defend by blocking.
        if (tenantId == Guid.Empty)
        {
            return Block(category,
                accessRecommendation: TenantBillingAccessRecommendation.Unknown,
                entitlementStatus: TenantBillingEntitlementStatus.Unknown,
                reason: "missing tenant context");
        }

        var decision = await _resolver.GetTenantBillingAccessAsync(tenantId, ct);
        var rec = NormalizeRecommendation(decision.AccessRecommendation);
        var status = decision.EntitlementStatus ?? TenantBillingEntitlementStatus.Unknown;

        // Map snapshot recommendation → effective enforcement mode using
        // the configured Unknown / GraceLimited modes.
        var effective = MapEffective(rec, status, opts, decision);

        return effective switch
        {
            EffectiveMode.Allow      => Allow(category, rec, status, decision.Reason ?? "ok"),
            EffectiveMode.GraceLimited => DecideGraceLimited(category, rec, status, decision.Reason, opts),
            EffectiveMode.ReadOnly   => DecideReadOnly(category, rec, status, decision.Reason, opts),
            EffectiveMode.Block      => Block(category, rec, status,
                                              decision.Reason ?? "tenant billing blocked"),
            _                        => Block(category, rec, status, "unknown enforcement mode"),
        };
    }

    private enum EffectiveMode { Allow, GraceLimited, ReadOnly, Block }

    private static EffectiveMode MapEffective(
        string rec, string status, EntitlementEnforcementOptions opts,
        TenantBillingAccessDecision decision)
    {
        // Outright Block always blocks regardless of profile/snapshot.
        if (rec == TenantBillingAccessRecommendation.Block)
            return EffectiveMode.Block;

        // Active profile + Allow snapshot → allow.
        if (decision.IsEnabled
            && rec == TenantBillingAccessRecommendation.Allow
            && status == TenantBillingEntitlementStatus.Enabled)
        {
            return EffectiveMode.Allow;
        }

        if (rec == TenantBillingAccessRecommendation.GraceLimited)
            return ParseMode(opts.GraceLimitedMode, EffectiveMode.GraceLimited);

        if (rec == TenantBillingAccessRecommendation.ReadOnly)
            return EffectiveMode.ReadOnly;

        // Unknown / missing-profile / missing-snapshot / non-Active profile.
        return ParseMode(opts.UnknownMode, EffectiveMode.ReadOnly);
    }

    private static EffectiveMode ParseMode(string raw, EffectiveMode fallback)
        => raw?.Trim().ToLowerInvariant() switch
        {
            "block"    => EffectiveMode.Block,
            "readonly" => EffectiveMode.ReadOnly,
            _          => fallback,
        };

    private static TenantBillingEnforcementDecision DecideGraceLimited(
        TenantBillingOperationCategory category, string rec, string status,
        string? reason, EntitlementEnforcementOptions opts)
    {
        // GraceLimited preserves payment recovery + statement reads, but
        // blocks new write expansion (customers, invoices, templates,
        // exports). PaymentWrite always allowed. StatementGenerate
        // honours the AllowStatementsInReadOnly flag.
        var msg = reason ?? "tenant billing is grace-limited";
        return category switch
        {
            TenantBillingOperationCategory.PaymentWrite      => Allow(category, rec, status, msg),
            TenantBillingOperationCategory.StatementGenerate =>
                opts.AllowStatementsInReadOnly
                    ? Allow(category, rec, status, msg)
                    : Block(category, rec, status, msg),
            _ => Block(category, rec, status, msg),
        };
    }

    private static TenantBillingEnforcementDecision DecideReadOnly(
        TenantBillingOperationCategory category, string rec, string status,
        string? reason, EntitlementEnforcementOptions opts)
    {
        var msg = reason ?? "tenant billing is read-only for this tenant";
        return category switch
        {
            TenantBillingOperationCategory.PaymentWrite =>
                opts.AllowPaymentsInReadOnly
                    ? Allow(category, rec, status, msg)
                    : Block(category, rec, status, msg),
            TenantBillingOperationCategory.StatementGenerate =>
                opts.AllowStatementsInReadOnly
                    ? Allow(category, rec, status, msg)
                    : Block(category, rec, status, msg),
            TenantBillingOperationCategory.ExportWrite =>
                opts.AllowExportsInReadOnly
                    ? Allow(category, rec, status, msg)
                    : Block(category, rec, status, msg),
            _ => Block(category, rec, status, msg),
        };
    }

    private static string NormalizeRecommendation(string? raw)
        => TenantBillingAccessRecommendation.IsValid(raw)
            ? raw!
            : TenantBillingAccessRecommendation.Unknown;

    private static TenantBillingEnforcementDecision Allow(
        TenantBillingOperationCategory category, string accessRecommendation,
        string entitlementStatus, string reason)
        => new(IsAllowed: true,
               Category: category,
               AccessRecommendation: accessRecommendation,
               EntitlementStatus: entitlementStatus,
               Reason: reason,
               HttpStatus: 200,
               ProblemTitle: string.Empty,
               ProblemDetail: string.Empty);

    private static TenantBillingEnforcementDecision Block(
        TenantBillingOperationCategory category, string accessRecommendation,
        string entitlementStatus, string reason)
        => new(IsAllowed: false,
               Category: category,
               AccessRecommendation: accessRecommendation,
               EntitlementStatus: entitlementStatus,
               Reason: reason,
               HttpStatus: 403,
               ProblemTitle: "Tenant Billing access is restricted",
               ProblemDetail: BuildProblemDetail(category, accessRecommendation));

    private static string BuildProblemDetail(
        TenantBillingOperationCategory category, string rec)
        => rec switch
        {
            TenantBillingAccessRecommendation.Block        => "Tenant billing is currently blocked for this tenant.",
            TenantBillingAccessRecommendation.ReadOnly     => "Tenant billing is currently read-only for this tenant.",
            TenantBillingAccessRecommendation.GraceLimited => "Tenant billing is currently grace-limited for this tenant.",
            _                                              => "Tenant billing is not enabled for this tenant.",
        };
}
