namespace Tenant.Application.DTOs;

public record UpdateTenantRequest(
    string  DisplayName,
    string? LegalName              = null,
    string? TimeZone               = null,
    string? Subdomain              = null,
    string? Status                 = null,
    Guid?   LogoDocumentId        = null,
    Guid?   LogoWhiteDocumentId   = null);
