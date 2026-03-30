using System.Text.Json;
using PlatformAuditEventService.DTOs.Ingest;
using PlatformAuditEventService.Entities;

namespace PlatformAuditEventService.Mappers;

/// <summary>
/// Maps an <see cref="IngestAuditEventRequest"/> to a fully-initialised
/// <see cref="AuditEventRecord"/> ready for persistence.
///
/// Responsibilities of the mapper (vs. the ingest service):
///   Mapper   — pure structural translation; no I/O, no side effects.
///   Service  — idempotency check, hash computation, chain lookup.
///
/// Hash/PreviousHash fields are left null here. The ingest service
/// populates them after the idempotency check, immediately before
/// calling AppendAsync.
///
/// AuditId is generated as a new random Guid. When a UUIDv7 library is
/// available in the project (time-ordered inserts, better clustered-index
/// locality on MySQL), replace <c>Guid.NewGuid()</c> with the v7 factory.
/// </summary>
public static class AuditEventRecordMapper
{
    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        WriteIndented          = false,
        PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Translates the inbound DTO into a new <see cref="AuditEventRecord"/>.
    /// </summary>
    /// <param name="req">The validated ingest request.</param>
    /// <param name="now">
    /// Server receipt timestamp (UTC). Used as <c>RecordedAtUtc</c> and as
    /// <c>OccurredAtUtc</c> when the caller omits the event time.
    /// </param>
    public static AuditEventRecord ToEntity(IngestAuditEventRequest req, DateTimeOffset now)
    {
        return new AuditEventRecord
        {
            // ── Identity ──────────────────────────────────────────────────
            AuditId = Guid.NewGuid(),   // TODO: upgrade to UUIDv7 for clustered inserts
            EventId = req.EventId,

            // ── Classification ────────────────────────────────────────────
            EventType     = req.EventType,
            EventCategory = req.EventCategory,

            // ── Source provenance ─────────────────────────────────────────
            SourceSystem      = req.SourceSystem,
            SourceService     = req.SourceService,
            SourceEnvironment = req.SourceEnvironment,

            // ── Scope / tenancy ───────────────────────────────────────────
            PlatformId     = ParseGuid(req.Scope.PlatformId),
            TenantId       = req.Scope.TenantId,
            OrganizationId = req.Scope.OrganizationId,
            UserScopeId    = req.Scope.UserId,
            ScopeType      = req.Scope.ScopeType,

            // ── Actor ─────────────────────────────────────────────────────
            ActorId        = req.Actor.Id,
            ActorType      = req.Actor.Type,
            ActorName      = req.Actor.Name,
            ActorIpAddress = req.Actor.IpAddress,
            ActorUserAgent = req.Actor.UserAgent,

            // ── Target entity ─────────────────────────────────────────────
            EntityType = req.Entity?.Type,
            EntityId   = req.Entity?.Id,

            // ── Action description ────────────────────────────────────────
            Action      = req.Action,
            Description = req.Description,

            // ── State snapshots (stored verbatim) ─────────────────────────
            BeforeJson   = req.Before,
            AfterJson    = req.After,
            MetadataJson = req.Metadata,

            // ── Correlation / tracing ─────────────────────────────────────
            CorrelationId = req.CorrelationId,
            RequestId     = req.RequestId,
            SessionId     = req.SessionId,

            // ── Access control ────────────────────────────────────────────
            VisibilityScope = req.Visibility,
            Severity        = req.Severity,

            // ── Timestamps ────────────────────────────────────────────────
            OccurredAtUtc = req.OccurredAtUtc ?? now,   // fall back to server time
            RecordedAtUtc = now,

            // ── Integrity chain (computed by ingest service, not the mapper) ──
            Hash         = null,
            PreviousHash = null,

            // ── Dedup / replay ────────────────────────────────────────────
            IdempotencyKey = req.IdempotencyKey,
            IsReplay       = req.IsReplay,

            // ── Tags ──────────────────────────────────────────────────────
            TagsJson = SerializeTags(req.Tags),
        };
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Attempts to parse a string as a <see cref="Guid"/>.
    /// Returns null when the input is null, empty, or not a valid GUID format.
    /// </summary>
    private static Guid? ParseGuid(string? value) =>
        Guid.TryParse(value, out var g) ? g : null;

    /// <summary>
    /// Serializes a tag list to a compact JSON array string, or null when the list
    /// is null or empty. Stored as a raw <c>text</c> column; parsed by query consumers.
    /// </summary>
    private static string? SerializeTags(IReadOnlyList<string>? tags) =>
        tags is { Count: > 0 }
            ? JsonSerializer.Serialize(tags, _jsonOpts)
            : null;
}
