namespace Identity.Domain;

/// <summary>
/// BE-BIO-007: Append-only ledger of every refresh-token generation ever issued
/// within a device session's token family. Needed because DeviceSession.Rotate()
/// overwrites RefreshTokenHash in place, so detecting reuse of an *older*
/// (not just immediately-prior) generation requires a durable per-generation
/// record to look up by hash.
///
/// On every rotation: the entry matching the previously-current hash transitions
/// ACTIVE -> ROTATED, and a new ACTIVE entry is inserted for the new hash. A
/// resubmission of any ROTATED hash is the signal for confirmed token reuse.
/// </summary>
public class RefreshTokenLedgerEntry
{
    public Guid Id { get; private set; }
    public Guid DeviceSessionId { get; private set; }

    /// <summary>Denormalized from DeviceSession so a reuse lookup works even if the session row's own state is being concurrently modified.</summary>
    public Guid TokenFamilyId { get; private set; }

    /// <summary>SHA-256 hex hash of this generation's raw refresh token. Never store raw tokens.</summary>
    public string TokenHash { get; private set; } = string.Empty;

    public string Status { get; private set; } = DeviceSessionStatuses.Active;

    public DateTime IssuedAtUtc { get; private set; }
    public DateTime? RotatedAtUtc { get; private set; }

    /// <summary>Self-referencing pointer to the next generation's ledger row, once known.</summary>
    public Guid? RotatedIntoLedgerEntryId { get; private set; }

    public DeviceSession DeviceSession { get; private set; } = null!;

    private RefreshTokenLedgerEntry() { }

    public static RefreshTokenLedgerEntry CreateActive(Guid deviceSessionId, Guid tokenFamilyId, string tokenHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        return new RefreshTokenLedgerEntry
        {
            Id              = Guid.CreateVersion7(),
            DeviceSessionId = deviceSessionId,
            TokenFamilyId   = tokenFamilyId,
            TokenHash       = tokenHash,
            Status          = DeviceSessionStatuses.Active,
            IssuedAtUtc     = DateTime.UtcNow,
        };
    }

    public void MarkRotated(Guid rotatedIntoLedgerEntryId)
    {
        Status                    = DeviceSessionStatuses.Rotated;
        RotatedAtUtc              = DateTime.UtcNow;
        RotatedIntoLedgerEntryId  = rotatedIntoLedgerEntryId;
    }

    /// <summary>Marks this specific generation as the one whose resubmission triggered reuse detection. Informational only — the session-level MarkCompromised() is what actually blocks further use.</summary>
    public void MarkReused()
    {
        Status = DeviceSessionStatuses.Reused;
    }

    public void MarkRevoked()
    {
        if (Status is DeviceSessionStatuses.Revoked or DeviceSessionStatuses.Reused) return;
        Status = DeviceSessionStatuses.Revoked;
    }
}
