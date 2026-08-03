namespace Identity.Application.DTOs;

public record CreateUserRequest(
    Guid TenantId,
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? Title = null,
    List<Guid>? RoleIds = null);
