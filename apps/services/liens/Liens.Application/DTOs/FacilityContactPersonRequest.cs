namespace Liens.Application.DTOs;

public sealed class FacilityContactPersonResponse
{
    public Guid    Id         { get; init; }
    public Guid    FacilityId { get; init; }
    public string  FirstName  { get; init; } = string.Empty;
    public string  LastName   { get; init; } = string.Empty;
    public string? Position   { get; init; }
    public string? Email      { get; init; }
    public string? Phone      { get; init; }
    public bool    IsActive   { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}

public sealed class CreateFacilityContactPersonRequest
{
    public string  FirstName { get; init; } = string.Empty;
    public string  LastName  { get; init; } = string.Empty;
    public string? Position  { get; init; }
    public string? Email     { get; init; }
    public string? Phone     { get; init; }
}

public sealed class UpdateFacilityContactPersonRequest
{
    public string  FirstName { get; init; } = string.Empty;
    public string  LastName  { get; init; } = string.Empty;
    public string? Position  { get; init; }
    public string? Email     { get; init; }
    public string? Phone     { get; init; }
}
