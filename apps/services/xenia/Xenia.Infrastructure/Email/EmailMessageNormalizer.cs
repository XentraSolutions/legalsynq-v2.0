using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xenia.Application.Email.Ingestion;
using Xenia.Domain.Email;

namespace Xenia.Infrastructure.Email;

/// <summary>
/// Normalizes provider message envelopes into the canonical EmailMessage model.
///
/// Security responsibilities:
/// - Strips sensitive headers (Authorization, Cookie, X-Auth-Token, etc.)
/// - Caps body sizes
/// - Normalizes email addresses to lower-case
/// - Converts all timestamps to UTC
/// - Generates a safe body preview (no HTML, no scripts)
/// - Computes content hash for duplicate fallback
/// - Never logs message body content
/// </summary>
internal sealed class EmailMessageNormalizer : IMessageNormalizer
{
    private static readonly HashSet<string> _sensitiveHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization", "Cookie", "Set-Cookie", "X-Auth-Token", "X-API-Key",
        "X-MS-Exchange-Organization-SCL", "X-Google-DKIM-Signature",
        "X-Forwarded-Authorization", "Proxy-Authorization",
        "X-Original-Authentication-Results", "ARC-Authentication-Results",
    };

    private readonly XeniaIngestionOptions _opts;
    private readonly ILogger<EmailMessageNormalizer> _logger;

    public EmailMessageNormalizer(
        IOptions<XeniaIngestionOptions> opts,
        ILogger<EmailMessageNormalizer> logger)
    {
        _opts   = opts.Value;
        _logger = logger;
    }

    public NormalizationResult Normalize(
        ProviderMessageEnvelope envelope,
        EmailProviderType providerType,
        string? correlationId = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(envelope.ProviderMessageId))
                return Fail("PROVIDER_MESSAGE_ID_REQUIRED", "Provider message ID is required.");

            var bodyText    = Truncate(envelope.BodyText, _opts.MaxMessageBodyBytes);
            var bodyHtml    = Truncate(envelope.BodyHtml, _opts.MaxMessageBodyBytes);
            var bodyType    = DetermineBodyType(bodyText, bodyHtml);
            var bodyPreview = GeneratePreview(bodyText, bodyHtml, _opts.BodyPreviewLength);

            var sanitizedHeaders  = SanitizeHeaders(envelope.Headers, _opts.MaxHeaderBytes);
            var headersJson       = sanitizedHeaders.Count > 0
                ? JsonSerializer.Serialize(sanitizedHeaders)
                : null;

            var providerMeta = envelope.ProviderMetadata.Count > 0
                ? JsonSerializer.Serialize(envelope.ProviderMetadata)
                : null;

            var contentHash = ComputeContentHash(
                envelope.ProviderMessageId, envelope.InternetMessageId,
                envelope.FromAddress, envelope.Subject, envelope.SentAt);

            var recipients = BuildRecipients(envelope);
            var replyToCsv = ReplyToCsv(envelope.ReplyTo);

            var msg = new NormalizedMessage
            {
                ProviderMessageId   = envelope.ProviderMessageId.Trim(),
                InternetMessageId   = envelope.InternetMessageId?.Trim(),
                ThreadId            = Truncate(envelope.ThreadId, 500),
                ConversationId      = Truncate(envelope.ConversationId, 500),
                Subject             = Truncate(envelope.Subject, EmailMessage.SubjectMaxLength),
                FromAddress         = NormalizeAddress(envelope.FromAddress),
                FromName            = Truncate(envelope.FromName, EmailMessage.DisplayNameMaxLength),
                SenderAddress       = NormalizeAddress(envelope.SenderAddress),
                SenderName          = Truncate(envelope.SenderName, EmailMessage.DisplayNameMaxLength),
                ReplyToAddressesCsv = replyToCsv,
                SentAt              = ToUtc(envelope.SentAt),
                ReceivedAt          = ToUtc(envelope.ReceivedAt),
                Importance          = envelope.Importance,
                IsRead              = envelope.IsRead,
                HasAttachments      = envelope.Attachments.Count > 0,
                AttachmentCount     = envelope.Attachments.Count,
                BodyType            = bodyType,
                BodyText            = bodyText,
                BodyHtml            = bodyHtml,
                BodyPreview         = bodyPreview,
                HeadersJson         = headersJson,
                ProviderMetadataJson= providerMeta,
                ContentHash         = contentHash,
                Recipients          = recipients,
                Attachments         = envelope.Attachments,
            };

            return new NormalizationResult { IsValid = true, Message = msg };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Normalization error for provider={Provider} correlationId={CorrelationId}",
                providerType, correlationId);
            return Fail("NORMALIZATION_ERROR", "Message could not be normalized.");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static NormalizationResult Fail(string code, string summary) =>
        new() { IsValid = false, ErrorCode = code, SafeErrorSummary = summary };

    private static EmailMessageBodyType DetermineBodyType(string? text, string? html) =>
        (text is not null, html is not null) switch
        {
            (true, true)  => EmailMessageBodyType.Both,
            (false, true) => EmailMessageBodyType.Html,
            (true, false) => EmailMessageBodyType.Plain,
            _             => EmailMessageBodyType.Unknown,
        };

    private static string GeneratePreview(string? bodyText, string? bodyHtml, int maxLen)
    {
        var source = bodyText ?? StripHtmlTags(bodyHtml);
        if (string.IsNullOrWhiteSpace(source)) return string.Empty;
        var collapsed = System.Text.RegularExpressions.Regex.Replace(source.Trim(), @"\s+", " ");
        return collapsed.Length > maxLen ? collapsed[..maxLen] : collapsed;
    }

    internal static string StripHtmlTags(string? html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;
        // Remove script/style blocks entirely
        var noScript = System.Text.RegularExpressions.Regex.Replace(html,
            @"<(script|style)[^>]*>[\s\S]*?</(script|style)>",
            string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        // Strip remaining tags
        return System.Text.RegularExpressions.Regex.Replace(noScript, @"<[^>]+>", string.Empty);
    }

    private static Dictionary<string, string> SanitizeHeaders(
        IReadOnlyDictionary<string, string> headers,
        int maxBytes)
    {
        var result  = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int current = 0;
        foreach (var kv in headers)
        {
            if (_sensitiveHeaders.Contains(kv.Key)) continue;
            var entry = kv.Key.Length + kv.Value.Length + 4; // key, value, quotes, colon/newline
            if (current + entry > maxBytes) break;
            result[kv.Key] = kv.Value;
            current += entry;
        }
        return result;
    }

    private static string? NormalizeAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return null;
        var trimmed = address.Trim();
        return trimmed.Length > EmailMessage.AddressMaxLength
            ? trimmed[..EmailMessage.AddressMaxLength].ToLowerInvariant()
            : trimmed.ToLowerInvariant();
    }

    private static DateTime? ToUtc(DateTime? dt)
    {
        if (dt is null) return null;
        return dt.Value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(dt.Value, DateTimeKind.Utc)
            : dt.Value.ToUniversalTime();
    }

    private static string? Truncate(string? value, int maxLen)
    {
        if (value is null) return null;
        return value.Length > maxLen ? value[..maxLen] : value;
    }

    private static List<NormalizedRecipient> BuildRecipients(ProviderMessageEnvelope envelope)
    {
        var list = new List<NormalizedRecipient>();
        void Add(IReadOnlyList<ProviderRecipient> src, EmailRecipientType type)
        {
            foreach (var r in src)
            {
                var addr = NormalizeAddress(r.EmailAddress);
                if (string.IsNullOrEmpty(addr)) continue;
                list.Add(new NormalizedRecipient(type, addr, r.DisplayName?.Trim()));
            }
        }
        Add(envelope.To, EmailRecipientType.To);
        Add(envelope.Cc, EmailRecipientType.Cc);
        Add(envelope.Bcc, EmailRecipientType.Bcc);
        Add(envelope.ReplyTo, EmailRecipientType.ReplyTo);
        return list;
    }

    private static string? ReplyToCsv(IReadOnlyList<ProviderRecipient> replyTo)
    {
        if (replyTo.Count == 0) return null;
        var csv = string.Join(",", replyTo
            .Where(r => !string.IsNullOrWhiteSpace(r.EmailAddress))
            .Select(r => NormalizeAddress(r.EmailAddress)));
        return Truncate(csv, EmailMessage.ReplyToAddressesMaxLength);
    }

    private static string ComputeContentHash(
        string? providerMessageId,
        string? internetMessageId,
        string? fromAddress,
        string? subject,
        DateTime? sentAt)
    {
        var canonical = $"{providerMessageId}|{internetMessageId}|{fromAddress?.ToLowerInvariant()}|{subject}|{sentAt?.ToString("o")}";
        var bytes     = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
