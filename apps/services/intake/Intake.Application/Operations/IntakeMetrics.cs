using System.Diagnostics.Metrics;

namespace Intake.Application.Operations;

public sealed class IntakeMetrics
{
    public const string MeterName = "LegalSynq.Intake";
    private readonly Meter meter = new(MeterName, "1.0.0");
    private readonly Counter<long> recoveryStale;
    private readonly Counter<long> recoveryRecovered;
    private readonly Counter<long> recoveryFailed;
    private readonly Counter<long> recoveryExhausted;
    private readonly Counter<long> recoveryManual;
    private readonly Histogram<double> recoveryDuration;

    public IntakeMetrics()
    {
        recoveryStale = meter.CreateCounter<long>("intake.recovery.stale");
        recoveryRecovered = meter.CreateCounter<long>("intake.recovery.recovered");
        recoveryFailed = meter.CreateCounter<long>("intake.recovery.failed");
        recoveryExhausted = meter.CreateCounter<long>("intake.recovery.exhausted");
        recoveryManual = meter.CreateCounter<long>("intake.recovery.manual");
        recoveryDuration = meter.CreateHistogram<double>("intake.recovery.duration", "ms");
    }

    public void Stale(string stage) => recoveryStale.Add(1, new KeyValuePair<string, object?>("stage", stage));
    public void Recovered(string stage) => recoveryRecovered.Add(1, new KeyValuePair<string, object?>("stage", stage));
    public void Failed(string stage, string category) =>
        recoveryFailed.Add(1,
            new KeyValuePair<string, object?>("stage", stage),
            new KeyValuePair<string, object?>("failure_category", category));
    public void Exhausted(string stage) => recoveryExhausted.Add(1, new KeyValuePair<string, object?>("stage", stage));
    public void Manual(string stage) => recoveryManual.Add(1, new KeyValuePair<string, object?>("stage", stage));
    public void Duration(string stage, double milliseconds) =>
        recoveryDuration.Record(milliseconds, new KeyValuePair<string, object?>("stage", stage));
}