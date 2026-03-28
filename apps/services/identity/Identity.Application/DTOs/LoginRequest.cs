namespace Identity.Application.DTOs;

public record LoginRequest(
    string TenantCode,
    string Email,
    string Password);
