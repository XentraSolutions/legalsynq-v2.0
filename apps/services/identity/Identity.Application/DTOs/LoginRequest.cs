namespace Identity.Application.DTOs;

public record LoginRequest(
    string TenantCode,
    string Email,
    string Password,
    string?  Subdomain = null,
    // AUTH-B01: Optional Tenant-service-resolved tenant ID.
    // Used as a final fallback when both code and subdomain lookups miss the
    // Identity idt_Tenants table (e.g. tenant was provisioned via Tenant service
    // but the Identity write-through row has a different code/no subdomain set).
    Guid? TenantId = null);
