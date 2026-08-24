using BuildingBlocks.Domain;

namespace Liens.Domain.Entities;

public sealed class SellingPartyAlias : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string ScopeKind { get; private set; } = string.Empty;
    public Guid ScopeId { get; private set; }
    public string Namespace { get; private set; } = string.Empty;
    public string WorkflowProvenance { get; private set; } = string.Empty;
    public Guid ExternalId { get; private set; }
    public Guid? CompanyId { get; private set; }
    public Guid? CompanyContactPersonId { get; private set; }
    public bool IsPreferred { get; private set; }
    public Guid? PreferredCompanyKey { get; private set; }
    public Guid? PreferredContactPersonKey { get; private set; }

    public Company? Company { get; private set; }
    public CompanyContactPerson? CompanyContactPerson { get; private set; }

    private SellingPartyAlias() { }

    public static SellingPartyAlias CreateForCompany(
        Guid tenantId, string scopeKind, Guid scopeId, string aliasNamespace,
        string workflowProvenance, Guid externalId, Guid companyId,
        bool isPreferred, Guid createdByUserId)
        => Create(tenantId, scopeKind, scopeId, aliasNamespace, workflowProvenance,
            externalId, companyId, null, isPreferred, createdByUserId);

    public static SellingPartyAlias CreateForContactPerson(
        Guid tenantId, string scopeKind, Guid scopeId, string aliasNamespace,
        string workflowProvenance, Guid externalId, Guid companyContactPersonId,
        bool isPreferred, Guid createdByUserId)
        => Create(tenantId, scopeKind, scopeId, aliasNamespace, workflowProvenance,
            externalId, null, companyContactPersonId, isPreferred, createdByUserId);

    private static SellingPartyAlias Create(
        Guid tenantId, string scopeKind, Guid scopeId, string aliasNamespace,
        string workflowProvenance, Guid externalId, Guid? companyId,
        Guid? companyContactPersonId, bool isPreferred, Guid createdByUserId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (scopeId == Guid.Empty) throw new ArgumentException("ScopeId is required.", nameof(scopeId));
        if (externalId == Guid.Empty) throw new ArgumentException("ExternalId is required.", nameof(externalId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));
        if (companyId.HasValue == companyContactPersonId.HasValue)
            throw new ArgumentException("Exactly one canonical alias target is required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(aliasNamespace);
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowProvenance);

        var now = DateTime.UtcNow;
        return new SellingPartyAlias
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            ScopeKind = scopeKind.Trim(),
            ScopeId = scopeId,
            Namespace = aliasNamespace.Trim(),
            WorkflowProvenance = workflowProvenance.Trim(),
            ExternalId = externalId,
            CompanyId = companyId,
            CompanyContactPersonId = companyContactPersonId,
            IsPreferred = isPreferred,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }
}
