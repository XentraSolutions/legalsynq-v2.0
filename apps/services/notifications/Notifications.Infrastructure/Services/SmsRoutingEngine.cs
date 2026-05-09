using System.Text.Json;
using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;
using Notifications.Application.Options;
using Notifications.Domain;
using Microsoft.Extensions.Options;

namespace Notifications.Infrastructure.Services;

/// <summary>
/// LS-NOTIF-SMS-014: SMS routing engine.
/// Selects the best provider route from a candidate list based on the active routing policy.
///
/// Routing modes:
///   priority         — preserve existing ProviderRoutingService route order (backward compat)
///   cost_optimized   — select lowest estimated cost provider; fallback to priority
///   health_optimized — skip providers with health_status = "down"; fallback to priority
///   hybrid           — health gate first, then cost, then priority
///   regional         — prefer providers matching country/region; fallback to priority
///
/// NEVER calls external providers. Uses only locally persisted health/cost config data.
/// Persists routing decision to DB (caller handles persistence using returned result).
/// </summary>
public class SmsRoutingEngine : ISmsRoutingEngine
{
    private readonly ISmsRoutingPolicyRepository _policyRepo;
    private readonly IProviderHealthRepository   _healthRepo;
    private readonly SmsCostAnalyticsOptions     _costOptions;
    private readonly ILogger<SmsRoutingEngine>   _logger;

    private static readonly HashSet<string> ValidModes = new(StringComparer.OrdinalIgnoreCase)
    {
        "priority", "cost_optimized", "health_optimized", "hybrid", "regional"
    };

    public SmsRoutingEngine(
        ISmsRoutingPolicyRepository policyRepo,
        IProviderHealthRepository healthRepo,
        IOptions<SmsCostAnalyticsOptions> costOptions,
        ILogger<SmsRoutingEngine> logger)
    {
        _policyRepo  = policyRepo;
        _healthRepo  = healthRepo;
        _costOptions = costOptions.Value;
        _logger      = logger;
    }

