using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PlatformAuditEventService.Configuration;
using PlatformAuditEventService.DTOs.Ingest;
using PlatformAuditEventService.Mappers;
using PlatformAuditEventService.Repositories;
using PlatformAuditEventService.Utilities;

namespace PlatformAuditEventService.Services;

/// <summary>
/// Canonical ingestion pipeline for audit event records.
///
/// Pipeline (per event):
///   1. Idempotency check  — probe IdempotencyKey; return DuplicateIdempotencyKey if found.
///   2. AuditId + time     — generate AuditId (Guid.NewGuid) and capture RecordedAtUtc (server UTC).
///   3. Chain lookup       — fetch PreviousHash from the (TenantId, SourceSystem) chain head.
///                           Skipped when integrity signing is disabled.
///   4. Hash computation   — HMAC-SHA256 over canonical fields; needs AuditId + RecordedAtUtc
///                           computed in step 2 so the hash covers the exact persisted values.
///                           Skipped when integrity signing is disabled.
///   5. Entity mapping     — AuditEventRecordMapper.ToEntity receives all values including hashes.
///   6. Append             — IAuditEventRecordRepository.AppendAsync (append-only, no updates).
///   7. Result             — IngestItemResult { Accepted, AuditId } or rejection with reason.
///
/// Integrity signing:
///   Enabled when <c>IntegrityOptions.HmacKeyBase64</c> is non-null and non-empty.
///   When disabled, Hash and PreviousHash are null on the persisted record.
///   The service never throws due to a missing key — it silently omits hashes.
///   This allows development and staging environments to run without configuring a key.
///
/// Replay records:
///   <see cref="IngestAuditEventRequest.IsReplay"/> = true marks the record as a replay of a
///   previously ingested event (e.g. during a migration or re-processing run). Replay records:
///     - Bypass idempotency enforcement when no IdempotencyKey is supplied (no key → no check).
///     - Still get a new AuditId and RecordedAtUtc assigned by this server.
///     - Still participate in the integrity chain (linked via PreviousHash).
///     - Are persisted as normal records; the IsReplay flag is a semantic marker only.
///   Callers that supply IdempotencyKey on a replay are still protected against double-submission.
///
/// Transport extensibility:
///   The service delegates persistence exclusively through <see cref="IAuditEventRecordRepository"/>.
///   To switch from direct-to-database ingest to queued or outbox-driven ingest, register a
///   different repository implementation that writes to a queue or outbox table:
///
///     Direct    (default)  — EfAuditEventRecordRepository writes synchronously to AuditEventRecords.
///     Queued    (future)   — QueuedAuditEventRecordRepository enqueues the record; a worker persists.
///     Outbox    (future)   — OutboxAuditEventRecordRepository writes to a transactional outbox;
///                            a relay background service moves records to AuditEventRecords.
///
///   The idempotency probe (ExistsIdempotencyKeyAsync) and chain lookup (GetLatestInChainAsync)
///   are also on the repository interface. For queued transport, the pre-ingestion idempotency
///   probe becomes best-effort (race window exists between probe and consumer write). The consumer
///   must enforce idempotency at the final write using a unique index on IdempotencyKey.
/// </summary>
public sealed class AuditEventIngestionService : IAuditEventIngestionService
{
    // ── Rejection reason constants ─────────────────────────────────────────────
    // Centralised here so callers and tests can reference them without magic strings.

    /// <summary>Supplied IdempotencyKey already exists in the record store.</summary>
    public const string ReasonDuplicateIdempotencyKey = "DuplicateIdempotencyKey";

    /// <summary>DB write failed due to a transient or permanent persistence error.</summary>
    public const string ReasonPersistenceError        = "PersistenceError";

    /// <summary>
    /// Processing was halted by <see cref="BatchIngestRequest.StopOnFirstError"/> after
    /// a prior item in the same batch failed. This item was never attempted.
    /// </summary>
    public const string ReasonSkipped                 = "Skipped";

    // ── Fields ────────────────────────────────────────────────────────────────

    private readonly IAuditEventRecordRepository         _records;
    private readonly IntegrityOptions                    _integrity;
    private readonly byte[]?                             _hmacSecret;
    private readonly ILogger<AuditEventIngestionService> _logger;

    // ── Constructor ───────────────────────────────────────────────────────────

    public AuditEventIngestionService(
        IAuditEventRecordRepository         records,
        IOptions<IntegrityOptions>          integrityOptions,
        ILogger<AuditEventIngestionService> logger)
    {
        _records   = records;
        _integrity = integrityOptions.Value;
        _logger    = logger;

        // Integrity signing is optional. When the key is absent (development or staging
        // environments where tamper-evidence is not required), signing is silently skipped.
        _hmacSecret = _integrity.HmacKeyBase64 is { Length: > 0 }
            ? Convert.FromBase64String(_integrity.HmacKeyBase64)
            : null;
    }

    // ── IAuditEventIngestionService ───────────────────────────────────────────

