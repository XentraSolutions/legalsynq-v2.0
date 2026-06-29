using BuildingBlocks.Domain;

namespace Liens.Domain.Entities;

public class BatchTemplate : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid? TenantId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string ColumnsHeader { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public bool IsSystem { get; private set; }

    private BatchTemplate() { }

    public static BatchTemplate Create(
        string code,
        string name,
        string columnsHeader,
        Guid createdByUserId,
        Guid? tenantId = null,
        bool isSystem = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(columnsHeader);
        if (createdByUserId == Guid.Empty) throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));

        var now = DateTime.UtcNow;
        return new BatchTemplate
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            Code = code.Trim(),
            Name = name.Trim(),
            ColumnsHeader = columnsHeader.Trim(),
            IsActive = true,
            IsSystem = isSystem,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }
}
