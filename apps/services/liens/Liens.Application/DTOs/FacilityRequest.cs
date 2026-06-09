namespace Liens.Application.DTOs;

public sealed class CreateFacilityRequest
{
    public string  Name              { get; init; } = string.Empty;
    public string? Code              { get; init; }
    public string? ExternalReference { get; init; }
    public string? AddressLine1      { get; init; }
    public string? AddressLine2      { get; init; }
    public string? City              { get; init; }
    public string? State             { get; init; }
    public string? PostalCode        { get; init; }
    public string? Phone             { get; init; }
    public string? Email             { get; init; }
    public string? Fax               { get; init; }
}

public sealed class UpdateFacilityRequest
{
    public string  Name              { get; init; } = string.Empty;
    public string? Code              { get; init; }
    public string? ExternalReference { get; init; }
    public string? AddressLine1      { get; init; }
    public string? AddressLine2      { get; init; }
    public string? City              { get; init; }
    public string? State             { get; init; }
    public string? PostalCode        { get; init; }
    public string? Phone             { get; init; }
    public string? Email             { get; init; }
    public string? Fax               { get; init; }
}
