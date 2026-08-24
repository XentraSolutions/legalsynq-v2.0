using BuildingBlocks.Domain;

namespace Liens.Domain.Entities;

public class ContactPersonType : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid? TenantId { get; private set; }
    public Guid? OrgId { get; private set; }
    public Guid CompanyTypeId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }

    public CompanyType? CompanyType { get; private set; }

    private ContactPersonType() { }

    public static ContactPersonType Create(
        Guid tenantId,
        Guid orgId,
        Guid companyTypeId,
        string code,
        string name,
        int sortOrder,
        Guid createdByUserId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (orgId == Guid.Empty) throw new ArgumentException("OrgId is required.", nameof(orgId));
        if (companyTypeId == Guid.Empty) throw new ArgumentException("CompanyTypeId is required.", nameof(companyTypeId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (sortOrder <= 0) throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order must be positive.");

        var now = DateTime.UtcNow;
        return new ContactPersonType
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            OrgId = orgId,
            CompanyTypeId = companyTypeId,
            Code = code.Trim(),
            Name = name.Trim(),
            SortOrder = sortOrder,
            IsActive = true,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }
}
