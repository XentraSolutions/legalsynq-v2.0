namespace Identity.Application.DTOs;

/// <summary>
/// Summary of a tenant the user has access to.
/// Emitted in the LoginResponse for multi-tenant users.
/// </summary>
public record TenantSummary(Guid TenantId, string TenantCode);
