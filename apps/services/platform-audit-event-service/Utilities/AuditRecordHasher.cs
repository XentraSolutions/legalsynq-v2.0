using System.Security.Cryptography;
using System.Text;
using PlatformAuditEventService.Entities;

namespace PlatformAuditEventService.Utilities;

/// <summary>
/// Produces a deterministic HMAC-SHA256 integrity hash over the canonical fields of
/// an <see cref="AuditEventRecord"/>.
///
/// Canonical field set (order is significant — changes break existing records):
///   AuditId | EventType | SourceSystem | TenantId | ActorId |
///   EntityType | EntityId | Action | OccurredAtUtc (O) | RecordedAtUtc (O)
///
/// Fields that are NULL are represented as empty string in the canonical string.
/// This prevents reordering or injection attacks across optional fields.
///
/// Separation of concerns:
///   This class covers <see cref="AuditEventRecord"/> (the canonical entity from Step 3).
///   The legacy <see cref="IntegrityHasher"/> covers the old <c>AuditEvent</c> flat model
///   and is maintained for backward compatibility only.
/// </summary>
public static class AuditRecordHasher
{
    private const char Separator = '|';

    /// <summary>
    /// Computes the integrity hash from individual canonical field values.
    ///
    /// This overload is called by the ingest service BEFORE creating the entity,
    /// because the entity's <c>Hash</c> field is <c>init</c>-only and must be
    /// supplied at construction time. The service generates <c>auditId</c> and
    /// <c>recordedAtUtc</c> itself and passes them here so the hash can be computed
    /// over the exact values that will be persisted.
    /// </summary>
    public static string Compute(
        Guid           auditId,
        string         eventType,
        string         sourceSystem,
        string?        tenantId,
        string?        actorId,
        string?        entityType,
        string?        entityId,
        string         action,
        DateTimeOffset occurredAtUtc,
        DateTimeOffset recordedAtUtc,
        byte[]         hmacSecret)
    {
        var canonical = BuildCanonical(
            auditId, eventType, sourceSystem,
            tenantId, actorId, entityType, entityId,
            action, occurredAtUtc, recordedAtUtc);

        return ComputeHmac(canonical, hmacSecret);
    }

    /// <summary>
    /// Computes the integrity hash directly from a persisted <see cref="AuditEventRecord"/>.
    /// Used for verification on read (when <c>IntegrityOptions.VerifyOnRead = true</c>).
    /// </summary>
    public static string ComputeFromEntity(AuditEventRecord record, byte[] hmacSecret)
    {
        var canonical = BuildCanonical(
            record.AuditId,
            record.EventType,
            record.SourceSystem,
            record.TenantId,
            record.ActorId,
            record.EntityType,
            record.EntityId,
            record.Action,
            record.OccurredAtUtc,
            record.RecordedAtUtc);

        return ComputeHmac(canonical, hmacSecret);
    }

    /// <summary>
    /// Verifies that a persisted record's <see cref="AuditEventRecord.Hash"/> matches
    /// a freshly computed hash using the provided HMAC secret.
    ///
    /// Uses constant-time comparison to resist timing-based side-channel attacks.
    /// Returns false when the record's Hash is null (integrity signing was disabled
    /// when the record was written).
    /// </summary>
    public static bool Verify(AuditEventRecord record, byte[] hmacSecret)
    {
        if (record.Hash is null)
            return false;

        var expected = ComputeFromEntity(record, hmacSecret);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(record.Hash));
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static string BuildCanonical(
        Guid auditId, string eventType, string sourceSystem,
        string? tenantId, string? actorId, string? entityType, string? entityId,
        string action, DateTimeOffset occurredAtUtc, DateTimeOffset recordedAtUtc)
    {
        // Use Span<string> for stack allocation efficiency
        return string.Join(Separator,
            auditId.ToString("D"),                  // canonical GUID format (lowercase, hyphens)
            eventType,
            sourceSystem,
            tenantId      ?? string.Empty,
            actorId       ?? string.Empty,
            entityType    ?? string.Empty,
            entityId      ?? string.Empty,
            action,
            occurredAtUtc.ToString("O"),            // ISO 8601 round-trip format
            recordedAtUtc.ToString("O"));
    }

    private static string ComputeHmac(string canonical, byte[] hmacSecret)
    {
        using var hmac = new HMACSHA256(hmacSecret);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
