using Liens.Domain.Entities;

namespace Liens.Application.Interfaces;

public interface ISellerOrganizationDisplayResolver
{
    Task<SellerOrganizationDisplay> ResolveAsync(
        Guid tenantId,
        Guid sellerOrgId,
        IReadOnlyList<Contact> sellerContacts,
        Guid? sellerUserId = null,
        string? fallbackEmail = null,
        bool includeIdentityOwnerEmailFallback = false,
        CancellationToken ct = default);

    Task<SellerOrganizationDisplay> ResolveAsync(
        Guid tenantId,
        Guid sellerOrgId,
        IReadOnlyList<CompanyContactPerson> sellerContacts,
        Guid? sellerUserId = null,
        string? fallbackEmail = null,
        bool includeIdentityOwnerEmailFallback = false,
        CancellationToken ct = default);
}

public sealed record SellerOrganizationDisplay(
    string Name,
    string Company,
    string? Email);
