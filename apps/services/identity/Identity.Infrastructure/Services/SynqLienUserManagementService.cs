using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Data;
using BuildingBlocks.Authorization;
using Identity.Application.DTOs;
using Identity.Application.Interfaces;
using Identity.Domain;
using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using ProductCodes = BuildingBlocks.Authorization.ProductCodes;

namespace Identity.Infrastructure.Services;

public sealed class SynqLienUserManagementService : ISynqLienUserManagementService
{
    private const string ProductCode = ProductCodes.SynqLiens;
    private const string AdministratorRole = "Administrator";
    private static readonly string[] QualityAssurancePermissions =
    [
        PermissionCodes.LienUsersView, PermissionCodes.LienRolesView,
        PermissionCodes.CaseRead, PermissionCodes.CaseUpdate,
        PermissionCodes.LienRead, PermissionCodes.LienUpdate,
        PermissionCodes.TaskRead, PermissionCodes.TaskNoteManage,
        PermissionCodes.CaseNoteManage, PermissionCodes.LienSaleRead,
        PermissionCodes.LienSaleViewAnalytics,
    ];
    private static readonly string[] ViewOnlyPermissions =
    [
        PermissionCodes.LienUsersView, PermissionCodes.LienRolesView,
        PermissionCodes.CaseRead, PermissionCodes.LienRead,
        PermissionCodes.TaskRead, PermissionCodes.LienSaleRead,
    ];

    private readonly IdentityDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuditPublisher _audit;
    private readonly INotificationsCacheClient _notificationsCache;

