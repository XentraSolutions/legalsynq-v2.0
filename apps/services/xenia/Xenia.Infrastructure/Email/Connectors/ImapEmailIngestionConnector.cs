using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Xenia.Application.Email;
using Xenia.Application.Email.Ingestion;
using Xenia.Domain.Email;

namespace Xenia.Infrastructure.Email.Connectors;

/// <summary>
/// Real IMAP email ingestion connector using MailKit.
///
/// Implements full IMAP protocol ingestion:
/// - TLS-required connections (SecureSocketOptions.SslOnConnect)
/// - Read-only folder access (OpenAsync with FolderAccess.ReadOnly)
/// - UID-based cursor (UIDVALIDITY + max UID seen)
/// - Paginated message fetching
/// - MIME parsing for body, recipients, and attachment descriptors
/// - No message modification (no mark-as-read, no delete, no move)
/// - No binary attachment storage — only ProviderAttachmentDescriptor metadata
///
/// Cursor format (stored protected in DB):
///   "UIDVALIDITY:MaxUID" — e.g. "1234567890:42"
///   On UIDVALIDITY change: cursor is invalidated and full re-sync starts.
///
/// Authentication: username + password resolved from ISecretReferenceService.
/// Secret values are never logged.
/// </summary>
internal sealed class ImapEmailIngestionConnector : IEmailIngestionConnector
{
    private readonly ISecretReferenceService _secretService;
    private readonly XeniaIngestionOptions _opts;
    private readonly ILogger<ImapEmailIngestionConnector> _logger;

    public EmailProviderType ProviderType => EmailProviderType.Imap;

    public ImapEmailIngestionConnector(
        ISecretReferenceService secretService,
        IOptions<XeniaIngestionOptions> opts,
        ILogger<ImapEmailIngestionConnector> logger)
    {
        _secretService = secretService;
        _opts          = opts.Value;
        _logger        = logger;
    }

    public ProviderSyncCapabilities GetIngestionCapabilities() =>
        new()
        {
            ProviderType        = EmailProviderType.Imap,
            CanFetchMessages    = true,
            CanFetchAttachments = true,
            SupportsDeltaSync   = true,
            SupportsCancel      = true,
            UnavailableReason   = null,
        };

