using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using Identity.Application.DTOs;
using Identity.Application;
using Identity.Application.Errors;
using Identity.Application.Interfaces;
using Identity.Domain;
using Identity.Infrastructure.Data;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;
using LegalSynq.AuditClient.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure.Services;

/// <summary>
/// BE-BIO: device-specific refresh-token lifecycle. The core security-critical
/// operation is <see cref="RefreshAsync"/> — see its doc comment for the full
/// rotation/reuse-detection algorithm.
///
/// SEC-006: this service never trusts a client-asserted "biometric succeeded"
/// claim anywhere. Authorization is based solely on possession of a valid,
/// unrevoked, correctly-rotated refresh token.
/// </summary>
public class DeviceSessionService : IDeviceSessionService
{
    private readonly IdentityDbContext _db;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IUserRepository? _userRepository;
    private readonly IEffectiveAccessService? _effectiveAccessService;
    private readonly IAuditEventClient _auditClient;
    private readonly RefreshTokenPolicyOptions _policy;
    private readonly ILogger<DeviceSessionService> _logger;

    public DeviceSessionService(
        IdentityDbContext db,
        IJwtTokenService jwtTokenService,
        IUserRepository userRepository,
        IEffectiveAccessService effectiveAccessService,
        IAuditEventClient auditClient,
        IOptions<RefreshTokenPolicyOptions> policy,
        ILogger<DeviceSessionService> logger)
    {
        _db = db;
        _jwtTokenService = jwtTokenService;
        _userRepository = userRepository;
        _effectiveAccessService = effectiveAccessService;
        _auditClient = auditClient;
        _policy = policy.Value;
        _logger = logger;
    }

    public DeviceSessionService(IdentityDbContext db, IJwtTokenService jwtTokenService,
        IAuditEventClient auditClient, IOptions<RefreshTokenPolicyOptions> policy,
        ILogger<DeviceSessionService> logger)
        : this(db, jwtTokenService, null!, null!, auditClient, policy, logger) { }

    public async Task<RefreshTokenResponse> CreateDeviceSessionAsync(
        Guid userId,
        Guid tenantId,
        DeviceInfo deviceInfo,
        string accessToken,
        DateTime accessTokenExpiresAtUtc,
        CancellationToken ct = default)
    {
        var (rawToken, tokenHash) = GenerateToken();
        var tokenFamilyId = Guid.CreateVersion7();

        var session = DeviceSession.Create(
            userId,
            tenantId,
            tokenHash,
            tokenFamilyId,
            deviceInfo.Platform,
            deviceInfo.AppVersion ?? string.Empty,
            deviceInfo.OsVersion ?? string.Empty,
            deviceInfo.DeviceDisplayName ?? string.Empty,
            absoluteExpiryDays: _policy.RefreshAbsoluteDays,
            inactivityExpiryDays: _policy.RefreshInactivityDays);

        _db.DeviceSessions.Add(session);
        _db.RefreshTokenLedgerEntries.Add(RefreshTokenLedgerEntry.CreateActive(session.Id, tokenFamilyId, tokenHash));

        if (_policy.MaxActiveSessionsPerUser > 0)
            await EnforceSessionCapAsync(userId, ct);

        await _db.SaveChangesAsync(ct);
        DeviceSessionMetrics.RecordCreated(deviceInfo.Platform);

        EmitAudit(
            eventType: "identity.session.device_created",
            severity: SeverityLevel.Info,
            userId: userId,
            tenantId: tenantId,
            deviceSessionId: session.Id,
            action: "DeviceSessionCreated",
            description: $"Device session created for platform={deviceInfo.Platform}.",
            tags: ["auth", "device-session", "biometric"]);

        return new RefreshTokenResponse(
            accessToken,
            accessTokenExpiresAtUtc,
            rawToken,
            EarliestExpiry(session),
            session.Id);
    }