    public SynqLienUserManagementService(
        IdentityDbContext db,
        IPasswordHasher passwordHasher,
        IAuditPublisher audit,
        INotificationsCacheClient notificationsCache)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _audit = audit;
        _notificationsCache = notificationsCache;
    }

    public async Task<SynqLienUserManagementResult<SynqLienPagedUsers>> ListAsync(
        SynqLienManagementScope scope, SynqLienUserListQuery query, CancellationToken ct)
    {
        var denied = await AuthorizeAsync<SynqLienPagedUsers>(scope, PermissionCodes.LienUsersView, ct);
        if (denied is not null) return denied;
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 25 : query.PageSize, 1, 100);
        if ((long)(page - 1) * pageSize > int.MaxValue) return Validation<SynqLienPagedUsers>("page is too large.");
        if (query.Search?.Length > 200 || query.Department?.Length > 150)
            return Validation<SynqLienPagedUsers>("search or department exceeds the maximum length.");

        var now = DateTime.UtcNow;
        var membershipUserIds = await _db.UserOrganizationMemberships.AsNoTracking()
            .Where(x => x.OrganizationId == scope.OrganizationId && x.IsActive)
            .Select(x => x.UserId).ToListAsync(ct);
        var pendingUserIds = await _db.UserInvitations.AsNoTracking()
            .Where(x => x.TenantId == scope.TenantId && x.OrganizationId == scope.OrganizationId &&
                x.ProductCode == ProductCode && x.Status == UserInvitation.Statuses.Pending && x.ExpiresAtUtc > now)
            .Select(x => x.UserId).ToListAsync(ct);
        var scopedUserIds = membershipUserIds.Concat(pendingUserIds).Distinct().ToList();

        var users = await _db.Users.AsNoTracking().Where(x => scopedUserIds.Contains(x.Id)).ToListAsync(ct);
        var memberships = await _db.UserOrganizationMemberships.AsNoTracking()
            .Where(x => scopedUserIds.Contains(x.UserId) && x.OrganizationId == scope.OrganizationId && x.IsActive)
            .ToDictionaryAsync(x => x.UserId, ct);
        var invitations = await _db.UserInvitations.AsNoTracking()
            .Where(x => scopedUserIds.Contains(x.UserId) && x.TenantId == scope.TenantId &&
                x.OrganizationId == scope.OrganizationId && x.ProductCode == ProductCode &&
                x.Status == UserInvitation.Statuses.Pending && x.ExpiresAtUtc > now)
            .OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);
        var accesses = await _db.UserProductAccessRecords.AsNoTracking()
            .Where(x => scopedUserIds.Contains(x.UserId) && x.TenantId == scope.TenantId &&
                x.OrganizationId == scope.OrganizationId && x.ProductCode == ProductCode)
            .ToListAsync(ct);
        var assignments = await _db.SynqLienUserAccessRoleAssignments.AsNoTracking()
            .Where(x => scopedUserIds.Contains(x.UserId) && x.TenantId == scope.TenantId &&
                x.OrganizationId == scope.OrganizationId && x.IsActive && x.Role.IsActive)
            .Select(x => new { x.UserId, x.RoleId, x.Role.Name }).ToListAsync(ct);
        var rolesByUser = assignments.GroupBy(x => x.UserId).ToDictionary(x => x.Key, x => x.First());
        var roleNames = await _db.SynqLienAccessRoles.AsNoTracking()
            .Where(x => x.TenantId == scope.TenantId && x.OrganizationId == scope.OrganizationId)
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        var items = users.Select(user =>
        {
            memberships.TryGetValue(user.Id, out var membership);
            var invitation = invitations.FirstOrDefault(x => x.UserId == user.Id);
            rolesByUser.TryGetValue(user.Id, out var assignment);
            var hasAccess = accesses.Any(x => x.UserId == user.Id && x.AccessStatus == AccessStatus.Granted);
            return new SynqLienUserListItem(
                user.Id, user.Email, user.FirstName, user.LastName,
                membership?.Department ?? invitation?.PendingDepartment,
                membership?.JobTitle ?? invitation?.PendingJobTitle,
                assignment?.RoleId ?? invitation?.PendingAccessRoleId,
                assignment?.Name ?? (invitation?.PendingAccessRoleId is Guid pendingRoleId &&
                    roleNames.TryGetValue(pendingRoleId, out var pendingRoleName) ? pendingRoleName : null),
                ResolveStatus(user.IsLocked, invitation is not null, hasAccess), user.LastLoginAtUtc);
        }).ToList();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            items = items.Where(x => x.Email.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.FirstName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.LastName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                $"{x.FirstName} {x.LastName}".Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        if (!string.IsNullOrWhiteSpace(query.Department))
            items = items.Where(x => string.Equals(x.Department, query.Department.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        if (query.RoleId.HasValue) items = items.Where(x => x.RoleId == query.RoleId).ToList();
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim().ToUpperInvariant();
            if (status is not ("ACTIVE" or "INACTIVE" or "INVITED" or "LOCKED"))
                return Validation<SynqLienPagedUsers>("status must be ACTIVE, INACTIVE, INVITED, or LOCKED.");
            items = items.Where(x => x.Status == status).ToList();
        }
        var sort = query.Sort?.Trim().ToUpperInvariant();
        if (sort is not null and not "" && sort is not ("NAME_ASC" or "NAME_DESC" or "LAST_LOGIN_DESC"))
            return Validation<SynqLienPagedUsers>("sort must be NAME_ASC, NAME_DESC, or LAST_LOGIN_DESC.");
        items = sort switch
        {
            "NAME_DESC" => items.OrderByDescending(x => x.LastName).ThenByDescending(x => x.FirstName).ToList(),
            "LAST_LOGIN_DESC" => items.OrderByDescending(x => x.LastLoginAtUtc).ThenBy(x => x.LastName).ToList(),
            null or "" or "NAME_ASC" => items.OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ToList(),
            _ => items,
        };
        var total = items.Count;
        return SynqLienUserManagementResult<SynqLienPagedUsers>.Success(
            new(items.Skip((page - 1) * pageSize).Take(pageSize).ToList(), page, pageSize, total));
    }

    public async Task<SynqLienUserManagementResult<SynqLienUserListItem>> GetAsync(
        SynqLienManagementScope scope, Guid userId, CancellationToken ct)
    {
        var denied = await AuthorizeAsync<SynqLienUserListItem>(scope, PermissionCodes.LienUsersView, ct);
        if (denied is not null) return denied;
        var user = await _db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null) return NotFound<SynqLienUserListItem>();
        var membership = await _db.UserOrganizationMemberships.AsNoTracking().SingleOrDefaultAsync(x =>
            x.UserId == userId && x.OrganizationId == scope.OrganizationId && x.IsActive, ct);
        var now = DateTime.UtcNow;
        var invitation = await _db.UserInvitations.AsNoTracking()
            .Where(x => x.UserId == userId && x.TenantId == scope.TenantId &&
                x.OrganizationId == scope.OrganizationId && x.ProductCode == ProductCode &&
                x.Status == UserInvitation.Statuses.Pending && x.ExpiresAtUtc > now)
            .OrderByDescending(x => x.CreatedAtUtc).FirstOrDefaultAsync(ct);
        if (membership is null && invitation is null) return NotFound<SynqLienUserListItem>();
        var assignment = await _db.SynqLienUserAccessRoleAssignments.AsNoTracking()
            .Where(x => x.UserId == userId && x.TenantId == scope.TenantId &&
                x.OrganizationId == scope.OrganizationId && x.IsActive && x.Role.IsActive)
            .Select(x => new { x.RoleId, x.Role.Name }).FirstOrDefaultAsync(ct);
        var roleId = assignment?.RoleId ?? invitation?.PendingAccessRoleId;
        var roleName = assignment?.Name;
        if (roleName is null && roleId.HasValue)
            roleName = await _db.SynqLienAccessRoles.AsNoTracking()
                .Where(x => x.Id == roleId.Value && x.TenantId == scope.TenantId && x.OrganizationId == scope.OrganizationId)
                .Select(x => x.Name).SingleOrDefaultAsync(ct);
        var hasAccess = await _db.UserProductAccessRecords.AsNoTracking().AnyAsync(x =>
            x.UserId == userId && x.TenantId == scope.TenantId && x.OrganizationId == scope.OrganizationId &&
            x.ProductCode == ProductCode && x.AccessStatus == AccessStatus.Granted, ct);
        return SynqLienUserManagementResult<SynqLienUserListItem>.Success(new(
            user.Id, user.Email, user.FirstName, user.LastName,
            membership?.Department ?? invitation?.PendingDepartment,
            membership?.JobTitle ?? invitation?.PendingJobTitle,
            roleId, roleName, ResolveStatus(user.IsLocked, invitation is not null, hasAccess), user.LastLoginAtUtc));
    }

    public async Task<SynqLienUserManagementResult<SynqLienUserManagementOptions>> GetOptionsAsync(
        SynqLienManagementScope scope, CancellationToken ct)
    {
        var denied = await AuthorizeAsync<SynqLienUserManagementOptions>(scope, PermissionCodes.LienUsersView, ct);
        if (denied is not null) return denied;
        await EnsureStarterRolesAsync(scope, ct);
        var roles = await LoadRolesAsync(scope, ct);
        var permissionQuery = _db.Permissions.AsNoTracking()
            .Where(x => x.IsActive && x.Product.Code == ProductCode);
        var canSeeFullCatalog = await _db.Organizations.AsNoTracking().AnyAsync(x =>
                x.Id == scope.OrganizationId && x.OwnerUserId == scope.ActorUserId, ct) ||
            await IsBreakGlassAsync(scope, ct);
        if (!canSeeFullCatalog)
        {
            var grantableCodes = await GetAssignedPermissionCodesAsync(scope, ct);
            permissionQuery = permissionQuery.Where(x => grantableCodes.Contains(x.Code));
        }
        var permissions = await permissionQuery
            .OrderBy(x => x.Category).ThenBy(x => x.Name)
            .Select(x => new SynqLienPermissionOption(x.Code, x.Name, x.Description, x.Category)).ToListAsync(ct);
        var departments = await _db.UserOrganizationMemberships.AsNoTracking()
            .Where(x => x.OrganizationId == scope.OrganizationId && x.IsActive && x.Department != null)
            .Select(x => x.Department!).Distinct().OrderBy(x => x).ToListAsync(ct);
        return SynqLienUserManagementResult<SynqLienUserManagementOptions>.Success(
            new(roles, permissions, departments, ["ACTIVE", "INACTIVE", "INVITED", "LOCKED"],
                ["NAME_ASC", "NAME_DESC", "LAST_LOGIN_DESC"]));
    }

    public async Task<SynqLienUserManagementResult<SynqLienInvitationMutation>> InviteAsync(
        SynqLienManagementScope scope, SynqLienInviteRequest request, CancellationToken ct)
    {
        var denied = await AuthorizeAsync<SynqLienInvitationMutation>(scope, PermissionCodes.LienInvitationsManage, ct);
        if (denied is not null) return denied;
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            return Validation<SynqLienInvitationMutation>("email, firstName, and lastName are required.");
        if (!MailAddress.TryCreate(request.Email.Trim(), out _) || request.Email.Length > 320 ||
            request.FirstName.Length > 100 || request.LastName.Length > 100 ||
            request.Department?.Length > 150 || request.JobTitle?.Length > 150)
            return Validation<SynqLienInvitationMutation>("One or more invitation fields are invalid or exceed the maximum length.");
        var role = await FindRoleAsync(scope, request.RoleId, ct);
        if (role is null) return Validation<SynqLienInvitationMutation>("roleId is not an active role in this organization.");
        if (!await CanAssignRoleAsync(scope, role, ct))
            return Forbidden<SynqLienInvitationMutation>();

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.SingleOrDefaultAsync(x => x.Email == email, ct);
        var requiresAccountActivation = user is null;
        if (user is null)
        {
            user = User.Create(scope.TenantId, email, _passwordHasher.Hash(Guid.CreateVersion7().ToString()), request.FirstName, request.LastName);
            user.Deactivate();
            _db.Users.Add(user);
        }
        else if (!user.IsActive)
        {
            requiresAccountActivation = await _db.UserInvitations.AnyAsync(x =>
                x.UserId == user.Id && x.TenantId == scope.TenantId && x.OrganizationId == scope.OrganizationId &&
                x.ProductCode == ProductCode && x.RequiresAccountActivation, ct);
            if (!requiresAccountActivation)
                return Conflict<SynqLienInvitationMutation>(
                    "This Identity account is inactive and must be restored by an Identity administrator before SynqLien access can be granted.",
                    "synqlien.identity_account_inactive");
        }

        var currentPending = await PendingInvitations(scope, user.Id, ct);
        if (currentPending.Any(x => !x.IsExpired()))
            return Conflict<SynqLienInvitationMutation>(
                "A SynqLien invitation is already pending for this user in the organization.",
                "synqlien.invitation_pending");

        var isActiveMember = user.IsActive && await _db.UserOrganizationMemberships.AnyAsync(x =>
            x.UserId == user.Id && x.OrganizationId == scope.OrganizationId && x.IsActive, ct);
        if (isActiveMember)
        {
            if (user.Id == scope.ActorUserId)
                return Conflict<SynqLienInvitationMutation>(
                    "You cannot change your own SynqLien management role.",
                    "synqlien.self_role_change");
            await EnsureTenantMembershipAsync(scope, user.Id, ct);
            var membership = await _db.UserOrganizationMemberships.SingleAsync(x =>
                x.UserId == user.Id && x.OrganizationId == scope.OrganizationId, ct);
            membership.UpdateOrganizationProfile(request.Department, request.JobTitle);
            await GrantProductAccessAsync(scope, user, ct);
            await ReplaceAccessRoleAsync(scope, user, role, ct);
            await EnsureSellerPersonaAsync(scope, user, ct);
            await _db.SaveChangesAsync(ct);
            Publish(scope, "identity.synqlien.user.access_granted", "SynqLienUserAccessGranted", user.Id);
            return SynqLienUserManagementResult<SynqLienInvitationMutation>.Success(new(
                user.Id, null, user.Email, $"{user.FirstName} {user.LastName}".Trim(), "ACCESS_GRANTED", null));
        }

        await RevokePendingInvitationsAsync(scope, user.Id, ct);
        var rawToken = CreateRawToken();
        var invitation = UserInvitation.Create(user.Id, scope.TenantId, HashToken(rawToken),
            UserInvitation.PortalOrigins.TenantPortal, scope.ActorUserId, 72, scope.OrganizationId,
            ProductCode, role.Id, request.Department, request.JobTitle,
            requiresAccountActivation);
        _db.UserInvitations.Add(invitation);
        await _db.SaveChangesAsync(ct);
        Publish(scope, "identity.synqlien.user.invited", "SynqLienUserInvited", user.Id,
            after: JsonSerializer.Serialize(new { role.Id, role.Name, request.Department, request.JobTitle }));
        return SynqLienUserManagementResult<SynqLienInvitationMutation>.Success(new(
            user.Id, invitation.Id, user.Email, $"{user.FirstName} {user.LastName}".Trim(), "INVITED", rawToken));
    }

    public async Task<SynqLienUserManagementResult<SynqLienInvitationMutation>> ResendInvitationAsync(
        SynqLienManagementScope scope, Guid userId, CancellationToken ct)
    {
        var denied = await AuthorizeAsync<SynqLienInvitationMutation>(scope, PermissionCodes.LienInvitationsManage, ct);
        if (denied is not null) return denied;
        var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == userId, ct);
        var pending = await PendingInvitations(scope, userId, ct);
        if (user is null || pending.Count == 0) return NotFound<SynqLienInvitationMutation>();
        var latest = pending.OrderByDescending(x => x.CreatedAtUtc).First();
        if (!latest.PendingAccessRoleId.HasValue)
            return Conflict<SynqLienInvitationMutation>(
                "This invitation has no SynqLien access role.",
                "synqlien.invitation_role_unavailable");
        var role = await FindRoleAsync(scope, latest.PendingAccessRoleId.Value, ct);
        if (role is null)
            return Conflict<SynqLienInvitationMutation>(
                "The invitation's SynqLien access role is no longer available.",
                "synqlien.invitation_role_unavailable");
        if (!await CanAssignRoleAsync(scope, role, ct))
            return Forbidden<SynqLienInvitationMutation>();
        foreach (var item in pending) item.Revoke();
        var rawToken = CreateRawToken();
        var replacement = UserInvitation.Create(userId, scope.TenantId, HashToken(rawToken),
            UserInvitation.PortalOrigins.TenantPortal, scope.ActorUserId, 72, scope.OrganizationId,
            ProductCode, latest.PendingAccessRoleId, latest.PendingDepartment, latest.PendingJobTitle,
            latest.RequiresAccountActivation);
        _db.UserInvitations.Add(replacement);
        await _db.SaveChangesAsync(ct);
        Publish(scope, "identity.synqlien.invitation.resent", "SynqLienInvitationResent", userId);
        return SynqLienUserManagementResult<SynqLienInvitationMutation>.Success(new(
            user.Id, replacement.Id, user.Email, $"{user.FirstName} {user.LastName}".Trim(), "INVITED", rawToken));
    }

    public async Task<SynqLienUserManagementResult<bool>> CancelInvitationAsync(
        SynqLienManagementScope scope, Guid userId, CancellationToken ct)
    {
        var denied = await AuthorizeAsync<bool>(scope, PermissionCodes.LienInvitationsManage, ct);
        if (denied is not null) return denied;
        var pending = await PendingInvitations(scope, userId, ct);
        if (pending.Count == 0) return NotFound<bool>();
        foreach (var item in pending) item.Revoke();
        await _db.SaveChangesAsync(ct);
        Publish(scope, "identity.synqlien.invitation.cancelled", "SynqLienInvitationCancelled", userId);
        return SynqLienUserManagementResult<bool>.Success(true);
    }

    public async Task<SynqLienUserManagementResult<SynqLienUserListItem>> UpdateOrganizationProfileAsync(
        SynqLienManagementScope scope, Guid userId, SynqLienOrganizationProfileRequest request, CancellationToken ct)
    {
        var denied = await AuthorizeAsync<SynqLienUserListItem>(scope, PermissionCodes.LienUsersManage, ct);
        if (denied is not null) return denied;
        if (request.Department?.Length > 150 || request.JobTitle?.Length > 150)
            return Validation<SynqLienUserListItem>("department and jobTitle must be at most 150 characters.");
        var membership = await FindMembershipAsync(scope, userId, ct);
        if (membership is null) return NotFound<SynqLienUserListItem>();
        membership.UpdateOrganizationProfile(request.Department, request.JobTitle);
        await _db.SaveChangesAsync(ct);
        Publish(scope, "identity.synqlien.user.profile_updated", "SynqLienOrganizationProfileUpdated", userId);
        return await GetAsync(scope, userId, ct);
    }

    public async Task<SynqLienUserManagementResult<SynqLienUserListItem>> ReplaceRoleAsync(
        SynqLienManagementScope scope, Guid userId, SynqLienRoleRequest request, CancellationToken ct)
    {
        var denied = await AuthorizeAsync<SynqLienUserListItem>(scope, PermissionCodes.LienUsersManage, ct);
        if (denied is not null) return denied;
        await using var transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
            : null;
        if (userId == scope.ActorUserId)
            return Conflict<SynqLienUserListItem>("You cannot change your own SynqLien management role.", "synqlien.self_role_change");
        var user = await FindMemberUserAsync(scope, userId, ct);
        var role = await FindRoleAsync(scope, request.RoleId, ct);
        if (user is null) return NotFound<SynqLienUserListItem>();
        if (role is null) return Validation<SynqLienUserListItem>("roleId is not an active role in this organization.");
        if (!await CanAssignRoleAsync(scope, role, ct))
            return Forbidden<SynqLienUserListItem>();
        if (await IsLastAdministratorAsync(scope, userId, ct) && !string.Equals(role.Name, AdministratorRole, StringComparison.OrdinalIgnoreCase))
            return Conflict<SynqLienUserListItem>("The organization must retain at least one active Administrator.", "synqlien.last_administrator");
        await ReplaceAccessRoleAsync(scope, user, role, ct);
        await _db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        Publish(scope, "identity.synqlien.user.role_replaced", "SynqLienUserRoleReplaced", userId);
        return await GetAsync(scope, userId, ct);
    }

    public async Task<SynqLienUserManagementResult<bool>> SetProductAccessAsync(
        SynqLienManagementScope scope, Guid userId, bool activate, CancellationToken ct)
    {
        var denied = await AuthorizeAsync<bool>(scope, PermissionCodes.LienUsersManage, ct);
        if (denied is not null) return denied;
        await using var transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
            : null;
        if (!activate && userId == scope.ActorUserId)
            return Conflict<bool>("You cannot deactivate your own SynqLien access.", "synqlien.self_deactivation");
        var user = await FindMemberUserAsync(scope, userId, ct);
        if (user is null) return NotFound<bool>();
        if (!activate && await IsLastAdministratorAsync(scope, userId, ct))
            return Conflict<bool>("The organization must retain at least one active Administrator.", "synqlien.last_administrator");
        var access = await _db.UserProductAccessRecords.SingleOrDefaultAsync(x =>
            x.UserId == userId && x.TenantId == scope.TenantId && x.OrganizationId == scope.OrganizationId && x.ProductCode == ProductCode, ct);
        if (activate)
        {
            if (access is null) _db.UserProductAccessRecords.Add(UserProductAccess.Create(scope.TenantId, userId, ProductCode, scope.OrganizationId, scope.ActorUserId));
            else access.Grant(scope.ActorUserId);
        }
        else access?.Revoke(scope.ActorUserId);
        user.IncrementAccessVersion();
        await _db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        Publish(scope, activate ? "identity.synqlien.user.activated" : "identity.synqlien.user.deactivated",
            activate ? "SynqLienUserActivated" : "SynqLienUserDeactivated", userId);
        _notificationsCache.InvalidateTenant(scope.TenantId, "identity.synqlien.user.access_changed", activate ? "activated" : "deactivated");
        return SynqLienUserManagementResult<bool>.Success(true);
    }

    public async Task<SynqLienUserManagementResult<IReadOnlyList<SynqLienAccessRoleDto>>> ListRolesAsync(
        SynqLienManagementScope scope, CancellationToken ct)
    {
        var denied = await AuthorizeAsync<IReadOnlyList<SynqLienAccessRoleDto>>(scope, PermissionCodes.LienRolesView, ct);
        if (denied is not null) return denied;
        await EnsureStarterRolesAsync(scope, ct);
        return SynqLienUserManagementResult<IReadOnlyList<SynqLienAccessRoleDto>>.Success(await LoadRolesAsync(scope, ct));
    }

    public async Task<SynqLienUserManagementResult<SynqLienAccessRoleDto>> CreateRoleAsync(
        SynqLienManagementScope scope, SynqLienAccessRoleRequest request, CancellationToken ct)
    {
        var denied = await AuthorizeAsync<SynqLienAccessRoleDto>(scope, PermissionCodes.LienRolesManage, ct);
        if (denied is not null) return denied;
        var validation = await ValidateRoleRequestAsync(scope, request, null, ct);
        if (validation is not null) return validation;
        var role = SynqLienAccessRole.Create(scope.TenantId, scope.OrganizationId, request.Name, request.Description, false, scope.ActorUserId);
        _db.SynqLienAccessRoles.Add(role);
        await ReplaceRolePermissionsAsync(role, request.Permissions, ct);
        await _db.SaveChangesAsync(ct);
        Publish(scope, "identity.synqlien.role.created", "SynqLienRoleCreated", role.Id);
        return SynqLienUserManagementResult<SynqLienAccessRoleDto>.Success(await MapRoleAsync(role.Id, ct));
    }

    public async Task<SynqLienUserManagementResult<SynqLienAccessRoleDto>> UpdateRoleAsync(
        SynqLienManagementScope scope, Guid roleId, SynqLienAccessRoleRequest request, CancellationToken ct)
    {
        var denied = await AuthorizeAsync<SynqLienAccessRoleDto>(scope, PermissionCodes.LienRolesManage, ct);
        if (denied is not null) return denied;
        var role = await FindRoleAsync(scope, roleId, ct);
        if (role is null) return NotFound<SynqLienAccessRoleDto>();
        if (role.IsSystem) return Conflict<SynqLienAccessRoleDto>("Starter roles cannot be edited.", "synqlien.system_role");
        var validation = await ValidateRoleRequestAsync(scope, request, roleId, ct);
        if (validation is not null) return validation;
        role.Update(request.Name, request.Description, scope.ActorUserId);
        await ReplaceRolePermissionsAsync(role, request.Permissions, ct);
        await IncrementAssignedUsersAsync(role.Id, ct);
        await _db.SaveChangesAsync(ct);
        Publish(scope, "identity.synqlien.role.updated", "SynqLienRoleUpdated", role.Id);
        return SynqLienUserManagementResult<SynqLienAccessRoleDto>.Success(await MapRoleAsync(role.Id, ct));
    }

    public async Task<SynqLienUserManagementResult<bool>> DeleteRoleAsync(
        SynqLienManagementScope scope, Guid roleId, CancellationToken ct)
    {
        var denied = await AuthorizeAsync<bool>(scope, PermissionCodes.LienRolesManage, ct);
        if (denied is not null) return denied;
        var role = await FindRoleAsync(scope, roleId, ct);
        if (role is null) return NotFound<bool>();
        if (role.IsSystem) return Conflict<bool>("Starter roles cannot be deleted.", "synqlien.system_role");
        if (await _db.SynqLienUserAccessRoleAssignments.AnyAsync(x => x.RoleId == roleId && x.IsActive, ct))
            return Conflict<bool>("Reassign users before deleting this role.", "synqlien.role_in_use");
        role.Deactivate(scope.ActorUserId);
        await _db.SaveChangesAsync(ct);
        Publish(scope, "identity.synqlien.role.deleted", "SynqLienRoleDeleted", role.Id);
        return SynqLienUserManagementResult<bool>.Success(true);
    }

    private async Task<SynqLienUserManagementResult<T>?> AuthorizeAsync<T>(SynqLienManagementScope scope, string permission, CancellationToken ct)
    {
        var org = await _db.Organizations.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == scope.OrganizationId && x.TenantId == scope.TenantId && x.IsActive, ct);
        if (org is null || !string.Equals(org.OrgType, Identity.Domain.OrgType.LawFirm, StringComparison.OrdinalIgnoreCase))
            return NotFound<T>();
        var tenantHasProduct = await _db.TenantProducts.AsNoTracking().AnyAsync(x =>
            x.TenantId == scope.TenantId && x.IsEnabled && x.Product.Code == ProductCode && x.Product.IsActive, ct);
        if (!tenantHasProduct) return Forbidden<T>();
        var actorIsActive = await _db.Users.AsNoTracking().AnyAsync(x => x.Id == scope.ActorUserId && x.IsActive && !x.IsLocked, ct);
        if (!actorIsActive) return Forbidden<T>();
        var breakGlass = await IsBreakGlassAsync(scope, ct);
        if (breakGlass)
        {
            Publish(scope, "identity.synqlien.break_glass", "SynqLienBreakGlassAccess", scope.ActorUserId,
                after: JsonSerializer.Serialize(new { permission }));
            return null;
        }
        var isActiveMember = await _db.UserTenants.AsNoTracking().AnyAsync(x =>
            x.UserId == scope.ActorUserId && x.TenantId == scope.TenantId && x.IsActive, ct) &&
            await _db.UserOrganizationMemberships.AsNoTracking().AnyAsync(x =>
                x.UserId == scope.ActorUserId && x.OrganizationId == scope.OrganizationId && x.IsActive, ct);
        if (!isActiveMember) return Forbidden<T>();
        var hasProductAccess = await _db.UserProductAccessRecords.AsNoTracking().AnyAsync(x =>
            x.UserId == scope.ActorUserId && x.TenantId == scope.TenantId &&
            x.OrganizationId == scope.OrganizationId && x.ProductCode == ProductCode &&
            x.AccessStatus == AccessStatus.Granted, ct);
        if (!hasProductAccess) return Forbidden<T>();
        if (org.OwnerUserId == scope.ActorUserId) return null;
        var allowed = await _db.SynqLienUserAccessRoleAssignments.AsNoTracking().AnyAsync(x =>
            x.TenantId == scope.TenantId && x.OrganizationId == scope.OrganizationId && x.UserId == scope.ActorUserId &&
            x.IsActive && x.Role.IsActive && x.Role.Permissions.Any(p => p.Permission.IsActive && p.Permission.Code == permission), ct);
        return allowed ? null : Forbidden<T>();
    }

    private Task<bool> IsBreakGlassAsync(SynqLienManagementScope scope, CancellationToken ct) =>
        _db.ScopedRoleAssignments.AsNoTracking().AnyAsync(x =>
            x.UserId == scope.ActorUserId && x.IsActive && x.ScopeType == ScopedRoleAssignment.ScopeTypes.Global &&
            (x.Role.Name == Roles.PlatformAdmin ||
             (x.Role.Name == Roles.TenantAdmin && x.Role.TenantId == scope.TenantId)), ct);

    private async Task<List<SynqLienAccessRoleDto>> LoadRolesAsync(SynqLienManagementScope scope, CancellationToken ct) =>
        await _db.SynqLienAccessRoles.AsNoTracking()
            .Where(x => x.TenantId == scope.TenantId && x.OrganizationId == scope.OrganizationId && x.IsActive)
            .OrderByDescending(x => x.IsSystem).ThenBy(x => x.Name)
            .Select(x => new SynqLienAccessRoleDto(x.Id, x.Name, x.Description, x.IsSystem, x.IsActive,
                x.Assignments.Count(a => a.IsActive), x.Permissions.Where(p => p.Permission.IsActive).Select(p => p.Permission.Code).OrderBy(p => p).ToList()))
            .ToListAsync(ct);

    private async Task EnsureStarterRolesAsync(SynqLienManagementScope scope, CancellationToken ct)
    {
        if (await _db.SynqLienAccessRoles.AnyAsync(x =>
                x.TenantId == scope.TenantId && x.OrganizationId == scope.OrganizationId && x.IsSystem, ct))
            return;

        var organization = await _db.Organizations.AsNoTracking().SingleAsync(x =>
            x.Id == scope.OrganizationId && x.TenantId == scope.TenantId, ct);
        var permissions = await _db.Permissions.Where(x =>
                x.IsActive && x.Product.Code == ProductCode)
            .ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, ct);

        var administrator = SynqLienAccessRole.Create(scope.TenantId, scope.OrganizationId,
            AdministratorRole, "Full SynqLien access and organization user management", true, scope.ActorUserId);
        var qualityAssurance = SynqLienAccessRole.Create(scope.TenantId, scope.OrganizationId,
            "Quality Assurance", "Operational review and quality-control access", true, scope.ActorUserId);
        var viewOnly = SynqLienAccessRole.Create(scope.TenantId, scope.OrganizationId,
            "View Only", "Read-only SynqLien access", true, scope.ActorUserId);
        _db.SynqLienAccessRoles.AddRange(administrator, qualityAssurance, viewOnly);

        AddPermissions(administrator, permissions.Keys);
        AddPermissions(qualityAssurance, QualityAssurancePermissions);
        AddPermissions(viewOnly, ViewOnlyPermissions);

        if (organization.OwnerUserId.HasValue)
        {
            _db.SynqLienUserAccessRoleAssignments.Add(SynqLienUserAccessRoleAssignment.Create(
                scope.TenantId, scope.OrganizationId, organization.OwnerUserId.Value,
                administrator.Id, scope.ActorUserId));
        }

        await _db.SaveChangesAsync(ct);

        void AddPermissions(SynqLienAccessRole role, IEnumerable<string> codes)
        {
            foreach (var code in codes.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (permissions.TryGetValue(code, out var permission))
                    _db.SynqLienAccessRolePermissions.Add(SynqLienAccessRolePermission.Create(role.Id, permission.Id));
            }
        }
    }

    private Task<SynqLienAccessRole?> FindRoleAsync(SynqLienManagementScope scope, Guid roleId, CancellationToken ct) =>
        _db.SynqLienAccessRoles.SingleOrDefaultAsync(x => x.Id == roleId && x.TenantId == scope.TenantId &&
            x.OrganizationId == scope.OrganizationId && x.IsActive, ct);

    private async Task<SynqLienAccessRoleDto> MapRoleAsync(Guid roleId, CancellationToken ct) =>
        await _db.SynqLienAccessRoles.AsNoTracking().Where(x => x.Id == roleId)
            .Select(x => new SynqLienAccessRoleDto(x.Id, x.Name, x.Description, x.IsSystem, x.IsActive,
                x.Assignments.Count(a => a.IsActive), x.Permissions.Where(p => p.Permission.IsActive).Select(p => p.Permission.Code).OrderBy(p => p).ToList()))
            .SingleAsync(ct);

    private async Task<SynqLienUserManagementResult<SynqLienAccessRoleDto>?> ValidateRoleRequestAsync(
        SynqLienManagementScope scope, SynqLienAccessRoleRequest request, Guid? existingId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 100 || request.Description?.Length > 500)
            return Validation<SynqLienAccessRoleDto>("name is required and role fields exceed their maximum length.");
        if (request.Permissions is null || request.Permissions.Count == 0)
            return Validation<SynqLienAccessRoleDto>("Select at least one permission.");
        if (await _db.SynqLienAccessRoles.AnyAsync(x => x.TenantId == scope.TenantId && x.OrganizationId == scope.OrganizationId &&
            x.IsActive && x.Id != existingId && x.Name.ToLower() == request.Name.Trim().ToLower(), ct))
            return Conflict<SynqLienAccessRoleDto>("A role with this name already exists.", "synqlien.role_name_conflict");
        var distinct = request.Permissions.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var validCount = await _db.Permissions.CountAsync(x => x.IsActive && x.Product.Code == ProductCode && distinct.Contains(x.Code), ct);
        if (validCount != distinct.Count)
            return Validation<SynqLienAccessRoleDto>("One or more permissions are invalid for SynqLien.");
        var organizationOwner = await _db.Organizations.AsNoTracking().AnyAsync(x =>
            x.Id == scope.OrganizationId && x.OwnerUserId == scope.ActorUserId, ct);
        if (organizationOwner || await IsBreakGlassAsync(scope, ct)) return null;
        var grantable = await GetAssignedPermissionCodesAsync(scope, ct);
        return distinct.All(code => grantable.Contains(code, StringComparer.OrdinalIgnoreCase))
            ? null
            : Forbidden<SynqLienAccessRoleDto>();
    }

    private Task<List<string>> GetAssignedPermissionCodesAsync(SynqLienManagementScope scope, CancellationToken ct) =>
        _db.SynqLienUserAccessRoleAssignments.AsNoTracking()
            .Where(x => x.TenantId == scope.TenantId && x.OrganizationId == scope.OrganizationId &&
                x.UserId == scope.ActorUserId && x.IsActive && x.Role.IsActive)
            .SelectMany(x => x.Role.Permissions.Where(p => p.Permission.IsActive).Select(p => p.Permission.Code))
            .Distinct().ToListAsync(ct);

    private async Task<bool> CanAssignRoleAsync(
        SynqLienManagementScope scope,
        SynqLienAccessRole role,
        CancellationToken ct)
    {
        var organizationOwner = await _db.Organizations.AsNoTracking().AnyAsync(x =>
            x.Id == scope.OrganizationId && x.OwnerUserId == scope.ActorUserId, ct);
        if (organizationOwner || await IsBreakGlassAsync(scope, ct)) return true;

        var rolePermissions = await _db.SynqLienAccessRolePermissions.AsNoTracking()
            .Where(x => x.RoleId == role.Id && x.Permission.IsActive)
            .Select(x => x.Permission.Code)
            .Distinct()
            .ToListAsync(ct);
        var grantable = await GetAssignedPermissionCodesAsync(scope, ct);
        return rolePermissions.All(code => grantable.Contains(code, StringComparer.OrdinalIgnoreCase));
    }

    private async Task ReplaceRolePermissionsAsync(SynqLienAccessRole role, IReadOnlyList<string> codes, CancellationToken ct)
    {
        var existing = await _db.SynqLienAccessRolePermissions.Where(x => x.RoleId == role.Id).ToListAsync(ct);
        _db.SynqLienAccessRolePermissions.RemoveRange(existing);
        var ids = await _db.Permissions.Where(x => x.IsActive && x.Product.Code == ProductCode && codes.Contains(x.Code)).Select(x => x.Id).ToListAsync(ct);
        _db.SynqLienAccessRolePermissions.AddRange(ids.Select(x => SynqLienAccessRolePermission.Create(role.Id, x)));
    }

    private async Task ReplaceAccessRoleAsync(SynqLienManagementScope scope, User user, SynqLienAccessRole role, CancellationToken ct)
    {
        var current = await _db.SynqLienUserAccessRoleAssignments.Where(x => x.TenantId == scope.TenantId &&
            x.OrganizationId == scope.OrganizationId && x.UserId == user.Id && x.IsActive).ToListAsync(ct);
        foreach (var assignment in current) assignment.Remove(scope.ActorUserId);
        _db.SynqLienUserAccessRoleAssignments.Add(SynqLienUserAccessRoleAssignment.Create(
            scope.TenantId, scope.OrganizationId, user.Id, role.Id, scope.ActorUserId));
        user.IncrementAccessVersion();
    }

    private async Task GrantProductAccessAsync(SynqLienManagementScope scope, User user, CancellationToken ct)
    {
        var access = await _db.UserProductAccessRecords.SingleOrDefaultAsync(x => x.UserId == user.Id &&
            x.TenantId == scope.TenantId && x.OrganizationId == scope.OrganizationId && x.ProductCode == ProductCode, ct);
        if (access is null) _db.UserProductAccessRecords.Add(UserProductAccess.Create(scope.TenantId, user.Id, ProductCode, scope.OrganizationId, scope.ActorUserId));
        else access.Grant(scope.ActorUserId);
        user.IncrementAccessVersion();
    }

    private async Task EnsureTenantMembershipAsync(SynqLienManagementScope scope, Guid userId, CancellationToken ct)
    {
        var membership = await _db.UserTenants.SingleOrDefaultAsync(x => x.UserId == userId && x.TenantId == scope.TenantId, ct);
        if (membership is null) _db.UserTenants.Add(UserTenant.Create(userId, scope.TenantId)); else membership.Activate();
    }

    private async Task EnsureSellerPersonaAsync(SynqLienManagementScope scope, User user, CancellationToken ct)
    {
        if (!await _db.UserRoleAssignments.AnyAsync(x => x.TenantId == scope.TenantId && x.OrganizationId == scope.OrganizationId &&
            x.UserId == user.Id && x.ProductCode == ProductCode && x.RoleCode == ProductRoleCodes.SynqLienSeller &&
            x.AssignmentStatus == AssignmentStatus.Active, ct))
            _db.UserRoleAssignments.Add(UserRoleAssignment.Create(scope.TenantId, user.Id, ProductRoleCodes.SynqLienSeller,
                ProductCode, scope.OrganizationId, scope.ActorUserId));
    }

    private Task<User?> FindMemberUserAsync(SynqLienManagementScope scope, Guid userId, CancellationToken ct) =>
        _db.Users.SingleOrDefaultAsync(x => x.Id == userId && _db.UserOrganizationMemberships.Any(m =>
            m.UserId == x.Id && m.OrganizationId == scope.OrganizationId && m.IsActive), ct);

    private Task<UserOrganizationMembership?> FindMembershipAsync(SynqLienManagementScope scope, Guid userId, CancellationToken ct) =>
        _db.UserOrganizationMemberships.SingleOrDefaultAsync(x => x.UserId == userId && x.OrganizationId == scope.OrganizationId && x.IsActive, ct);

    private Task<List<UserInvitation>> PendingInvitations(SynqLienManagementScope scope, Guid userId, CancellationToken ct) =>
        _db.UserInvitations.Where(x => x.UserId == userId && x.TenantId == scope.TenantId &&
            x.OrganizationId == scope.OrganizationId && x.ProductCode == ProductCode && x.Status == UserInvitation.Statuses.Pending).ToListAsync(ct);

    private async Task RevokePendingInvitationsAsync(SynqLienManagementScope scope, Guid userId, CancellationToken ct)
    {
        foreach (var invitation in await PendingInvitations(scope, userId, ct)) invitation.Revoke();
    }

    private async Task<bool> IsLastAdministratorAsync(SynqLienManagementScope scope, Guid userId, CancellationToken ct)
    {
        var targetIsAdmin = await _db.SynqLienUserAccessRoleAssignments.AnyAsync(x => x.TenantId == scope.TenantId &&
            x.OrganizationId == scope.OrganizationId && x.UserId == userId && x.IsActive && x.Role.IsActive && x.Role.Name == AdministratorRole, ct);
        if (!targetIsAdmin) return false;
        return await _db.SynqLienUserAccessRoleAssignments.CountAsync(x => x.TenantId == scope.TenantId &&
            x.OrganizationId == scope.OrganizationId && x.IsActive && x.Role.IsActive && x.Role.Name == AdministratorRole &&
            _db.UserProductAccessRecords.Any(a => a.TenantId == scope.TenantId && a.OrganizationId == scope.OrganizationId &&
                a.UserId == x.UserId && a.ProductCode == ProductCode && a.AccessStatus == AccessStatus.Granted), ct) <= 1;
    }

    private async Task IncrementAssignedUsersAsync(Guid roleId, CancellationToken ct)
    {
        var ids = await _db.SynqLienUserAccessRoleAssignments.Where(x => x.RoleId == roleId && x.IsActive).Select(x => x.UserId).ToListAsync(ct);
        var users = await _db.Users.Where(x => ids.Contains(x.Id)).ToListAsync(ct);
        foreach (var user in users) user.IncrementAccessVersion();
    }

    private void Publish(SynqLienManagementScope scope, string eventType, string action, Guid id, string? before = null, string? after = null) =>
        _audit.Publish(eventType, action, $"{action} for '{id}'.", scope.TenantId, scope.ActorUserId,
            "SynqLienUserManagement", id.ToString(), before, after,
            JsonSerializer.Serialize(new { scope.OrganizationId, ProductCode }), scope.CorrelationId);

    private static string ResolveStatus(bool locked, bool invited, bool active) => locked ? "LOCKED" : invited ? "INVITED" : active ? "ACTIVE" : "INACTIVE";
    private static string CreateRawToken() => Guid.CreateVersion7().ToString("N") + Guid.CreateVersion7().ToString("N");
    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static SynqLienUserManagementResult<T> NotFound<T>() => SynqLienUserManagementResult<T>.Fail(SynqLienUserManagementError.NotFound, "Resource not found.", "synqlien.not_found");
    private static SynqLienUserManagementResult<T> Forbidden<T>() => SynqLienUserManagementResult<T>.Fail(SynqLienUserManagementError.Forbidden, "You do not have permission to manage SynqLien users in this organization.", "synqlien.forbidden");
    private static SynqLienUserManagementResult<T> Validation<T>(string message) => SynqLienUserManagementResult<T>.Fail(SynqLienUserManagementError.Validation, message, "synqlien.validation");
    private static SynqLienUserManagementResult<T> Conflict<T>(string message, string? code = null) => SynqLienUserManagementResult<T>.Fail(SynqLienUserManagementError.Conflict, message, code ?? "synqlien.conflict");
}
