namespace Tenant.Application.DTOs;

public record TenantResponse(
    Guid     Id,
    string   Code,
    string   DisplayName,
    string?  LegalName,
    string   Status,
    string?  Subdomain,
    Guid?    LogoDocumentId,
    Guid?    LogoWhiteDocumentId,
    string?  TimeZone,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
