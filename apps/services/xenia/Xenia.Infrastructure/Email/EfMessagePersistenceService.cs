using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xenia.Application.Adapters.Interfaces;
using Xenia.Application.Email.Ingestion;
using Xenia.Domain.Email;
using Xenia.Infrastructure.Persistence;

namespace Xenia.Infrastructure.Email;

/// <summary>
/// EF Core implementation of message persistence.
///
/// Process:
/// 1. If duplicate — record observation on existing message and return early.
/// 2. Create EmailMessage + populate from NormalizedMessage.
/// 3. Create EmailMessageRecipient records.
/// 4. SaveChanges atomically.
/// 5. Create EmailAttachmentReference stubs (Pending) — separate save.
/// 6. Return attachment stubs for dispatcher.
///
/// No binary content is stored here.
/// Audit events: xenia.email.message.imported, .duplicate, .failed
/// Note: xenia.email.message.updated is not applicable — the current persistence
///       layer has no update pathway; duplicates are observed, not re-imported.
/// </summary>
internal sealed class EfMessagePersistenceService : IMessagePersistenceService
{
    private readonly XeniaDbContext _db;
    private readonly IAuditAdapter _auditAdapter;
    private readonly ILogger<EfMessagePersistenceService> _logger;

    public EfMessagePersistenceService(
        XeniaDbContext db,
        IAuditAdapter auditAdapter,
        ILogger<EfMessagePersistenceService> logger)
    {
        _db           = db;
        _auditAdapter = auditAdapter;
        _logger       = logger;
    }

    public async Task<MessagePersistenceResult> PersistMessageAsync(
        Guid tenantId,
        Guid emailSourceId,
        EmailProviderType providerType,
        NormalizedMessage message,
        Guid runId,
        DuplicateCheckResult duplicateCheck,
        CancellationToken ct = default)
    {
        try
        {
            if (duplicateCheck.IsDuplicate && duplicateCheck.ExistingMessageId.HasValue)
            {
                var existing = await _db.EmailMessages
                    .FirstOrDefaultAsync(m => m.Id == duplicateCheck.ExistingMessageId.Value, ct);
                if (existing is not null)
                {
                    existing.MarkDuplicate(runId);
                    await _db.SaveChangesAsync(ct);
                }

                await TryAuditAsync(new XeniaAuditEvent
                {
                    Action        = "xenia.email.message.duplicate",
                    ResourceType  = "email_message",
                    ResourceId    = duplicateCheck.ExistingMessageId.Value.ToString(),
                    Result        = "duplicate",
                    TenantId      = tenantId,
                    ActorId       = null,
                    CorrelationId = null,
                    OccurredAt    = DateTime.UtcNow,
                    Detail        = $"source_id={emailSourceId} run_id={runId}",
                });

                return new MessagePersistenceResult
                {
                    Success              = true,
                    MessageId            = duplicateCheck.ExistingMessageId,
                    ImportStatus         = MessageImportStatus.Duplicate,
                    AttachmentReferenceIds = [],
                };
            }

            var msg = EmailMessage.Create(tenantId, emailSourceId, providerType, message.ProviderMessageId);
            msg.SetAddressing(
                message.Subject, message.FromAddress, message.FromName,
                message.SenderAddress, message.SenderName,
                message.ReplyToAddressesCsv,
                message.InternetMessageId, message.ThreadId, message.ConversationId);
            msg.SetTimestamps(message.SentAt, message.ReceivedAt);
            msg.SetMetadata(message.Importance, message.IsRead, message.HasAttachments, message.AttachmentCount);
            msg.SetBody(message.BodyType, message.BodyText, message.BodyHtml, message.BodyPreview);
            msg.SetHeadersAndMetadata(message.HeadersJson, message.ProviderMetadataJson, message.ContentHash);
            msg.MarkImported(runId);

            _db.EmailMessages.Add(msg);

            foreach (var r in message.Recipients)
            {
                _db.EmailMessageRecipients.Add(
                    EmailMessageRecipient.Create(tenantId, msg.Id, r.RecipientType, r.EmailAddress, r.DisplayName));
            }

            await _db.SaveChangesAsync(ct);

            var attachmentIds = new List<Guid>();
            foreach (var att in message.Attachments)
            {
                var stub = EmailAttachmentReference.Create(
                    tenantId, msg.Id, att.ProviderAttachmentId,
                    att.FileName, att.MimeType, att.SizeBytes,
                    att.IsInline, att.ContentId);
                _db.EmailAttachmentReferences.Add(stub);
                attachmentIds.Add(stub.Id);
            }

            if (attachmentIds.Count > 0)
                await _db.SaveChangesAsync(ct);

            await TryAuditAsync(new XeniaAuditEvent
            {
                Action        = "xenia.email.message.imported",
                ResourceType  = "email_message",
                ResourceId    = msg.Id.ToString(),
                Result        = "success",
                TenantId      = tenantId,
                ActorId       = null,
                CorrelationId = null,
                OccurredAt    = DateTime.UtcNow,
                Detail        = $"source_id={emailSourceId} run_id={runId} provider={providerType} attachments={attachmentIds.Count}",
            });

            return new MessagePersistenceResult
            {
                Success              = true,
                MessageId            = msg.Id,
                ImportStatus         = MessageImportStatus.Imported,
                AttachmentReferenceIds = attachmentIds,
            };
        }
        catch (DbUpdateException ex) when (IsDuplicateKeyException(ex))
        {
            _logger.LogDebug(ex, "Concurrent duplicate detected for tenantId={TenantId}", tenantId);

            await TryAuditAsync(new XeniaAuditEvent
            {
                Action        = "xenia.email.message.duplicate",
                ResourceType  = "email_message",
                ResourceId    = null,
                Result        = "duplicate",
                TenantId      = tenantId,
                ActorId       = null,
                CorrelationId = null,
                OccurredAt    = DateTime.UtcNow,
                Detail        = $"source_id={emailSourceId} run_id={runId} reason=concurrent_race",
            });

            return new MessagePersistenceResult
            {
                Success      = true,
                ImportStatus = MessageImportStatus.Duplicate,
                AttachmentReferenceIds = [],
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Persistence error for tenantId={TenantId} sourceId={SourceId}", tenantId, emailSourceId);

            await TryAuditAsync(new XeniaAuditEvent
            {
                Action        = "xenia.email.message.failed",
                ResourceType  = "email_message",
                ResourceId    = null,
                Result        = "failure",
                TenantId      = tenantId,
                ActorId       = null,
                CorrelationId = null,
                OccurredAt    = DateTime.UtcNow,
                Detail        = $"source_id={emailSourceId} run_id={runId} error=PERSISTENCE_ERROR",
            });

            return new MessagePersistenceResult
            {
                Success          = false,
                ImportStatus     = MessageImportStatus.Failed,
                ErrorCode        = "PERSISTENCE_ERROR",
                SafeErrorSummary = "Message could not be saved.",
                AttachmentReferenceIds = [],
            };
        }
    }

    private async Task TryAuditAsync(XeniaAuditEvent ev)
    {
        try { await _auditAdapter.RecordEventAsync(ev); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audit emit failed for action={Action}", ev.Action);
        }
    }

    private static bool IsDuplicateKeyException(DbUpdateException ex)
    {
        var inner = ex.InnerException?.Message ?? string.Empty;
        return inner.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase)
            || inner.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase)
            || inner.Contains("duplicate key", StringComparison.OrdinalIgnoreCase);
    }
}
