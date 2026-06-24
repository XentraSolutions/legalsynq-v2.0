namespace CareConnect.Application.DTOs;

public sealed record ReferralTokenValidationOutcome
{
    public bool IsValid { get; init; }
    public Guid? ReferralId { get; init; }
    public int? TokenVersion { get; init; }
    public string? FailureReason { get; init; }

    public static ReferralTokenValidationOutcome Success(Guid referralId, int tokenVersion) => new()
    {
        IsValid = true,
        ReferralId = referralId,
        TokenVersion = tokenVersion,
    };

    public static ReferralTokenValidationOutcome Failure(
        string failureReason,
        Guid? referralId = null,
        int? tokenVersion = null) => new()
    {
        IsValid = false,
        FailureReason = failureReason,
        ReferralId = referralId,
        TokenVersion = tokenVersion,
    };
}
