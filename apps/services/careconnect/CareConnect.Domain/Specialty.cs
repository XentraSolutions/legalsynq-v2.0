namespace CareConnect.Domain;

public class Specialty
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public List<ProviderSpecialty> ProviderSpecialties { get; private set; } = new();

    private Specialty() { }

    public static Specialty Create(string name, string code, string? description = null)
    {
        var now = DateTime.UtcNow;
        return new Specialty
        {
            Id = Guid.CreateVersion7(),
            Name = name.Trim(),
            Code = NormalizeCode(code),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    public void Update(string name, string code, string? description, bool isActive)
    {
        Name = name.Trim();
        Code = NormalizeCode(code);
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        IsActive = isActive;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public static string NormalizeCode(string code)
        => code.Trim().ToUpperInvariant().Replace(' ', '_');
}
