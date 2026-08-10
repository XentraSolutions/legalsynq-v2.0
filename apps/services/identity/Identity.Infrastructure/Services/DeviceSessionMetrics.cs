using System.Diagnostics.Metrics;

namespace Identity.Infrastructure.Services;

public static class DeviceSessionMetrics
{
    public const string MeterName = "LegalSynq.Identity.DeviceSessions";
    private static readonly Meter Meter = new(MeterName, "1.0.0");
    private static readonly Counter<long> SessionsCreated = Meter.CreateCounter<long>("identity.device_sessions.created");
    private static readonly Counter<long> SessionsRevoked = Meter.CreateCounter<long>("identity.device_sessions.revoked");
    private static readonly Counter<long> Refreshes = Meter.CreateCounter<long>("identity.device_sessions.refreshes");
    private static readonly Counter<long> ReuseDetections = Meter.CreateCounter<long>("identity.device_sessions.reuse_detected");
    private static readonly Histogram<double> RefreshLatency = Meter.CreateHistogram<double>("identity.device_sessions.refresh.duration_ms", "ms");

    public static void RecordCreated(string platform) => SessionsCreated.Add(1, new KeyValuePair<string, object?>("platform", platform));
    public static void RecordRevoked(string reason) => SessionsRevoked.Add(1, new KeyValuePair<string, object?>("reason", reason));
    public static void RecordRefresh(string outcome, double elapsedMs)
    {
        var tag = new KeyValuePair<string, object?>("outcome", outcome);
        Refreshes.Add(1, tag);
        RefreshLatency.Record(elapsedMs, tag);
    }
    public static void RecordReuse() => ReuseDetections.Add(1);
}
