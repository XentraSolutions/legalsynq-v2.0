using Microsoft.Extensions.Options;
using Billing.Domain.Statements.Delivery;

namespace Billing.Infrastructure.Delivery;

/// <summary>
/// MS-BILL-INT-003 — In-memory rolling-window implementation of
/// <see cref="IProviderHealthMonitor"/>.
///
/// <para>
/// Single process, single bounded queue, lock-protected. Sized to
/// the <see cref="ProviderHealthOptions.WindowSeconds"/> window
/// with a hard cap (1024 entries) to keep memory bounded even if
/// outcomes arrive faster than the window evicts them. Eviction
/// happens lazily on every Record / Get call, so the queue never
/// grows unbounded between low-traffic windows.
/// </para>
///
/// <para>
/// State derivation is deterministic from the rolling failure
/// count alone:
/// </para>
/// <list type="bullet">
///   <item>failures &lt;
///   <see cref="ProviderHealthOptions.DegradedAfterFailures"/>
///   → <see cref="ProviderHealthState.Healthy"/></item>
///   <item>failures &gt;=
///   <see cref="ProviderHealthOptions.DegradedAfterFailures"/>
///   AND &lt;
///   <see cref="ProviderHealthOptions.UnavailableAfterFailures"/>
///   → <see cref="ProviderHealthState.Degraded"/></item>
///   <item>failures &gt;=
///   <see cref="ProviderHealthOptions.UnavailableAfterFailures"/>
///   → <see cref="ProviderHealthState.Unavailable"/></item>
/// </list>
///
/// <para>
/// "Failure" = anything other than
/// <see cref="StatementDeliveryStatus.Sent"/>, with the explicit
/// exclusion of <see cref="StatementDeliveryStatus.InvalidRecipient"/>
/// (a recipient address is a customer-record problem, not a
/// provider problem) and
/// <see cref="StatementDeliveryStatus.RetryNotAllowed"/> (a
/// governance short-circuit, not a provider event — never even
/// hits the provider).
/// </para>
/// </summary>
public sealed class ProviderHealthMonitor : IProviderHealthMonitor
{
    // Hard cap so a misbehaving caller cannot grow the queue
    // unboundedly between window-eviction calls.
    private const int MaxEntries = 1024;

    private readonly IOptionsMonitor<StatementRetryOptions> _options;
    private readonly object _gate = new();
    private readonly LinkedList<Entry> _entries = new();

    public ProviderHealthMonitor(IOptionsMonitor<StatementRetryOptions> options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public void RecordOutcome(string providerName, string deliveryStatus, DateTime nowUtc)
    {
        // Defensive: monitor must never throw — orchestrator is in
        // its happy path when it calls this.
        if (string.IsNullOrWhiteSpace(deliveryStatus)) return;

        var classification = Classify(deliveryStatus);
        if (classification is null) return;

        lock (_gate)
        {
            _entries.AddLast(new Entry(nowUtc, classification.Value));
            EvictExpiredLocked(nowUtc);
            // Hard cap pruning: drop oldest until under cap.
            while (_entries.Count > MaxEntries)
            {
                _entries.RemoveFirst();
            }
        }
    }

    public ProviderHealthSnapshot GetHealth(DateTime nowUtc)
    {
        var hp = NormalisedOptions();
        int failures = 0, successes = 0;

        lock (_gate)
        {
            EvictExpiredLocked(nowUtc);
            foreach (var e in _entries)
            {
                if (e.Outcome == EntryKind.Failure) failures++;
                else successes++;
            }
        }

        var state = failures switch
        {
            _ when failures >= hp.UnavailableAfterFailures => ProviderHealthState.Unavailable,
            _ when failures >= hp.DegradedAfterFailures => ProviderHealthState.Degraded,
            _ => ProviderHealthState.Healthy,
        };

        return new ProviderHealthSnapshot(
            State: state,
            RecentFailures: failures,
            RecentSuccesses: successes,
            WindowSeconds: hp.WindowSeconds,
            ObservedAtUtc: nowUtc);
    }

    private void EvictExpiredLocked(DateTime nowUtc)
    {
        var hp = NormalisedOptions();
        var cutoff = nowUtc.AddSeconds(-hp.WindowSeconds);
        while (_entries.First is { } first && first.Value.At < cutoff)
        {
            _entries.RemoveFirst();
        }
    }

    /// <summary>
    /// Coerce hostile / partial config to safe values so a typo
    /// like <c>UnavailableAfterFailures = 0</c> cannot flip the
    /// monitor permanently into Unavailable.
    /// </summary>
    private NormalisedHealth NormalisedOptions()
    {
        var src = _options.CurrentValue.ProviderHealth ?? new ProviderHealthOptions();
        var window = src.WindowSeconds > 0 ? src.WindowSeconds : 60;
        var degraded = src.DegradedAfterFailures > 0 ? src.DegradedAfterFailures : 3;
        var unavailable = src.UnavailableAfterFailures >= degraded
            ? src.UnavailableAfterFailures
            : degraded;
        return new NormalisedHealth(window, degraded, unavailable);
    }

    private static EntryKind? Classify(string deliveryStatus) => deliveryStatus switch
    {
        StatementDeliveryStatus.Sent => EntryKind.Success,
        // InvalidRecipient is a customer-record problem, not a
        // provider problem — recording it would degrade the health
        // signal for an unrelated reason.
        StatementDeliveryStatus.InvalidRecipient => null,
        // RetryNotAllowed never hits the provider.
        StatementDeliveryStatus.RetryNotAllowed => null,
        StatementDeliveryStatus.ProviderUnavailable => EntryKind.Failure,
        StatementDeliveryStatus.RetryableFailure => EntryKind.Failure,
        StatementDeliveryStatus.Failed => EntryKind.Failure,
        _ => null,
    };

    private readonly record struct Entry(DateTime At, EntryKind Outcome);
    private enum EntryKind { Success, Failure }
    private readonly record struct NormalisedHealth(
        int WindowSeconds, int DegradedAfterFailures, int UnavailableAfterFailures);
}
