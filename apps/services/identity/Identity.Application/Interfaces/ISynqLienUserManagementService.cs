using Identity.Application.DTOs;

namespace Identity.Application.Interfaces;

public interface ISynqLienUserManagementService
{
    Task<SynqLienUserManagementResult<SynqLienPagedUsers>> ListAsync(SynqLienManagementScope scope, SynqLienUserListQuery query, CancellationToken ct);
    Task<SynqLienUserManagementResult<SynqLienUserListItem>> GetAsync(SynqLienManagementScope scope, Guid userId, CancellationToken ct);
    Task<SynqLienUserManagementResult<SynqLienUserManagementOptions>> GetOptionsAsync(SynqLienManagementScope scope, CancellationToken ct);
    Task<SynqLienUserManagementResult<SynqLienInvitationMutation>> InviteAsync(SynqLienManagementScope scope, SynqLienInviteRequest request, CancellationToken ct);
    Task<SynqLienUserManagementResult<SynqLienInvitationMutation>> ResendInvitationAsync(SynqLienManagementScope scope, Guid userId, CancellationToken ct);
    Task<SynqLienUserManagementResult<bool>> CancelInvitationAsync(SynqLienManagementScope scope, Guid userId, CancellationToken ct);
    Task<SynqLienUserManagementResult<SynqLienUserListItem>> UpdateOrganizationProfileAsync(SynqLienManagementScope scope, Guid userId, SynqLienOrganizationProfileRequest request, CancellationToken ct);
    Task<SynqLienUserManagementResult<SynqLienUserListItem>> ReplaceRoleAsync(SynqLienManagementScope scope, Guid userId, SynqLienRoleRequest request, CancellationToken ct);
    Task<SynqLienUserManagementResult<bool>> SetProductAccessAsync(SynqLienManagementScope scope, Guid userId, bool activate, CancellationToken ct);
    Task<SynqLienUserManagementResult<IReadOnlyList<SynqLienAccessRoleDto>>> ListRolesAsync(SynqLienManagementScope scope, CancellationToken ct);
    Task<SynqLienUserManagementResult<SynqLienAccessRoleDto>> CreateRoleAsync(SynqLienManagementScope scope, SynqLienAccessRoleRequest request, CancellationToken ct);
    Task<SynqLienUserManagementResult<SynqLienAccessRoleDto>> UpdateRoleAsync(SynqLienManagementScope scope, Guid roleId, SynqLienAccessRoleRequest request, CancellationToken ct);
    Task<SynqLienUserManagementResult<bool>> DeleteRoleAsync(SynqLienManagementScope scope, Guid roleId, CancellationToken ct);
}
