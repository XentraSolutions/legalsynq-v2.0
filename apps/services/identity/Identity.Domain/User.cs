namespace Identity.Domain;

public class User
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public Tenant Tenant { get; private set; } = null!;
    public ICollection<UserOrganizationMembership> OrganizationMemberships { get; private set; } = [];

    // Phase 4+: scoped role assignments — sole authoritative role source (Phase G).
    public ICollection<ScopedRoleAssignment> ScopedRoleAssignments { get; private set; } = [];

    private User() { }

    /// <summary>
    /// Marks the user as inactive. Idempotent — safe to call when already inactive.
    /// Returns true if state changed, false if already inactive.
    /// </summary>
    public bool Deactivate()
    {
        if (!IsActive) return false;
        IsActive     = false;
        UpdatedAtUtc = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// Marks the user as active. Idempotent — safe to call when already active.
    /// Returns true if state changed, false if already active.
    /// </summary>
    public bool Activate()
    {
        if (IsActive) return false;
        IsActive     = true;
        UpdatedAtUtc = DateTime.UtcNow;
        return true;
    }

    public static User Create(
        Guid tenantId,
        string email,
        string passwordHash,
        string firstName,
        string lastName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);

        var now = DateTime.UtcNow;
        return new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = email.ToLowerInvariant().Trim(),
            PasswordHash = passwordHash,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }
}
