namespace Liens.Domain.Enums;

public static class LienStatus
{
    public static readonly Guid LegacyOpenFilterId = Guid.Parse("10f4afc1-dc10-4e59-96eb-18af4f6edfe6");
    public static readonly Guid LegacyClosedFilterId = Guid.Parse("77956de1-2976-4760-b56e-ca1b9a22bd27");

    public const string Draft      = "Draft";
    public const string Offered    = "Offered";
    public const string Accepted   = "Accepted";
    public const string Declined   = "Declined";
    public const string UnderReview = "UnderReview";
    public const string Sold       = "Sold";
    public const string Active     = "Active";
    public const string Settled    = "Settled";
    public const string Withdrawn  = "Withdrawn";
    public const string Cancelled  = "Cancelled";
    public const string Disputed   = "Disputed";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Draft, Offered, Accepted, Declined, UnderReview, Sold, Active, Settled, Withdrawn, Cancelled, Disputed
    };

    public static readonly IReadOnlySet<string> Open = new HashSet<string>
    {
        Draft, Offered, Accepted, UnderReview, Sold, Active, Disputed
    };

    public static readonly IReadOnlySet<string> Terminal = new HashSet<string>
    {
        Declined, Settled, Withdrawn, Cancelled
    };

    public static bool TryGetLegacyFilterGroup(Guid id, out string group)
    {
        if (id == LegacyOpenFilterId)
        {
            group = "Open";
            return true;
        }

        if (id == LegacyClosedFilterId)
        {
            group = "Closed";
            return true;
        }

        group = string.Empty;
        return false;
    }

    /// <summary>
    /// Expands the legacy/UI lifecycle groups used by list filters into the
    /// persisted statuses that can be compared in database queries.
    /// </summary>
    public static IReadOnlySet<string> ExpandFilterValues(IEnumerable<string> values)
    {
        var expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var value in values.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            switch (value.Trim())
            {
                case var group when string.Equals(group, "Open", StringComparison.OrdinalIgnoreCase):
                    expanded.UnionWith(Open);
                    break;
                case var group when string.Equals(group, "Closed", StringComparison.OrdinalIgnoreCase):
                    expanded.Add(Settled);
                    break;
                case var group when string.Equals(group, "Rejected", StringComparison.OrdinalIgnoreCase):
                    expanded.UnionWith([Declined, Withdrawn, Cancelled]);
                    break;
                default:
                    expanded.Add(value.Trim());
                    break;
            }
        }

        return expanded;
    }

    public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedTransitions =
        new Dictionary<string, IReadOnlySet<string>>
        {
            [Draft]       = new HashSet<string> { Offered, Cancelled },
            [Offered]     = new HashSet<string> { Accepted, Declined, UnderReview, Sold, Withdrawn },
            [Accepted]    = new HashSet<string> { Sold, Withdrawn },
            [Declined]    = new HashSet<string>(),
            [UnderReview] = new HashSet<string> { Accepted, Declined, Sold, Withdrawn },
            [Sold]        = new HashSet<string> { Active, Cancelled },
            [Active]      = new HashSet<string> { Settled, Disputed, Cancelled },
            [Disputed]    = new HashSet<string> { Active, Settled, Cancelled },
            [Settled]     = new HashSet<string>(),
            [Withdrawn]   = new HashSet<string>(),
            [Cancelled]   = new HashSet<string>(),
        };
}
