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

    public async Task<LoginResponse> LoginAsync(LoginRequest request, string? ipAddress = null, CancellationToken ct = default)
    {
        // Canonical audit helpers — used when a login failure must be emitted before re-throwing.
        // fire-and-observe: never awaited, never allowed to gate the primary auth response.
        var tenantCodeNorm  = request.TenantCode.ToUpperInvariant().Trim();
        var emailNorm       = request.Email.ToLowerInvariant().Trim();

        var tenant = await _tenantRepository.GetByCodeAsync(tenantCodeNorm, ct);
        if (tenant is null || !tenant.IsActive)
        {
            EmitLoginFailed(emailNorm, tenantCode: tenantCodeNorm, userId: null, reason: "TenantNotFound", ipAddress: ipAddress);
            throw new UnauthorizedAccessException();
        }

        if (tenant.ProvisioningStatus == ProvisioningStatus.Verifying)
        {
            EmitLoginFailed(emailNorm, tenantCode: tenantCodeNorm, userId: null, reason: "TenantVerificationRetrying", ipAddress: ipAddress);
            throw new InvalidOperationException(
                $"Tenant '{tenantCodeNorm}' is currently verifying DNS configuration. " +
                "This process typically completes within a few minutes. Please try again shortly.");
        }

        if (tenant.ProvisioningStatus != ProvisioningStatus.Active)
        {
            EmitLoginFailed(emailNorm, tenantCode: tenantCodeNorm, userId: null, reason: "TenantNotProvisioned", ipAddress: ipAddress);
            throw new InvalidOperationException($"Tenant '{tenantCodeNorm}' is not fully provisioned (status: {tenant.ProvisioningStatus}). Please wait for setup to complete.");
        }

        var normalizedEmail = emailNorm;
        var user = await _userRepository.GetByTenantAndEmailAsync(tenant.Id, normalizedEmail, ct);
        if (user is null || !user.IsActive)
        {
            EmitLoginFailed(normalizedEmail, tenantCode: tenant.Code, userId: null, reason: "UserNotFound", ipAddress: ipAddress);
            throw new UnauthorizedAccessException();
        }

        // UIX-003-03: reject locked accounts (checked after IsActive so lock state is independent).
        if (user.IsLocked)
        {
            EmitLoginFailed(normalizedEmail, tenantCode: tenant.Code, userId: user.Id.ToString(), reason: "AccountLocked", ipAddress: ipAddress);
            EmitLockedLoginBlocked(user, tenant, ipAddress);
            throw new UnauthorizedAccessException();
        }

        var valid = _passwordHasher.Verify(request.Password, user.PasswordHash);
        if (!valid)
        {
            EmitLoginFailed(normalizedEmail, tenantCode: tenant.Code, userId: user.Id.ToString(), reason: "InvalidCredentials", ipAddress: ipAddress);
            throw new UnauthorizedAccessException();
        }

        var userWithRoles = await _userRepository.GetByIdWithRolesAsync(user.Id, ct);
        if (userWithRoles is null)
        {
            EmitLoginFailed(normalizedEmail, tenantCode: tenant.Code, userId: user.Id.ToString(), reason: "RoleLookupFailed", ipAddress: ipAddress);
            throw new UnauthorizedAccessException();
        }

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
            userWithRoles, tenant, roleNames, org, productRoles,
            sessionTimeoutMinutes: tenant.SessionTimeoutMinutes);

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
            EventType     = "identity.user.login.succeeded",
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
                Id        = userWithRoles.Id.ToString(),
                Type      = ActorType.User,
                Name      = $"{userWithRoles.FirstName} {userWithRoles.LastName}".Trim(),
                IpAddress = ipAddress,
            },
            Entity = new AuditEventEntityDto { Type = "User", Id = userWithRoles.Id.ToString() },
            Action      = "LoginSucceeded",
            Description = $"User {userWithRoles.Email} authenticated successfully in tenant {tenant.Code}.",
            Metadata    = JsonSerializer.Serialize(new { tenantCode = tenant.Code, email = userWithRoles.Email }),
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(now, "identity-service", "identity.user.login.succeeded", userWithRoles.Id.ToString()),
            Tags = ["auth", "login"],
        });

        // UIX-003-03: update LastLoginAtUtc. Best-effort — never gate login on this write.
        try
        {
            userWithRoles.RecordLogin();
            await _userRepository.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist LastLoginAtUtc for user {UserId}. Non-fatal.", userWithRoles.Id);
        }

        return new LoginResponse(token, expiresAtUtc, userResponse);
    }

    /// <summary>
    /// Assembles an AuthMeResponse from a validated ClaimsPrincipal.
    /// Most fields come from JWT claims; AvatarDocumentId is fetched from DB
    /// since it changes independently of the token lifecycle.
    ///
    /// UIX-003-03: validates SessionVersion from the JWT against the DB value.
    /// If the token's session_version is older than the current DB value,
    /// the session is considered force-logged-out and the request is rejected.
    /// </summary>
    public async Task<AuthMeResponse> GetCurrentUserAsync(ClaimsPrincipal principal, CancellationToken ct = default)
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

        // Read per-tenant idle session timeout embedded at login time.
        var timeoutClaim = principal.FindFirstValue("session_timeout_minutes");
        var sessionTimeoutMinutes = timeoutClaim is not null && int.TryParse(timeoutClaim, out var tm) ? tm : 30;

        // AvatarDocumentId is not in the JWT (changes independently) — fetch from DB.
        // UIX-003-03: also validate SessionVersion and IsLocked from DB.
        Guid? avatarDocumentId = null;
        if (Guid.TryParse(userId, out var userGuid))
        {
            var user = await _userRepository.GetByIdAsync(userGuid, ct);

            // UIX-003-03: reject locked accounts immediately — they cannot use existing sessions.
            if (user?.IsLocked == true)
                throw new UnauthorizedAccessException("Account is locked.");

            avatarDocumentId = user?.AvatarDocumentId;

            // UIX-003-03: validate session version. Tokens from before a force-logout
            // or lock will have an older session_version and must be rejected.
            // If the claim is absent (old tokens before this feature), allow through.
            var sessionVersionClaim = principal.FindFirstValue("session_version");
            if (sessionVersionClaim is not null
                && int.TryParse(sessionVersionClaim, out var tokenVersion)
                && user is not null
                && tokenVersion < user.SessionVersion)
            {
                throw new UnauthorizedAccessException("Session has been invalidated.");
            }
        }

        // Resolve which products are enabled at the tenant level.
        // Returns frontend-friendly codes (e.g. "SynqFund", "CareConnect") so the
        // tenant portal can filter its product tiles without knowing DB internals.
        List<string> enabledProducts = [];
        if (Guid.TryParse(tenantId, out var tenantGuid))
        {
            var dbCodes = await _tenantRepository.GetEnabledProductCodesAsync(tenantGuid, ct);
            enabledProducts = dbCodes
                .Select(code => DbToFrontendProductCode.TryGetValue(code, out var fc) ? fc : code)
                .ToList();
        }

        return new AuthMeResponse(
            UserId:                 userId,
            Email:                  email,
            TenantId:               tenantId,
            TenantCode:             tenantCode,
            OrgId:                  orgId,
            OrgType:                orgType,
            OrgName:                null,  // Phase 2: DB lookup by orgId for DisplayName ?? Name
            ProductRoles:           productRoles,
            SystemRoles:            systemRoles,
            ExpiresAtUtc:           expiresAtUtc,
            SessionTimeoutMinutes:  sessionTimeoutMinutes,
            AvatarDocumentId:       avatarDocumentId,
            EnabledProducts:        enabledProducts);
    }

    // Maps the DB product Code column → the frontend ProductCode (TypeScript).
    // Keep in sync with AdminEndpoints.DbToFrontendProductCode.
    private static readonly Dictionary<string, string> DbToFrontendProductCode
        = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SYNQ_FUND"]        = "SynqFund",
        ["SYNQ_LIENS"]       = "SynqLien",
        ["SYNQ_CARECONNECT"] = "CareConnect",
        ["SYNQ_AI"]          = "SynqAI",
        ["SYNQ_INSIGHTS"]    = "SynqInsights",
        ["SYNQ_BILL"]        = "SynqBill",
        ["SYNQ_RX"]          = "SynqRx",
        ["SYNQ_PAYOUT"]      = "SynqPayout",
    };

    // ── Canonical audit helpers ────────────────────────────────────────────────

    /// <summary>
    /// Emits a <c>identity.user.login.failed</c> canonical audit event.
    ///
    /// Fire-and-observe: the returned Task is discarded. This method never throws,
    /// never awaits the ingestion call, and never gates the primary auth failure response.
    ///
    /// The failure reason is stored as metadata only. The HTTP response to the caller
    /// never reveals which specific check failed (tenant/user/password) — the caller
    /// always receives 401 Unauthorized.
    ///
    /// HIPAA §164.312(b): failed login attempts are a required audit event.
    /// </summary>
    private void EmitLoginFailed(string email, string tenantCode, string? userId, string reason, string? ipAddress = null)
    {
        var now = DateTimeOffset.UtcNow;
        _ = _auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "identity.user.login.failed",
            EventCategory = EventCategory.Security,
            SourceSystem  = "identity-service",
            SourceService = "auth-api",
            Visibility    = VisibilityScope.Tenant,
            Severity      = SeverityLevel.Warn,
            OccurredAtUtc = now,
            Scope = new AuditEventScopeDto
            {
                ScopeType = ScopeType.Tenant,
                TenantId  = null,
            },
            Actor = new AuditEventActorDto
            {
                Id        = userId,
                Type      = ActorType.User,
                Name      = email,
                IpAddress = ipAddress,
            },
            Entity      = userId is not null ? new AuditEventEntityDto { Type = "User", Id = userId } : null,
            Action      = "LoginFailed",
            Description = $"Failed login attempt for '{email}' in tenant '{tenantCode}'.",
            Metadata    = System.Text.Json.JsonSerializer.Serialize(new
            {
                tenantCode,
                failureReason = reason,
            }),
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(now, "identity-service", "identity.user.login.failed", email),
            Tags = ["auth", "login", "failure", "security"],
        });
    }

    /// <summary>
    /// UIX-003-03: emits a dedicated <c>identity.user.login.blocked</c> event when a locked
    /// account attempts to authenticate. Separate from the generic login.failed event so
    /// security dashboards can distinguish intentional locks from bad credentials.
    /// Fire-and-observe.
    /// </summary>
    private void EmitLockedLoginBlocked(User user, Tenant tenant, string? ipAddress)
    {
        var now = DateTimeOffset.UtcNow;
        _ = _auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "identity.user.login.blocked",
            EventCategory = EventCategory.Security,
            SourceSystem  = "identity-service",
            SourceService = "auth-api",
            Visibility    = VisibilityScope.Tenant,
            Severity      = SeverityLevel.Warn,
            OccurredAtUtc = now,
            Scope = new AuditEventScopeDto
            {
                ScopeType = ScopeType.Tenant,
                TenantId  = tenant.Id.ToString(),
            },
            Actor = new AuditEventActorDto
            {
                Id        = user.Id.ToString(),
                Type      = ActorType.User,
                Name      = user.Email,
                IpAddress = ipAddress,
            },
            Entity      = new AuditEventEntityDto { Type = "User", Id = user.Id.ToString() },
            Action      = "LoginBlocked",
            Description = $"Login attempt blocked for locked account '{user.Email}' in tenant {tenant.Code}.",
            Metadata    = JsonSerializer.Serialize(new { tenantCode = tenant.Code, email = user.Email, reason = "AccountLocked" }),
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(now, "identity-service", "identity.user.login.blocked", user.Id.ToString()),
            Tags = ["auth", "login", "blocked", "security", "locked"],
        });
    }

    // ── Eligibility helpers ────────────────────────────────────────────────────

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
