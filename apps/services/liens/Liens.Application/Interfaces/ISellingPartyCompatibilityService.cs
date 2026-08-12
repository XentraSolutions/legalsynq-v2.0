namespace Liens.Application.Interfaces;

public sealed record SellingPartyCanonicalReference(Guid? CompanyId, Guid? CompanyContactPersonId);

public interface ISellingPartyCompatibilityService
{
    bool DualWriteEnabled { get; }
    bool ShadowReadEnabled { get; }
    bool CanonicalReadEnabled { get; }

    Task<SellingPartyCanonicalReference?> ResolveAsync(
        Guid tenantId, string scopeKind, Guid scopeId, string aliasNamespace,
        string workflowProvenance, Guid externalId, CancellationToken ct = default);

    Task EnsureCompanyAliasAsync(
        Guid tenantId, string scopeKind, Guid scopeId, string aliasNamespace,
        string workflowProvenance, Guid externalId, Guid companyId,
        bool isPreferred, Guid actorUserId, CancellationToken ct = default);

    Task EnsureContactPersonAliasAsync(
        Guid tenantId, string scopeKind, Guid scopeId, string aliasNamespace,
        string workflowProvenance, Guid externalId, Guid companyContactPersonId,
        bool isPreferred, Guid actorUserId, CancellationToken ct = default);

    Task<int> RunBackfillBatchAsync(Guid tenantId, Guid actorUserId, CancellationToken ct = default);
}