    /// <summary>
    /// BE-BIO-004/005/006/007: atomic refresh-token rotation.
    ///
    /// 1. Lock the DeviceSessions row by PK via `SELECT ... FOR UPDATE` (MySQL/Pomelo
    ///    has no native locking-read LINQ API, so raw SQL is the idiomatic path) —
    ///    this blocks a concurrent refresh against the same session, closing the
    ///    double-rotation race.
    /// 2. Validate in order: row exists -> status Active and unexpired (absolute and
    ///    inactivity checked independently) -> user active/not locked -> hash match
    ///    via constant-time comparison.
    /// 3. Match -> rotate: mark the old ledger entry Rotated, insert a new Active
    ///    entry, call session.Rotate(...), commit.
    /// 4. No match -> reuse-detection branch: look up the submitted hash in the
    ///    ledger. Found with Status=Rotated in the same token family -> confirmed
    ///    theft: revoke the whole family, log a Critical audit event. Not found at
    ///    all -> plain invalid, no family revoke (avoids misclassifying garbage
    ///    input as theft).
    /// 5. SEC-010: both the reuse case and the generic invalid case return the same
    ///    externally-visible REFRESH_TOKEN_INVALID code — the true distinction is
    ///    retained only in the DB Status column and the audit trail.
    /// </summary>
    public async Task<DeviceSessionRefreshResult> RefreshAsync(
        string rawRefreshToken,
        Guid deviceSessionId,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var refreshTimer = Stopwatch.StartNew();
        var submittedHash = ComputeHash(rawRefreshToken);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var session = await _db.DeviceSessions
            .FromSqlInterpolated($"SELECT * FROM `idt_DeviceSessions` WHERE `Id` = {deviceSessionId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);

        // EF Core's identity map returns an already-tracked instance for a given
        // PK as-is — it does NOT overwrite its property values with a subsequent
        // query's results. If this DbContext instance already tracked this row
        // (e.g. a caller that queried it earlier in the same scope), the FOR
        // UPDATE query above would silently hand back stale in-memory values
        // despite correctly acquiring the row lock. Force a reload so every
        // validation below sees the true current row; since we already hold the
        // lock, this plain re-read is still consistent, not merely "latest
        // committed" from a concurrent writer's perspective.
        if (session is not null)
            await _db.Entry(session).ReloadAsync(ct);

        if (session is null)
        {
            await tx.RollbackAsync(ct);
            EmitAudit(
                eventType: "identity.session.refresh_failed",
                severity: SeverityLevel.Warn,
                userId: null,
                tenantId: null,
                deviceSessionId: deviceSessionId,
                action: "RefreshFailed",
                description: "Refresh attempted against an unknown device session.",
                tags: ["auth", "refresh", "failed"],
                ipAddress: ipAddress,
                metadata: new { reason = "DeviceSessionNotFound" });
            return DeviceSessionRefreshResult.Failure(AuthErrorCodes.DeviceSessionNotFound);
        }

        if (session.Status is DeviceSessionStatuses.Revoked or DeviceSessionStatuses.Compromised)
        {
            await tx.RollbackAsync(ct);
            EmitRefreshFailed(session, ipAddress, "DeviceSessionRevoked");
            return DeviceSessionRefreshResult.Failure(AuthErrorCodes.DeviceSessionRevoked);
        }

        if (session.Status == DeviceSessionStatuses.Expired || !session.IsUsable())
        {
            if (session.Status == DeviceSessionStatuses.Active)
                session.MarkExpired();
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            EmitRefreshFailed(session, ipAddress, "RefreshTokenExpired");
            return DeviceSessionRefreshResult.Failure(AuthErrorCodes.RefreshTokenExpired);
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == session.UserId, ct);
        if (user is null || !user.IsActive)
        {
            session.Revoke("AccountDisabled");
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            EmitRefreshFailed(session, ipAddress, "AccountDisabled");
            return DeviceSessionRefreshResult.Failure(AuthErrorCodes.AccountDisabled);
        }
        if (user.IsLocked)
        {
            session.Revoke("AccountLocked");
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            EmitRefreshFailed(session, ipAddress, "AccountLocked");
            return DeviceSessionRefreshResult.Failure(AuthErrorCodes.AccountLocked);
        }

        if (!ConstantTimeEquals(submittedHash, session.RefreshTokenHash))
        {
            // Reuse-detection branch: the submitted hash does not match the session's
            // current token. Check whether it is a hash we issued previously (now
            // superseded) — that is the signal for potential token theft.
            var ledgerHit = await _db.RefreshTokenLedgerEntries
                .FirstOrDefaultAsync(l => l.TokenHash == submittedHash, ct);

            if (ledgerHit is not null
                && ledgerHit.TokenFamilyId == session.TokenFamilyId
                && ledgerHit.Status == DeviceSessionStatuses.Rotated)
            {
                // Grace period: a resubmission of the *immediately-preceding*
                // generation, arriving within a short window after it was rotated,
                // is far more likely to be a benign client-side race (e.g. a
                // network-timeout retry that fired while the original request was
                // still completing server-side) than theft — an attacker who
                // actually stole a token has no reason to replay it within
                // milliseconds of its legitimate rotation. Outside the grace
                // window, treat it as confirmed reuse. This mirrors the standard
                // "rotation with reuse detection and grace period" pattern used by
                // reference OAuth2 refresh-token implementations.
                var withinGracePeriod = ledgerHit.RotatedAtUtc is not null
                    && DateTime.UtcNow - ledgerHit.RotatedAtUtc.Value <= TimeSpan.FromSeconds(_policy.ReuseGraceSeconds);

                if (withinGracePeriod)
                {
                    // Do not rotate again from a stale premise — just hand the
                    // caller the token that already superseded theirs, so a benign
                    // racing retry still ends up with a valid, current session
                    // instead of being treated as an attacker and locked out.
                    return await GraceReleaseWithoutRevocationAsync(ct);
                }

                ledgerHit.MarkReused();
                DeviceSessionMetrics.RecordReuse();
                _logger.LogCritical("SECURITY ALERT: refresh-token reuse detected for device session {DeviceSessionId} and family {TokenFamilyId}.", session.Id, session.TokenFamilyId);
                await RevokeFamilyWithinTransactionAsync(session, "ReuseDetected", ct);
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                EmitAudit(
                    eventType: "identity.session.refresh_reused",
                    severity: SeverityLevel.Critical,
                    userId: session.UserId,
                    tenantId: session.TenantId,
                    deviceSessionId: session.Id,
                    action: "RefreshTokenReused",
                    description: "A previously-rotated refresh token was resubmitted outside the reuse grace period — treated as confirmed token theft. Device session and token family revoked.",
                    tags: ["auth", "refresh", "security-incident", "reuse-detected"],
                    ipAddress: ipAddress,
                    metadata: new { tokenFamilyId = session.TokenFamilyId, deviceSessionId = session.Id, userId = session.UserId });

                // SEC-010: never reveal that reuse was specifically detected.
                return DeviceSessionRefreshResult.Failure(AuthErrorCodes.RefreshTokenInvalid);
            }

            await tx.RollbackAsync(ct);
            EmitRefreshFailed(session, ipAddress, "InvalidToken");
            return DeviceSessionRefreshResult.Failure(AuthErrorCodes.RefreshTokenInvalid);
        }

        // Match — rotate and respond within the still-open transaction.
        return await RotateAndBuildSuccessResultAsync(session, user, ipAddress, tx, ct);

        // ── local functions ──────────────────────────────────────────────────

        async Task<DeviceSessionRefreshResult> RotateAndBuildSuccessResultAsync(
            DeviceSession activeSession, User activeUser, string? reqIpAddress,
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction, CancellationToken innerCt)
        {
            var currentLedgerEntry = await _db.RefreshTokenLedgerEntries
                .FirstOrDefaultAsync(l => l.DeviceSessionId == activeSession.Id && l.Status == DeviceSessionStatuses.Active, innerCt);

            var (newRawToken, newHash) = GenerateToken();
            var newLedgerEntry = RefreshTokenLedgerEntry.CreateActive(activeSession.Id, activeSession.TokenFamilyId, newHash);
            _db.RefreshTokenLedgerEntries.Add(newLedgerEntry);
            currentLedgerEntry?.MarkRotated(newLedgerEntry.Id);

            activeSession.Rotate(newHash, _policy.RefreshInactivityDays);

            await _db.SaveChangesAsync(innerCt);
            await transaction.CommitAsync(innerCt);

            EmitAudit(
                eventType: "identity.session.refresh_succeeded",
                severity: SeverityLevel.Info,
                userId: activeSession.UserId,
                tenantId: activeSession.TenantId,
                deviceSessionId: activeSession.Id,
                action: "RefreshSucceeded",
                description: "Refresh token rotated successfully.",
                tags: ["auth", "refresh"],
                ipAddress: reqIpAddress);
            DeviceSessionMetrics.RecordRefresh("success", refreshTimer.Elapsed.TotalMilliseconds);

            return await BuildResponseAsync(activeSession, activeUser, newRawToken);
        }

        async Task<DeviceSessionRefreshResult> GraceReleaseWithoutRevocationAsync(CancellationToken innerCt)
        {
            await tx.CommitAsync(innerCt);
            // No new raw token was minted in this path — the caller loses the race
            // and must be told to use whatever token the winning request returned.
            // We cannot hand back a raw value we never persisted (hash-only storage
            // — BE-BIO-003), so the safest honest response is the same invalid-token
            // error the caller would see for any other unusable token, WITHOUT the
            // destructive family revocation a genuine reuse gets. The client's
            // in-flight winning request already has a usable token; this response
            // simply must not be the thing that destroys it.
            return DeviceSessionRefreshResult.Failure(AuthErrorCodes.RefreshTokenInvalid);
        }

        async Task<DeviceSessionRefreshResult> BuildResponseAsync(DeviceSession activeSession, User activeUser, string newRawToken)
        {
            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == activeSession.TenantId, ct);
            if (tenant is null)
            {
                _logger.LogError("BE-BIO: tenant {TenantId} not found while minting refreshed access token for device session {DeviceSessionId}.", activeSession.TenantId, activeSession.Id);
                return DeviceSessionRefreshResult.Failure(AuthErrorCodes.DeviceSessionNotFound);
            }

            if (_userRepository is null || _effectiveAccessService is null)
            {
                var fallback = _jwtTokenService.GenerateRefreshedAccessToken(activeUser, tenant, activeSession.Id);
                return DeviceSessionRefreshResult.Success(new RefreshTokenResponse(fallback.Token, fallback.ExpiresAtUtc,
                    newRawToken, EarliestExpiry(activeSession), activeSession.Id));
            }
            var userWithRoles = await _userRepository.GetByIdWithRolesAsync(activeUser.Id, ct);
            if (userWithRoles is null) return DeviceSessionRefreshResult.Failure(AuthErrorCodes.AccountDisabled);
            var organization = (await _userRepository.GetPrimaryOrgMembershipAsync(activeUser.Id, tenant.Id, ct))?.Organization;
            var effectiveAccess = await _effectiveAccessService.GetEffectiveAccessAsync(
                tenant.Id, activeUser.Id, organization?.Id, ct);
            var memberships = await _userRepository.GetActiveTenantMembershipsAsync(activeUser.Id, ct);
            var roles = userWithRoles.ScopedRoleAssignments
                .Where(s => s.ScopeType == ScopedRoleAssignment.ScopeTypes.Global
                         && s.Role.Scope is RoleScopes.Platform or RoleScopes.Tenant)
                .Select(s => s.Role.Name);
            var (accessToken, accessTokenExpiresAtUtc) = _jwtTokenService.GenerateRefreshedAccessToken(
                userWithRoles, tenant, activeSession.Id, roles, organization,
                effectiveAccess.ProductRolesFlat, tenant.SessionTimeoutMinutes ?? 30,
                effectiveAccess.Products, effectiveAccess.Permissions, memberships.Select(m => m.TenantId));

            return DeviceSessionRefreshResult.Success(new RefreshTokenResponse(
                accessToken,
                accessTokenExpiresAtUtc,
                newRawToken,
                EarliestExpiry(activeSession),
                activeSession.Id));
        }
    }

