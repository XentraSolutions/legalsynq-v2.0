namespace Liens.Application.Interfaces;

public interface IPublicBuyerAccountProvisioningService
{
    Task<PublicBuyerAccountProvisioningResult> ProvisionBuyerAccountAsync(
        PublicBuyerAccountProvisioningRequest request,
        CancellationToken ct = default);
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
