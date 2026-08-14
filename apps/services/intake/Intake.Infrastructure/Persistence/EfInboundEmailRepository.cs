using Intake.Application.Configuration;
using Intake.Application.Emails;
using Intake.Application.Sources;
using Intake.Contracts.Emails;
using Intake.Domain.Emails;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Intake.Infrastructure.Persistence;

public sealed class EfInboundEmailRepository(
    IntakeDbContext db) : IInboundEmailRepository
{
    public async Task RecordCaptureFailureAsync(
        InboundEmailCaptureFailure failure,
        CancellationToken cancellationToken)
    {
        db.InboundEmailCaptureFailures.Add(failure);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<InboundEmailPersistenceResult> PersistCaptureAsync(
        InboundEmail email,
        IReadOnlyList<InboundEmailRecipient> recipients,
        IReadOnlyList<InboundEmailAttachmentMetadata> attachments,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var existing = await db.InboundEmails.SingleOrDefaultAsync(
            item => item.IdempotencyKey == email.IdempotencyKey,
            cancellationToken);

        if (existing is not null)
        {
            existing.DuplicateCaptureCount++;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(existing.Id, true);
        }

        email.Recipients = recipients.ToList();
        email.AttachmentMetadata = attachments.ToList();
        db.InboundEmails.Add(email);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(email.Id, false);
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();

            await using var retryTransaction =
                await db.Database.BeginTransactionAsync(cancellationToken);
            var racedExisting = await db.InboundEmails.SingleOrDefaultAsync(
                item => item.IdempotencyKey == email.IdempotencyKey,
                cancellationToken);
            if (racedExisting is null)
                throw IntakeConfigurationException.Conflict(
                    "EMAIL_CAPTURE_IDEMPOTENCY_CONFLICT",
                    "The email capture conflicted with another persistence operation.");

            racedExisting.DuplicateCaptureCount++;
            racedExisting.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            await retryTransaction.CommitAsync(cancellationToken);
            return new(racedExisting.Id, true);
        }
    }

    public Task<InboundEmail?> FindTenantEmailAsync(
        Guid tenantId,
        Guid emailId,
        CancellationToken cancellationToken) =>
        db.InboundEmails
            .AsNoTracking()
            .Include(email => email.Recipients)
            .Include(email => email.AttachmentMetadata)
            .SingleOrDefaultAsync(
                email => email.TenantId == tenantId && email.Id == emailId,
                cancellationToken);

    public Task<InboundEmail?> FindByProviderIdentityAsync(
        Guid tenantId,
        Guid sourceId,
        string provider,
        string providerMessageId,
        CancellationToken cancellationToken) =>
        db.InboundEmails
            .AsNoTracking()
            .Include(email => email.Recipients)
            .Include(email => email.AttachmentMetadata)
            .SingleOrDefaultAsync(
                email => email.TenantId == tenantId &&
                         email.TenantIntakeSourceId == sourceId &&
                         email.Provider == provider &&
                         email.ProviderMessageId == providerMessageId,
                cancellationToken);

    public Task<InboundEmail?> FindByInternetMessageIdAsync(
        Guid tenantId,
        Guid sourceId,
        string internetMessageId,
        CancellationToken cancellationToken) =>
        db.InboundEmails
            .AsNoTracking()
            .Include(email => email.Recipients)
            .Include(email => email.AttachmentMetadata)
            .SingleOrDefaultAsync(
                email => email.TenantId == tenantId &&
                         email.TenantIntakeSourceId == sourceId &&
                         email.InternetMessageId == internetMessageId,
                cancellationToken);

    public async Task<PagedInboundEmailResponse> ListAsync(
        Guid tenantId,
        InboundEmailListQuery query,
        CancellationToken cancellationToken)
    {
        ValidateQuery(query);
        var page = query.Page;
        var pageSize = query.PageSize;
        var itemsQuery = db.InboundEmails
            .AsNoTracking()
            .Where(email => email.TenantId == tenantId);

        if (query.SourceId.HasValue)
            itemsQuery = itemsQuery.Where(email => email.TenantIntakeSourceId == query.SourceId);
        if (!string.IsNullOrWhiteSpace(query.Provider))
            itemsQuery = itemsQuery.Where(email => email.Provider == query.Provider.Trim().ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(query.Purpose))
            itemsQuery = itemsQuery.Where(email => email.Purpose == query.Purpose.Trim().ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(query.ProcessingProfileCode))
            itemsQuery = itemsQuery.Where(email =>
                email.ProcessingProfileCode == query.ProcessingProfileCode.Trim().ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(query.CaptureStatus))
            itemsQuery = itemsQuery.Where(email =>
                email.CaptureStatus == query.CaptureStatus.Trim().ToUpperInvariant());
        if (query.FromDate.HasValue)
            itemsQuery = itemsQuery.Where(email => email.ReceivedAt >= query.FromDate.Value.ToUniversalTime());
        if (query.ToDate.HasValue)
            itemsQuery = itemsQuery.Where(email => email.ReceivedAt <= query.ToDate.Value.ToUniversalTime());
        if (query.HasAttachments.HasValue)
            itemsQuery = itemsQuery.Where(email => email.HasAttachments == query.HasAttachments);
        if (!string.IsNullOrWhiteSpace(query.FromAddress))
        {
            var requestedFrom = query.FromAddress.Trim();
            _ = EmailAddressNormalizer.Normalize(requestedFrom);
            itemsQuery = itemsQuery.Where(email => email.FromAddress == requestedFrom);
        }

        var totalCount = await itemsQuery.LongCountAsync(cancellationToken);
        var items = await itemsQuery
            .OrderByDescending(email => email.ReceivedAt)
            .ThenByDescending(email => email.Id)
            .Select(email => new InboundEmailListItemResponse(
                email.Id,
                email.TenantIntakeSourceId,
                email.Purpose,
                email.ProcessingProfileCode,
                email.Provider,
                email.ProviderMessageId,
                email.InternetMessageId,
                email.ReceivedAt,
                email.FromAddress,
                email.FromDisplayName,
                email.Subject,
                email.HasAttachments,
                email.AttachmentCount,
                email.CaptureStatus,
                email.ProcessingStatus,
                email.DuplicateCaptureCount))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new(
            items,
            page,
            pageSize,
            totalCount,
            totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public async Task<InboundEmailAnalyticsResponse> GetAnalyticsAsync(
        Guid tenantId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        if (from.HasValue && to.HasValue && from > to)
            throw IntakeConfigurationException.BadRequest(
                "INVALID_EMAIL_DATE_RANGE",
                "The analytics from date must be before or equal to the to date.");

        var query = db.InboundEmails
            .AsNoTracking()
            .Where(email => email.TenantId == tenantId);
        if (from.HasValue)
            query = query.Where(email => email.ReceivedAt >= from.Value.ToUniversalTime());
        if (to.HasValue)
            query = query.Where(email => email.ReceivedAt <= to.Value.ToUniversalTime());

        var total = await query.LongCountAsync(cancellationToken);
        var byDayRows = await query
            .GroupBy(email => email.ReceivedAt.Date)
            .Select(group => new
            {
                Day = group.Key,
                Count = group.LongCount(),
            })
            .OrderBy(item => item.Day)
            .ToListAsync(cancellationToken);
        var byDay = byDayRows
            .Select(item => new InboundEmailCountByKey(
                item.Day.ToString("yyyy-MM-dd"),
                item.Count))
            .ToList();
        var bySourceRows = await query
            .GroupBy(email => email.TenantIntakeSourceId)
            .Select(group => new
            {
                SourceId = group.Key,
                Count = group.LongCount(),
            })
            .OrderBy(item => item.SourceId)
            .ToListAsync(cancellationToken);
        var bySource = bySourceRows
            .Select(item => new InboundEmailCountByKey(
                item.SourceId.ToString(),
                item.Count))
            .ToList();
        var byProvider = await GetStringCountsAsync(
            query.GroupBy(email => email.Provider),
            cancellationToken);
        var byPurpose = await GetStringCountsAsync(
            query.GroupBy(email => email.Purpose),
            cancellationToken);
        var byStatus = await GetStringCountsAsync(
            query.GroupBy(email => email.CaptureStatus),
            cancellationToken);
        var withAttachments = await query
            .LongCountAsync(email => email.HasAttachments, cancellationToken);
        var duplicateCount = await query
            .Select(email => (long)email.DuplicateCaptureCount)
            .SumAsync(cancellationToken);
        var failureQuery = db.InboundEmailCaptureFailures
            .AsNoTracking()
            .Where(failure => failure.TenantId == tenantId);
        if (from.HasValue)
            failureQuery = failureQuery.Where(failure =>
                failure.OccurredAt >= from.Value.ToUniversalTime());
        if (to.HasValue)
            failureQuery = failureQuery.Where(failure =>
                failure.OccurredAt <= to.Value.ToUniversalTime());
        var captureFailures = await failureQuery.LongCountAsync(cancellationToken);
        var averageAttachments = total == 0
            ? 0
            : await query.AverageAsync(email => (double)email.AttachmentCount, cancellationToken);

        return new(
            total,
            byDay,
            bySource,
            byProvider,
            byPurpose,
            byStatus,
            withAttachments,
            Convert.ToDecimal(averageAttachments),
            duplicateCount,
            captureFailures);
    }

    private static async Task<List<InboundEmailCountByKey>> GetStringCountsAsync(
        IQueryable<IGrouping<string, InboundEmail>> groups,
        CancellationToken cancellationToken)
    {
        var rows = await groups
            .Select(group => new
            {
                Key = group.Key,
                Count = group.LongCount(),
            })
            .OrderBy(item => item.Key)
            .ToListAsync(cancellationToken);
        return rows
            .Select(item => new InboundEmailCountByKey(item.Key, item.Count))
            .ToList();
    }

    private static void ValidateQuery(InboundEmailListQuery query)
    {
        if (query.Page < 1 || query.PageSize < 1 || query.PageSize > 200)
            throw IntakeConfigurationException.BadRequest(
                "INVALID_EMAIL_PAGINATION",
                "Page must be at least 1 and pageSize must be between 1 and 200.");
        if (query.FromDate.HasValue && query.ToDate.HasValue && query.FromDate > query.ToDate)
            throw IntakeConfigurationException.BadRequest(
                "INVALID_EMAIL_DATE_RANGE",
                "The list from date must be before or equal to the to date.");
    }

    private static bool IsDuplicateKey(DbUpdateException exception) =>
        exception.InnerException is MySqlException { Number: 1062 } ||
        exception.InnerException?.InnerException is MySqlException { Number: 1062 };
}