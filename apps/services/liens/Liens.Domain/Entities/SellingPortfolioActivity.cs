using BuildingBlocks.Domain;

namespace Liens.Domain.Entities;

public class SellingPortfolioActivity : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PortfolioId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public string? EntityId { get; private set; }
    public Guid ActorUserId { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public string Summary { get; private set; } = string.Empty;
    public string? MetadataJson { get; private set; }

    private SellingPortfolioActivity() { }

    public static SellingPortfolioActivity Create(
        Guid tenantId,
        Guid portfolioId,
        string action,
        string entityType,
        Guid actorUserId,
        string summary,
        string? entityId = null,
        string? metadataJson = null)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (portfolioId == Guid.Empty) throw new ArgumentException("PortfolioId is required.", nameof(portfolioId));
        if (actorUserId == Guid.Empty) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);

        var now = DateTime.UtcNow;
        return new SellingPortfolioActivity
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            PortfolioId = portfolioId,
            Action = action.Trim(),
            EntityType = entityType.Trim(),
            EntityId = entityId?.Trim(),
            ActorUserId = actorUserId,
            OccurredAtUtc = now,
            Summary = summary.Trim(),
            MetadataJson = metadataJson?.Trim(),
            CreatedByUserId = actorUserId,
            UpdatedByUserId = actorUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }
}
