using Microsoft.EntityFrameworkCore;
using Xenia.Application.Email;
using Xenia.Application.Email.Operations;
using Xenia.Domain.Email;
using Xenia.Infrastructure.Persistence;

namespace Xenia.Infrastructure.Email;

/// <summary>
/// Returns per-provider operational health aggregated from source data.
/// Classification is based on known connector capabilities — not auto-detected from code existence.
/// </summary>
internal sealed class EfProviderHealthService : IProviderHealthService
{
    private readonly XeniaDbContext _db;

    public EfProviderHealthService(XeniaDbContext db) => _db = db;

    private static readonly IReadOnlyDictionary<EmailProviderType, ProviderCapabilityProfile> Profiles =
        new Dictionary<EmailProviderType, ProviderCapabilityProfile>
        {
            [EmailProviderType.Imap] = new(
                Classification: ProviderClassification.Operational,
                DisplayName: "IMAP",
                SupportsAuthentication: true,
                SupportsIngestion: true,
                SupportsIncrementalCursor: true,
                SupportsAttachments: true,
                SupportsRateLimitHandling: false),

            [EmailProviderType.Gmail] = new(
                Classification: ProviderClassification.ProtocolCompleteUnverified,
                DisplayName: "Gmail",
                SupportsAuthentication: true,
                SupportsIngestion: true,
                SupportsIncrementalCursor: true,
                SupportsAttachments: true,
                SupportsRateLimitHandling: true),

            [EmailProviderType.Microsoft365] = new(
                Classification: ProviderClassification.ProtocolCompleteUnverified,
                DisplayName: "Microsoft 365",
                SupportsAuthentication: true,
                SupportsIngestion: true,
                SupportsIncrementalCursor: true,
                SupportsAttachments: true,
                SupportsRateLimitHandling: true),

            [EmailProviderType.Exchange] = new(
                Classification: ProviderClassification.Stub,
                DisplayName: "Exchange (IMAP mode)",
                SupportsAuthentication: true,
                SupportsIngestion: true,
                SupportsIncrementalCursor: true,
                SupportsAttachments: true,
                SupportsRateLimitHandling: false),

            [EmailProviderType.Pop3] = new(
                Classification: ProviderClassification.Stub,
                DisplayName: "POP3",
                SupportsAuthentication: true,
                SupportsIngestion: true,
                SupportsIncrementalCursor: false,
                SupportsAttachments: true,
                SupportsRateLimitHandling: false),
        };

    public async Task<IReadOnlyList<ProviderHealthSnapshot>> GetAllAsync(Guid tenantId, CancellationToken ct = default)
    {
        var sourcesByProvider = await _db.EmailSources
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .GroupBy(s => s.ProviderType)
            .Select(g => new
            {
                Provider      = g.Key,
                Total         = g.Count(),
                Healthy       = g.Count(s => s.HealthStatus == EmailHealthStatus.Healthy),
                Failed        = g.Count(s => s.HealthStatus == EmailHealthStatus.Unhealthy),
            })
            .ToListAsync(ct);

        var result = new List<ProviderHealthSnapshot>();

        foreach (EmailProviderType providerType in Enum.GetValues<EmailProviderType>())
        {
            if (!Profiles.TryGetValue(providerType, out var profile)) continue;

            var stats = sourcesByProvider.FirstOrDefault(s => s.Provider == providerType);

            var configured = stats?.Total ?? 0;
            var healthy    = stats?.Healthy ?? 0;
            var failed     = stats?.Failed ?? 0;

            var errorRate  = configured > 0 ? (double)failed / configured : 0.0;
            var opStatus   = profile.Classification == ProviderClassification.Operational
                ? "Operational"
                : profile.Classification.ToString();

            result.Add(new ProviderHealthSnapshot(
                Provider:                  providerType,
                ProviderDisplayName:       profile.DisplayName,
                Classification:            profile.Classification,
                OperationalStatus:         opStatus,
                SupportsAuthentication:    profile.SupportsAuthentication,
                SupportsIngestion:         profile.SupportsIngestion,
                SupportsIncrementalCursor: profile.SupportsIncrementalCursor,
                SupportsAttachments:       profile.SupportsAttachments,
                SupportsRateLimitHandling: profile.SupportsRateLimitHandling,
                ConfiguredSourceCount:     configured,
                HealthySourceCount:        healthy,
                FailedSourceCount:         failed,
                RecentErrorRate:           errorRate,
                AverageConnectionLatencyMs:null,
                LastSuccessfulOperationAt: null,
                AvailabilityPercent:       configured > 0 ? (double)healthy / configured * 100.0 : null,
                SafeDiagnostics:           null));
        }

        return result;
    }

    private sealed record ProviderCapabilityProfile(
        ProviderClassification Classification,
        string DisplayName,
        bool SupportsAuthentication,
        bool SupportsIngestion,
        bool SupportsIncrementalCursor,
        bool SupportsAttachments,
        bool SupportsRateLimitHandling);
}
