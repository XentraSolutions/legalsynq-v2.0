using BuildingBlocks.Domain;

namespace Liens.Domain.Entities;

public class ContactPersonType : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid CompanyTypeId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }

    public CompanyType? CompanyType { get; private set; }

    private ContactPersonType() { }
}
