namespace Identity.Domain;

/// <summary>
/// BE-BIO-001/010: Represents a single device's authenticated session, holding
/// the current device-specific refresh token (hashed) and revocation/expiry state.
/// A device session and its current refresh token are strictly 1:1 — rotation
/// overwrites RefreshTokenHash in place rather than spawning a new row.
///
/// The raw refresh token is never stored — only its SHA-256 hash. This entity
/// never stores biometric data; BiometricEnabled is an administrative flag only
/// (BE-BIO-011) and is never itself a source of authorization (SEC-006).
///
/// Status lifecycle: ACTIVE → REVOKED | EXPIRED | COMPROMISED.
/// </summary>
public class DeviceSession
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid TenantId { get; private set; }

    /// <summary>SHA-256 hex hash of the current raw refresh token. Never store raw tokens.</summary>
    public string RefreshTokenHash { get; private set; } = string.Empty;

    /// <summary>Constant across rotations of this session; used for reuse-detection cascade revocation.</summary>
    public Guid TokenFamilyId { get; private set; }

    public string Status { get; private set; } = DeviceSessionStatuses.Active;

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime LastUsedAtUtc { get; private set; }

    /// <summary>BE-BIO-009: immutable absolute expiry set at session creation.</summary>
    public DateTime AbsoluteExpiresAtUtc { get; private set; }

    /// <summary>BE-BIO-009: recomputed on every successful refresh as LastUsedAtUtc + inactivity window.</summary>
    public DateTime InactivityExpiresAtUtc { get; private set; }

    public DateTime? RevokedAtUtc { get; private set; }
    public string? RevokedReason { get; private set; }

    public string Platform { get; private set; } = string.Empty;
    public string AppVersion { get; private set; } = string.Empty;
    public string OsVersion { get; private set; } = string.Empty;
    public string DeviceDisplayName { get; private set; } = string.Empty;

    /// <summary>
    /// BE-BIO-011: administrative/informational flag only — set via the
    /// enable/disable endpoints. Never treated as proof that biometric
    /// authentication occurred for any specific request (SEC-006).
    /// </summary>
    public bool BiometricEnabled { get; private set; }

    /// <summary>Set only by primary authentication; refresh rotation must not extend the step-up window.</summary>
    public DateTime LastPrimaryAuthenticationAtUtc { get; private set; }

    /// <summary>Reserved for future risk/alerting use (BE-BIO-023); not actively computed in v1.</summary>
    public string RiskState { get; private set; } = "Normal";

    /// <summary>Defense-in-depth write counter; not wired into EF's optimistic-concurrency check (locking is pessimistic — see DeviceSessionService.RefreshAsync).</summary>
    public long RowVersion { get; private set; }

    public User User { get; private set; } = null!;

    private DeviceSession() { }

    public static DeviceSession Create(
        Guid userId,
        Guid tenantId,
        string refreshTokenHash,
        Guid tokenFamilyId,
        string platform,
        string appVersion,
        string osVersion,
        string deviceDisplayName,
        int absoluteExpiryDays,
        int inactivityExpiryDays)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshTokenHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(platform);

        var now = DateTime.UtcNow;
        return new DeviceSession
        {
            Id                       = Guid.CreateVersion7(),
            UserId                   = userId,
            TenantId                 = tenantId,
            RefreshTokenHash         = refreshTokenHash,
            TokenFamilyId            = tokenFamilyId,
            Status                   = DeviceSessionStatuses.Active,
            CreatedAtUtc             = now,
            LastUsedAtUtc            = now,
            AbsoluteExpiresAtUtc     = now.AddDays(absoluteExpiryDays),
            InactivityExpiresAtUtc   = now.AddDays(inactivityExpiryDays),
            Platform                 = platform.Trim(),
            AppVersion               = (appVersion ?? string.Empty).Trim(),
            OsVersion                = (osVersion ?? string.Empty).Trim(),
            DeviceDisplayName        = string.IsNullOrWhiteSpace(deviceDisplayName) ? "Unknown device" : deviceDisplayName.Trim(),
            BiometricEnabled         = false,
            LastPrimaryAuthenticationAtUtc = now,
            RiskState                = "Normal",
            RowVersion               = 0,
        };
    }

    /// <summary>
    /// BE-BIO-006: applies a successful rotation — overwrites the current hash,
    /// bumps LastUsedAtUtc/InactivityExpiresAtUtc. Does
    /// not touch AbsoluteExpiresAtUtc or TokenFamilyId, which are immutable for
    /// the life of the session.
    /// </summary>
    public void Rotate(string newRefreshTokenHash, int inactivityExpiryDays)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newRefreshTokenHash);

        var now = DateTime.UtcNow;
        RefreshTokenHash         = newRefreshTokenHash;
        LastUsedAtUtc            = now;
        InactivityExpiresAtUtc   = now.AddDays(inactivityExpiryDays);
        RowVersion++;
    }

    /// <summary>Idempotent — safe to call when already revoked. Returns true if state changed.</summary>
    public bool Revoke(string reason)
    {
        if (Status == DeviceSessionStatuses.Revoked || Status == DeviceSessionStatuses.Compromised)
            return false;

        Status         = DeviceSessionStatuses.Revoked;
        RevokedAtUtc   = DateTime.UtcNow;
        RevokedReason  = reason;
        RowVersion++;
        return true;
    }

    /// <summary>
    /// BE-BIO-007: applied specifically on confirmed refresh-token reuse (theft).
    /// Distinguishable from a normal user-initiated Revoke() in the DB/audit trail.
    /// </summary>
    public void MarkCompromised(string reason)
    {
        Status         = DeviceSessionStatuses.Compromised;
        RevokedAtUtc   = DateTime.UtcNow;
        RevokedReason  = reason;
        RowVersion++;
    }

    public void MarkExpired()
    {
        if (Status != DeviceSessionStatuses.Active) return;
        Status = DeviceSessionStatuses.Expired;
        RowVersion++;
    }

    public void EnableBiometric()
    {
        BiometricEnabled = true;
        RowVersion++;
    }

    public void DisableBiometric()
    {
        BiometricEnabled = false;
        RowVersion++;
    }

    /// <summary>
    /// BE-BIO-009: absolute and inactivity expiry are checked independently —
    /// either one being past-due makes the session unusable even if the other
    /// still has headroom.
    /// </summary>
    public bool IsUsable()
    {
        var now = DateTime.UtcNow;
        return Status == DeviceSessionStatuses.Active
            && now < AbsoluteExpiresAtUtc
            && now < InactivityExpiresAtUtc;
    }
}
