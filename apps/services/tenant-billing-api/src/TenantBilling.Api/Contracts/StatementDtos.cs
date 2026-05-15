using System.ComponentModel.DataAnnotations;

namespace TenantBilling.Api.Contracts;

/// <summary>
/// STAT-B01 — Query parameters for the JSON / HTML statement
/// endpoints. Both dates are required so the statement window is
/// always explicit; the <c>monthly</c> shortcut endpoint composes
/// them server-side from a year + month pair.
/// </summary>
public sealed class StatementPeriodQuery
{
    [Required]
    public DateTime? From { get; set; }

    [Required]
    public DateTime? To { get; set; }
}

/// <summary>
/// STAT-B01 — Query parameters for the monthly shortcut endpoint.
/// Year is bounded to a sensible range to surface obvious typos as
/// 400s rather than silently producing an empty statement for
/// e.g. year 19 ("19" missing the century).
/// </summary>
public sealed class StatementMonthlyQuery
{
    [Required]
    [Range(1900, 2100)]
    public int? Year { get; set; }

    [Required]
    [Range(1, 12)]
    public int? Month { get; set; }
}
