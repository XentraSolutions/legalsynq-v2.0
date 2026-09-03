using System.Text.Json.Serialization;

namespace Identity.Application.DTOs;

public sealed record SynqLienManagementScope(Guid TenantId, Guid OrganizationId, Guid ActorUserId, string? CorrelationId);

public sealed record SynqLienUserListQuery(
    string? Search, string? Status, Guid? RoleId, string? Department, int Page, int PageSize, string? Sort);

public sealed record SynqLienUserListItem(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string? Department,
    string? JobTitle,
    Guid? RoleId,
    string? RoleName,
    string Status,
    DateTime? LastLoginAtUtc);

public sealed record SynqLienPagedUsers(IReadOnlyList<SynqLienUserListItem> Items, int Page, int PageSize, int TotalCount);
public sealed record SynqLienPermissionOption(string Code, string Name, string? Description, string? Category);
public sealed record SynqLienAccessRoleDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystem,
    bool IsActive,
    int AssignedUserCount,
    IReadOnlyList<string> Permissions);

public sealed record SynqLienUserManagementOptions(
    IReadOnlyList<SynqLienAccessRoleDto> Roles,
    IReadOnlyList<SynqLienPermissionOption> Permissions,
    IReadOnlyList<string> Departments,
    IReadOnlyList<string> Statuses,
    IReadOnlyList<string> Sorts);

public sealed record SynqLienInviteRequest(
    string Email, string FirstName, string LastName, string? Department, string? JobTitle, Guid RoleId);
public sealed record SynqLienOrganizationProfileRequest(string? Department, string? JobTitle);
public sealed record SynqLienRoleRequest(Guid RoleId);
public sealed record SynqLienAccessRoleRequest(string Name, string? Description, IReadOnlyList<string> Permissions);

public sealed record SynqLienInvitationMutation(
    Guid UserId,
    Guid? InvitationId,
    string Email,
    string DisplayName,
    string Outcome,
    [property: JsonIgnore] string? RawToken);

public enum SynqLienUserManagementError { None, Validation, Forbidden, NotFound, Conflict }

public sealed record SynqLienUserManagementResult<T>(
    T? Value,
    SynqLienUserManagementError Error = SynqLienUserManagementError.None,
    string? Message = null,
    string? Code = null)
{
    public bool IsSuccess => Error == SynqLienUserManagementError.None;
    public static SynqLienUserManagementResult<T> Success(T value) => new(value);
    public static SynqLienUserManagementResult<T> Fail(SynqLienUserManagementError error, string message, string? code = null) =>
        new(default, error, message, code);
}
