using BuildingBlocks.Domain;
using Liens.Domain.Enums;

namespace Liens.Domain.Entities;

public class SellingPortfolio : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid SellerOrgId { get; private set; }

    public string PortfolioNumber { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string Status { get; private set; } = SellingPortfolioStatus.Draft;

    public int LienCount { get; private set; }
    public decimal OriginalAmountTotal { get; private set; }
    public decimal? CurrentBalanceTotal { get; private set; }
    public decimal? OfferPriceTotal { get; private set; }

    public DateTime? PublishedAtUtc { get; private set; }
    public DateTime? ClosedAtUtc { get; private set; }

    public List<SellingPortfolioLien> Liens { get; private set; } = [];
    public List<SellingPortfolioBuyer> Buyers { get; private set; } = [];
    public List<SellingPortfolioStatusHistory> StatusHistory { get; private set; } = [];

    private SellingPortfolio() { }

    public static SellingPortfolio Create(
        Guid tenantId,
        Guid sellerOrgId,
        string portfolioNumber,
        string name,
        Guid createdByUserId,
        string? description = null)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (sellerOrgId == Guid.Empty) throw new ArgumentException("SellerOrgId is required.", nameof(sellerOrgId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(portfolioNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var now = DateTime.UtcNow;
        return new SellingPortfolio
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            SellerOrgId = sellerOrgId,
            PortfolioNumber = portfolioNumber.Trim(),
            Name = name.Trim(),
            Description = description?.Trim(),
            Status = SellingPortfolioStatus.Draft,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    public void Update(string name, Guid updatedByUserId, string? description = null)
    {
        if (SellingPortfolioStatus.Terminal.Contains(Status))
            throw new InvalidOperationException($"Cannot update a portfolio in terminal status '{Status}'.");

        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name.Trim();
        Description = description?.Trim();
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void AddLien(SellingPortfolioLien lien, Guid updatedByUserId)
    {
        if (Status != SellingPortfolioStatus.Draft)
            throw new InvalidOperationException($"Liens can only be added while the portfolio is in '{SellingPortfolioStatus.Draft}'.");

        if (Liens.Any(existing => existing.LienId == lien.LienId))
            throw new InvalidOperationException($"Lien '{lien.LienId}' is already in this portfolio.");

        Liens.Add(lien);
        RecalculateTotals();
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void RemoveLien(Guid lienId, Guid updatedByUserId)
    {
        if (Status != SellingPortfolioStatus.Draft)
            throw new InvalidOperationException($"Liens can only be removed while the portfolio is in '{SellingPortfolioStatus.Draft}'.");

        var lien = Liens.FirstOrDefault(item => item.LienId == lienId)
            ?? throw new InvalidOperationException($"Lien '{lienId}' is not in this portfolio.");

        Liens.Remove(lien);
        RecalculateTotals();
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void AddBuyer(Guid buyerOrgId, Guid createdByUserId)
    {
        if (buyerOrgId == Guid.Empty)
            throw new ArgumentException("BuyerOrgId is required.", nameof(buyerOrgId));

        if (SellingPortfolioStatus.Terminal.Contains(Status))
            throw new InvalidOperationException($"Cannot add buyers to a portfolio in terminal status '{Status}'.");

        if (Buyers.Any(existing => existing.BuyerOrgId == buyerOrgId))
            return;

        Buyers.Add(SellingPortfolioBuyer.Create(TenantId, Id, buyerOrgId, createdByUserId));
        UpdatedByUserId = createdByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public SellingPortfolioStatusHistory TransitionStatus(string newStatus, Guid changedByUserId, string? notes = null)
    {
        if (changedByUserId == Guid.Empty)
            throw new ArgumentException("ChangedByUserId is required.", nameof(changedByUserId));

        if (!SellingPortfolioStatus.All.Contains(newStatus))
            throw new ArgumentException($"Invalid selling portfolio status: '{newStatus}'.", nameof(newStatus));

        if (!SellingPortfolioStatus.AllowedTransitions.TryGetValue(Status, out var allowed) ||
            !allowed.Contains(newStatus))
            throw new InvalidOperationException($"Cannot transition selling portfolio from '{Status}' to '{newStatus}'.");

        var fromStatus = Status;
        Status = newStatus;
        UpdatedByUserId = changedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;

        if (newStatus == SellingPortfolioStatus.Published)
            PublishedAtUtc = DateTime.UtcNow;

        if (SellingPortfolioStatus.Terminal.Contains(newStatus))
            ClosedAtUtc = DateTime.UtcNow;

        var history = SellingPortfolioStatusHistory.Create(
            TenantId,
            Id,
            fromStatus,
            newStatus,
            changedByUserId,
            notes);

        StatusHistory.Add(history);
        return history;
    }

    public void AddInitialStatusHistory(Guid createdByUserId)
    {
        StatusHistory.Add(SellingPortfolioStatusHistory.Create(
            TenantId,
            Id,
            fromStatus: null,
            toStatus: Status,
            changedByUserId: createdByUserId,
            notes: "Portfolio created"));
    }

    private void RecalculateTotals()
    {
        LienCount = Liens.Count;
        OriginalAmountTotal = Liens.Sum(l => l.OriginalAmount);
        CurrentBalanceTotal = Liens.Count == 0 ? null : Liens.Sum(l => l.CurrentBalance ?? 0);
        OfferPriceTotal = Liens.Count == 0 ? null : Liens.Sum(l => l.OfferPrice ?? 0);
    }
}
