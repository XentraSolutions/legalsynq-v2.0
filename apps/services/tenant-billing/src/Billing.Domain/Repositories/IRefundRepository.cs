using Billing.Domain.Entities;

namespace Billing.Domain.Repositories;

public interface IRefundRepository
{
    Task<Refund> AddAsync(Refund refund, CancellationToken ct = default);
    Task<Refund?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Refund>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Refund>> GetByInvoiceAsync(Guid invoiceId, CancellationToken ct = default);
}
