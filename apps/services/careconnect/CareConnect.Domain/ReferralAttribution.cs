using BuildingBlocks.Domain;

namespace CareConnect.Domain;

/// <summary>
/// A tenant-scoped, configurable label identifying who or what originated a referral
/// (a representative, a campaign, a partner). Generic by design — Cam Perry is seed
/// data, never a code path.
/// </summary>
public class ReferralAttribution : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }

    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public int? DisplayOrder { get; private set; }

    /// <summary>Not a mapped column — a convenience accessor over FirstName/LastName for
    /// call sites that just need a single name string (dropdown labels, audit descriptions).</summary>
    public string FullName => $"{FirstName} {LastName}".Trim();

    private ReferralAttribution() { }

    public static string NormalizeCode(string code) =>
        (code ?? throw new ArgumentNullException(nameof(code))).Trim().ToUpperInvariant();

    public static ReferralAttribution Create(
        Guid    tenantId,
        string  firstName,
        string  lastName,
        string  code,
        string? description,
        bool    isActive,
        int?    displayOrder,
        Guid?   createdByUserId)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            throw new ArgumentException("FirstName is required.", nameof(firstName));
        if (string.IsNullOrWhiteSpace(lastName))
            throw new ArgumentException("LastName is required.", nameof(lastName));

        var normalizedCode = NormalizeCode(code);
        if (string.IsNullOrWhiteSpace(normalizedCode))
            throw new ArgumentException("Code is required.", nameof(code));

        var now = DateTime.UtcNow;
        return new ReferralAttribution
        {
            Id              = Guid.CreateVersion7(),
            TenantId        = tenantId,
            FirstName       = firstName.Trim(),
            LastName        = lastName.Trim(),
            Code            = normalizedCode,
            Description     = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            IsActive        = isActive,
            DisplayOrder    = displayOrder,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc    = now,
            UpdatedAtUtc    = now
        };
    }

    public void Update(
        string  firstName,
        string  lastName,
        string? description,
        int?    displayOrder,
        Guid?   updatedByUserId)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            throw new ArgumentException("FirstName is required.", nameof(firstName));
        if (string.IsNullOrWhiteSpace(lastName))
            throw new ArgumentException("LastName is required.", nameof(lastName));

        FirstName       = firstName.Trim();
        LastName        = lastName.Trim();
        Description     = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        DisplayOrder    = displayOrder;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    public void SetActive(bool isActive, Guid? updatedByUserId)
    {
        IsActive        = isActive;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }
}
