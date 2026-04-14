using Liens.Domain.Entities;

namespace Liens.Application.Interfaces;

public interface IBillOfSaleDocumentService
{
    Task<Guid?> GenerateAndStoreAsync(BillOfSale billOfSale, Guid actingUserId, CancellationToken ct = default);
}
