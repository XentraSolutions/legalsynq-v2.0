namespace Xenia.Domain.Email;

/// <summary>
/// Durable database-backed synchronization lock for a single email source.
///
/// Ensures only one sync run is active per (TenantId, EmailSourceId) across
/// all service instances. Replaces the in-process SemaphoreSlim lock which is
/// not durable across process restarts or in multi-instance deployments.
///
/// Lock acquisition is atomic via unique constraint on (TenantId, EmailSourceId).
/// Expired locks can be recovered by any instance.
///
/// Security: TenantId is always part of the key — cross-tenant lock interference
/// is impossible by schema constraint.
/// </summary>
public sealed class EmailSourceSyncLock
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EmailSourceId { get; private set; }

    /// <summary>Unique identifier for the lock-holding worker instance (e.g. hostname + PID).</summary>
    public string LeaseOwnerId { get; private set; } = string.Empty;

    public DateTime AcquiredAt { get; private set; }
    public DateTime RenewedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    /// <summary>
    /// Monotonically increasing fencing token. Incremented on every acquisition.
    /// A worker that lost its lease must validate this matches its own token before
    /// committing cursor updates, run completion, or message state changes.
    /// </summary>
    public long FencingToken { get; private set; }

    /// <summary>Number of consecutive renewal failures while this lease is held.</summary>
    public int RenewalFailureCount { get; private set; }

    /// <summary>Optimistic concurrency version. Incremented on every state change.</summary>
    public int Version { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private EmailSourceSyncLock() { }

    /// <summary>Creates a new lock row for the given source.</summary>
    public static EmailSourceSyncLock Create(
        Guid tenantId,
        Guid emailSourceId,
        string leaseOwnerId,
        DateTime expiresAt)
    {
        var now = DateTime.UtcNow;
        return new EmailSourceSyncLock
        {
            Id               = Guid.CreateVersion7(),
            TenantId         = tenantId,
            EmailSourceId    = emailSourceId,
            LeaseOwnerId     = leaseOwnerId,
            AcquiredAt       = now,
            RenewedAt        = now,
            ExpiresAt        = expiresAt,
            FencingToken     = 1,
            RenewalFailureCount = 0,
            Version          = 1,
            CreatedAt        = now,
            UpdatedAt        = now,
        };
    }

    /// <summary>Takes over a stale/expired lock with a new owner. Increments the fencing token.</summary>
    public void Acquire(string leaseOwnerId, DateTime expiresAt)
    {
        var now = DateTime.UtcNow;
        LeaseOwnerId        = leaseOwnerId;
        AcquiredAt          = now;
        RenewedAt           = now;
        ExpiresAt           = expiresAt;
        FencingToken++;
        RenewalFailureCount = 0;
        Version++;
        UpdatedAt           = now;
    }

    /// <summary>Renews the lease expiry — only the current owner may renew.</summary>
    public void Renew(string leaseOwnerId, DateTime newExpiresAt)
    {
        if (!string.Equals(LeaseOwnerId, leaseOwnerId, StringComparison.Ordinal))
            throw new InvalidOperationException("Cannot renew a lock owned by a different instance.");

        var now  = DateTime.UtcNow;
        RenewedAt = now;
        ExpiresAt  = newExpiresAt;
        RenewalFailureCount = 0;
        Version++;
        UpdatedAt  = now;
    }

    /// <summary>Records a renewal failure. Returns true when the failure threshold is reached.</summary>
    public bool RecordRenewalFailure(int failureThreshold = 3)
    {
        RenewalFailureCount++;
        Version++;
        UpdatedAt = DateTime.UtcNow;
        return RenewalFailureCount >= failureThreshold;
    }

    /// <summary>
    /// Validates that the given fencing token matches the current token.
    /// Workers must call this before committing any cursor, run-completion, or message-state change.
    /// </summary>
    public bool ValidateFencingToken(long callerToken) => FencingToken == callerToken;

    /// <summary>Releases the lock by setting ExpiresAt to the past.</summary>
    public void Release()
    {
        ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
        Version++;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Whether the lease has expired and may be recovered by another instance.</summary>
    public bool IsExpired => ExpiresAt < DateTime.UtcNow;
}
