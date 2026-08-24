namespace Identity.Infrastructure.Services;

/// <summary>
/// BE-BIO-009: Bound from the "RefreshTokenPolicy" appsettings.json section.
/// These are the BRD's recommended defaults, explicitly flagged there as
/// needing Security/Product approval — kept in config, not hardcoded, so
/// ops can tune without a redeploy.
/// </summary>
public sealed class RefreshTokenPolicyOptions
{
    public int RefreshInactivityDays { get; set; } = 30;
    public int RefreshAbsoluteDays { get; set; } = 90;

    /// <summary>Optional cap on active device sessions per user; 0 disables the cap.</summary>
    public int MaxActiveSessionsPerUser { get; set; } = 10;

    /// <summary>SEC-014: how recent primary authentication must be to satisfy step-up checks.</summary>
    public int StepUpWindowMinutes { get; set; } = 15;

    /// <summary>
    /// BE-BIO-007: how long after a rotation a resubmission of the just-superseded
    /// token is treated as a benign client-side race (e.g. a network-timeout retry)
    /// rather than confirmed theft. Kept short — long enough to absorb ordinary
    /// retry/network jitter, short enough that a genuine attacker replay outside
    /// this window is still caught.
    /// </summary>
    public int ReuseGraceSeconds { get; set; } = 10;
}
