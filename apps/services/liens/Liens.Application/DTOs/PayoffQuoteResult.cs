namespace Liens.Application.DTOs;

public enum PayoffQuoteStatus
{
    Success,
    CaseNotFound,
    Unavailable,
}

public sealed class PayoffQuoteResult
{
    public PayoffQuoteStatus Status { get; init; }
    public string Url { get; init; } = string.Empty;
    public string Base64 { get; init; } = string.Empty;

    public static PayoffQuoteResult Success(string url, string base64) => new()
    {
        Status = PayoffQuoteStatus.Success,
        Url = url,
        Base64 = base64,
    };

    public static PayoffQuoteResult CaseNotFound() => new()
    {
        Status = PayoffQuoteStatus.CaseNotFound,
    };

    public static PayoffQuoteResult Unavailable() => new()
    {
        Status = PayoffQuoteStatus.Unavailable,
    };
}
