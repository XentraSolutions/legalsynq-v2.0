using Commerce.Domain.Billing.Enums;

namespace Commerce.Contracts.Billing;

public sealed record CreateBillingAccountRequest(
    string DisplayName,
    string? LegalName,
    string DefaultCurrency);

public sealed record UpdateBillingAccountRequest(
    string DisplayName,
    string? LegalName,
    string DefaultCurrency);

public sealed record BillingAccountResponse(
    Guid Id,
    string AccountNumber,
    string DisplayName,
    string? LegalName,
    BillingAccountStatus Status,
    string DefaultCurrency,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record CreateExternalRefRequest(
    string HostPlatformKey,
    string ExternalTenantId,
    string? ExternalCustomerRef,
    bool IsPrimary = false);

public sealed record UpdateExternalRefRequest(
    string HostPlatformKey,
    string ExternalTenantId,
    string? ExternalCustomerRef);

public sealed record ExternalRefResponse(
    Guid Id,
    Guid BillingAccountId,
    string HostPlatformKey,
    string ExternalTenantId,
    string? ExternalCustomerRef,
    bool IsPrimary,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record CreateBillingContactRequest(
    BillingContactType ContactType,
    string Name,
    string Email,
    string? Phone,
    bool IsPrimary = false);

public sealed record UpdateBillingContactRequest(
    BillingContactType ContactType,
    string Name,
    string Email,
    string? Phone);

public sealed record BillingContactResponse(
    Guid Id,
    Guid BillingAccountId,
    BillingContactType ContactType,
    string Name,
    string Email,
    string? Phone,
    bool IsPrimary,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record UpdateBillingProfileRequest(
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? StateRegion,
    string? PostalCode,
    string? Country,
    string? TaxId,
    bool TaxExempt = false);

public sealed record BillingProfileResponse(
    Guid Id,
    Guid BillingAccountId,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? StateRegion,
    string? PostalCode,
    string? Country,
    string? TaxId,
    bool TaxExempt,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record BillingAccountAuditEventResponse(
    Guid Id,
    Guid BillingAccountId,
    string EventType,
    string Description,
    BillingAccountAuditActorType ActorType,
    string? ActorId,
    string? MetadataJson,
    DateTime CreatedAtUtc);
