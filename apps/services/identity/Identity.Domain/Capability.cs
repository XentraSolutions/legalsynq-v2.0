namespace Identity.Domain;

public class Capability
{
    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public Product Product { get; private set; } = null!;
    public ICollection<RoleCapability> RoleCapabilities { get; private set; } = [];
    public ICollection<RoleCapabilityAssignment> RoleCapabilityAssignments { get; private set; } = [];

    private Capability() { }

    public static Capability Create(
        Guid productId,
        string code,
        string name,
        string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Capability
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Code = code.Trim().ToLowerInvariant(),
            Name = name.Trim(),
            Description = description?.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
