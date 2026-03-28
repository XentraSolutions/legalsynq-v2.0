namespace Identity.Application.DTOs;

public record UserResponse(
    Guid Id,
    Guid TenantId,
    string Email,
    string FirstName,
    string LastName,
    bool IsActive,
    List<string> Roles);
