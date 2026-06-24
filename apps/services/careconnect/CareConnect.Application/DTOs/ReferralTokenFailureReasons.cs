namespace CareConnect.Application.DTOs;

public static class ReferralTokenFailureReasons
{
    public const string Missing = "missing";
    public const string Malformed = "malformed";
    public const string Expired = "expired";
    public const string Revoked = "revoked";
    public const string ReferralMismatch = "referral_mismatch";
    public const string ReferralNotFound = "referral_not_found";
}
