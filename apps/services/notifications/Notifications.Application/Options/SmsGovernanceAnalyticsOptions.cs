namespace Notifications.Application.Options;

/// <summary>LS-NOTIF-SMS-020: Configuration for governance rule effectiveness analytics.</summary>
public sealed class SmsGovernanceAnalyticsOptions
{
    public const string SectionName = "SmsGovernanceAnalytics";

    /// <summary>Master switch. When false, match recording is a no-op.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Default date window in days for analytics queries (bounded default).</summary>
    public int WindowDays { get; set; } = 30;

    /// <summary>Maximum rows returned by any analytics endpoint.</summary>
    public int MaxResultRows { get; set; } = 200;

    /// <summary>
    /// Warn threshold used by the false-positive heuristic.
    /// Values greater than or equal to 1 are treated as an absolute warn-count floor.
    /// Values between 0 and 1 are treated as a warn-ratio floor.
    /// </summary>
    public double FalsePositiveWarnThreshold { get; set; } = 10;

    /// <summary>
    /// Live/simulation threshold used by the false-positive heuristic.
    /// Values between 0 and 1 are treated as the maximum allowed live share:
    /// live / (live + sim) must be below the configured value.
    /// Values greater than 1 are treated as a simulation-to-live ratio threshold:
    /// sim / live must be greater than the configured value.
    /// </summary>
    public double FalsePositiveLiveToSimRatio { get; set; } = 0.1;
}
