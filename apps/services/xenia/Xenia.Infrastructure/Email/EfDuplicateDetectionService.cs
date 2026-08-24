using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xenia.Application.Email.Ingestion;
using Xenia.Infrastructure.Persistence;

namespace Xenia.Infrastructure.Email;

/// <summary>
/// EF Core implementation of duplicate detection.
///
/// Signal priority:
/// 1. ProviderMessageId + TenantId + SourceId (strongest — exact provider match)
/// 2. InternetMessageId + TenantId (cross-source deduplication)
/// 3. ContentHash + TenantId (hash fallback)
/// </summary>
internal sealed class EfDuplicateDetectionService : IDuplicateDetectionService
{
    private readonly XeniaDbContext _db;
    private readonly ILogger<EfDuplicateDetectionService> _logger;

    public EfDuplicateDetectionService(XeniaDbContext db, ILogger<EfDuplicateDetectionService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task<DuplicateCheckResult> CheckAsync(
        Guid tenantId, Guid emailSourceId, NormalizedMessage message, CancellationToken ct = default)
    {
        // Signal 1: exact provider match
        var byProvider = await _db.EmailMessages
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId
                     && m.EmailSourceId == emailSourceId
                     && m.ProviderMessageId == message.ProviderMessageId)
            .Select(m => m.Id)
            .FirstOrDefaultAsync(ct);

        if (byProvider != Guid.Empty)
        {
            _logger.LogDebug("Duplicate by ProviderMessageId tenantId={TenantId}", tenantId);
            return DuplicateCheckResult.Duplicate(byProvider, "ProviderMessageId");
        }

        // Signal 2: InternetMessageId (cross-source within tenant)
        if (!string.IsNullOrWhiteSpace(message.InternetMessageId))
        {
            var byInternet = await _db.EmailMessages
                .AsNoTracking()
                .Where(m => m.TenantId == tenantId
                         && m.InternetMessageId == message.InternetMessageId)
                .Select(m => m.Id)
                .FirstOrDefaultAsync(ct);

            if (byInternet != Guid.Empty)
            {
                _logger.LogDebug("Duplicate by InternetMessageId tenantId={TenantId}", tenantId);
                return DuplicateCheckResult.Duplicate(byInternet, "InternetMessageId");
            }
        }

        // Signal 3: ContentHash fallback
        if (!string.IsNullOrWhiteSpace(message.ContentHash))
        {
            var byHash = await _db.EmailMessages
                .AsNoTracking()
                .Where(m => m.TenantId == tenantId
                         && m.ContentHash == message.ContentHash)
                .Select(m => m.Id)
                .FirstOrDefaultAsync(ct);

            if (byHash != Guid.Empty)
            {
                _logger.LogDebug("Duplicate by ContentHash tenantId={TenantId}", tenantId);
                return DuplicateCheckResult.Duplicate(byHash, "ContentHash");
            }
        }

        return DuplicateCheckResult.NotDuplicate();
    }
}
