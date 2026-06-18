namespace Liens.Application.DTOs;

public sealed class CreateSellingPortfolioRequest
{
    public string PortfolioNumber { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public List<Guid> LienIds { get; init; } = [];
    public List<Guid> BuyerOrgIds { get; init; } = [];
}

public sealed class UpdateSellingPortfolioRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
}

public sealed class AddSellingPortfolioLiensRequest
{
    public List<Guid> LienIds { get; init; } = [];
}

public sealed class AddSellingPortfolioBuyersRequest
{
    public List<Guid> BuyerOrgIds { get; init; } = [];
}

public sealed class TransitionSellingPortfolioStatusRequest
{
    public string Status { get; init; } = string.Empty;
    public string? Notes { get; init; }
}

public sealed class SellingPortfolioResponse
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public Guid SellerOrgId { get; init; }
    public string PortfolioNumber { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Status { get; init; } = string.Empty;
    public int LienCount { get; init; }
    public decimal OriginalAmountTotal { get; init; }
    public decimal? CurrentBalanceTotal { get; init; }
    public decimal? OfferPriceTotal { get; init; }
    public DateTime? PublishedAtUtc { get; init; }
    public DateTime? ClosedAtUtc { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
    public List<SellingPortfolioLienResponse> Liens { get; init; } = [];
    public List<SellingPortfolioBuyerResponse> Buyers { get; init; } = [];
}

public sealed class SellingPortfolioLienResponse
{
    public Guid Id { get; init; }
    public Guid PortfolioId { get; init; }
    public Guid LienId { get; init; }
    public string LienNumber { get; init; } = string.Empty;
    public string? LienExternalId { get; init; }
    public Guid? CaseId { get; init; }
    public string? CaseExternalId { get; init; }
    public Guid? FacilityId { get; init; }
    public string LienType { get; init; } = string.Empty;
    public string LienLifecycleStatus { get; init; } = string.Empty;
    public decimal OriginalAmount { get; init; }
    public decimal? CurrentBalance { get; init; }
    public decimal? OfferPrice { get; init; }
    public decimal? PurchasePrice { get; init; }
    public decimal? PayoffAmount { get; init; }
    public string? SubjectFirstName { get; init; }
    public string? SubjectLastName { get; init; }
    public string? Jurisdiction { get; init; }
    public DateOnly? IncidentDate { get; init; }
    public string? Description { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}

public sealed class SellingPortfolioBuyerResponse
{
    public Guid Id { get; init; }
    public Guid PortfolioId { get; init; }
    public Guid BuyerOrgId { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class SellingPortfolioStatusHistoryResponse
{
    public Guid Id { get; init; }
    public Guid PortfolioId { get; init; }
    public string? FromStatus { get; init; }
    public string ToStatus { get; init; } = string.Empty;
    public Guid ChangedByUserId { get; init; }
    public DateTime ChangedAtUtc { get; init; }
    public string? Notes { get; init; }
}
