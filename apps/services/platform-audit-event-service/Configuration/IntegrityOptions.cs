namespace PlatformAuditEventService.Configuration;

/// <summary>
/// Tamper-evidence / integrity hash options.
/// Bound from "Integrity" section in appsettings.
/// Environment variable override prefix: Integrity__
/// </summary>
public sealed class IntegrityOptions
{
    public const string SectionName = "Integrity";

    /// <summary>
    /// Base64-encoded 256-bit (32-byte) HMAC secret used to compute
    /// the integrity hash on every persisted AuditEvent.
    /// Must be set to a cryptographically random value in production.
    /// Generate: openssl rand -base64 32
    /// Environment variable: Integrity__HmacKeyBase64
    /// </summary>
    public string? HmacKeyBase64 { get; set; }

    /// <summary>
    /// Hash algorithm. Currently only "HMAC-SHA256" is supported.
    /// Reserved for future algorithm agility.
    /// </summary>
    public string Algorithm { get; set; } = "HMAC-SHA256";

    /// <summary>
    /// When true, integrity hash is verified on every read (GetById, Query).
    /// Failed verification logs a CRITICAL alert but does NOT suppress the record.
    /// Performance impact: one HMAC-SHA256 per record returned.
    /// Recommended: true in production, false in high-throughput dev/test.
    /// </summary>
    public bool VerifyOnRead { get; set; } = false;

    /// <summary>
    /// When true, records with missing or mismatched IntegrityHash are flagged
    /// with a "TamperSuspected" marker in the response.
    /// Only applies when VerifyOnRead = true.
    /// </summary>
    public bool FlagTamperedRecords { get; set; } = true;
}
