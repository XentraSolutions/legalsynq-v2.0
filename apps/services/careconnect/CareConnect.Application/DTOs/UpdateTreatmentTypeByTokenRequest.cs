namespace CareConnect.Application.DTOs;

/// <summary>Body for the public token-gated treatment-type update endpoint on the provider thread page.</summary>
public class UpdateTreatmentTypeByTokenRequest
{
    /// <summary>The HMAC-signed view token generated at referral creation time.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// The treatment type to set. Null or omitted clears the current value.
    /// Guid.Empty is also treated as a clear.
    /// </summary>
    public Guid? TreatmentTypeId { get; set; }
}
