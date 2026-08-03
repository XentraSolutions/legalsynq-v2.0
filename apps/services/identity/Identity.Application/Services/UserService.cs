using Identity.Application.DTOs;
using Identity.Application.Interfaces;
using Identity.Domain;

namespace Identity.Application.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IPasswordHasher _passwordHasher;

    public UserService(
        IUserRepository userRepository,
        ITenantRepository tenantRepository,
        IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _tenantRepository = tenantRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<UserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var tenant = await _tenantRepository.GetByIdAsync(request.TenantId, ct)
            ?? throw new InvalidOperationException("Tenant not found.");

        if (!tenant.IsActive)
            throw new InvalidOperationException("Tenant is not active.");

        var normalizedEmail = request.Email.ToLowerInvariant().Trim();

        var existing = await _userRepository.GetByTenantAndEmailAsync(request.TenantId, normalizedEmail, ct);
        if (existing is not null)
            throw new InvalidOperationException("A user with this email already exists in this tenant.");

        var passwordHash = _passwordHasher.Hash(request.Password);
        var user = User.Create(
            request.TenantId,
            request.Email,
            passwordHash,
            request.FirstName,
            request.LastName,
            title: request.Title);

        var roleIds = (request.RoleIds ?? []).AsReadOnly();
        await _userRepository.AddAsync(user, request.TenantId, roleIds, ct);

        var created = await _userRepository.GetByIdWithRolesAsync(user.Id, ct)
            ?? throw new InvalidOperationException("User creation failed.");

        return ToResponse(created, request.TenantId);
    }

    public async Task<List<UserResponse>> GetAllAsync(CancellationToken ct = default)
    {
        var users = await _userRepository.GetAllWithRolesAsync(ct);
        return users.Select(u => ToResponse(u)).ToList();
    }

    public async Task<List<UserResponse>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        var users = await _userRepository.GetByTenantWithRolesAsync(tenantId, ct);
        return users.Select(u => ToResponse(u, tenantId)).ToList();
    }

    public async Task<UserResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdWithRolesAsync(id, ct);
        return user is null ? null : ToResponse(user);
    }

    private static UserResponse ToResponse(User user, Guid tenantId = default) => new(
        user.Id,
        tenantId,
        user.Email,
        user.FirstName,
        user.LastName,
        user.IsActive,
        user.ScopedRoleAssignments
            .Where(s => s.IsActive && s.ScopeType == ScopedRoleAssignment.ScopeTypes.Global)
            .Select(s => s.Role.Name)
            .ToList(),
        AvatarDocumentId: user.AvatarDocumentId,
        Title: user.Title);
}
