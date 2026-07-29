namespace Liens.Application.Interfaces;

public interface IPublicBuyerAccountProvisioningService
{
    Task<PublicBuyerAccountStatusResult> GetBuyerAccountStatusAsync(
        PublicBuyerAccountStatusRequest request,
        CancellationToken ct = default);

    Task<PublicBuyerAccountProvisioningResult> ProvisionBuyerAccountAsync(
        PublicBuyerAccountProvisioningRequest request,
        CancellationToken ct = default);
}

public sealed record PublicBuyerAccountStatusRequest(
    Guid TenantId,
    string Email);

public sealed record PublicBuyerAccountStatusResult(
    bool Success,
    bool AccountExists,
    string? ErrorCode,
    string? ErrorMessage,
    int? StatusCode)
{
    public static PublicBuyerAccountStatusResult Found(bool accountExists)
        => new(true, accountExists, null, null, null);

    public static PublicBuyerAccountStatusResult Failed(
        string errorCode,
        string errorMessage,
        int? statusCode = null)
        => new(false, false, errorCode, errorMessage, statusCode);
}

public sealed record PublicBuyerAccountProvisioningRequest(
    Guid TenantId,
    Guid BuyerOrgId,
    string BuyerCompanyName,
    string Email,
    string Password,
    string FirstName,
    string? LastName,
    string? Phone);

public sealed record PublicBuyerAccountProvisioningResult(
    bool Success,
    Guid? UserId,
    bool IsNew,
    string? ErrorCode,
    string? ErrorMessage,
    int? StatusCode)
{
    public static PublicBuyerAccountProvisioningResult Created(Guid userId, bool isNew)
        => new(true, userId, isNew, null, null, null);

    public static PublicBuyerAccountProvisioningResult Failed(
        string errorCode,
        string errorMessage,
        int? statusCode = null)
        => new(false, null, false, errorCode, errorMessage, statusCode);
}