    /// <inheritdoc/>
    public Task<IngestItemResult> IngestSingleAsync(
        IngestAuditEventRequest request,
        CancellationToken ct = default) =>
        IngestOneAsync(request, index: 0, batchCorrelationFallback: null, ct);

    /// <inheritdoc/>
    public async Task<BatchIngestResponse> IngestBatchAsync(
        BatchIngestRequest request,
        CancellationToken ct = default)
    {
        var events  = request.Events;
        var results = new List<IngestItemResult>(events.Count);
        var accepted = 0;

        for (var i = 0; i < events.Count; i++)
        {
            var result = await IngestOneAsync(
                events[i],
                index:                    i,
                batchCorrelationFallback: request.BatchCorrelationId,
                ct);

            results.Add(result);

            if (result.Accepted)
            {
                accepted++;
            }
            else if (request.StopOnFirstError)
            {
                // Append Skipped placeholder results for every untried item.
                for (var j = i + 1; j < events.Count; j++)
                {
                    results.Add(new IngestItemResult
                    {
                        Index          = j,
                        EventType      = events[j].EventType,
                        IdempotencyKey = events[j].IdempotencyKey,
                        Accepted       = false,
                        RejectionReason = ReasonSkipped,
                    });
                }

                break;
            }
        }

        var rejected = events.Count - accepted;

        _logger.LogInformation(
            "Batch ingest complete: Submitted={Submitted} Accepted={Accepted} Rejected={Rejected} " +
            "BatchCorrelationId={BatchCorrelationId}",
            events.Count, accepted, rejected, request.BatchCorrelationId);

        return new BatchIngestResponse
        {
            Submitted          = events.Count,
            Accepted           = accepted,
            Rejected           = rejected,
            Results            = results,
            BatchCorrelationId = request.BatchCorrelationId,
        };
    }

    // ── Core single-event pipeline ─────────────────────────────────────────────

