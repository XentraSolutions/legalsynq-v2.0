using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;
using Notifications.Application.Options;
using Notifications.Domain;
using Notifications.Infrastructure.Data;

namespace Notifications.Infrastructure.Services;

/// <summary>
/// LS-NOTIF-SMS-019: Rule pack / compliance profile inheritance resolver.
///
/// Resolution order:
/// 1. Load global active packs (TenantId = null, status = active, not expired, enabled).
/// 2. Load tenant-specific active packs (status = active, not expired, enabled).
/// 3. Apply InheritanceMode from each tenant pack:
///    - merge       → global rules + tenant rules, all sorted by priority
///    - override    → tenant rules replace global rules with same RuleType
///    - append_only → global rules first (intact), tenant rules appended
/// 4. Resolve compliance profile assignment for tenant → apply EnforcementMode.
/// 5. Trim to MaxRulesPerEvaluation.
///
/// Failures fail-open: returns empty resolution (no rules evaluated).
/// </summary>
public sealed class SmsGovernanceRuleResolver : ISmsGovernanceRuleResolver
{
    private readonly NotificationsDbContext          _db;
    private readonly SmsGovernanceDynamicOptions     _options;
    private readonly ILogger<SmsGovernanceRuleResolver> _logger;

    public SmsGovernanceRuleResolver(
        NotificationsDbContext                   db,
        Microsoft.Extensions.Options.IOptions<SmsGovernanceDynamicOptions> options,
        ILogger<SmsGovernanceRuleResolver>       logger)
    {
        _db      = db;
        _options = options.Value;
        _logger  = logger;
    }

    public async Task<SmsGovernanceRuleResolution> ResolveRulesAsync(
        Guid? tenantId,
        string? context    = null,
        CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return Empty();

        try
        {
            var nowUtc   = DateTime.UtcNow;
            var packList = await LoadActivePacksAsync(tenantId, nowUtc, ct);

            if (packList.Count == 0)
                return Empty();

            var packIds = packList.Select(p => p.Id).ToList();

            var allRules = await _db.SmsGovernanceRules
                .AsNoTracking()
                .Where(r => packIds.Contains(r.RulePackId) && r.Enabled)
                .OrderBy(r => r.Priority)
                .Take(_options.MaxRulesPerEvaluation)
                .ToListAsync(ct);

            if (allRules.Count == 0)
                return Empty(packList);

            // Resolve InheritanceMode per tenant pack
            var globalPacks = packList.Where(p => p.TenantId == null).ToList();
            var tenantPacks = packList.Where(p => p.TenantId != null).ToList();

            List<SmsGovernanceRule> resolvedRules;

            if (tenantPacks.Count == 0)
            {
                resolvedRules = allRules;
            }
            else
            {
                var globalRules = allRules.Where(r => globalPacks.Any(p => p.Id == r.RulePackId)).ToList();
                var tenantRules = allRules.Where(r => tenantPacks.Any(p => p.Id == r.RulePackId)).ToList();

                // Use the inheritance mode from the first (highest-priority) tenant pack
                var primaryMode = tenantPacks.OrderBy(p => p.Priority).First().InheritanceMode;

                resolvedRules = primaryMode switch
                {
                    "override"    => ResolveOverride(globalRules, tenantRules),
                    "append_only" => [.. globalRules.OrderBy(r => r.Priority),
                                      .. tenantRules.OrderBy(r => r.Priority)],
                    _             => [.. globalRules.Concat(tenantRules).OrderBy(r => r.Priority)], // merge
                };
            }

            // Resolve compliance profile
            var (enforcementMode, profileAssigned) = await ResolveEnforcementModeAsync(tenantId, ct);

            return new SmsGovernanceRuleResolution
            {
                Rules            = resolvedRules,
                Packs            = packList,
                EnforcementMode  = enforcementMode,
                ProfileAssigned  = profileAssigned,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SmsGovernanceRuleResolver: ResolveRulesAsync failed for tenant {TenantId} — failing open",
                tenantId);
            return Empty();
        }
    }

    public async Task<IReadOnlyList<SmsGovernanceRulePack>> ResolveRulePacksAsync(
        Guid? tenantId,
        CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return [];

        try
        {
            return await LoadActivePacksAsync(tenantId, DateTime.UtcNow, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SmsGovernanceRuleResolver: ResolveRulePacksAsync failed for tenant {TenantId}",
                tenantId);
            return [];
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private async Task<List<SmsGovernanceRulePack>> LoadActivePacksAsync(
        Guid? tenantId, DateTime nowUtc, CancellationToken ct)
    {
        return await _db.SmsGovernanceRulePacks
            .AsNoTracking()
            .Where(p =>
                p.Enabled &&
                p.Status == "active" &&
                (p.TenantId == null || p.TenantId == tenantId) &&
                (p.EffectiveFrom == null || p.EffectiveFrom <= nowUtc) &&
                (p.EffectiveTo   == null || p.EffectiveTo   >= nowUtc))
            .OrderBy(p => p.Priority)
            .ToListAsync(ct);
    }

    /// <summary>
    /// override mode: tenant rules replace global rules that share the same RuleType.
    /// Global rule types not covered by any tenant rule are preserved.
    /// </summary>
    private static List<SmsGovernanceRule> ResolveOverride(
        List<SmsGovernanceRule> globalRules,
        List<SmsGovernanceRule> tenantRules)
    {
        var overriddenTypes = tenantRules.Select(r => r.RuleType).ToHashSet();
        var kept = globalRules.Where(r => !overriddenTypes.Contains(r.RuleType)).ToList();
        return [.. kept.Concat(tenantRules).OrderBy(r => r.Priority)];
    }

    private async Task<(string enforcementMode, bool profileAssigned)> ResolveEnforcementModeAsync(
        Guid? tenantId, CancellationToken ct)
    {
        if (tenantId == null) return ("standard", false);

        try
        {
            var assignment = await _db.SmsComplianceProfileAssignments
                .AsNoTracking()
                .Where(a => a.TenantId == tenantId && a.Scope == "tenant" && a.Enabled)
                .FirstOrDefaultAsync(ct);

            if (assignment == null) return ("standard", false);

            var profile = await _db.SmsComplianceProfiles
                .AsNoTracking()
                .Where(p => p.Id == assignment.ProfileId && p.Enabled)
                .FirstOrDefaultAsync(ct);

            return profile == null
                ? ("standard", false)
                : (profile.EnforcementMode, true);
        }
        catch
        {
            return ("standard", false);
        }
    }

    private static SmsGovernanceRuleResolution Empty(
        List<SmsGovernanceRulePack>? packs = null) =>
        new()
        {
            Rules           = [],
            Packs           = packs ?? [],
            EnforcementMode = "standard",
            ProfileAssigned = false,
        };
}
