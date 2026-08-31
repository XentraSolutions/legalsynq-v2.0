using System.Data;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BuildingBlocks.Authorization;
using AuthorizationProductCodes = BuildingBlocks.Authorization.ProductCodes;
using Identity.Application.Interfaces;
using Identity.Domain;
using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Services;

public sealed class SynqLienUserManagementService : ISynqLienUserManagementService
{
    private const string ProductCode = AuthorizationProductCodes.SynqLiens;

    private static readonly HashSet<string> AssignableRoleCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        ProductRoleCodes.SynqLienSeller,
        ProductRoleCodes.SynqLienBuyer,
        ProductRoleCodes.SynqLienHolder,
        ProductRoleCodes.SynqLienUserAdmin,
    };

    private readonly IdentityDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuditPublisher _audit;

    public SynqLienUserManagementService(
        IdentityDbContext db,
        IPasswordHasher passwordHasher,
        IAuditPublisher audit)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _audit = audit;
    }

    public async Task<SynqLienPagedResult<SynqLienUserSummary>> ListUsersAsync(
        Guid tenantId, Guid actorUserId, string? search, string? status, string? roleCode,
        int page, int pageSize, CancellationToken ct = default)
    {
        await AuthorizeAsync(tenantId, actorUserId, PermissionCodes.LienUserRead, ct);
        (page, pageSize) = NormalizePaging(page, pageSize);
        var now = DateTime.UtcNow;

        var users = _db.Users.AsNoTracking()
            .Where(u => _db.UserTenants.Any(ut =>
                ut.UserId == u.Id && ut.TenantId == tenantId && ut.IsActive))
            .Where(u =>
                _db.UserProductAccessRecords.Any(a =>
                    a.TenantId == tenantId && a.UserId == u.Id && a.ProductCode == ProductCode &&
                    a.OrganizationId == null) ||
                _db.AccessGroupMemberships.Any(m =>
                    m.TenantId == tenantId && m.UserId == u.Id && m.MembershipStatus == MembershipStatus.Active &&
                    _db.AccessGroups.Any(g => g.Id == m.GroupId && g.TenantId == tenantId &&
                        g.Status == GroupStatus.Active && g.OrganizationId == null) &&
                    _db.GroupProductAccessRecords.Any(a =>
                        a.TenantId == tenantId && a.GroupId == m.GroupId && a.ProductCode == ProductCode &&
                        a.AccessStatus == AccessStatus.Granted)) ||
                _db.UserInvitations.Any(i =>
                    i.TenantId == tenantId && i.UserId == u.Id && i.ProductCode == ProductCode &&
                    i.Status == UserInvitation.Statuses.Pending && i.ExpiresAtUtc >= now));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            users = users.Where(u =>
                u.Email.ToLower().Contains(term) ||
                u.FirstName.ToLower().Contains(term) ||
                u.LastName.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(roleCode))
        {
            var normalizedRole = NormalizeRoleCode(roleCode);
            users = users.Where(u =>
                _db.UserRoleAssignments.Any(a =>
                    a.TenantId == tenantId && a.UserId == u.Id && a.ProductCode == ProductCode &&
                    a.RoleCode == normalizedRole && a.OrganizationId == null &&
                    a.AssignmentStatus == AssignmentStatus.Active) ||
                _db.AccessGroupMemberships.Any(m =>
                    m.TenantId == tenantId && m.UserId == u.Id && m.MembershipStatus == MembershipStatus.Active &&
                    _db.AccessGroups.Any(g => g.Id == m.GroupId && g.TenantId == tenantId &&
                        g.Status == GroupStatus.Active && g.OrganizationId == null) &&
                    _db.GroupRoleAssignments.Any(a =>
                        a.TenantId == tenantId && a.GroupId == m.GroupId && a.ProductCode == ProductCode &&
                        a.RoleCode == normalizedRole && a.OrganizationId == null &&
                        a.AssignmentStatus == AssignmentStatus.Active)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim().ToUpperInvariant();
            users = normalizedStatus switch
            {
                "INVITED" => users.Where(u => _db.UserInvitations.Any(i =>
                    i.TenantId == tenantId && i.UserId == u.Id && i.ProductCode == ProductCode &&
                    i.Status == UserInvitation.Statuses.Pending && i.ExpiresAtUtc >= now)),
                "GRANTED" or "ACTIVE" => users.Where(u => _db.UserProductAccessRecords.Any(a =>
                    a.TenantId == tenantId && a.UserId == u.Id && a.ProductCode == ProductCode &&
                    a.OrganizationId == null && a.AccessStatus == AccessStatus.Granted) ||
                    _db.AccessGroupMemberships.Any(m =>
                        m.TenantId == tenantId && m.UserId == u.Id && m.MembershipStatus == MembershipStatus.Active &&
                        _db.AccessGroups.Any(g => g.Id == m.GroupId && g.TenantId == tenantId &&
                            g.Status == GroupStatus.Active && g.OrganizationId == null) &&
                        _db.GroupProductAccessRecords.Any(a =>
                            a.TenantId == tenantId && a.GroupId == m.GroupId && a.ProductCode == ProductCode &&
                            a.AccessStatus == AccessStatus.Granted))),
                "REVOKED" or "SUSPENDED" => users.Where(u => _db.UserProductAccessRecords.Any(a =>
                    a.TenantId == tenantId && a.UserId == u.Id && a.ProductCode == ProductCode &&
                    a.OrganizationId == null && a.AccessStatus == AccessStatus.Revoked) &&
                    !_db.AccessGroupMemberships.Any(m =>
                        m.TenantId == tenantId && m.UserId == u.Id && m.MembershipStatus == MembershipStatus.Active &&
                        _db.AccessGroups.Any(g => g.Id == m.GroupId && g.TenantId == tenantId &&
                            g.Status == GroupStatus.Active && g.OrganizationId == null) &&
                        _db.GroupProductAccessRecords.Any(a =>
                            a.TenantId == tenantId && a.GroupId == m.GroupId && a.ProductCode == ProductCode &&
                            a.AccessStatus == AccessStatus.Granted))),
                _ => throw Error(400, "VALIDATION_FAILED", "status must be Invited, Granted, or Revoked."),
            };
        }

        var totalCount = await users.CountAsync(ct);
        var pageUsers = await users
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ThenBy(u => u.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = new List<SynqLienUserSummary>(pageUsers.Count);
        foreach (var user in pageUsers)
        {
            var detail = await BuildUserDetailAsync(tenantId, user, ct);
            items.Add(new SynqLienUserSummary(
                detail.Id, detail.Email, detail.FirstName, detail.LastName, detail.Phone,
                detail.AccountStatus, detail.LiensAccessStatus, detail.Roles,
                detail.Invitation, detail.AccessVersion));
        }

        return new SynqLienPagedResult<SynqLienUserSummary>(items, page, pageSize, totalCount);
    }

    public async Task<SynqLienUserDetail> GetUserAsync(
        Guid tenantId, Guid actorUserId, Guid userId, CancellationToken ct = default)
    {
        await AuthorizeAsync(tenantId, actorUserId, PermissionCodes.LienUserRead, ct);
        var user = await FindTenantUserAsync(tenantId, userId, tracking: false, ct);
        return await BuildUserDetailAsync(tenantId, user, ct);
    }

    public async Task<IReadOnlyList<SynqLienAssignableRole>> ListRolesAsync(
        Guid tenantId, Guid actorUserId, CancellationToken ct = default)
    {
        await AuthorizeAsync(tenantId, actorUserId, PermissionCodes.LienUserRoleAssign, ct);
        return await _db.ProductRoles.AsNoTracking()
            .Where(r => r.IsActive && r.Product.Code == ProductCode && AssignableRoleCodes.Contains(r.Code))
            .OrderBy(r => r.Name)
            .Select(r => new SynqLienAssignableRole(r.Code, r.Name, r.Description))
            .ToListAsync(ct);
    }

    public async Task<SynqLienPagedResult<SynqLienInvitationSummary>> ListInvitationsAsync(
        Guid tenantId, Guid actorUserId, string? status, int page, int pageSize,
        CancellationToken ct = default)
    {
        await AuthorizeAsync(tenantId, actorUserId, PermissionCodes.LienUserInvite, ct);
        (page, pageSize) = NormalizePaging(page, pageSize);

        var invitations = _db.UserInvitations.AsNoTracking()
            .Where(i => i.TenantId == tenantId && i.ProductCode == ProductCode);

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim().ToUpperInvariant();
            if (normalized is not (UserInvitation.Statuses.Pending or UserInvitation.Statuses.Accepted or
                UserInvitation.Statuses.Expired or UserInvitation.Statuses.Revoked))
                throw Error(400, "VALIDATION_FAILED", "Unknown invitation status.");
            var now = DateTime.UtcNow;
            invitations = normalized switch
            {
                UserInvitation.Statuses.Pending => invitations.Where(i =>
                    i.Status == UserInvitation.Statuses.Pending && i.ExpiresAtUtc >= now),
                UserInvitation.Statuses.Expired => invitations.Where(i =>
                    i.Status == UserInvitation.Statuses.Expired ||
                    i.Status == UserInvitation.Statuses.Pending && i.ExpiresAtUtc < now),
                _ => invitations.Where(i => i.Status == normalized),
            };
        }

        var totalCount = await invitations.CountAsync(ct);
        var rows = await invitations
            .Include(i => i.User)
            .Include(i => i.RoleGrants)
            .OrderByDescending(i => i.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = rows.Select(i => new SynqLienInvitationSummary(
            i.Id, i.UserId, i.User.Email, i.User.FirstName, i.User.LastName, InvitationDisplayStatus(i),
            i.RoleGrants.Select(g => g.RoleCode).OrderBy(c => c).ToList(),
            i.CreatedAtUtc, i.ExpiresAtUtc, i.AcceptedAtUtc, i.RevokedAtUtc)).ToList();

        return new SynqLienPagedResult<SynqLienInvitationSummary>(items, page, pageSize, totalCount);
    }

    public async Task<SynqLienInviteResult> InviteAsync(
        Guid tenantId, Guid actorUserId, SynqLienInviteCommand command,
        CancellationToken ct = default)
    {
        await using var transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
            : null;
        await AuthorizeAsync(tenantId, actorUserId, PermissionCodes.LienUserInvite, ct);
        var roles = await ValidateRolesAsync(command.RoleCodes, requireAtLeastOne: true, ct);

        if (string.IsNullOrWhiteSpace(command.Email) || string.IsNullOrWhiteSpace(command.FirstName) ||
            string.IsNullOrWhiteSpace(command.LastName))
            throw Error(400, "VALIDATION_FAILED", "email, firstName, and lastName are required.");

        var email = command.Email.Trim().ToLowerInvariant();
        if (email.Length > 320 || !MailAddress.TryCreate(email, out var parsedEmail) ||
            !string.Equals(parsedEmail.Address, email, StringComparison.OrdinalIgnoreCase))
            throw Error(400, "VALIDATION_FAILED", "email must be a valid address of at most 320 characters.");
        if (command.FirstName.Trim().Length > 100 || command.LastName.Trim().Length > 100)
            throw Error(400, "VALIDATION_FAILED", "firstName and lastName must be at most 100 characters.");
        if (command.Phone?.Trim().Length > 32)
            throw Error(400, "VALIDATION_FAILED", "phone must be at most 32 characters.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        var isNewUser = user is null;

        if (user is null)
        {
            user = User.Create(
                tenantId, email, _passwordHasher.Hash(Guid.CreateVersion7().ToString()),
                command.FirstName, command.LastName);
            user.SetPhone(command.Phone);
            user.Deactivate();
            _db.Users.Add(user);
        }
        else if (!string.IsNullOrWhiteSpace(command.Phone))
        {
            user.SetPhone(command.Phone);
        }

        var membership = await _db.UserTenants.FirstOrDefaultAsync(
            ut => ut.UserId == user.Id && ut.TenantId == tenantId, ct);
        if (membership is null)
            _db.UserTenants.Add(UserTenant.Create(user.Id, tenantId));
        else if (!membership.IsActive)
            throw Error(409, "INACTIVE_TENANT_MEMBERSHIP", "The existing tenant membership is inactive and must be restored by a tenant administrator.");

        if (user.IsActive)
        {
            var staleInvitations = await _db.UserInvitations.Where(i =>
                i.TenantId == tenantId && i.UserId == user.Id && i.ProductCode == ProductCode &&
                i.Status == UserInvitation.Statuses.Pending).ToListAsync(ct);
            foreach (var staleInvitation in staleInvitations)
                staleInvitation.Revoke();

            var changed = await StageAccessAndRolesAsync(tenantId, user, roles, actorUserId, ct);
            if (changed) user.IncrementAccessVersion();
            await SaveChangesAsync(ct);
            if (transaction is not null)
                await transaction.CommitAsync(ct);

            PublishAudit("identity.synqlien_user.access_granted", "SynqLienUserEnrolled",
                $"User '{user.Id}' enrolled into SynqLien.", tenantId, actorUserId, user.Id,
                after: JsonSerializer.Serialize(new { productCode = ProductCode, roleCodes = roles }));

            return new SynqLienInviteResult(user.Id, null, user.Email, isNewUser, true, null);
        }

        var pendingInvitations = await _db.UserInvitations.Where(i =>
            i.TenantId == tenantId && i.UserId == user.Id && i.ProductCode == ProductCode &&
            i.Status == UserInvitation.Statuses.Pending).ToListAsync(ct);
        if (pendingInvitations.Any(i => !i.IsExpired()))
            throw Error(409, "PENDING_INVITATION_EXISTS", "A pending SynqLien invitation already exists for this user.");
        foreach (var expiredInvitation in pendingInvitations)
            expiredInvitation.Revoke();

        var (invitation, rawToken) = CreateInvitation(user.Id, tenantId, actorUserId);
        _db.UserInvitations.Add(invitation);
        foreach (var role in roles)
            _db.UserInvitationRoleGrants.Add(UserInvitationRoleGrant.Create(invitation.Id, tenantId, ProductCode, role));

        await SaveChangesAsync(ct);
        if (transaction is not null)
            await transaction.CommitAsync(ct);

        PublishAudit("identity.synqlien_user.invited", "SynqLienUserInvited",
            $"User '{user.Id}' invited to SynqLien.", tenantId, actorUserId, user.Id,
            after: JsonSerializer.Serialize(new { invitationId = invitation.Id, productCode = ProductCode, roleCodes = roles }));

        return new SynqLienInviteResult(user.Id, invitation.Id, user.Email, isNewUser, false, rawToken);
    }

    public async Task<SynqLienInviteResult> ResendInvitationAsync(
        Guid tenantId, Guid actorUserId, Guid invitationId, CancellationToken ct = default)
    {
        await using var transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
            : null;
        await AuthorizeAsync(tenantId, actorUserId, PermissionCodes.LienUserInvite, ct);
        var invitation = await _db.UserInvitations
            .Include(i => i.User)
            .Include(i => i.RoleGrants)
            .FirstOrDefaultAsync(i => i.Id == invitationId && i.TenantId == tenantId && i.ProductCode == ProductCode, ct)
            ?? throw Error(404, "LIENS_INVITATION_NOT_FOUND", "SynqLien invitation was not found.");

        if (invitation.Status != UserInvitation.Statuses.Pending)
            throw Error(409, "INVITATION_NOT_PENDING", "Only pending invitations can be resent.");

        invitation.Revoke();
        var (replacement, rawToken) = CreateInvitation(invitation.UserId, tenantId, actorUserId);
        _db.UserInvitations.Add(replacement);
        foreach (var grant in invitation.RoleGrants)
            _db.UserInvitationRoleGrants.Add(UserInvitationRoleGrant.Create(
                replacement.Id, tenantId, ProductCode, grant.RoleCode));

        await SaveChangesAsync(ct);
        if (transaction is not null)
            await transaction.CommitAsync(ct);

        PublishAudit("identity.synqlien_user.invitation_resent", "SynqLienInvitationResent",
            $"SynqLien invitation resent for '{invitation.User.Email}'.", tenantId, actorUserId,
            invitation.UserId, after: JsonSerializer.Serialize(new { invitationId = replacement.Id }));

        return new SynqLienInviteResult(
            invitation.UserId, replacement.Id, invitation.User.Email, false, false, rawToken);
    }

    public async Task CancelInvitationAsync(
        Guid tenantId, Guid actorUserId, Guid invitationId, CancellationToken ct = default)
    {
        await using var transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
            : null;
        await AuthorizeAsync(tenantId, actorUserId, PermissionCodes.LienUserInvite, ct);
        var invitation = await _db.UserInvitations.FirstOrDefaultAsync(i =>
            i.Id == invitationId && i.TenantId == tenantId && i.ProductCode == ProductCode, ct)
            ?? throw Error(404, "LIENS_INVITATION_NOT_FOUND", "SynqLien invitation was not found.");

        if (invitation.Status != UserInvitation.Statuses.Pending)
            throw Error(409, "INVITATION_NOT_PENDING", "Only pending invitations can be cancelled.");

        invitation.Revoke();
        await SaveChangesAsync(ct);
        if (transaction is not null)
            await transaction.CommitAsync(ct);
        PublishAudit("identity.synqlien_user.invitation_cancelled", "SynqLienInvitationCancelled",
            $"SynqLien invitation '{invitation.Id}' cancelled.", tenantId, actorUserId,
            invitation.UserId, after: JsonSerializer.Serialize(new { invitationId = invitation.Id }));
    }

    public async Task<SynqLienUserDetail> SetAccessAsync(
        Guid tenantId, Guid actorUserId, Guid userId, bool enabled, int expectedAccessVersion,
        CancellationToken ct = default)
    {
        await using var transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
            : null;
        await AuthorizeAsync(tenantId, actorUserId, PermissionCodes.LienUserAccessManage, ct);
        var user = await FindTenantUserAsync(tenantId, userId, tracking: true, ct);
        ConfigureConcurrency(user, expectedAccessVersion);

        if (!enabled && actorUserId == userId)
            throw Error(409, "SELF_ADMIN_ACCESS_REMOVAL", "You cannot revoke your own SynqLien access.");

        var productAccess = await _db.UserProductAccessRecords.FirstOrDefaultAsync(a =>
            a.TenantId == tenantId && a.UserId == userId && a.ProductCode == ProductCode, ct);
        if (productAccess?.OrganizationId is not null)
            throw Error(409, "ORGANIZATION_SCOPED_ACCESS_UNSUPPORTED",
                "This user has organization-scoped SynqLien access, which cannot be managed by the tenant-scoped API.");
        var directAccess = productAccess;
        var changed = false;

        if (enabled)
        {
            if (directAccess is null)
            {
                _db.UserProductAccessRecords.Add(UserProductAccess.Create(
                    tenantId, userId, ProductCode, organizationId: null, createdByUserId: actorUserId));
                changed = true;
            }
            else if (directAccess.AccessStatus != AccessStatus.Granted)
            {
                directAccess.Grant(actorUserId);
                changed = true;
            }
        }
        else
        {
            if (await HasDirectAdminRoleAsync(tenantId, userId, ct))
                await EnsureAnotherEffectiveAdminAsync(tenantId, userId, ct);

            if (directAccess?.AccessStatus == AccessStatus.Granted)
            {
                directAccess.Revoke(actorUserId);
                changed = true;
            }

            var directRoles = await _db.UserRoleAssignments.Where(a =>
                a.TenantId == tenantId && a.UserId == userId && a.ProductCode == ProductCode &&
                a.OrganizationId == null && a.AssignmentStatus == AssignmentStatus.Active).ToListAsync(ct);
            foreach (var role in directRoles)
            {
                role.Remove(actorUserId);
                changed = true;
            }
        }

        if (changed)
        {
            user.IncrementAccessVersion();
            await SaveChangesAsync(ct);
        }

        if (transaction is not null)
            await transaction.CommitAsync(ct);

        if (changed)
            PublishAudit(enabled ? "identity.synqlien_user.access_granted" : "identity.synqlien_user.access_revoked",
                enabled ? "SynqLienAccessGranted" : "SynqLienAccessRevoked",
                $"SynqLien access {(enabled ? "granted to" : "revoked from")} user '{user.Id}'.",
                tenantId, actorUserId, userId, after: JsonSerializer.Serialize(new { enabled }));

        return await BuildUserDetailAsync(tenantId, user, ct);
    }

    public async Task<SynqLienUserDetail> ReplaceRolesAsync(
        Guid tenantId, Guid actorUserId, Guid userId, IReadOnlyCollection<string> roleCodes,
        int expectedAccessVersion, CancellationToken ct = default)
    {
        await using var transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
            : null;
        await AuthorizeAsync(tenantId, actorUserId, PermissionCodes.LienUserRoleAssign, ct);
        var desired = await ValidateRolesAsync(roleCodes, requireAtLeastOne: true, ct);
        var user = await FindTenantUserAsync(tenantId, userId, tracking: true, ct);
        ConfigureConcurrency(user, expectedAccessVersion);

        var hasProductAccess = await HasTenantScopedProductAccessAsync(tenantId, userId, ct);
        if (!hasProductAccess)
            throw Error(409, "LIENS_ACCESS_REQUIRED", "Grant SynqLien access before assigning roles.");

        var current = await _db.UserRoleAssignments.Where(a =>
            a.TenantId == tenantId && a.UserId == userId && a.ProductCode == ProductCode &&
            a.OrganizationId == null && a.AssignmentStatus == AssignmentStatus.Active).ToListAsync(ct);
        var currentCodes = current.Select(a => a.RoleCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var removesAdmin = currentCodes.Contains(ProductRoleCodes.SynqLienUserAdmin) &&
            !desired.Contains(ProductRoleCodes.SynqLienUserAdmin, StringComparer.OrdinalIgnoreCase);

        if (removesAdmin && actorUserId == userId)
            throw Error(409, "SELF_ADMIN_ACCESS_REMOVAL", "You cannot remove your own SynqLien administrator role.");
        if (removesAdmin)
            await EnsureAnotherEffectiveAdminAsync(tenantId, userId, ct);

        var changed = false;
        foreach (var assignment in current.Where(a => !desired.Contains(a.RoleCode, StringComparer.OrdinalIgnoreCase)))
        {
            assignment.Remove(actorUserId);
            changed = true;
        }

        foreach (var role in desired.Where(r => !currentCodes.Contains(r)))
        {
            _db.UserRoleAssignments.Add(UserRoleAssignment.Create(
                tenantId, userId, role, ProductCode, organizationId: null, createdByUserId: actorUserId));
            changed = true;
        }

        if (changed)
        {
            user.IncrementAccessVersion();
            await SaveChangesAsync(ct);
        }

        if (transaction is not null)
            await transaction.CommitAsync(ct);

        if (changed)
            PublishAudit("identity.synqlien_user.roles_replaced", "SynqLienRolesReplaced",
                $"Direct SynqLien roles replaced for user '{user.Id}'.", tenantId, actorUserId, userId,
                before: JsonSerializer.Serialize(new { roleCodes = currentCodes.OrderBy(c => c) }),
                after: JsonSerializer.Serialize(new { roleCodes = desired.OrderBy(c => c) }));

        return await BuildUserDetailAsync(tenantId, user, ct);
    }

    /// <summary>
    /// Stages product and role grants on the shared scoped DbContext. The caller owns
    /// the final SaveChanges so password activation, invitation acceptance, and access
    /// grants commit atomically.
    /// </summary>
    public async Task ApplyInvitationGrantsAsync(Guid invitationId, CancellationToken ct = default)
    {
        var invitation = await _db.UserInvitations
            .Include(i => i.User)
            .Include(i => i.RoleGrants)
            .FirstOrDefaultAsync(i => i.Id == invitationId, ct)
            ?? throw Error(404, "LIENS_INVITATION_NOT_FOUND", "Invitation was not found.");

        if (!string.Equals(invitation.ProductCode, ProductCode, StringComparison.OrdinalIgnoreCase))
            return;
        if (invitation.Status != UserInvitation.Statuses.Accepted)
            throw Error(409, "INVITATION_NOT_ACCEPTED",
                "SynqLien invitation grants can only be applied during invitation acceptance.");

        var activeMembership = await _db.UserTenants.AnyAsync(ut =>
            ut.TenantId == invitation.TenantId && ut.UserId == invitation.UserId && ut.IsActive, ct);
        if (!activeMembership)
            throw Error(409, "INACTIVE_TENANT_MEMBERSHIP",
                "The tenant membership is inactive and must be restored before accepting this invitation.");

        await EnsureTenantEntitledAsync(invitation.TenantId, ct);
        var roles = await ValidateRolesAsync(invitation.RoleGrants.Select(g => g.RoleCode).ToList(), true, ct);
        var changed = await StageAccessAndRolesAsync(
            invitation.TenantId, invitation.User, roles, invitation.InvitedByUserId, ct);

        foreach (var grant in invitation.RoleGrants) grant.MarkApplied();
        if (changed) invitation.User.IncrementAccessVersion();
    }

    private async Task AuthorizeAsync(Guid tenantId, Guid actorUserId, string permissionCode, CancellationToken ct)
    {
        if (tenantId == Guid.Empty || actorUserId == Guid.Empty)
            throw Error(401, "AUTHENTICATION_REQUIRED", "A valid tenant and user context is required.");

        var activeActor = await _db.Users.AsNoTracking().AnyAsync(u => u.Id == actorUserId && u.IsActive && !u.IsLocked, ct);
        var activeMembership = await _db.UserTenants.AsNoTracking().AnyAsync(ut =>
            ut.UserId == actorUserId && ut.TenantId == tenantId && ut.IsActive, ct);
        if (!activeActor || !activeMembership)
            throw Error(403, "LIENS_USER_MANAGEMENT_FORBIDDEN", "The caller is not an active member of this tenant.");

        await EnsureTenantEntitledAsync(tenantId, ct);

        var isAdmin = await _db.ScopedRoleAssignments.AsNoTracking().AnyAsync(a =>
            a.UserId == actorUserId && a.IsActive &&
            (a.Role.Name == Roles.PlatformAdmin ||
             (a.Role.Name == Roles.TenantAdmin && (a.TenantId == null || a.TenantId == tenantId))), ct);
        if (isAdmin) return;

        if (!await HasTenantScopedPermissionAsync(tenantId, actorUserId, permissionCode, ct))
            throw Error(403, "LIENS_USER_MANAGEMENT_FORBIDDEN", $"Permission '{permissionCode}' is required.");
    }

    private async Task EnsureTenantEntitledAsync(Guid tenantId, CancellationToken ct)
    {
        var entitled = await _db.TenantProducts.AsNoTracking().AnyAsync(tp =>
            tp.TenantId == tenantId && tp.IsEnabled && tp.Product.Code == ProductCode, ct);
        if (!entitled)
            throw Error(409, "LIENS_TENANT_NOT_ENTITLED", "The tenant does not have an active SynqLien entitlement.");
    }

    private async Task<User> FindTenantUserAsync(Guid tenantId, Guid userId, bool tracking, CancellationToken ct)
    {
        var query = tracking ? _db.Users.AsQueryable() : _db.Users.AsNoTracking();
        return await query.FirstOrDefaultAsync(u => u.Id == userId && _db.UserTenants.Any(ut =>
                   ut.UserId == u.Id && ut.TenantId == tenantId && ut.IsActive), ct)
               ?? throw Error(404, "LIENS_USER_NOT_FOUND", "SynqLien user was not found in this tenant.");
    }

    private async Task<SynqLienUserDetail> BuildUserDetailAsync(Guid tenantId, User user, CancellationToken ct)
    {
        var directAccess = await _db.UserProductAccessRecords.AsNoTracking().FirstOrDefaultAsync(a =>
            a.TenantId == tenantId && a.UserId == user.Id && a.ProductCode == ProductCode &&
            a.OrganizationId == null, ct);
        var directRoles = await _db.UserRoleAssignments.AsNoTracking().Where(a =>
            a.TenantId == tenantId && a.UserId == user.Id && a.ProductCode == ProductCode &&
            a.OrganizationId == null && a.AssignmentStatus == AssignmentStatus.Active).ToListAsync(ct);
        var validGroupIds = await GetTenantScopedGroupIdsAsync(tenantId, user.Id, ct);
        var inheritedRoles = validGroupIds.Count == 0
            ? []
            : await _db.GroupRoleAssignments.AsNoTracking().Where(a =>
                a.TenantId == tenantId && validGroupIds.Contains(a.GroupId) &&
                a.ProductCode == ProductCode && a.OrganizationId == null &&
                a.AssignmentStatus == AssignmentStatus.Active).ToListAsync(ct);
        var invitation = await _db.UserInvitations.AsNoTracking().Where(i =>
                i.TenantId == tenantId && i.UserId == user.Id && i.ProductCode == ProductCode)
            .OrderByDescending(i => i.CreatedAtUtc).FirstOrDefaultAsync(ct);

        var roleNameRows = await _db.ProductRoles.AsNoTracking()
            .Where(r => r.Product.Code == ProductCode)
            .Select(r => new { r.Code, r.Name })
            .ToListAsync(ct);
        var roleNames = roleNameRows.ToDictionary(r => r.Code, r => r.Name, StringComparer.OrdinalIgnoreCase);
        var directRoleCodes = directRoles.Select(r => r.RoleCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var roles = directRoles
            .Select(r => new SynqLienRoleAssignment(
                r.Id, r.RoleCode, roleNames.GetValueOrDefault(r.RoleCode, r.RoleCode), "Direct", false))
            .Concat(inheritedRoles
                .Where(r => !directRoleCodes.Contains(r.RoleCode))
                .GroupBy(r => r.RoleCode, StringComparer.OrdinalIgnoreCase)
                .Select(g => new SynqLienRoleAssignment(
                    null, g.Key, roleNames.GetValueOrDefault(g.Key, g.Key), "Inherited", true)))
            .OrderBy(r => r.Name)
            .ToList();

        var inheritedAccess = validGroupIds.Count > 0 && await _db.GroupProductAccessRecords.AsNoTracking().AnyAsync(a =>
            a.TenantId == tenantId && validGroupIds.Contains(a.GroupId) && a.ProductCode == ProductCode &&
            a.AccessStatus == AccessStatus.Granted, ct);
        var effectiveGranted = directAccess?.AccessStatus == AccessStatus.Granted || inheritedAccess;
        var accessStatus = invitation?.Status == UserInvitation.Statuses.Pending
            ? "Pending"
            : effectiveGranted
                ? directAccess?.AccessStatus == AccessStatus.Granted ? "Granted" : "Inherited"
                : directAccess?.AccessStatus == AccessStatus.Revoked ? "Revoked" : "None";

        return new SynqLienUserDetail(
            user.Id, user.Email, user.FirstName, user.LastName, user.Phone,
            user.IsActive ? "Active" : "Inactive", accessStatus, inheritedAccess, roles,
            invitation is null ? null : ToInvitationState(invitation), user.AccessVersion);
    }

    private async Task<List<string>> ValidateRolesAsync(
        IReadOnlyCollection<string>? roleCodes, bool requireAtLeastOne, CancellationToken ct)
    {
        var normalized = (roleCodes ?? [])
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(NormalizeRoleCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (requireAtLeastOne && normalized.Count == 0)
            throw Error(400, "VALIDATION_FAILED", "At least one SynqLien role is required.");
        if (normalized.Any(c => !AssignableRoleCodes.Contains(c)))
            throw Error(409, "ROLE_NOT_ALLOWED", "One or more roles are not assignable by SynqLien user management.");

        var activeRoles = await _db.ProductRoles.AsNoTracking()
            .Where(r => r.IsActive && r.Product.Code == ProductCode && normalized.Contains(r.Code))
            .Select(r => r.Code).ToListAsync(ct);
        if (activeRoles.Count != normalized.Count)
            throw Error(409, "ROLE_NOT_ALLOWED", "One or more SynqLien roles are missing or inactive.");
        return normalized;
    }

    private async Task<bool> StageAccessAndRolesAsync(
        Guid tenantId, User user, IReadOnlyCollection<string> roleCodes,
        Guid? actorUserId, CancellationToken ct)
    {
        await EnsureTenantEntitledAsync(tenantId, ct);
        var changed = false;
        var access = await _db.UserProductAccessRecords.FirstOrDefaultAsync(a =>
            a.TenantId == tenantId && a.UserId == user.Id && a.ProductCode == ProductCode, ct);
        if (access?.OrganizationId is not null)
            throw Error(409, "ORGANIZATION_SCOPED_ACCESS_UNSUPPORTED",
                "This user has organization-scoped SynqLien access, which cannot be managed by the tenant-scoped API.");
        if (access is null)
        {
            _db.UserProductAccessRecords.Add(UserProductAccess.Create(
                tenantId, user.Id, ProductCode, organizationId: null, createdByUserId: actorUserId));
            changed = true;
        }
        else if (access.AccessStatus != AccessStatus.Granted)
        {
            access.Grant(actorUserId);
            changed = true;
        }

        var currentRoleCodes = await _db.UserRoleAssignments.Where(a =>
                a.TenantId == tenantId && a.UserId == user.Id && a.ProductCode == ProductCode &&
                a.OrganizationId == null && a.AssignmentStatus == AssignmentStatus.Active)
            .Select(a => a.RoleCode).ToListAsync(ct);
        foreach (var role in roleCodes.Where(r => !currentRoleCodes.Contains(r, StringComparer.OrdinalIgnoreCase)))
        {
            _db.UserRoleAssignments.Add(UserRoleAssignment.Create(
                tenantId, user.Id, role, ProductCode, organizationId: null, createdByUserId: actorUserId));
            changed = true;
        }
        return changed;
    }

    private async Task<bool> HasDirectAdminRoleAsync(Guid tenantId, Guid userId, CancellationToken ct) =>
        await _db.UserRoleAssignments.AnyAsync(a =>
            a.TenantId == tenantId && a.UserId == userId && a.ProductCode == ProductCode &&
            a.RoleCode == ProductRoleCodes.SynqLienUserAdmin && a.OrganizationId == null &&
            a.AssignmentStatus == AssignmentStatus.Active, ct);

    private async Task EnsureAnotherEffectiveAdminAsync(Guid tenantId, Guid excludedUserId, CancellationToken ct)
    {
        var candidateIds = await _db.UserTenants.AsNoTracking()
            .Where(ut => ut.TenantId == tenantId && ut.IsActive && ut.UserId != excludedUserId && ut.User.IsActive)
            .Select(ut => ut.UserId).Distinct().ToListAsync(ct);
        foreach (var candidateId in candidateIds)
        {
            var isSystemAdmin = await _db.ScopedRoleAssignments.AsNoTracking().AnyAsync(a =>
                a.UserId == candidateId && a.IsActive &&
                (a.Role.Name == Roles.PlatformAdmin ||
                 (a.Role.Name == Roles.TenantAdmin && (a.TenantId == null || a.TenantId == tenantId))), ct);
            if (isSystemAdmin ||
                await HasTenantScopedProductAccessAsync(tenantId, candidateId, ct) &&
                await HasTenantScopedRoleAsync(tenantId, candidateId, ProductRoleCodes.SynqLienUserAdmin, ct))
                return;
        }
        throw Error(409, "LAST_LIENS_ADMIN", "The final active SynqLien user administrator cannot be removed.");
    }

    private async Task<bool> HasTenantScopedPermissionAsync(
        Guid tenantId, Guid userId, string permissionCode, CancellationToken ct)
    {
        if (!await HasTenantScopedProductAccessAsync(tenantId, userId, ct))
            return false;

        var roleCodes = await _db.UserRoleAssignments.AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.UserId == userId && a.ProductCode == ProductCode &&
                a.OrganizationId == null && a.AssignmentStatus == AssignmentStatus.Active)
            .Select(a => a.RoleCode)
            .ToListAsync(ct);

        var validGroupIds = await GetTenantScopedGroupIdsAsync(tenantId, userId, ct);
        if (validGroupIds.Count > 0)
        {
            roleCodes.AddRange(await _db.GroupRoleAssignments.AsNoTracking()
                .Where(a => a.TenantId == tenantId && validGroupIds.Contains(a.GroupId) &&
                    a.ProductCode == ProductCode && a.OrganizationId == null &&
                    a.AssignmentStatus == AssignmentStatus.Active)
                .Select(a => a.RoleCode)
                .ToListAsync(ct));
        }

        roleCodes = roleCodes.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (roleCodes.Count == 0)
            return false;

        return await _db.ProductRoles.AsNoTracking()
            .Where(r => r.IsActive && r.Product.Code == ProductCode && roleCodes.Contains(r.Code))
            .Join(_db.RolePermissionMappings,
                role => role.Id,
                mapping => mapping.ProductRoleId,
                (role, mapping) => mapping.Permission)
            .AnyAsync(permission => permission.IsActive && permission.Code == permissionCode &&
                permission.Product.Code == ProductCode, ct);
    }

    private async Task<bool> HasTenantScopedProductAccessAsync(
        Guid tenantId, Guid userId, CancellationToken ct)
    {
        var direct = await _db.UserProductAccessRecords.AsNoTracking().AnyAsync(a =>
            a.TenantId == tenantId && a.UserId == userId && a.ProductCode == ProductCode &&
            a.OrganizationId == null && a.AccessStatus == AccessStatus.Granted, ct);
        if (direct)
            return true;

        var validGroupIds = await GetTenantScopedGroupIdsAsync(tenantId, userId, ct);
        return validGroupIds.Count > 0 && await _db.GroupProductAccessRecords.AsNoTracking().AnyAsync(a =>
            a.TenantId == tenantId && validGroupIds.Contains(a.GroupId) && a.ProductCode == ProductCode &&
            a.AccessStatus == AccessStatus.Granted, ct);
    }

    private async Task<bool> HasTenantScopedRoleAsync(
        Guid tenantId, Guid userId, string roleCode, CancellationToken ct)
    {
        var direct = await _db.UserRoleAssignments.AsNoTracking().AnyAsync(a =>
            a.TenantId == tenantId && a.UserId == userId && a.ProductCode == ProductCode &&
            a.RoleCode == roleCode && a.OrganizationId == null &&
            a.AssignmentStatus == AssignmentStatus.Active, ct);
        if (direct)
            return true;

        var validGroupIds = await GetTenantScopedGroupIdsAsync(tenantId, userId, ct);
        return validGroupIds.Count > 0 && await _db.GroupRoleAssignments.AsNoTracking().AnyAsync(a =>
            a.TenantId == tenantId && validGroupIds.Contains(a.GroupId) && a.ProductCode == ProductCode &&
            a.RoleCode == roleCode && a.OrganizationId == null &&
            a.AssignmentStatus == AssignmentStatus.Active, ct);
    }

    private Task<List<Guid>> GetTenantScopedGroupIdsAsync(
        Guid tenantId, Guid userId, CancellationToken ct) =>
        (from membership in _db.AccessGroupMemberships.AsNoTracking()
         join accessGroup in _db.AccessGroups.AsNoTracking() on membership.GroupId equals accessGroup.Id
         where membership.TenantId == tenantId && membership.UserId == userId &&
               membership.MembershipStatus == MembershipStatus.Active &&
               accessGroup.TenantId == tenantId && accessGroup.Status == GroupStatus.Active &&
               accessGroup.OrganizationId == null
         select membership.GroupId)
        .Distinct()
        .ToListAsync(ct);

    private void ConfigureConcurrency(User user, int expectedAccessVersion)
    {
        if (expectedAccessVersion < 0 || user.AccessVersion != expectedAccessVersion)
            throw Error(412, "CONCURRENCY_CONFLICT", "The user access record changed. Refresh and retry.");
        _db.Entry(user).Property(u => u.AccessVersion).OriginalValue = expectedAccessVersion;
    }

    private async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw Error(412, "CONCURRENCY_CONFLICT", "The user-management state changed. Refresh and retry.");
        }
    }

    private static (UserInvitation Invitation, string RawToken) CreateInvitation(
        Guid userId, Guid tenantId, Guid actorUserId)
    {
        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
        return (UserInvitation.Create(
            userId, tenantId, tokenHash, UserInvitation.PortalOrigins.TenantPortal,
            actorUserId, productCode: ProductCode), rawToken);
    }

    private void PublishAudit(
        string eventType, string action, string description, Guid tenantId,
        Guid actorUserId, Guid targetUserId, string? before = null, string? after = null)
    {
        _audit.Publish(eventType, action, description, tenantId, actorUserId,
            "User", targetUserId.ToString(), before, after);
    }

    private static SynqLienInvitationState ToInvitationState(UserInvitation invitation) =>
        new(invitation.Id, InvitationDisplayStatus(invitation), invitation.CreatedAtUtc, invitation.ExpiresAtUtc,
            invitation.AcceptedAtUtc, invitation.RevokedAtUtc);

    private static string InvitationDisplayStatus(UserInvitation invitation) =>
        invitation.IsExpired() ? UserInvitation.Statuses.Expired : invitation.Status;

    private static string NormalizeRoleCode(string roleCode) => roleCode.Trim().ToUpperInvariant();

    private static (int Page, int PageSize) NormalizePaging(int page, int pageSize) =>
        (Math.Max(page, 1), Math.Clamp(pageSize <= 0 ? 25 : pageSize, 1, 100));

    private static SynqLienUserManagementException Error(int status, string code, string message) =>
        new(status, code, message);
}
