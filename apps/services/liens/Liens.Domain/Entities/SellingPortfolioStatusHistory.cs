using BuildingBlocks.Domain;

namespace Liens.Domain.Entities;

public class SellingPortfolioStatusHistory : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PortfolioId { get; private set; }
    public string? FromStatus { get; private set; }
    public string ToStatus { get; private set; } = string.Empty;
    public Guid ChangedByUserId { get; private set; }
    public DateTime ChangedAtUtc { get; private set; }
    public string? Notes { get; private set; }

    private SellingPortfolioStatusHistory() { }

    public static SellingPortfolioStatusHistory Create(
        Guid tenantId,
        Guid portfolioId,
        string? fromStatus,
        string toStatus,
        Guid changedByUserId,
        string? notes = null)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (portfolioId == Guid.Empty) throw new ArgumentException("PortfolioId is required.", nameof(portfolioId));
        if (changedByUserId == Guid.Empty) throw new ArgumentException("ChangedByUserId is required.", nameof(changedByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(toStatus);

        var now = DateTime.UtcNow;
        return new SellingPortfolioStatusHistory
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            PortfolioId = portfolioId,
            FromStatus = fromStatus,
            ToStatus = toStatus.Trim(),
            ChangedByUserId = changedByUserId,
            ChangedAtUtc = now,
            Notes = notes?.Trim(),
            CreatedByUserId = changedByUserId,
            UpdatedByUserId = changedByUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }
}
