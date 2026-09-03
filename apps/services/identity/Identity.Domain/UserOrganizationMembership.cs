namespace Identity.Domain;

public class UserOrganizationMembership
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string MemberRole { get; private set; } = string.Empty;
    public bool IsPrimary { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime JoinedAtUtc { get; private set; }
    public Guid? GrantedByUserId { get; private set; }
    public string? Department { get; private set; }
    public string? JobTitle { get; private set; }

    public User User { get; private set; } = null!;
    public Organization Organization { get; private set; } = null!;

    private UserOrganizationMembership() { }

    public static UserOrganizationMembership Create(
        Guid userId,
        Guid organizationId,
        string memberRole,
        Guid? grantedByUserId = null,
        string? department = null,
        string? jobTitle = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(memberRole);

        if (!Identity.Domain.MemberRole.IsValid(memberRole))
            throw new ArgumentException($"Invalid MemberRole: {memberRole}", nameof(memberRole));

        return new UserOrganizationMembership
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            OrganizationId = organizationId,
            MemberRole = memberRole,
            IsActive = true,
            JoinedAtUtc = DateTime.UtcNow,
            GrantedByUserId = grantedByUserId,
            Department = NormalizeOptional(department),
            JobTitle = NormalizeOptional(jobTitle)
        };
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    public void UpdateOrganizationProfile(string? department, string? jobTitle)
    {
        Department = NormalizeOptional(department);
        JobTitle = NormalizeOptional(jobTitle);
    }

    public void SetPrimary() => IsPrimary = true;

    public void ClearPrimary() => IsPrimary = false;

    public void UpdateRole(string memberRole)
    {
        if (!Identity.Domain.MemberRole.IsValid(memberRole))
            throw new ArgumentException($"Invalid MemberRole: {memberRole}", nameof(memberRole));

        MemberRole = memberRole;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
