namespace Tenant.Application.DTOs;

public sealed record SubmitTenantRegistrationRequest(string TenantName, string TenantCode, string OrganizationType,
    string? StreetAddress, string AdminFirstName, string AdminLastName, string AdminEmail,
    string? AddressLine1 = null, string? AddressCity = null, string? AddressState = null, string? AddressPostalCode = null);
public sealed record SubmitTenantRegistrationResponse(Guid RegistrationId, string RegistrationStatus,
    string ProvisioningStatus, string Message);
public sealed record DeclineTenantRegistrationRequest(string Reason);
public sealed record TenantRegistrationResponse(Guid Id, string TenantName, string TenantCode, string OrganizationType,
    string? StreetAddress, string? AddressLine1, string? AddressCity, string? AddressState, string? AddressPostalCode, string AdminFirstName, string AdminLastName, string AdminEmail,
    string RegistrationStatus, string ProvisioningStatus, Guid? TenantId, string? ProvisioningHostname,
    string? ProvisioningError, string? ProvisioningFailureStage, string? DecisionReason, Guid? ReviewedByUserId,
    DateTime? ReviewedAtUtc, DateTime? ProvisioningStartedAtUtc, DateTime? ProvisionedAtUtc,
    DateTime CreatedAtUtc, DateTime UpdatedAtUtc);
public sealed record TenantRegistrationListResponse(IReadOnlyList<TenantRegistrationResponse> Items, int TotalCount,
    int Page, int PageSize);
public sealed record TenantRegistrationDecisionResponse(string RegistrationStatus, Guid? TenantId,
    string? TenantStatus, string AdministratorEmail, string ProvisioningStatus, string? Hostname,
    IReadOnlyList<string> ProvisioningWarnings, IReadOnlyList<string> ProvisioningErrors,
    string NextAction, string? FailureStage = null);
