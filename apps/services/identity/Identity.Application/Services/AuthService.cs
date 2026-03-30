using System.Security.Claims;
using Identity.Application.DTOs;
using Identity.Application.Interfaces;
using Identity.Domain;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository userRepository,
        ITenantRepository tenantRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _tenantRepository = tenantRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _logger = logger;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var tenant = await _tenantRepository.GetByCodeAsync(request.TenantCode.ToUpperInvariant().Trim(), ct);
        if (tenant is null || !tenant.IsActive)
            throw new UnauthorizedAccessException();

        var normalizedEmail = request.Email.ToLowerInvariant().Trim();
        var user = await _userRepository.GetByTenantAndEmailAsync(tenant.Id, normalizedEmail, ct);
        if (user is null || !user.IsActive)
            throw new UnauthorizedAccessException();

        var valid = _passwordHasher.Verify(request.Password, user.PasswordHash);
        if (!valid)
            throw new UnauthorizedAccessException();

        var userWithRoles = await _userRepository.GetByIdWithRolesAsync(user.Id, ct)
            ?? throw new UnauthorizedAccessException();

        // Primary role source: UserRoles (flat user→role mapping)
        var roleNames = userWithRoles.UserRoles.Select(ur => ur.Role.Name).ToList();

        // Phase 4: merge GLOBAL-scoped ScopedRoleAssignments into role names (additive).
        // ORGANIZATION/PRODUCT/RELATIONSHIP-scoped assignments are intentionally excluded
        // here; they will be used for fine-grained authorization in future phases.
        var scopedGlobalRoles = userWithRoles.ScopedRoleAssignments
            .Where(s => s.IsActive && s.ScopeType == Domain.ScopedRoleAssignment.ScopeTypes.Global)
            .Select(s => s.Role.Name)
            .ToList();

        foreach (var r in scopedGlobalRoles)
            if (!roleNames.Contains(r, StringComparer.OrdinalIgnoreCase))
                roleNames.Add(r);

        // Load org membership and compute product roles
        var orgMembership = await _userRepository.GetPrimaryOrgMembershipAsync(user.Id, ct);
        var org = orgMembership?.Organization;

        List<string> productRoles = [];
        if (org is not null)
        {
            // Phase 3: resolve product roles via ProductOrganizationTypeRule (DB-backed).
            // Track which eligibility path was used for each role for observability.
            int dbRuleCount      = 0;
            int legacyCount      = 0;
            int unrestrictedCount = 0;

            var eligibleCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var op in org.OrganizationProducts.Where(op => op.IsEnabled))
            {
                foreach (var pr in op.Product.ProductRoles.Where(pr => pr.IsActive))
                {
                    var (eligible, path) = IsEligibleWithPath(pr, org);
                    if (!eligible) continue;

                    if (eligibleCodes.Add(pr.Code))
                    {
                        switch (path)
                        {
                            case EligibilityPath.DbRule:       dbRuleCount++;       break;
                            case EligibilityPath.LegacyString: legacyCount++;       break;
                            case EligibilityPath.Unrestricted: unrestrictedCount++; break;
                        }
                    }
                }
            }

            productRoles = eligibleCodes.OrderBy(c => c).ToList();

            // Log eligibility resolution summary — once per login, structured, non-noisy.
            _logger.LogDebug(
                "Product role eligibility resolved for user={UserId} org={OrgId}: " +
                "{TotalRoles} role(s) — DB-rule={DbRule}, legacy-string={Legacy}, unrestricted={Unrestricted}.",
                user.Id, org.Id, productRoles.Count, dbRuleCount, legacyCount, unrestrictedCount);

            if (legacyCount > 0)
            {
                _logger.LogInformation(
                    "Legacy EligibleOrgType fallback used for {Count} product role(s) during login " +
                    "for user={UserId} org={OrgId} orgType={OrgType}. " +
                    "Seed ProductOrganizationTypeRule rows to remove this fallback.",
                    legacyCount, user.Id, org.Id, org.OrgType);
            }
        }

        var (token, expiresAtUtc) = _jwtTokenService.GenerateToken(
            userWithRoles, tenant, roleNames, org, productRoles);

        var userResponse = new UserResponse(
            userWithRoles.Id,
            userWithRoles.TenantId,
            userWithRoles.Email,
            userWithRoles.FirstName,
            userWithRoles.LastName,
            userWithRoles.IsActive,
            roleNames,
            org?.Id,
            org?.OrgType,
            productRoles);

        return new LoginResponse(token, expiresAtUtc, userResponse);
    }

    /// <summary>
    /// Assembles an AuthMeResponse from a validated ClaimsPrincipal.
    /// All fields come from JWT claims — no DB query required.
    /// The Identity service validates the token signature; this method reads the payload.
    /// </summary>
    public Task<AuthMeResponse> GetCurrentUserAsync(ClaimsPrincipal principal, CancellationToken ct = default)
    {
        var userId     = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub")
            ?? throw new InvalidOperationException("sub claim missing");
        var email      = principal.FindFirstValue(ClaimTypes.Email)
            ?? principal.FindFirstValue("email")
            ?? string.Empty;
        var tenantId   = principal.FindFirstValue("tenant_id")   ?? string.Empty;
        var tenantCode = principal.FindFirstValue("tenant_code") ?? string.Empty;
        var orgId      = principal.FindFirstValue("org_id");
        var orgType    = principal.FindFirstValue("org_type");

        var productRoles = principal.FindAll("product_roles")
            .Select(c => c.Value)
            .ToList();

        var systemRoles = principal.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        // Derive expiry from the "exp" claim (Unix epoch seconds)
        var expClaim    = principal.FindFirstValue("exp");
        var expiresAtUtc = expClaim is not null && long.TryParse(expClaim, out var expUnix)
            ? DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime
            : DateTime.UtcNow.AddMinutes(60);

        var response = new AuthMeResponse(
            UserId:       userId,
            Email:        email,
            TenantId:     tenantId,
            TenantCode:   tenantCode,
            OrgId:        orgId,
            OrgType:      orgType,
            OrgName:      null,  // Phase 2: DB lookup by orgId for DisplayName ?? Name
            ProductRoles: productRoles,
            SystemRoles:  systemRoles,
            ExpiresAtUtc: expiresAtUtc);

        return Task.FromResult(response);
    }

    // ── Eligibility helpers ───────────────────────────────────────────────────

    private enum EligibilityPath { DbRule, LegacyString, Unrestricted }

    /// <summary>
    /// Returns whether the given product role is eligible for the organization,
    /// and which eligibility path was used (for observability/logging).
    ///
    /// Resolution order (most specific → least specific):
    ///   1. Phase 3 DB-backed rule table via OrgTypeRules nav property.
    ///      - If org.OrganizationTypeId is set: match by ID (authoritative, no string drift).
    ///      - Else: match by OrgType code string (transitional fallback within Phase 3).
    ///   2. Legacy EligibleOrgType string on ProductRole (pre-Phase 3 data).
    ///      - TODO [LEGACY]: retire when all ProductRoles have OrgTypeRules seeded.
    ///   3. No restriction configured → allow (unrestricted).
    /// </summary>
    private static (bool Eligible, EligibilityPath Path) IsEligibleWithPath(ProductRole pr, Organization org)
    {
        // Path 1: DB-backed rule table
        if (pr.OrgTypeRules is { Count: > 0 })
        {
            var matched = pr.OrgTypeRules.Any(r =>
            {
                if (!r.IsActive || r.OrganizationType is null) return false;
                if (org.OrganizationTypeId.HasValue)
                    return r.OrganizationTypeId == org.OrganizationTypeId.Value;
                return r.OrganizationType.Code == org.OrgType;
            });
            return (matched, EligibilityPath.DbRule);
        }

        // Path 2: legacy EligibleOrgType string
        // TODO [LEGACY — Phase F]: retire once all ProductRoles have OrgTypeRules seeded
        //   and all Organizations have OrganizationTypeId backfilled.
        if (pr.EligibleOrgType is not null)
            return (pr.EligibleOrgType == org.OrgType, EligibilityPath.LegacyString);

        // Path 3: no restriction configured — unrestricted
        return (true, EligibilityPath.Unrestricted);
    }
}