    public async Task<SmsRoutingDecisionResult> SelectRouteAsync(
        SmsRoutingRequest request,
        CancellationToken ct = default)
    {
        var candidates = request.CandidateRoutes.ToList();

        if (candidates.Count == 0)
        {
            _logger.LogDebug("SmsRoutingEngine: no candidate routes for tenant {TenantId}", request.TenantId);
            return SmsRoutingDecisionResult.NoRoute("priority", Array.Empty<string>(), "no_candidate_routes");
        }

        var candidateProviders = candidates.Select(r => r.ProviderType).ToList();

        // Resolve active routing policy (tenant-specific first, then global)
        var policy = await ResolvePolicy(request.TenantId, ct);
        var mode   = policy?.Enabled == true ? (policy.RoutingMode ?? "priority") : "priority";
        if (!ValidModes.Contains(mode)) mode = "priority";

        _logger.LogDebug(
            "SmsRoutingEngine: tenant={TenantId}, mode={Mode}, policy={PolicyId}, candidates=[{Candidates}]",
            request.TenantId, mode, policy?.Id, string.Join(",", candidateProviders));

        // Apply excluded providers from policy
        var excluded = new List<string>();
        if (policy != null && !string.IsNullOrEmpty(policy.ExcludedProvidersJson))
        {
            try
            {
                var ex = JsonSerializer.Deserialize<List<string>>(policy.ExcludedProvidersJson);
                if (ex != null) excluded.AddRange(ex.Select(p => p.ToLowerInvariant()));
            }
            catch { /* ignore malformed JSON */ }
        }

        var filtered = candidates
            .Where(r => !excluded.Contains(r.ProviderType.ToLowerInvariant()))
            .ToList();

        if (filtered.Count == 0)
        {
            return SmsRoutingDecisionResult.NoRoute(mode, candidateProviders,
                "all_candidates_excluded_by_policy");
        }

        // Apply preferred order from policy (re-sort, don't remove)
        if (policy != null && !string.IsNullOrEmpty(policy.PreferredProvidersJson))
        {
            try
            {
                var preferred = JsonSerializer.Deserialize<List<string>>(policy.PreferredProvidersJson)
                    ?? new List<string>();
                filtered = ApplyPreferredOrder(filtered, preferred);
            }
            catch { /* ignore malformed JSON */ }
        }

        // Apply cost cap from policy
        decimal? maxCost = policy?.MaxEstimatedCostPerMessage;
        if (maxCost.HasValue)
        {
            filtered = filtered.Where(r =>
            {
                var est = _costOptions.GetEstimatedCost(r.ProviderType);
                return !est.HasValue || est.Value <= maxCost.Value;
            }).ToList();

            if (filtered.Count == 0)
            {
                return SmsRoutingDecisionResult.NoRoute(mode, candidateProviders,
                    "all_candidates_exceed_max_cost_policy");
            }
        }

        // Mode-specific selection
        ProviderRoute? selected;
        string decisionReason;

        switch (mode.ToLowerInvariant())
        {
            case "cost_optimized":
                (selected, decisionReason) = SelectCostOptimized(filtered);
                break;

            case "health_optimized":
                (selected, decisionReason) = await SelectHealthOptimizedAsync(
                    filtered, policy?.RequireHealthyProvider ?? false, ct);
                break;

            case "hybrid":
                (selected, decisionReason) = await SelectHybridAsync(
                    filtered, policy?.RequireHealthyProvider ?? false, ct);
                break;

            case "regional":
                (selected, decisionReason) = SelectRegional(filtered, request.CountryCode, request.Region);
                break;

            default: // "priority"
                selected       = filtered[0];
                decisionReason = "priority_first_candidate";
                break;
        }

        if (selected == null)
        {
            return SmsRoutingDecisionResult.NoRoute(mode, candidateProviders, decisionReason);
        }

        var estimatedCost = _costOptions.GetEstimatedCost(selected.ProviderType);

        return new SmsRoutingDecisionResult
        {
            Success                  = true,
            SelectedRoute            = selected,
            RoutingMode              = mode,
            SelectedProvider         = selected.ProviderType,
            SelectedProviderConfigId = selected.TenantProviderConfigId,
            ProviderOwnershipMode    = selected.OwnershipMode,
            DecisionReason           = decisionReason,
            CandidateProviders       = candidateProviders,
            ExcludedProviders        = excluded,
            MatchedPolicyId          = policy?.Id,
            EstimatedCostAmount      = estimatedCost,
            CostCurrency             = estimatedCost.HasValue ? _costOptions.DefaultCurrency : null,
            CountryCode              = request.CountryCode,
            Region                   = request.Region,
        };
    }

    // ── Routing mode implementations ──────────────────────────────────────────

    private (ProviderRoute? route, string reason) SelectCostOptimized(List<ProviderRoute> candidates)
    {
        // Select route with lowest estimated cost. If no estimates available, fallback to priority.
        var withCost = candidates
            .Select(r => (Route: r, Cost: _costOptions.GetEstimatedCost(r.ProviderType)))
            .Where(x => x.Cost.HasValue)
            .OrderBy(x => x.Cost!.Value)
            .ToList();

        if (withCost.Count > 0)
            return (withCost[0].Route, $"cost_optimized_cheapest_{withCost[0].Route.ProviderType}_{withCost[0].Cost:F4}");

        // Fallback: no cost estimates available
        _logger.LogDebug("SmsRoutingEngine: cost_optimized — no estimates available, fallback to priority");
        return (candidates[0], "cost_optimized_fallback_priority_no_estimates");
    }

