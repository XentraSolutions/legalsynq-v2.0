namespace CareConnect.Application.DTOs;

public class GetProvidersQuery
{
    public string? Name              { get; init; }
    public string? CategoryCode      { get; init; }
    public string? City              { get; init; }
    public string? State             { get; init; }
    public bool?   AcceptingReferrals { get; init; }
    public bool?   IsActive          { get; init; }
    public int     Page              { get; init; } = 1;
    public int     PageSize          { get; init; } = 20;

    public double? Latitude          { get; init; }
    public double? Longitude         { get; init; }
    public double? RadiusMiles       { get; init; }
}
