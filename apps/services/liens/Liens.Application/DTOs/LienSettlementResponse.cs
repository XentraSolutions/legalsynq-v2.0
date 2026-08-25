using System.Text.Json.Serialization;

namespace Liens.Application.DTOs;

public sealed class LienSettlementResponse
{
    public Guid    Id            { get; init; }
    public Guid    TenantId      { get; init; }
    public Guid    CaseId        { get; init; }
    public Guid    LienId        { get; init; }
    public int     PaymentNumber { get; init; }
    public decimal Amount        { get; init; }
    public DateOnly? SettlementDate { get; init; }
    public string  Status        { get; init; } = "Pending";
    public string? Note          { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
    public Guid?   CreatedByUserId { get; init; }
    public Guid?   UpdatedByUserId { get; init; }
}

public sealed class CreateLienSettlementRequest
{
    public Guid    CaseId        { get; init; }
    public Guid    LienId        { get; init; }
    public int     PaymentNumber { get; init; }
    public decimal Amount        { get; init; }
    public DateOnly? SettlementDate { get; init; }
    public string? Status        { get; init; }
    public string? Note          { get; init; }
}

public sealed class UpdateLienSettlementRequest
{
    public int     PaymentNumber { get; init; }
    public decimal Amount        { get; init; }
    public DateOnly? SettlementDate { get; init; }
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
    public string?  PaymentMethod { get; init; }
    public string?  SettlementTypeId { get; init; }
    public string?  SettlementStatusId { get; init; }
    public decimal? NetProfit     { get; init; }
    public DateTime CreatedAtUtc  { get; init; }
    public DateTime UpdatedAtUtc  { get; init; }
    public Guid?    CreatedByUserId { get; init; }
    public Guid?    UpdatedByUserId { get; init; }
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

    // Aliases used by the current tenant-portal payment form.
    public string?  PaymentMethod   { get; init; }
    public string?  ReferenceNumber { get; init; }
    public string?  Notes           { get; init; }
    public string?  SettlementType  { get; init; }
    public string?  SettlementStatus { get; init; }
    public string?  LienStatus      { get; init; }
    public string?  Type            { get; init; }
    public string?  Status          { get; init; }
    public decimal? NetProfit       { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class UpdateSettlementPaymentDetailRequest
{
    public required decimal  Amount           { get; init; }
    public required DateOnly PaymentDate      { get; init; }
    public string? PaymentMethod    { get; init; }
    public string? ReferenceNumber  { get; init; }
    public string? Notes            { get; init; }
    public string? SettlementType   { get; init; }
    public string? SettlementStatus { get; init; }
    public string? LienStatus       { get; init; }
}
