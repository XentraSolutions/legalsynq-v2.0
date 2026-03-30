using System.Security.Claims;
using System.Text.Json;
using Identity.Application.DTOs;
using Identity.Application.Interfaces;
using Identity.Domain;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;
using LegalSynq.AuditClient.Enums;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IAuditEventClient _auditClient;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository userRepository,
        ITenantRepository tenantRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IAuditEventClient auditClient,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _tenantRepository = tenantRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _auditClient = auditClient;
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

        // Phase G: ScopedRoleAssignments (GLOBAL) is the sole authoritative role source.
        // UserRoles table has been dropped (migration 20260330200004).
        var roleNames = userWithRoles.ScopedRoleAssignments
            .Where(s => s.IsActive && s.ScopeType == Domain.ScopedRoleAssignment.ScopeTypes.Global)
            .Select(s => s.Role.Name)
            .ToList();

        // Load org membership and compute product roles
        var orgMembership = await _userRepository.GetPrimaryOrgMembershipAsync(user.Id, ct);
        var org = orgMembership?.Organization;

        List<string> productRoles = [];
        if (org is not null)
        {
            // Phase I: warn when the OrgType string fallback path will be taken during
            // product-role eligibility checks.  After migration 200005 this should never
            // fire; if it does, the admin should re-run the backfill migration.
            if (!org.OrganizationTypeId.HasValue)
                _logger.LogWarning(
                    "Organization {OrgId} has no OrganizationTypeId set; " +
                    "product-role eligibility will use the OrgType string fallback. " +
                    "Run migration 200005 (PhaseI_BackfillOrganizationTypeId) to resolve.",
                    org.Id);

            // Phase F: resolve product roles exclusively via ProductOrganizationTypeRule (DB-backed).
            // EligibleOrgType legacy fallback removed — see migration 20260330200003.
            int dbRuleCount       = 0;
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
                            case EligibilityPath.Unrestricted: unrestrictedCount++; break;
                        }
                    }
                }
            }

            productRoles = eligibleCodes.OrderBy(c => c).ToList();

            // Log eligibility resolution summary — once per login, structured, non-noisy.
            _logger.LogDebug(
                "Product role eligibility resolved for user={UserId} org={OrgId}: " +
                "{TotalRoles} role(s) — DB-rule={DbRule}, unrestricted={Unrestricted}.",
                user.Id, org.Id, productRoles.Count, dbRuleCount, unrestrictedCount);
        }

        var (token, expiresAtUtc) = _jwtTokenService.GenerateToken(
            userWithRoles, tenant, roleNames, org, productRoles);

        // Phase H: derive org_type code from OrganizationTypeId FK (authoritative) when available;
        // fall back to the stored OrgType string for compatibility.
        // TODO [Phase H — remove OrgType string]: remove OrgType string from UserResponse once column is dropped.
        var orgTypeForResponse = org is not null
            ? (Domain.OrgTypeMapper.TryResolveCode(org.OrganizationTypeId) ?? org.OrgType)
            : null;

        var userResponse = new UserResponse(
            userWithRoles.Id,
            userWithRoles.TenantId,
            userWithRoles.Email,
            userWithRoles.FirstName,
            userWithRoles.LastName,
            userWithRoles.IsActive,
            roleNames,
            org?.Id,
            orgTypeForResponse,
            productRoles);

        // Canonical audit: fire-and-observe — never throw, never gate login on audit success.
        var now = DateTimeOffset.UtcNow;
        _ = _auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "user.login.succeeded",
            EventCategory = EventCategory.Security,
            SourceSystem  = "identity-service",
            SourceService = "auth-api",
            Visibility    = VisibilityScope.Tenant,
            Severity      = SeverityLevel.Info,
            OccurredAtUtc = now,
            Scope = new AuditEventScopeDto
            {
                ScopeType = ScopeType.Tenant,
                TenantId  = tenant.Id.ToString(),
            },
            Actor = new AuditEventActorDto
            {
                Id   = userWithRoles.Id.ToString(),
                Type = ActorType.User,
                Name = $"{userWithRoles.FirstName} {userWithRoles.LastName}".Trim(),
            },
            Entity = new AuditEventEntityDto { Type = "User", Id = userWithRoles.Id.ToString() },
            Action      = "LoginSucceeded",
            Description = $"User {userWithRoles.Email} authenticated successfully in tenant {tenant.Code}.",
            Metadata    = JsonSerializer.Serialize(new { tenantCode = tenant.Code, email = userWithRoles.Email }),
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(now, "identity-service", "user.login.succeeded", userWithRoles.Id.ToString()),
            Tags = ["auth", "login"],
        });

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

    private enum EligibilityPath { DbRule, Unrestricted }

    /// <summary>
    /// Returns whether the given product role is eligible for the organization,
    /// and which eligibility path was used (for observability/logging).
    ///
    /// Phase F: EligibleOrgType column removed (migration 20260330200003).
    /// Eligibility is now exclusively controlled by ProductOrganizationTypeRules.
    ///
    /// Resolution order:
    ///   1. DB-backed rule table (OrgTypeRules nav property).
    ///      - If org.OrganizationTypeId is set: match by ID (authoritative, no string drift).
    ///      - Else: match by OrgType code string (transitional fallback within Phase 3).
    ///   2. No OrgTypeRules configured → allow (unrestricted).
    /// </summary>
    private static (bool Eligible, EligibilityPath Path) IsEligibleWithPath(ProductRole pr, Organization org)
    {
        // Path 1: DB-backed rule table (Phase 3+)
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

        // Path 2: no OrgTypeRules → unrestricted access
        return (true, EligibilityPath.Unrestricted);
    }
}
