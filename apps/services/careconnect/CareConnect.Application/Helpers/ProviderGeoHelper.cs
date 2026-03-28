namespace CareConnect.Application.Helpers;

public static class ProviderGeoHelper
{
    private const double MilesPerDegreeLat = 69.0;
    private const double MaxRadiusMiles    = 100.0;

    public static (double MinLat, double MaxLat, double MinLon, double MaxLon)
        BoundingBox(double centerLat, double centerLon, double radiusMiles)
    {
        var deltaLat = radiusMiles / MilesPerDegreeLat;
        var deltaLon = radiusMiles / (MilesPerDegreeLat * Math.Cos(centerLat * Math.PI / 180.0));

        return (
            centerLat - deltaLat,
            centerLat + deltaLat,
            centerLon - deltaLon,
            centerLon + deltaLon
        );
    }

    public static void ValidateGeoSearch(double? latitude, double? longitude, double? radiusMiles,
        Dictionary<string, string[]> errors)
    {
        if (latitude.HasValue || longitude.HasValue || radiusMiles.HasValue)
        {
            if (!latitude.HasValue)
                errors["latitude"] = new[] { "Latitude is required when performing a geo search." };
            else if (latitude < -90 || latitude > 90)
                errors["latitude"] = new[] { "Latitude must be between -90 and 90." };

            if (!longitude.HasValue)
                errors["longitude"] = new[] { "Longitude is required when performing a geo search." };
            else if (longitude < -180 || longitude > 180)
                errors["longitude"] = new[] { "Longitude must be between -180 and 180." };

            if (!radiusMiles.HasValue)
                errors["radiusMiles"] = new[] { "RadiusMiles is required when performing a geo search." };
            else if (radiusMiles <= 0)
                errors["radiusMiles"] = new[] { "RadiusMiles must be greater than 0." };
            else if (radiusMiles > MaxRadiusMiles)
                errors["radiusMiles"] = new[] { $"RadiusMiles must not exceed {MaxRadiusMiles}." };
        }
    }

    public static void ValidateGeoFields(double? latitude, double? longitude, string? geoPointSource,
        Dictionary<string, string[]> errors)
    {
        if (latitude.HasValue && (latitude < -90 || latitude > 90))
            errors["latitude"] = new[] { "Latitude must be between -90 and 90." };

        if (longitude.HasValue && (longitude < -180 || longitude > 180))
            errors["longitude"] = new[] { "Longitude must be between -180 and 180." };

        if (latitude.HasValue && !longitude.HasValue)
            errors["longitude"] = new[] { "Longitude is required when Latitude is provided." };

        if (!latitude.HasValue && longitude.HasValue)
            errors["latitude"] = new[] { "Latitude is required when Longitude is provided." };

        if (geoPointSource is not null && !Domain.GeoPointSource.IsValid(geoPointSource))
            errors["geoPointSource"] = new[]
            {
                $"'{geoPointSource}' is not a valid geo point source. " +
                $"Allowed: {string.Join(", ", Domain.GeoPointSource.All)}."
            };
    }
}
