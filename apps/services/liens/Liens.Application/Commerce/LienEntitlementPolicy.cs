using BuildingBlocks.Commerce;
using BuildingBlocks.Context;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Liens.Application.Commerce;

/// <summary>
/// Evaluates whether the current tenant is commercially entitled to use
/// the SynqLien product via the Commerce ecosystem integration.
///
/// <para>
/// This is an opt-in helper — endpoints do not automatically call this policy.
/// Inject and call <see cref="EvaluateAsync"/> in high-value endpoints (e.g.
/// lien creation, marketplace access) when Commerce enforcement is desired.
/// </para>
///
/// <para>
/// <b>Safety guarantees:</b>
/// <list type="bullet">
///   <item><description>
///     When Commerce integration is disabled (<c>Enabled=false</c>): always returns
///     <see cref="LienEntitlementDecision.Permit"/> — no HTTP calls made.
///   </description></item>
///   <item><description>
///     When Commerce is unavailable (HTTP error, timeout, tenant not found):
///     always returns <see cref="LienEntitlementDecision.Permit"/> — permissive fallback.
///   </description></item>
///   <item><description>
///     When <c>EnforcementEnabled=false</c> (default): fetches entitlement and
///     logs the result, but always returns <see cref="LienEntitlementDecision.Permit"/>.
///   </description></item>
///   <item><description>
///     When <c>EnforcementEnabled=true</c>: access recommendation of <c>"Block"</c>
///     returns <see cref="LienEntitlementDecision.Deny"/>; all others permit.
///   </description></item>
/// </list>
/// </para>
/// </summary>
public sealed class LienEntitlementPolicy
{
    private readonly ICommerceEntitlementClient             _entitlementClient;
    private readonly CommerceIntegrationOptions             _options;
    private readonly ICurrentRequestContext                 _requestContext;
    private readonly ILogger<LienEntitlementPolicy>         _logger;

    private const string ProductKey     = "SYNQLIEN";
    private const string HostPlatformKey = "legalsynq";

    public LienEntitlementPolicy(
        ICommerceEntitlementClient           entitlementClient,
        IOptions<CommerceIntegrationOptions> options,
        ICurrentRequestContext               requestContext,
        ILogger<LienEntitlementPolicy>       logger)
    {
        _entitlementClient = entitlementClient;
        _options           = options.Value;
        _requestContext    = requestContext;
        _logger            = logger;
    }

    /// <summary>
    /// Evaluates the Commerce entitlement for the current request's tenant.
    /// Never throws.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="LienEntitlementDecision"/> with reason.</returns>
    public async Task<LienEntitlementDecision> EvaluateAsync(CancellationToken ct = default)
    {
        // Noop path — Commerce integration disabled
        if (!_options.Enabled)
        {
            return LienEntitlementDecision.Permit("Commerce integration disabled — permissive fallback.");
        }

        var tenantId = _requestContext.TenantId?.ToString();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            _logger.LogDebug(
                "LienEntitlementPolicy: no tenant ID in request context — permissive fallback.");
            return LienEntitlementDecision.Permit("No tenant context — permissive fallback.");
        }

        CommerceEntitlementResult result;
        try
        {
            result = await _entitlementClient
                .GetByHostTenantAsync(HostPlatformKey, tenantId, ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "LienEntitlementPolicy: unexpected exception fetching entitlement for tenant {TenantId} — permissive fallback.",
                tenantId);
            return LienEntitlementDecision.Permit("Entitlement check exception — permissive fallback.");
        }

        // Commerce unavailable (disabled, tenant not found, HTTP error)
        if (!result.IsAvailable)
        {
            _logger.LogDebug(
                "LienEntitlementPolicy: entitlement unavailable for tenant {TenantId} ({Reason}) — permissive fallback.",
                tenantId, result.ErrorMessage ?? "unknown");
            return LienEntitlementDecision.Permit($"Entitlement unavailable — permissive fallback. ({result.ErrorMessage})");
        }

        // Log entitlement details for visibility regardless of enforcement mode
        _logger.LogDebug(
            "LienEntitlementPolicy: tenant {TenantId} recommendation={Recommendation} standing={Standing} products=[{Products}] enforcement={EnforcementEnabled}",
            tenantId,
            result.AccessRecommendation,
            result.AccountStandingStatus,
            string.Join(",", result.ProductKeys),
            _options.EnforcementEnabled);

        // Non-enforcing path — fetch + log, always permit
        if (!_options.EnforcementEnabled)
        {
            return LienEntitlementDecision.Permit(
                $"Entitlement check passed (enforcement disabled). Recommendation={result.AccessRecommendation}");
        }

        // Enforcing path — evaluate access recommendation
        return result.AccessRecommendation switch
        {
            CommerceAccessRecommendationValues.Allow =>
                LienEntitlementDecision.Permit($"Commerce allows access. Standing={result.AccountStandingStatus}"),

            CommerceAccessRecommendationValues.GraceLimited =>
                LienEntitlementDecision.Permit(
                    $"Commerce grace-limited access permitted. Standing={result.AccountStandingStatus}"),

            CommerceAccessRecommendationValues.ReadOnly =>
                LienEntitlementDecision.Permit(
                    $"Commerce read-only recommendation — full access permitted in current configuration. Standing={result.AccountStandingStatus}"),

            CommerceAccessRecommendationValues.Block =>
                LienEntitlementDecision.Deny(
                    $"Commerce denies access. Standing={result.AccountStandingStatus} Reason={result.AccountStandingReason}"),

            _ =>
                LienEntitlementDecision.Permit(
                    $"Unknown Commerce recommendation '{result.AccessRecommendation}' — permissive fallback.")
        };
    }
}

/// <summary>
/// Decision returned by <see cref="LienEntitlementPolicy.EvaluateAsync"/>.
/// </summary>
public sealed record LienEntitlementDecision(
    bool    IsPermitted,
    string  Reason)
{
    /// <summary>Access permitted.</summary>
    public static LienEntitlementDecision Permit(string reason) => new(true, reason);

    /// <summary>Access denied.</summary>
    public static LienEntitlementDecision Deny(string reason) => new(false, reason);
}
