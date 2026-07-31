using Liens.Domain.Entities;

namespace Liens.Application.Interfaces;

public interface ISellerOrganizationDisplayResolver
{
    Task<SellerOrganizationDisplay> ResolveAsync(
        Guid tenantId,
        Guid sellerOrgId,
        IReadOnlyList<Contact> sellerContacts,
        string? fallbackEmail = null,
        CancellationToken ct = default);
}

public sealed record SellerOrganizationDisplay(
    string Name,
    string Company,
    string? Email);
