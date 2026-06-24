namespace CareConnect.Application.DTOs;

public sealed record PublicReferralAccessResult<T>
{
    public T? Data { get; init; }
    public string? FailureReason { get; init; }

    public static PublicReferralAccessResult<T> Success(T data) => new()
    {
        Data = data,
    };

    public static PublicReferralAccessResult<T> Failure(string failureReason) => new()
    {
        FailureReason = failureReason,
    };
}