    public async Task<bool> EnableBiometricAsync(Guid userId, Guid deviceSessionId, CancellationToken ct = default)
    {
        var session = await _db.DeviceSessions
            .FirstOrDefaultAsync(s => s.Id == deviceSessionId && s.UserId == userId, ct);
        if (session is null || session.Status != DeviceSessionStatuses.Active) return false;

        session.EnableBiometric();
        await _db.SaveChangesAsync(ct);

        EmitAudit(
            eventType: "identity.user.biometric_enabled",
            severity: SeverityLevel.Info,
            userId: userId,
            tenantId: session.TenantId,
            deviceSessionId: session.Id,
            action: "BiometricEnabled",
            description: "Biometric login marked enabled for device session.",
            tags: ["auth", "biometric"]);

        return true;
    }

    public async Task<bool> DisableBiometricAsync(Guid userId, Guid deviceSessionId, CancellationToken ct = default)
    {
        var session = await _db.DeviceSessions
            .FirstOrDefaultAsync(s => s.Id == deviceSessionId && s.UserId == userId, ct);
        if (session is null) return false;

        session.DisableBiometric();
        var wasActive = session.Revoke("BiometricDisabled");
        await _db.SaveChangesAsync(ct);

        EmitAudit(
            eventType: "identity.user.biometric_disabled",
            severity: SeverityLevel.Info,
            userId: userId,
            tenantId: session.TenantId,
            deviceSessionId: session.Id,
            action: "BiometricDisabled",
            description: "Biometric login disabled; device session revoked.",
            tags: ["auth", "biometric"]);

        if (wasActive)
        {
            EmitAudit(
                eventType: "identity.session.device_revoked",
                severity: SeverityLevel.Info,
                userId: userId,
                tenantId: session.TenantId,
                deviceSessionId: session.Id,
                action: "DeviceSessionRevoked",
                description: "Device session revoked as part of disabling biometric login.",
                tags: ["auth", "device-session"]);
        }

        return true; // idempotent — true whether or not it was already disabled/revoked
    }

