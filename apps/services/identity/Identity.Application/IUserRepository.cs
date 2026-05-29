using Identity.Domain;

namespace Identity.Application;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByIdWithRolesAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByTenantAndEmailAsync(Guid tenantId, string email, CancellationToken ct = default);
    Task<List<User>> GetAllWithRolesAsync(CancellationToken ct = default);
    Task<List<User>> GetByTenantWithRolesAsync(Guid tenantId, CancellationToken ct = default);
    Task AddAsync(User user, IReadOnlyList<Guid> roleIds, CancellationToken ct = default);

    /// <summary>
    /// Returns the primary org membership for a user (single-tenant callers that do
    /// not supply a tenantId). Uses the IsPrimary flag for disambiguation.
    /// </summary>
    Task<UserOrganizationMembership?> GetPrimaryOrgMembershipAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Returns the active org membership for a user scoped to a specific tenant.
    /// Used by multi-tenant login to resolve the correct org_id JWT claim.
    /// When tenantId is supplied the IsPrimary filter is dropped — a user has exactly
    /// one active org per tenant in the CareConnect model.
    /// </summary>
    Task<UserOrganizationMembership?> GetPrimaryOrgMembershipAsync(Guid userId, Guid tenantId, CancellationToken ct = default);

    Task<List<UserOrganizationMembership>> GetActiveMembershipsWithProductsAsync(Guid userId, Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Returns all active UserTenant rows for a user, ordered by JoinedAtUtc ascending.
    /// Used by the JWT builder to populate the tenant_ids multi-tenant claim.
    /// </summary>
    Task<List<UserTenant>> GetActiveTenantMembershipsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Updates the user's AvatarDocumentId. Pass null to clear the avatar.
    /// </summary>
    Task UpdateAvatarAsync(Guid userId, Guid? avatarDocumentId, CancellationToken ct = default);

    /// <summary>
    /// Updates the user's primary phone number. Pass null or whitespace to clear it.
    /// Callers are expected to have already validated the value against E.164 format.
    /// Returns true when the value actually changed, false when it was already equal.
    /// Throws InvalidOperationException when the user is not found.
    /// </summary>
    Task<bool> UpdatePhoneAsync(Guid userId, string? phone, CancellationToken ct = default);

    /// <summary>
    /// UIX-003-03: Persists pending EF change-tracked mutations on the Users entity
    /// (e.g. RecordLogin, IncrementSessionVersion). Callers must load the entity via
    /// this repository before mutating — do not use this for unrelated entities.
    /// </summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