    private async Task<(ProviderRoute? route, string reason)> SelectHealthOptimizedAsync(
        List<ProviderRoute> candidates,
        bool requireHealthy,
        CancellationToken ct)
    {
        // Filter out "down" providers using locally persisted health data.
        var healthy = new List<ProviderRoute>();
        foreach (var route in candidates)
        {
            var health = await _healthRepo.FindByProviderAsync(
                route.ProviderType, "sms",
                route.OwnershipMode,
                route.TenantProviderConfigId);

            if (health?.HealthStatus == "down")
            {
                _logger.LogDebug("SmsRoutingEngine: health_optimized — skipping {Provider} (down)", route.ProviderType);
                continue;
            }
            healthy.Add(route);
        }

        if (healthy.Count > 0)
            return (healthy[0], $"health_optimized_first_healthy_{healthy[0].ProviderType}");

        if (requireHealthy)
            return (null, "no_healthy_provider");

        // Fallback: all degraded/unknown — use priority anyway
        _logger.LogDebug("SmsRoutingEngine: health_optimized — all providers unhealthy, fallback to priority");
        return (candidates[0], "health_optimized_fallback_priority_all_degraded");
    }

    private async Task<(ProviderRoute? route, string reason)> SelectHybridAsync(
        List<ProviderRoute> candidates,
        bool requireHealthy,
        CancellationToken ct)
    {
        // Step 1: Health gate (exclude "down")
        var healthy = new List<ProviderRoute>();
        foreach (var route in candidates)
        {
            var health = await _healthRepo.FindByProviderAsync(
                route.ProviderType, "sms",
                route.OwnershipMode,
                route.TenantProviderConfigId);

            if (health?.HealthStatus != "down")
                healthy.Add(route);
        }

        var pool = healthy.Count > 0 ? healthy : (requireHealthy ? new List<ProviderRoute>() : candidates);

        if (pool.Count == 0)
            return (null, "no_healthy_provider");

        // Step 2: Cost optimization within health-filtered pool
        var withCost = pool
            .Select(r => (Route: r, Cost: _costOptions.GetEstimatedCost(r.ProviderType)))
            .Where(x => x.Cost.HasValue)
            .OrderBy(x => x.Cost!.Value)
            .ToList();

        if (withCost.Count > 0)
            return (withCost[0].Route, $"hybrid_healthy_cheapest_{withCost[0].Route.ProviderType}");

        // Step 3: Priority fallback
        return (pool[0], $"hybrid_healthy_priority_fallback_{pool[0].ProviderType}");
    }

    private (ProviderRoute? route, string reason) SelectRegional(
        List<ProviderRoute> candidates,
        string? countryCode,
        string? region)
    {
        // Regional routing requires country code derivation (not yet implemented).
        // Fall back to priority with documented reason.
        if (string.IsNullOrEmpty(countryCode) && string.IsNullOrEmpty(region))
        {
            _logger.LogDebug("SmsRoutingEngine: regional — no country/region data, fallback to priority");
            return (candidates[0], "regional_fallback_no_country_data");
        }

        // When country code is available: prefer providers whose name matches a regional prefix.
        // This is a stub — real regional routing requires provider regional config/metadata.
        return (candidates[0], $"regional_fallback_priority_{countryCode ?? region}");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static List<ProviderRoute> ApplyPreferredOrder(
        List<ProviderRoute> routes,
        IReadOnlyList<string> preferredOrder)
    {
        var preferred = new List<ProviderRoute>();
        var remaining = routes.ToList();

        foreach (var p in preferredOrder)
        {
            var match = remaining.FirstOrDefault(r =>
                string.Equals(r.ProviderType, p, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                preferred.Add(match);
                remaining.Remove(match);
            }
        }

        preferred.AddRange(remaining);
        return preferred;
    }

    private async Task<SmsRoutingPolicy?> ResolvePolicy(Guid tenantId, CancellationToken ct)
    {
        try
        {
            var tenantPolicies = await _policyRepo.GetActiveForTenantAsync(tenantId, ct);
            // Tenant-specific takes precedence (lowest Priority value first)
            var tenantSpecific = tenantPolicies
                .Where(p => p.TenantId == tenantId)
                .OrderBy(p => p.Priority)
                .FirstOrDefault();
            if (tenantSpecific != null) return tenantSpecific;

            // Global policy fallback (TenantId == null)
            var global = tenantPolicies
                .Where(p => p.TenantId == null)
                .OrderBy(p => p.Priority)
                .FirstOrDefault();
            return global;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SmsRoutingEngine: failed to load routing policy for tenant {TenantId} — using priority mode", tenantId);
            return null;
        }
    }
}