    public async Task LogoutCurrentAsync(string rawRefreshToken, Guid deviceSessionId, CancellationToken ct = default)
    {
        var submittedHash = ComputeHash(rawRefreshToken);
        var session = await _db.DeviceSessions
            .FirstOrDefaultAsync(s => s.Id == deviceSessionId, ct);
        if (session is null || !ConstantTimeEquals(submittedHash, session.RefreshTokenHash)) return;

        var wasActive = session.Revoke("UserLogout");
        if (wasActive) DeviceSessionMetrics.RecordRevoked("UserLogout");
        await _db.SaveChangesAsync(ct);

        EmitAudit(
            eventType: "identity.user.logged_out",
            severity: SeverityLevel.Info,
            userId: session.UserId,
            tenantId: session.TenantId,
            deviceSessionId: session.Id,
            action: "Logout",
            description: "User logged out; device session revoked.",
            tags: ["auth", "logout", "session"]);

        if (wasActive)
        {
            EmitAudit(
                eventType: "identity.session.device_revoked",
                severity: SeverityLevel.Info,
                userId: session.UserId,
                tenantId: session.TenantId,
                deviceSessionId: session.Id,
                action: "DeviceSessionRevoked",
                description: "Device session revoked by user logout.",
                tags: ["auth", "device-session"]);
        }

    }

