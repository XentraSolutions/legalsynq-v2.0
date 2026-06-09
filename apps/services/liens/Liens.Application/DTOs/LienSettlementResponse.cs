namespace Liens.Application.DTOs;

public sealed class LienSettlementResponse
{
    public Guid    Id            { get; init; }
    public Guid    TenantId      { get; init; }
    public Guid    CaseId        { get; init; }
    public Guid    LienId        { get; init; }
    public int     PaymentNumber { get; init; }
    public decimal Amount        { get; init; }
    public string  Status        { get; init; } = "Pending";
    public string? Note          { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}

public sealed class CreateLienSettlementRequest
{
    public Guid    CaseId        { get; init; }
    public Guid    LienId        { get; init; }
    public int     PaymentNumber { get; init; }
    public decimal Amount        { get; init; }
    public string? Status        { get; init; }
    public string? Note          { get; init; }
}

public sealed class UpdateLienSettlementRequest
{
    public int     PaymentNumber { get; init; }
    public decimal Amount        { get; init; }
    public string? Status        { get; init; }
    public string? Note          { get; init; }
}

public sealed class SettlementPaymentDetailResponse
{
    public Guid     Id            { get; init; }
    public Guid     TenantId      { get; init; }
    public Guid     CaseId        { get; init; }
    public Guid     LienId        { get; init; }
    public int      PaymentNumber { get; init; }
    public decimal  Amount        { get; init; }
    public DateOnly? PaymentDate  { get; init; }
    public string?  Payee         { get; init; }
    public string?  CheckNumber   { get; init; }
    public string?  Note          { get; init; }
    public DateTime CreatedAtUtc  { get; init; }
    public DateTime UpdatedAtUtc  { get; init; }
}

public sealed class CreateSettlementPaymentDetailRequest
{
    public Guid     CaseId        { get; init; }
    public Guid     LienId        { get; init; }
    public int      PaymentNumber { get; init; }
    public decimal  Amount        { get; init; }
    public DateOnly? PaymentDate  { get; init; }
    public string?  Payee         { get; init; }
    public string?  CheckNumber   { get; init; }
    public string?  Note          { get; init; }
}
