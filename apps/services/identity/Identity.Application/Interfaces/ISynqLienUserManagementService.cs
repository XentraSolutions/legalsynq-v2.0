namespace Identity.Application.Interfaces;

public interface ISynqLienUserManagementService
{
    Task<SynqLienPagedResult<SynqLienUserSummary>> ListUsersAsync(
        Guid tenantId, Guid actorUserId, string? search, string? status, string? roleCode,
        int page, int pageSize, CancellationToken ct = default);

    Task<SynqLienUserDetail> GetUserAsync(
        Guid tenantId, Guid actorUserId, Guid userId, CancellationToken ct = default);

    Task<IReadOnlyList<SynqLienAssignableRole>> ListRolesAsync(
        Guid tenantId, Guid actorUserId, CancellationToken ct = default);

    Task<SynqLienPagedResult<SynqLienInvitationSummary>> ListInvitationsAsync(
        Guid tenantId, Guid actorUserId, string? status, int page, int pageSize,
        CancellationToken ct = default);

    Task<SynqLienInviteResult> InviteAsync(
        Guid tenantId, Guid actorUserId, SynqLienInviteCommand command,
        CancellationToken ct = default);

    Task<SynqLienInviteResult> ResendInvitationAsync(
        Guid tenantId, Guid actorUserId, Guid invitationId,
        CancellationToken ct = default);

    Task CancelInvitationAsync(
        Guid tenantId, Guid actorUserId, Guid invitationId,
        CancellationToken ct = default);

    Task<SynqLienUserDetail> SetAccessAsync(
        Guid tenantId, Guid actorUserId, Guid userId, bool enabled, int expectedAccessVersion,
        CancellationToken ct = default);

    Task<SynqLienUserDetail> ReplaceRolesAsync(
        Guid tenantId, Guid actorUserId, Guid userId, IReadOnlyCollection<string> roleCodes,
        int expectedAccessVersion, CancellationToken ct = default);

    Task ApplyInvitationGrantsAsync(Guid invitationId, CancellationToken ct = default);
}

public sealed record SynqLienPagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record SynqLienRoleAssignment(
    Guid? AssignmentId,
    string Code,
    string Name,
    string Source,
    bool IsInherited);

public sealed record SynqLienInvitationState(
    Guid Id,
    string Status,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    DateTime? AcceptedAtUtc,
    DateTime? RevokedAtUtc);

public sealed record SynqLienUserSummary(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string? Phone,
    string AccountStatus,
    string LiensAccessStatus,
    IReadOnlyList<SynqLienRoleAssignment> Roles,
    SynqLienInvitationState? Invitation,
    int AccessVersion);

public sealed record SynqLienUserDetail(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string? Phone,
    string AccountStatus,
    string LiensAccessStatus,
    bool HasInheritedAccess,
    IReadOnlyList<SynqLienRoleAssignment> Roles,
    SynqLienInvitationState? Invitation,
    int AccessVersion);

public sealed record SynqLienAssignableRole(string Code, string Name, string? Description);

public sealed record SynqLienInvitationSummary(
    Guid Id,
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string Status,
    IReadOnlyList<string> RoleCodes,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    DateTime? AcceptedAtUtc,
    DateTime? RevokedAtUtc);

public sealed record SynqLienInviteCommand(
    string Email,
    string FirstName,
    string LastName,
    string? Phone,
    IReadOnlyCollection<string> RoleCodes);

public sealed record SynqLienInviteResult(
    Guid UserId,
    Guid? InvitationId,
    string Email,
    bool IsNewUser,
    bool AccessGrantedImmediately,
    string? RawToken);

public sealed class SynqLienUserManagementException : Exception
{
    public SynqLienUserManagementException(int statusCode, string code, string message)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }

    public int StatusCode { get; }
    public string Code { get; }
}
