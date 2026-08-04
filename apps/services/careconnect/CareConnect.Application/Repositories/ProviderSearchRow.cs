using CareConnect.Domain;

namespace CareConnect.Application.Repositories;

public sealed record ProviderSearchRow(Provider Provider, double? DistanceMiles, Facility? Facility = null);
