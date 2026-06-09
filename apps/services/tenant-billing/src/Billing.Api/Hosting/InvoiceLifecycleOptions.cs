namespace Billing.Api.Hosting;

/// <summary>
/// Bound from <c>InvoiceLifecycle</c> in appsettings. Controls the
/// background overdue scheduler. The scheduler is opt-in
/// (<see cref="OverdueJobEnabled"/> defaults to false) so the API host
/// starts in a quiet state in dev / CI / tests; ops turns it on per
/// environment.
/// </summary>
public sealed class InvoiceLifecycleOptions
{
    public const string SectionName = "InvoiceLifecycle";

    /// <summary>
    /// Master switch for <see cref="InvoiceOverdueHostedService"/>. When
    /// false (the default) the hosted service exits its
    /// <c>ExecuteAsync</c> loop immediately and never touches the database.
    /// </summary>
    public bool OverdueJobEnabled { get; set; } = false;

    /// <summary>
    /// Cadence at which the scheduler scans for newly-eligible invoices.
    /// Clamped to a minimum of 1 minute at runtime to prevent a runaway
    /// loop from a misconfigured zero / negative value.
    /// </summary>
    public int OverdueJobIntervalMinutes { get; set; } = 15;

    /// <summary>
    /// Per-tick batch cap. The scheduler may take up to this many invoices
    /// in a single sweep; further candidates wait for the next tick. Keeps
    /// any one tick from monopolising the database.
    /// </summary>
    public int OverdueBatchSize { get; set; } = 200;
}
