namespace Identity.Domain;

public class Tenant
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>
    /// Per-tenant idle session timeout in minutes.
    /// Null means "use the platform default" (30 minutes).
    /// Set via the Control Center tenant settings panel.
    /// </summary>
    public int? SessionTimeoutMinutes { get; private set; }

    /// <summary>
    /// Document ID of the tenant's logo image, stored in the Documents service.
    /// Null means no custom logo — the platform default (LegalSynq) is displayed.
    /// </summary>
    public Guid? LogoDocumentId { get; private set; }

    public ICollection<User> Users { get; private set; } = [];
    public ICollection<Role> Roles { get; private set; } = [];
    public ICollection<TenantProduct> TenantProducts { get; private set; } = [];
    public ICollection<Organization> Organizations { get; private set; } = [];
    public ICollection<TenantDomain> Domains { get; private set; } = [];

    private Tenant() { }

    public static Tenant Create(string name, string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var now = DateTime.UtcNow;
        return new Tenant
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Code = code.ToUpperInvariant().Trim(),
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void SetSessionTimeout(int? minutes)
    {
        if (minutes.HasValue && (minutes.Value < 5 || minutes.Value > 480))
            throw new ArgumentOutOfRangeException(nameof(minutes), "Session timeout must be between 5 and 480 minutes.");
        SessionTimeoutMinutes = minutes;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetLogo(Guid documentId)
    {
        LogoDocumentId = documentId;
        UpdatedAtUtc   = DateTime.UtcNow;
    }

    public void ClearLogo()
    {
        LogoDocumentId = null;
        UpdatedAtUtc   = DateTime.UtcNow;
    }
}