    public async Task<int> LogoutAllAsync(Guid userId, CancellationToken ct = default)
    {
        var count = await RevokeAllActiveSessionsAsync(userId, "UserLogoutAll", ct);
        if (count > 0) DeviceSessionMetrics.RecordRevoked("UserLogoutAll");

        EmitAudit(
            eventType: "identity.user.logged_out_all_sessions",
            severity: SeverityLevel.Warn,
            userId: userId,
            tenantId: null,
            deviceSessionId: null,
            action: "LogoutAllSessions",
            description: $"User revoked all device sessions ({count} session(s) affected).",
            tags: ["auth", "logout", "session", "logout-all"]);

        return count;
    }

    public async Task<int> RevokeAllForUserAsync(Guid userId, string reason, CancellationToken ct = default)
    {
        var count = await RevokeAllActiveSessionsAsync(userId, reason, ct);

        if (count > 0)
        {
            EmitAudit(
                eventType: "identity.session.device_revoked",
                severity: SeverityLevel.Warn,
                userId: userId,
                tenantId: null,
                deviceSessionId: null,
                action: "AllDeviceSessionsRevoked",
                description: $"All device sessions revoked for user (reason={reason}, count={count}).",
                tags: ["auth", "device-session", "security"]);
        }

        return count;
    }

