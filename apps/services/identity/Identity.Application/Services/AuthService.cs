using System.Security.Claims;
using Identity.Application.DTOs;
using Identity.Application.Interfaces;
using Identity.Domain;

namespace Identity.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthService(
        IUserRepository userRepository,
        ITenantRepository tenantRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService)
    {
        _userRepository = userRepository;
        _tenantRepository = tenantRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
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

        var roleNames = userWithRoles.UserRoles.Select(ur => ur.Role.Name).ToList();

        // Load org membership and compute product roles
        var orgMembership = await _userRepository.GetPrimaryOrgMembershipAsync(user.Id, ct);
        var org = orgMembership?.Organization;

        List<string> productRoles = [];
        if (org is not null)
        {
            // Phase 3: resolve product roles via ProductOrganizationTypeRule (DB-backed).
            // Each enabled product's roles are included when:
            //   a) A matching ProductOrganizationTypeRule exists for the org's OrganizationTypeId, OR
            //   b) (transitional fallback) the legacy EligibleOrgType string matches the org's OrgType.
            // Both checks are OR-combined so existing tenants work without re-migration.
            productRoles = org.OrganizationProducts
                .Where(op => op.IsEnabled)
                .SelectMany(op => op.Product.ProductRoles)
                .Where(pr => pr.IsActive && IsEligible(pr, org))
                .Select(pr => pr.Code)
                .Distinct()
                .OrderBy(c => c)
                .ToList();
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

    /// <summary>
    /// Returns true when the given product role is eligible for the organization.
    /// Checks the new DB-backed rule table first (Phase 3); falls back to the legacy
    /// EligibleOrgType string so existing data keeps working during the migration window.
    /// </summary>
    private static bool IsEligible(ProductRole pr, Organization org)
    {
        // Phase 3: new rule table check (loaded via navigation property if present)
        if (pr.OrgTypeRules is { Count: > 0 })
        {
            return pr.OrgTypeRules.Any(r =>
                r.IsActive &&
                r.OrganizationType is not null &&
                r.OrganizationType.Code == org.OrgType);
        }

        // Transitional fallback: legacy EligibleOrgType string comparison
        return pr.EligibleOrgType is null || pr.EligibleOrgType == org.OrgType;
    }
}
