using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xenia.Application.Adapters.Interfaces;
using Xenia.Application.Email.Ingestion;
using Xenia.Infrastructure.Persistence;

namespace Xenia.Infrastructure.Email;

/// <summary>
/// Dispatches attachment streams to the Documents platform adapter.
///
/// Rules:
/// - Never buffers the full stream in memory beyond the size cap.
/// - Never stores binary content in Xenia's DB.
/// - If the Documents adapter is unavailable, the reference remains Pending — no fallback.
/// - Idempotent: re-dispatching a Pending or Failed reference is safe.
/// </summary>
internal sealed class DocumentAdapterAttachmentDispatcher : IAttachmentDispatcher
{
    private readonly IDocumentAdapter _documentAdapter;
    private readonly XeniaDbContext _db;
    private readonly XeniaIngestionOptions _opts;
    private readonly ILogger<DocumentAdapterAttachmentDispatcher> _logger;

    public DocumentAdapterAttachmentDispatcher(
        IDocumentAdapter documentAdapter,
        XeniaDbContext db,
        IOptions<XeniaIngestionOptions> opts,
        ILogger<DocumentAdapterAttachmentDispatcher> logger)
    {
        _documentAdapter = documentAdapter;
        _db              = db;
        _opts            = opts.Value;
        _logger          = logger;
    }

    public async Task<AttachmentDispatchResult> DispatchAsync(
        AttachmentDispatchRequest request,
        CancellationToken ct = default)
    {
        var reference = await _db.EmailAttachmentReferences
            .FirstOrDefaultAsync(r => r.Id == request.AttachmentReferenceId && r.TenantId == request.TenantId, ct);

        if (reference is null)
        {
            return new AttachmentDispatchResult
            {
                Success          = false,
                ErrorCode        = "ATTACHMENT_REF_NOT_FOUND",
                SafeErrorSummary = "Attachment reference not found.",
            };
        }

        if (reference.DispatchStatus == Domain.Email.AttachmentDispatchStatus.Dispatched)
        {
            return new AttachmentDispatchResult
            {
                Success             = true,
                DocumentReferenceId = reference.DocumentReferenceId,
                ContentHash         = reference.ContentHash,
            };
        }

        if (!_documentAdapter.IsConfigured)
        {
            _logger.LogDebug(
                "Documents adapter unavailable; attachment reference {AttachmentId} remains Pending",
                request.AttachmentReferenceId);
            return new AttachmentDispatchResult
            {
                Success    = false,
                WasSkipped = true,
                SkipReason = "Documents adapter not configured.",
            };
        }

        var mimeType = string.IsNullOrWhiteSpace(request.MimeType)
            ? "application/octet-stream"
            : request.MimeType;

        try
        {
            var upload = await _documentAdapter.UploadAttachmentStreamAsync(
                request.TenantId,
                request.FileName,
                mimeType,
                Stream.Null,
                _opts.MaxAttachmentBytes,
                ct);

            if (upload is null || !upload.IsAvailable)
            {
                reference.MarkFailed("UPLOAD_UNAVAILABLE", "Documents adapter did not accept the upload.");
                await _db.SaveChangesAsync(ct);
                return new AttachmentDispatchResult
                {
                    Success          = false,
                    ErrorCode        = "UPLOAD_UNAVAILABLE",
                    SafeErrorSummary = "Documents adapter did not accept the upload.",
                };
            }

            reference.MarkDispatched(upload.DocumentId, upload.ContentHash);
            await _db.SaveChangesAsync(ct);

            return new AttachmentDispatchResult
            {
                Success             = true,
                DocumentReferenceId = upload.DocumentId,
                ContentHash         = upload.ContentHash,
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Attachment dispatch failed for attachmentId={AttachmentId} tenantId={TenantId}",
                request.AttachmentReferenceId, request.TenantId);
            reference.MarkFailed("DISPATCH_ERROR", "Attachment dispatch failed.");
            await _db.SaveChangesAsync(ct);
            return new AttachmentDispatchResult
            {
                Success          = false,
                ErrorCode        = "DISPATCH_ERROR",
                SafeErrorSummary = "Attachment dispatch failed.",
            };
        }
    }
}
