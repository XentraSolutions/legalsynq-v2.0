using Identity.Domain;

namespace Identity.Application;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByIdWithRolesAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByTenantAndEmailAsync(Guid tenantId, string email, CancellationToken ct = default);

    /// <summary>
    /// Loads a user by tenant + email in a single tracked query that includes active
    /// ScopedRoleAssignments and their Roles. Tracked so RecordLogin() can be saved.
    /// </summary>
    Task<User?> GetByTenantAndEmailWithRolesAsync(Guid tenantId, string email, CancellationToken ct = default);

    /// <summary>
    /// Lightweight login path: returns only the user's primary Organization with its
    /// OrganizationTypeRef. Does NOT load products, roles, or OrgTypeRules.
    /// </summary>
    Task<Organization?> GetPrimaryOrganizationForLoginAsync(Guid userId, CancellationToken ct = default);
    Task<List<User>> GetAllWithRolesAsync(CancellationToken ct = default);
    Task<List<User>> GetByTenantWithRolesAsync(Guid tenantId, CancellationToken ct = default);
    Task AddAsync(User user, IReadOnlyList<Guid> roleIds, CancellationToken ct = default);
    Task<UserOrganizationMembership?> GetPrimaryOrgMembershipAsync(Guid userId, CancellationToken ct = default);

    Task<List<UserOrganizationMembership>> GetActiveMembershipsWithProductsAsync(Guid userId, Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Updates the user's AvatarDocumentId. Pass null to clear the avatar.
    /// </summary>
    Task UpdateAvatarAsync(Guid userId, Guid? avatarDocumentId, CancellationToken ct = default);

    /// <summary>
    /// UIX-003-03: Persists pending EF change-tracked mutations on the Users entity
    /// (e.g. RecordLogin, IncrementSessionVersion). Callers must load the entity via
    /// this repository before mutating — do not use this for unrelated entities.
    /// </summary>
    Task SaveChangesAsync(CancellationToken ct = default);

    // ── Invitation and token access (used by AuthService) ────────────────────

    /// <summary>Returns the invitation (with its User navigation) matching the given token hash, or null.</summary>
    Task<UserInvitation?> GetInvitationWithUserByTokenHashAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>Returns the password-reset token (with its User navigation) matching the given token hash, or null.</summary>
    Task<PasswordResetToken?> GetPasswordResetTokenWithUserByHashAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>Returns all PENDING password-reset tokens for the given user (for revocation before issuing a new one).</summary>
    Task<List<PasswordResetToken>> GetPendingPasswordResetTokensAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Adds a new PasswordResetToken to the context and persists all pending changes.</summary>
    Task AddPasswordResetTokenAsync(PasswordResetToken resetToken, CancellationToken ct = default);
}
