namespace CareConnect.Application.DTOs;

public class ReferralAttributionAccessCodeResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ReferralAttributionId { get; set; }
    public string? ReferralAttributionFullName { get; set; }
    public bool IsActive { get; set; }
    public DateTime? AccessStartAtUtc { get; set; }
    public DateTime? AccessEndAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

/// <summary>
/// Returned only from the generate endpoint, only once. Code is the plaintext value —
/// it is never persisted and can never be retrieved again after this response.
/// </summary>
public class GeneratedReferralAttributionAccessCodeResponse : ReferralAttributionAccessCodeResponse
{
    public string Code { get; set; } = string.Empty;
}

public class CreateReferralAttributionAccessCodeRequest
{
    public Guid ReferralAttributionId { get; set; }
    public DateTime? AccessStartAtUtc { get; set; }
    public DateTime? AccessEndAtUtc { get; set; }
}

public class SetReferralAttributionAccessCodeActiveRequest
{
    public bool IsActive { get; set; }
}

/// <summary>
/// Anonymous, stateless code check for the Representative Portal — no login, no
/// "redeemer" recorded. Every subsequent data request must present the same code again
/// (see PublicRepresentativeEndpoints); nothing here mutates the access code record.
/// </summary>
public class VerifyReferralAttributionAccessCodeRequest
{
    public string Code { get; set; } = string.Empty;
}

public class VerifyReferralAttributionAccessCodeResponse
{
    public bool Ok { get; set; }
    public Guid? ReferralAttributionId { get; set; }
    public string? ReferralAttributionFullName { get; set; }
}
