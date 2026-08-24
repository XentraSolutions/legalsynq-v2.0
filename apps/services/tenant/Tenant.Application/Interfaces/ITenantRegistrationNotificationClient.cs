namespace Tenant.Application.Interfaces;

public interface ITenantRegistrationNotificationClient
{
    Task<(bool Success, string? Error)> SendSubmittedAsync(
        Guid registrationId, string toEmail, string displayName, string tenantName,
        CancellationToken ct = default);

    Task<(bool Success, string? Error)> SendDeclinedAsync(
        Guid registrationId, string toEmail, string displayName, string tenantName, string reason,
        CancellationToken ct = default);
}
