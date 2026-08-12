namespace CareConnect.Domain;

public static class GeoPointSource
{
    public const string Manual       = "Manual";
    public const string Geocoded     = "Geocoded";
    public const string Imported     = "Imported";
    public const string CityCentroid = "CityCentroid";

    private static readonly IReadOnlySet<string> AllValues =
        new HashSet<string>(StringComparer.Ordinal) { Manual, Geocoded, Imported, CityCentroid };

    public static bool IsValid(string value) => AllValues.Contains(value);

    public static IReadOnlySet<string> All => AllValues;
}
