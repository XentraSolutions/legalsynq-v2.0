namespace Liens.Application.DTOs;

public sealed class LienReductionResponse
{
    public Guid     Id            { get; init; }
    public Guid     TenantId      { get; init; }
    public Guid     CaseId        { get; init; }
    public Guid     LienId        { get; init; }
    public DateOnly ReductionDate { get; init; }
    public decimal  Amount        { get; init; }
    public string?  Note          { get; init; }
    public DateTime CreatedAtUtc  { get; init; }
    public DateTime UpdatedAtUtc  { get; init; }
    public Guid?    CreatedByUserId { get; init; }
    public Guid?    UpdatedByUserId { get; init; }
}

public sealed class CreateLienReductionRequest
{
    public Guid     CaseId        { get; init; }
    public Guid     LienId        { get; init; }
    public DateOnly ReductionDate { get; init; }
    public decimal  Amount        { get; init; }
    public string?  Note          { get; init; }
}

public sealed class UpdateLienReductionRequest
{
    public DateOnly ReductionDate { get; init; }
    public decimal  Amount        { get; init; }
    public string?  Note          { get; init; }
}
