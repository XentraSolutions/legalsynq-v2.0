namespace Tenant.Application.DTOs;

public record CreateTenantRequest(
    string  Code,
    string  DisplayName,
    string? LegalName   = null,
    string? Subdomain   = null,
    string? TimeZone    = null);
