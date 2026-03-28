using Identity.Application.DTOs;
using Identity.Application.Interfaces;

namespace Identity.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthService(
        IUserRepository userRepository,
        ITenantRepository tenantRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService)
    {
        _userRepository = userRepository;
        _tenantRepository = tenantRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var tenant = await _tenantRepository.GetByCodeAsync(request.TenantCode.ToUpperInvariant().Trim(), ct);
        if (tenant is null || !tenant.IsActive)
            throw new UnauthorizedAccessException();

        var normalizedEmail = request.Email.ToLowerInvariant().Trim();
        var user = await _userRepository.GetByTenantAndEmailAsync(tenant.Id, normalizedEmail, ct);
        if (user is null || !user.IsActive)
            throw new UnauthorizedAccessException();

        var valid = _passwordHasher.Verify(request.Password, user.PasswordHash);
        if (!valid)
            throw new UnauthorizedAccessException();

        var userWithRoles = await _userRepository.GetByIdWithRolesAsync(user.Id, ct)
            ?? throw new UnauthorizedAccessException();

        var roleNames = userWithRoles.UserRoles.Select(ur => ur.Role.Name).ToList();

        var (token, expiresAtUtc) = _jwtTokenService.GenerateToken(userWithRoles, tenant, roleNames);

        var userResponse = new UserResponse(
            userWithRoles.Id,
            userWithRoles.TenantId,
            userWithRoles.Email,
            userWithRoles.FirstName,
            userWithRoles.LastName,
            userWithRoles.IsActive,
            roleNames);

        return new LoginResponse(token, expiresAtUtc, userResponse);
    }
}