    public async Task<IReadOnlyList<DeviceSessionSummary>> ListSessionsAsync(
        Guid userId, Guid? currentDeviceSessionId, CancellationToken ct = default)
    {
        return await _db.DeviceSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.Status == DeviceSessionStatuses.Active)
            .OrderByDescending(s => s.LastUsedAtUtc)
            .Select(s => new DeviceSessionSummary(
                s.Id,
                s.DeviceDisplayName,
                s.Platform,
                s.LastUsedAtUtc,
                s.CreatedAtUtc,
                currentDeviceSessionId != null && s.Id == currentDeviceSessionId,
                s.BiometricEnabled))
            .ToListAsync(ct);
    }

    public async Task<bool> RevokeSessionAsync(Guid userId, Guid deviceSessionId, string reason, CancellationToken ct = default)
    {
        var session = await _db.DeviceSessions
            .FirstOrDefaultAsync(s => s.Id == deviceSessionId && s.UserId == userId, ct);
        if (session is null) return false;

        var wasActive = session.Revoke(reason);
        await _db.SaveChangesAsync(ct);

        if (wasActive)
        {
            EmitAudit(
                eventType: "identity.session.device_revoked",
                severity: SeverityLevel.Info,
                userId: userId,
                tenantId: session.TenantId,
                deviceSessionId: session.Id,
                action: "DeviceSessionRevoked",
                description: $"Device session revoked (reason={reason}).",
                tags: ["auth", "device-session"]);
        }

        return true; // idempotent
    }

    public async Task RevokeByTokenFamilyAsync(Guid tokenFamilyId, string reason, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var session = await _db.DeviceSessions
            .FirstOrDefaultAsync(s => s.TokenFamilyId == tokenFamilyId, ct);
        if (session is null)
        {
            await tx.RollbackAsync(ct);
            return;
        }

        await RevokeFamilyWithinTransactionAsync(session, reason, ct);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        EmitAudit(
            eventType: "identity.session.device_revoked",
            severity: SeverityLevel.Warn,
            userId: session.UserId,
            tenantId: session.TenantId,
            deviceSessionId: session.Id,
            action: "TokenFamilyRevoked",
            description: $"Token family revoked (reason={reason}).",
            tags: ["auth", "device-session", "security"]);
    }

    public async Task<DateTime?> GetLastPrimaryAuthenticationAsync(Guid userId, Guid deviceSessionId, CancellationToken ct = default)
    {
        return await _db.DeviceSessions
            .AsNoTracking()
            .Where(s => s.Id == deviceSessionId && s.UserId == userId)
            .Select(s => (DateTime?)s.LastPrimaryAuthenticationAtUtc)
            .FirstOrDefaultAsync(ct);
    }

    // ── internals ────────────────────────────────────────────────────────────

    private async Task RevokeFamilyWithinTransactionAsync(DeviceSession session, string reason, CancellationToken ct)
    {
        session.MarkCompromised(reason);

        var ledgerEntries = await _db.RefreshTokenLedgerEntries
            .Where(l => l.TokenFamilyId == session.TokenFamilyId)
            .ToListAsync(ct);
        foreach (var entry in ledgerEntries)
            entry.MarkRevoked();
    }

    private async Task<int> RevokeAllActiveSessionsAsync(Guid userId, string reason, CancellationToken ct)
    {
        // Bulk update — not per-row locked, unlike RefreshAsync's single-session race
        // guard. Performance matters more here than serializing against a concurrent
        // refresh on one of potentially many sessions; a session mid-refresh will
        // simply be revoked immediately after its in-flight rotation completes.
        return await _db.DeviceSessions
            .Where(s => s.UserId == userId && s.Status == DeviceSessionStatuses.Active)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.Status, DeviceSessionStatuses.Revoked)
                .SetProperty(s => s.RevokedAtUtc, DateTime.UtcNow)
                .SetProperty(s => s.RevokedReason, reason),
                ct);
    }

    private async Task EnforceSessionCapAsync(Guid userId, CancellationToken ct)
    {
        var activeCount = await _db.DeviceSessions
            .CountAsync(s => s.UserId == userId && s.Status == DeviceSessionStatuses.Active, ct);
        if (activeCount < _policy.MaxActiveSessionsPerUser) return;

        var oldest = await _db.DeviceSessions
            .Where(s => s.UserId == userId && s.Status == DeviceSessionStatuses.Active)
            .OrderBy(s => s.LastUsedAtUtc)
            .Take(activeCount - _policy.MaxActiveSessionsPerUser + 1)
            .ToListAsync(ct);
        foreach (var s in oldest)
            s.Revoke("MaxActiveSessionsExceeded");
    }

    private static (string RawToken, string Hash) GenerateToken()
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return (rawToken, ComputeHash(rawToken));
    }

    private static string ComputeHash(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    private static bool ConstantTimeEquals(string a, string b)
    {
        var bytesA = Encoding.UTF8.GetBytes(a);
        var bytesB = Encoding.UTF8.GetBytes(b);
        // Lengths are always equal in practice (both fixed 64-char SHA-256 hex hashes);
        // CryptographicOperations.FixedTimeEquals requires equal-length spans, so an
        // early unequal-length return is safe here (not itself a token-content leak).
        if (bytesA.Length != bytesB.Length) return false;
        return CryptographicOperations.FixedTimeEquals(bytesA, bytesB);
    }

    private static DateTime EarliestExpiry(DeviceSession session) =>
        session.AbsoluteExpiresAtUtc < session.InactivityExpiresAtUtc
            ? session.AbsoluteExpiresAtUtc
            : session.InactivityExpiresAtUtc;

    private void EmitRefreshFailed(DeviceSession session, string? ipAddress, string reason) =>
        EmitAudit(
            eventType: "identity.session.refresh_failed",
            severity: SeverityLevel.Warn,
            userId: session.UserId,
            tenantId: session.TenantId,
            deviceSessionId: session.Id,
            action: "RefreshFailed",
            description: $"Refresh failed (reason={reason}).",
            tags: ["auth", "refresh", "failed"],
            ipAddress: ipAddress,
            metadata: new { reason });

    /// <summary>
    /// Fire-and-observe audit emission, matching the pattern used throughout
    /// AuthEndpoints.cs/AuthService.cs. Never gates the caller's response on
    /// audit-ingestion success. Payloads never include raw tokens, hashes, or
    /// passwords — only opaque GUIDs and non-sensitive metadata (SEC-009).
    /// </summary>
    private void EmitAudit(
        string eventType,
        SeverityLevel severity,
        Guid? userId,
        Guid? tenantId,
        Guid? deviceSessionId,
        string action,
        string description,
        List<string> tags,
        string? ipAddress = null,
        object? metadata = null)
    {
        var now = DateTimeOffset.UtcNow;
        _ = _auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = eventType,
            EventCategory = EventCategory.Security,
            SourceSystem  = "identity-service",
            SourceService = "auth-api",
            Visibility    = VisibilityScope.User,
            Severity      = severity,
            OccurredAtUtc = now,
            Scope = new AuditEventScopeDto
            {
                ScopeType = ScopeType.Tenant,
                TenantId  = tenantId?.ToString(),
            },
            Actor = new AuditEventActorDto
            {
                Id        = userId?.ToString(),
                Type      = ActorType.User,
                IpAddress = ipAddress,
            },
            Entity      = deviceSessionId is not null
                ? new AuditEventEntityDto { Type = "DeviceSession", Id = deviceSessionId.ToString() }
                : null,
            Action      = action,
            Description = description,
            Metadata    = metadata is not null ? JsonSerializer.Serialize(metadata) : null,
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(
                now, "identity-service", eventType, deviceSessionId?.ToString() ?? userId?.ToString() ?? "unknown"),
            Tags = tags,
        });
    }
}