    public async Task<ProviderInitialCursorResult> GetInitialCursorAsync(
        EmailSourceConnectorContext context,
        CancellationToken ct = default)
    {
        try
        {
            using var client = await ConnectAndAuthenticateAsync(context, ct);
            var inbox   = client.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadOnly, ct);

            var uidValidity = inbox.UidValidity;
            var cursor = new ProviderSyncCursor
            {
                CursorType   = SyncCursorType.ImapUidCursor,
                RawValue     = $"{uidValidity}:0",
                SafeSummary  = $"IMAP initial cursor UIDVALIDITY={uidValidity}",
            };
            await client.DisconnectAsync(true, ct);
            return new ProviderInitialCursorResult { Success = true, Cursor = cursor };
        }
        catch (OperationCanceledException)
        {
            return new ProviderInitialCursorResult
            {
                Success = false, ErrorCode = "CANCELLED",
                SafeErrorSummary = "Initial cursor retrieval was cancelled.",
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "IMAP initial cursor failed for sourceId={SourceId}", context.SourceId);
            return new ProviderInitialCursorResult
            {
                Success = false, ErrorCode = "INITIAL_CURSOR_FAILED",
                SafeErrorSummary = "Failed to connect to IMAP server.",
            };
        }
    }

    public async Task<ProviderFetchPageResult> FetchMessagePageAsync(
        EmailSourceConnectorContext context,
        ProviderSyncCursor? cursor,
        int pageSize,
        CancellationToken ct = default)
    {
        try
        {
            var (uidValidity, minUid) = ParseCursor(cursor?.RawValue);

            using var client = await ConnectAndAuthenticateAsync(context, ct);
            var inbox = client.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadOnly, ct);

            // Validate UIDVALIDITY — invalidate cursor if changed
            if (cursor is not null && inbox.UidValidity != uidValidity)
            {
                _logger.LogWarning(
                    "IMAP UIDVALIDITY changed for sourceId={SourceId}: was={Old} now={New} — cursor invalidated",
                    context.SourceId, uidValidity, inbox.UidValidity);
                await client.DisconnectAsync(true, ct);
                return new ProviderFetchPageResult
                {
                    Success = false, IsInvalidCursor = true,
                    ErrorCode = "UIDVALIDITY_CHANGED",
                    SafeErrorSummary = "IMAP folder UIDVALIDITY changed. Full re-sync required.",
                };
            }

            // Search for messages with UID > minUid
            var searchQuery = minUid > 0
                ? SearchQuery.Uids(new UniqueIdRange(new UniqueId((uint)(minUid + 1)), UniqueId.MaxValue))
                : SearchQuery.All;

            var allUids = await inbox.SearchAsync(searchQuery, ct);

            // Take a page
            var pageUids    = allUids.Take(Math.Min(pageSize, _opts.MaxPageSize)).ToList();
            var hasMore     = allUids.Count > pageUids.Count;

            var fetchItems = MessageSummaryItems.UniqueId
                           | MessageSummaryItems.Envelope
                           | MessageSummaryItems.BodyStructure
                           | MessageSummaryItems.Size;

            var summaries = await inbox.FetchAsync(pageUids, fetchItems, ct);

            var messages = new List<ProviderMessageEnvelope>(summaries.Count);
            uint maxUidSeen = (uint)minUid;

            foreach (var summary in summaries)
            {
                ct.ThrowIfCancellationRequested();
                var envelope = await BuildEnvelopeAsync(inbox, summary, ct);
                if (envelope is not null)
                {
                    messages.Add(envelope);
                    if (summary.UniqueId.Id > maxUidSeen)
                        maxUidSeen = summary.UniqueId.Id;
                }
            }

            ProviderSyncCursor? nextCursor = null;
            if (hasMore || (messages.Count > 0 && maxUidSeen > (uint)minUid))
            {
                nextCursor = new ProviderSyncCursor
                {
                    CursorType  = SyncCursorType.ImapUidCursor,
                    RawValue    = $"{inbox.UidValidity}:{maxUidSeen}",
                    SafeSummary = $"IMAP UIDVALIDITY={inbox.UidValidity} maxUID={maxUidSeen}",
                };
            }

            await client.DisconnectAsync(true, ct);

            return new ProviderFetchPageResult
            {
                Success = true,
                Page    = new ProviderSyncPage
                {
                    Messages   = messages,
                    NextCursor = nextCursor,
                },
            };
        }
        catch (OperationCanceledException)
        {
            return new ProviderFetchPageResult
            {
                Success = false, ErrorCode = "CANCELLED",
                SafeErrorSummary = "IMAP page fetch was cancelled.",
            };
        }
        catch (AuthenticationException)
        {
            return new ProviderFetchPageResult
            {
                Success = false, IsAuthFailure = true,
                ErrorCode = "IMAP_AUTH_FAILED",
                SafeErrorSummary = "IMAP authentication failed.",
            };
        }
        catch (ImapCommandException ex)
        {
            _logger.LogWarning("IMAP command error for sourceId={SourceId}: {Code}",
                context.SourceId, ex.Message.Length > 100 ? ex.Message[..100] : ex.Message);
            return new ProviderFetchPageResult
            {
                Success = false, ErrorCode = "IMAP_COMMAND_ERROR",
                SafeErrorSummary = "IMAP command failed.",
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "IMAP fetch failed for sourceId={SourceId}", context.SourceId);
            return new ProviderFetchPageResult
            {
                Success = false, IsProviderTimeout = true,
                ErrorCode = "IMAP_FETCH_FAILED",
                SafeErrorSummary = "IMAP fetch failed.",
            };
        }
    }

    public async Task<Stream?> GetAttachmentStreamAsync(
        EmailSourceConnectorContext context,
        string providerMessageId,
        string providerAttachmentId,
        CancellationToken ct = default)
    {
        try
        {
            using var client = await ConnectAndAuthenticateAsync(context, ct);
            var inbox = client.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadOnly, ct);

            if (!UniqueId.TryParse(providerMessageId, out var uid))
                return null;

            var message = await inbox.GetMessageAsync(uid, ct);
            if (message is null) return null;

            // Find the attachment part by provider attachment ID (section id)
            MimePart? part = null;
            foreach (var entity in message.BodyParts)
            {
                if (entity is MimePart mp && mp.ContentId == providerAttachmentId)
                {
                    part = mp;
                    break;
                }
            }

            if (part is null) return null;

            var memoryStream = new MemoryStream();
            await part.Content.DecodeToAsync(memoryStream, ct);
            memoryStream.Position = 0;

            await client.DisconnectAsync(true, ct);
            return memoryStream;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "IMAP attachment stream failed for sourceId={SourceId} messageId={MessageId}",
                context.SourceId, providerMessageId);
            return null;
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<ImapClient> ConnectAndAuthenticateAsync(
        EmailSourceConnectorContext context, CancellationToken ct)
    {
        var host = context.IncomingHost
            ?? throw new InvalidOperationException("IMAP host is required.");
        var port  = context.IncomingPort ?? 993;

        if (!_secretService.IsConfigured)
            throw new InvalidOperationException("Secret service is not configured.");

        var resolution = await _secretService.ResolveAsync(context.SecretReferenceId!, ct);
        if (!resolution.Success)
            throw new AuthenticationException("Could not resolve IMAP credentials.");

        // Secret format: "username:password" (base64-encoded or plain)
        var raw  = resolution.Value ?? string.Empty;
        var sep  = raw.IndexOf(':');
        var user = sep > 0 ? raw[..sep] : raw;
        var pass = sep > 0 ? raw[(sep + 1)..] : string.Empty;

        var timeout = (int)_opts.ProviderRequestTimeout.TotalMilliseconds;
        var client  = new ImapClient { Timeout = timeout };

        await client.ConnectAsync(host, port, SecureSocketOptions.SslOnConnect, ct);
        await client.AuthenticateAsync(user, pass, ct);

        return client;
    }

    private static async Task<ProviderMessageEnvelope?> BuildEnvelopeAsync(
        IMailFolder inbox, IMessageSummary summary, CancellationToken ct)
    {
        if (summary.Envelope is null) return null;

        var env = summary.Envelope;
        var uid = summary.UniqueId.ToString();

        // Body
        string? bodyText = null;
        string? bodyHtml = null;
        try
        {
            var msg = await inbox.GetMessageAsync(summary.UniqueId, ct);
            bodyText = msg.TextBody;
            bodyHtml = msg.HtmlBody;
        }
        catch
        {
            // Body fetch failure is non-fatal
        }

        // Attachments
        var attachments = new List<ProviderAttachmentDescriptor>();
        if (summary.Body is BodyPartMultipart multipart)
        {
            foreach (var part in multipart.BodyParts.OfType<BodyPartBasic>())
            {
                if (part.ContentDisposition?.Disposition ==
                    ContentDisposition.Attachment ||
                    part.ContentType.MimeType != "text/plain" &&
                    part.ContentType.MimeType != "text/html")
                {
                    var fileName = part.ContentDisposition?.FileName
                               ?? part.ContentType.Name
                               ?? "attachment";
                    attachments.Add(new ProviderAttachmentDescriptor
                    {
                        ProviderAttachmentId = part.ContentId ?? $"{uid}-{attachments.Count}",
                        FileName             = fileName,
                        MimeType             = part.ContentType.MimeType,
                        SizeBytes            = part.Octets > 0 ? part.Octets : null,
                        IsInline             = part.ContentDisposition?.Disposition ==
                                              ContentDisposition.Inline,
                        ContentId            = part.ContentId,
                    });
                }
            }
        }

        return new ProviderMessageEnvelope
        {
            ProviderMessageId = uid,
            InternetMessageId = env.MessageId,
            Subject           = env.Subject,
            FromAddress       = env.From?.Mailboxes.FirstOrDefault()?.Address,
            FromName          = env.From?.Mailboxes.FirstOrDefault()?.Name,
            SenderAddress     = env.Sender?.OfType<MailboxAddress>().FirstOrDefault()?.Address,
            SenderName        = env.Sender?.OfType<MailboxAddress>().FirstOrDefault()?.Name,
            To                = MapAddressList(env.To),
            Cc                = MapAddressList(env.Cc),
            Bcc               = MapAddressList(env.Bcc),
            ReplyTo           = MapAddressList(env.ReplyTo),
            SentAt            = env.Date?.UtcDateTime,
            ReceivedAt        = env.Date?.UtcDateTime,
            BodyText          = bodyText,
            BodyHtml          = bodyHtml,
            Attachments       = attachments,
        };
    }

    private static IReadOnlyList<ProviderRecipient> MapAddressList(
        InternetAddressList? list)
    {
        if (list is null || list.Count == 0) return [];
        return list.Mailboxes
            .Select(m => new ProviderRecipient(m.Address, m.Name))
            .ToList();
    }

    private static (uint uidValidity, long minUid) ParseCursor(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return (0, 0);

        var parts = rawValue.Split(':');
        if (parts.Length == 2
            && uint.TryParse(parts[0], out var uv)
            && long.TryParse(parts[1], out var uid))
            return (uv, uid);

        return (0, 0);
    }
}
