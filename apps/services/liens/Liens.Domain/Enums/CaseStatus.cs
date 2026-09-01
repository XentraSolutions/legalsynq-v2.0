namespace Liens.Domain.Enums;

public static class CaseStatus
{
    public const string PreDemand     = "PreDemand";
    public const string DemandSent    = "DemandSent";
    public const string InNegotiation = "InNegotiation";
    public const string LitigationOpen = "Litigation (Open)";
    public const string LitigationPending = "Litigation (Pending)";
    public const string CaseSettled   = "CaseSettled";
    public const string Closed        = "Closed";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        PreDemand, DemandSent, InNegotiation, LitigationOpen, LitigationPending, CaseSettled, Closed
    };

    /// <summary>
    /// Converts the legacy labels exposed by the case-status lookup into the
    /// canonical values stored by current cases. Litigation variants are also
    /// retained so historic records that persisted their legacy label remain
    /// discoverable.
    /// </summary>
    public static IReadOnlySet<string> ExpandFilterValues(IEnumerable<string> values)
    {
        var expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var value in values.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            var normalized = value.Trim();
            switch (normalized)
            {
                case var status when string.Equals(status, "New", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(status, "Processing", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(status, "Pre-demand", StringComparison.OrdinalIgnoreCase):
                    expanded.Add(PreDemand);
                    break;
                case var status when string.Equals(status, "Negotiations", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(status, "Litigation", StringComparison.OrdinalIgnoreCase):
                    expanded.Add(InNegotiation);
                    expanded.Add(normalized);
                    break;
                case var status when IsLitigationVariant(status, "Pending"):
                    expanded.Add(LitigationPending);
                    expanded.Add(normalized);
                    break;
                case var status when IsLitigationVariant(status, "Open"):
                    expanded.Add(LitigationOpen);
                    expanded.Add(normalized);
                    break;
                case var status when string.Equals(status, "Case Settled", StringComparison.OrdinalIgnoreCase):
                    expanded.Add(CaseSettled);
                    break;
                default:
                    expanded.Add(normalized);
                    break;
            }
        }

        return expanded;
    }

    private static bool IsLitigationVariant(string value, string variant)
    {
        var compact = value
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("(", string.Empty, StringComparison.Ordinal)
            .Replace(")", string.Empty, StringComparison.Ordinal);

        return string.Equals(compact, $"Litigation{variant}", StringComparison.OrdinalIgnoreCase);
    }
}
