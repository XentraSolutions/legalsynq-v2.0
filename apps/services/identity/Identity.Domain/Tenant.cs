namespace Identity.Domain;

public class Tenant
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public ICollection<User> Users { get; private set; } = [];
    public ICollection<Role> Roles { get; private set; } = [];
    public ICollection<TenantProduct> TenantProducts { get; private set; } = [];
    public ICollection<Organization> Organizations { get; private set; } = [];

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
}
