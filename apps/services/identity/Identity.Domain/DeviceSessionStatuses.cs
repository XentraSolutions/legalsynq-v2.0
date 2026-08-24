namespace Identity.Domain;

/// <summary>
/// BE-BIO: Shared status constants for DeviceSession and RefreshTokenLedgerEntry.
/// Not every status applies to every entity — DeviceSession uses
/// Active/Revoked/Expired/Compromised; RefreshTokenLedgerEntry uses
/// Active/Rotated/Reused/Revoked. Sharing one set of constants avoids
/// duplicate string literals drifting between the two tables.
/// </summary>
public static class DeviceSessionStatuses
{
    public const string Active      = "ACTIVE";
    public const string Rotated     = "ROTATED";
    public const string Revoked     = "REVOKED";
    public const string Expired     = "EXPIRED";
    public const string Reused      = "REUSED";
    public const string Compromised = "COMPROMISED";
}
