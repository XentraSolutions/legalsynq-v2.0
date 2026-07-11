using Xenia.Domain.Automation;

namespace Xenia.Application.Automation;

/// <summary>
/// Deduplicates automation execution requests using durable idempotency records.
///
/// The DB-level unique constraint on (tenant_id, automation_key, idempotency_key)
/// is the atomic fence that prevents duplicate executions across concurrent requests.
///
/// Safety:
/// - RequestFingerprint is a SHA-256 hash of safe canonical request metadata.
/// - No raw request payload is stored.
/// - Expired records may be purged without affecting active executions.
/// </summary>
public interface IAutomationIdempotencyService
{
    /// <summary>
    /// Attempts to reserve an idempotency slot for the supplied key.
    ///
    /// Returns:
    /// - <see cref="IdempotencyReservationResult.Reserved"/> — slot claimed, caller should proceed.
    /// - <see cref="IdempotencyReservationResult.AlreadyExists"/> — duplicate request; ExecutionId may be set if bound.
    /// - <see cref="IdempotencyReservationResult.Conflict"/> — same key, different fingerprint.
    /// </summary>
    Task<IdempotencyReservation> TryReserveAsync(
        Guid tenantId,
        string automationKey,
        string idempotencyKey,
        string requestFingerprint,
        DateTime expiresAt,
        CancellationToken ct = default);

    /// <summary>
    /// Binds the reserved idempotency record to the created execution ID.
    /// Must be called immediately after creating the execution record.
    /// </summary>
    Task<bool> BindExecutionAsync(
        Guid tenantId,
        string automationKey,
        string idempotencyKey,
        Guid executionId,
        CancellationToken ct = default);

    Task<AutomationIdempotencyRecord?> GetAsync(
        Guid tenantId,
        string automationKey,
        string idempotencyKey,
        CancellationToken ct = default);
}

public sealed record IdempotencyReservation
{
    public required IdempotencyReservationResult Result { get; init; }

    /// <summary>Set when Result is AlreadyExists and the slot is already bound to an execution.</summary>
    public Guid? ExistingExecutionId { get; init; }

    public static IdempotencyReservation Reserved() =>
        new() { Result = IdempotencyReservationResult.Reserved };

    public static IdempotencyReservation AlreadyExists(Guid? executionId) =>
        new() { Result = IdempotencyReservationResult.AlreadyExists, ExistingExecutionId = executionId };

    public static IdempotencyReservation Conflict() =>
        new() { Result = IdempotencyReservationResult.Conflict };
}

public enum IdempotencyReservationResult
{
    Reserved,
    AlreadyExists,
    Conflict,
}
