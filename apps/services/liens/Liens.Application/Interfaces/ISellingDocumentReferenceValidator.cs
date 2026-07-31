namespace Liens.Application.Interfaces;

/// <summary>
/// Validates a pre-existing Documents-service resource before Selling stores a
/// reference to it. The Documents service remains the authority for ownership.
/// </summary>
public interface ISellingDocumentReferenceValidator
{
    Task<bool> IsAccessibleAsync(
        Guid tenantId,
        Guid sellerOrgId,
        Guid actingUserId,
        Guid lienId,
        Guid? caseId,
        Guid documentId,
        CancellationToken ct = default);
}
