using Microsoft.EntityFrameworkCore;
using Xenia.Application.Email.Ingestion;
using Xenia.Domain.Email;
using Xenia.Infrastructure.Persistence;

namespace Xenia.Infrastructure.Email;

internal sealed class EfEmailMessageService : IEmailMessageService
{
    private readonly XeniaDbContext _db;

    public EfEmailMessageService(XeniaDbContext db) => _db = db;

    public async Task<EmailMessagePage> ListMessagesAsync(EmailMessageQuery query, CancellationToken ct = default)
    {
        var q = _db.EmailMessages
            .AsNoTracking()
            .Where(m => m.TenantId == query.TenantId);

        if (query.EmailSourceId.HasValue)
            q = q.Where(m => m.EmailSourceId == query.EmailSourceId.Value);
        if (!string.IsNullOrWhiteSpace(query.FromAddress))
            q = q.Where(m => m.FromAddress != null && m.FromAddress.Contains(query.FromAddress.ToLowerInvariant()));
        if (!string.IsNullOrWhiteSpace(query.SubjectContains))
            q = q.Where(m => m.Subject != null && m.Subject.Contains(query.SubjectContains));
        if (query.ImportStatus.HasValue)
            q = q.Where(m => m.ImportStatus == query.ImportStatus.Value);
        if (query.HasAttachments.HasValue)
            q = q.Where(m => m.HasAttachments == query.HasAttachments.Value);
        if (query.ReceivedAfter.HasValue)
            q = q.Where(m => m.ReceivedAt >= query.ReceivedAfter.Value);
        if (query.ReceivedBefore.HasValue)
            q = q.Where(m => m.ReceivedAt <= query.ReceivedBefore.Value);

        var total = await q.CountAsync(ct);

        var messages = await q
            .OrderByDescending(m => m.ReceivedAt)
            .Skip(query.PageOffset)
            .Take(Math.Min(query.PageSize, 200))
            .Select(m => new EmailMessageSummary
            {
                Id             = m.Id,
                TenantId       = m.TenantId,
                EmailSourceId  = m.EmailSourceId,
                Subject        = m.Subject,
                FromAddress    = m.FromAddress,
                FromName       = m.FromName,
                ReceivedAt     = m.ReceivedAt,
                HasAttachments = m.HasAttachments,
                AttachmentCount= m.AttachmentCount,
                Importance     = m.Importance,
                IsRead         = m.IsRead,
                BodyPreview    = m.BodyPreview,
                ImportStatus   = m.ImportStatus,
                ImportedAt     = m.ImportedAt,
            })
            .ToListAsync(ct);

        return new EmailMessagePage
        {
            Messages    = messages,
            TotalCount  = total,
            PageSize    = query.PageSize,
            PageOffset  = query.PageOffset,
        };
    }

    public async Task<EmailMessageDetail?> GetMessageAsync(Guid tenantId, Guid messageId, CancellationToken ct = default)
    {
        var msg = await _db.EmailMessages
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == messageId, ct);

        if (msg is null) return null;

        var recipients = await _db.EmailMessageRecipients
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.EmailMessageId == messageId)
            .Select(r => new RecipientDto(r.RecipientType.ToString(), r.EmailAddress, r.DisplayName))
            .ToListAsync(ct);

        var attachments = await _db.EmailAttachmentReferences
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.EmailMessageId == messageId)
            .Select(a => new AttachmentReferenceDto
            {
                Id                  = a.Id,
                FileName            = a.FileName,
                MimeType            = a.MimeType,
                SizeBytes           = a.SizeBytes,
                IsInline            = a.IsInline,
                DispatchStatus      = a.DispatchStatus.ToString(),
                DocumentReferenceId = a.DocumentReferenceId,
            })
            .ToListAsync(ct);

        return new EmailMessageDetail
        {
            Id               = msg.Id,
            TenantId         = msg.TenantId,
            EmailSourceId    = msg.EmailSourceId,
            Subject          = msg.Subject,
            FromAddress      = msg.FromAddress,
            FromName         = msg.FromName,
            SenderAddress    = msg.SenderAddress,
            Recipients       = recipients,
            SentAt           = msg.SentAt,
            ReceivedAt       = msg.ReceivedAt,
            Importance       = msg.Importance,
            IsRead           = msg.IsRead,
            HasAttachments   = msg.HasAttachments,
            BodyType         = msg.BodyType,
            BodyText         = msg.BodyText,
            BodyHtml         = msg.BodyHtml,
            BodyPreview      = msg.BodyPreview,
            InternetMessageId= msg.InternetMessageId,
            ThreadId         = msg.ThreadId,
            ImportStatus     = msg.ImportStatus,
            ImportedAt       = msg.ImportedAt,
            Attachments      = attachments,
        };
    }

    public async Task<IReadOnlyList<AttachmentReferenceDto>> GetAttachmentsAsync(
        Guid tenantId, Guid messageId, CancellationToken ct = default)
    {
        return await _db.EmailAttachmentReferences
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.EmailMessageId == messageId)
            .Select(a => new AttachmentReferenceDto
            {
                Id                  = a.Id,
                FileName            = a.FileName,
                MimeType            = a.MimeType,
                SizeBytes           = a.SizeBytes,
                IsInline            = a.IsInline,
                DispatchStatus      = a.DispatchStatus.ToString(),
                DocumentReferenceId = a.DocumentReferenceId,
            })
            .ToListAsync(ct);
    }
}