    /// <summary>
    /// Executes the full ingestion pipeline for a single event.
    /// Called by both <see cref="IngestSingleAsync"/> and <see cref="IngestBatchAsync"/>.
    /// </summary>
    /// <param name="req">The validated ingest request for this event.</param>
    /// <param name="index">Zero-based index of this item in the enclosing batch (0 for single ingest).</param>
    /// <param name="batchCorrelationFallback">
    /// BatchCorrelationId from the enclosing batch request, used as a CorrelationId fallback
    /// when the individual item does not supply one. Null for single-event ingest.
    /// </param>
    private async Task<IngestItemResult> IngestOneAsync(
        IngestAuditEventRequest req,
        int                     index,
        string?                 batchCorrelationFallback,
        CancellationToken       ct)
    {
        // ── Step 1: Idempotency check ─────────────────────────────────────────
        //
        // Only check when an IdempotencyKey was supplied. Callers without a key get
        // no deduplication guard (intentional — they are responsible for retrying safely).
        // Replay records with a key are still protected against double submission.
        if (!string.IsNullOrWhiteSpace(req.IdempotencyKey))
        {
            var isDuplicate = await _records.ExistsIdempotencyKeyAsync(req.IdempotencyKey, ct);
            if (isDuplicate)
            {
                _logger.LogDebug(
                    "Duplicate IdempotencyKey rejected: Key={Key} EventType={EventType}",
                    req.IdempotencyKey, req.EventType);

                return Rejected(index, req, ReasonDuplicateIdempotencyKey);
            }
        }

        // ── Step 2: Server-side identity and timestamp ────────────────────────
        //
        // AuditId and RecordedAtUtc are generated here, not by the mapper, because the
        // integrity hash (step 4) must cover their exact values. If the mapper generated
        // them internally we would need to construct the entity first and then recompute
        // the hash — requiring either mutable fields or a two-allocation pattern.
        //
        // TODO: replace Guid.NewGuid() with a UUIDv7 factory once available. UUIDv7
        //       GUIDs are time-ordered, which improves clustered-index insert locality
        //       on MySQL / MariaDB (Pomelo target) significantly for high-volume append.
        var auditId = Guid.NewGuid();
        var now     = DateTimeOffset.UtcNow;

        // ── Step 3: Integrity chain lookup ────────────────────────────────────
        //
        // Fetch the hash from the most recently persisted record in the same
        // (TenantId, SourceSystem) chain. This becomes PreviousHash on the new record,
        // forming a singly-linked hash chain that can be audited for gaps or insertions.
        //
        // Skipped entirely when integrity signing is disabled (no HMAC key configured).
        string? previousHash = null;
        if (_hmacSecret is not null)
        {
            var chainHead = await _records.GetLatestInChainAsync(
                req.Scope.TenantId, req.SourceSystem, ct);

            previousHash = chainHead?.Hash;
        }

        // ── Step 4: Integrity hash computation ────────────────────────────────
        //
        // Computed from the canonical field set over the values that will be persisted.
        // AuditId and RecordedAtUtc are generated in step 2 and are known here before
        // the entity is created, avoiding any chicken-and-egg ordering problem.
        //
        // Skipped when signing is disabled.
        string? hash = null;
        if (_hmacSecret is not null)
        {
            var occurredAtUtc = req.OccurredAtUtc ?? now;  // must match ToEntity logic

            hash = AuditRecordHasher.Compute(
                auditId:        auditId,
                eventType:      req.EventType,
                sourceSystem:   req.SourceSystem,
                tenantId:       req.Scope.TenantId,
                actorId:        req.Actor.Id,
                entityType:     req.Entity?.Type,
                entityId:       req.Entity?.Id,
                action:         req.Action,
                occurredAtUtc:  occurredAtUtc,
                recordedAtUtc:  now,
                hmacSecret:     _hmacSecret);
        }

        // ── Step 5: Entity construction ───────────────────────────────────────
        //
        // The mapper is a pure structural translation — no I/O, no side-effects.
        // We supply all externally-generated values (auditId, correlationId override,
        // hashes) so the mapper can construct the fully-initialised init-only entity
        // in a single allocation.
        //
        // CorrelationId fallback: if the item has no CorrelationId, use the batch-level
        // BatchCorrelationId so the entire batch is traceable as a unit.
        var correlationIdOverride = req.CorrelationId is null
            ? batchCorrelationFallback
            : null;    // item's own key wins; don't override it

        var entity = AuditEventRecordMapper.ToEntity(
            req,
            auditId:              auditId,
            now:                  now,
            correlationIdOverride: correlationIdOverride,
            hash:                 hash,
            previousHash:         previousHash);

        // ── Step 6: Append-only persistence ───────────────────────────────────
        //
        // AppendAsync enforces insert-only semantics (no UPDATE, no UPSERT).
        // DbUpdateException with a unique-constraint violation on IdempotencyKey
        // is caught and translated to DuplicateIdempotencyKey to handle the rare
        // concurrent duplicate submission that slips past the step-1 probe.
        try
        {
            var persisted = await _records.AppendAsync(entity, ct);

            _logger.LogInformation(
                "AuditEvent ingested: AuditId={AuditId} EventType={EventType} " +
                "SourceSystem={SourceSystem} TenantId={TenantId} IsReplay={IsReplay} " +
                "Signed={Signed}",
                persisted.AuditId, persisted.EventType, persisted.SourceSystem,
                persisted.TenantId, persisted.IsReplay, hash is not null);

            return new IngestItemResult
            {
                Index          = index,
                EventType      = req.EventType,
                IdempotencyKey = req.IdempotencyKey,
                Accepted       = true,
                AuditId        = persisted.AuditId,
            };
        }
        catch (DbUpdateException dbEx) when (IsUniqueConstraintViolation(dbEx))
        {
            // Race condition: two concurrent requests with the same IdempotencyKey both
            // passed the step-1 probe before either committed. The unique index wins.
            _logger.LogWarning(
                "Concurrent duplicate IdempotencyKey detected at commit: Key={Key} EventType={EventType}",
                req.IdempotencyKey, req.EventType);

            return Rejected(index, req, ReasonDuplicateIdempotencyKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Persistence failure for AuditEvent: AuditId={AuditId} EventType={EventType} " +
                "SourceSystem={SourceSystem}",
                auditId, req.EventType, req.SourceSystem);

            return Rejected(index, req, ReasonPersistenceError);
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Builds a rejected <see cref="IngestItemResult"/> with the given reason.
    /// </summary>
    private static IngestItemResult Rejected(
        int                     index,
        IngestAuditEventRequest req,
        string                  reason) =>
        new()
        {
            Index           = index,
            EventType       = req.EventType,
            IdempotencyKey  = req.IdempotencyKey,
            Accepted        = false,
            RejectionReason = reason,
        };

    /// <summary>
    /// Returns true when a <see cref="DbUpdateException"/> is caused by a unique-constraint
    /// violation on any column. Uses the inner exception message heuristic since EF Core
    /// does not expose a typed exception for constraint violations.
    ///
    /// Pomelo (MySQL) surfaces unique violations as MySqlException with ErrorCode 1062.
    /// The string check is a portable fallback for other providers (SQLite in tests).
    /// </summary>
    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        if (inner is null) return false;

        // Pomelo / MySQL: check the numeric error code to avoid string matching
        var innerTypeName = inner.GetType().Name;
        if (innerTypeName.Contains("MySql", StringComparison.OrdinalIgnoreCase))
        {
            // MySqlException.ErrorCode 1062 = ER_DUP_ENTRY
            var errorCodeProp = inner.GetType().GetProperty("ErrorCode") ??
                                inner.GetType().GetProperty("Number");
            if (errorCodeProp?.GetValue(inner) is int code && (code == 1062 || code == 1169))
                return true;
        }

        // Generic fallback: covers SQLite (in unit tests) and other providers
        var msg = inner.Message ?? string.Empty;
        return msg.Contains("unique", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("duplicate", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase);
    }
}
