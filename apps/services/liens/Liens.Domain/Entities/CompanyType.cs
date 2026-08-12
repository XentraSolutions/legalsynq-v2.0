using BuildingBlocks.Domain;

namespace Liens.Domain.Entities;

public class CompanyType : AuditableEntity
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }

    private CompanyType() { }
}
