// LSCC-011: Activation funnel analytics service interface.
using CareConnect.Application.DTOs;

namespace CareConnect.Application.Interfaces;

/// <summary>
/// Computes activation funnel metrics from existing CareConnect data.
/// All metrics are derived — no analytics tables are created.
/// </summary>
public interface IActivationFunnelAnalyticsService
{
    /// <summary>
    /// Returns funnel counts and conversion rates for the given date range.
    /// The range is inclusive: [startDate, endDate] (UTC, date-only boundaries).
    /// If startDate > endDate the range is swapped before querying.
    /// </summary>
    Task<ActivationFunnelMetrics> GetMetricsAsync(
        DateTime          startDate,
        DateTime          endDate,
        CancellationToken ct = default);
}
