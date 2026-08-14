using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Intake.Application.Configuration;
using Intake.Application.Sources;
using Intake.Contracts.Emails;
using Intake.Contracts.Sources;
using Intake.Domain.Emails;
using Intake.Domain.Sources;
using Microsoft.Extensions.Logging;

namespace Intake.Application.Emails;

public sealed class EmailCaptureService(
    IIntakeSourceRepository sourceRepository,
    IIntakeSourceResolver sourceResolver,
    IIntakeConfigurationService configurationService,
    IInboundEmailRepository repository,
    IIntakeConfigurationAuditSink auditSink,
    EmailCaptureOptions options,
    ILogger<EmailCaptureService> logger) : IEmailCaptureService
{
    private static readonly Regex Sha256Pattern = new(
        "^[A-Fa-f0-9]{64}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task<InboundEmailCaptureResponse> CaptureAsync(
        CaptureInboundEmailCommand command,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var limits = options;
        ValidateLimitsConfiguration(limits);

        if (command.TenantId == Guid.Empty || command.SourceId == Guid.Empty)
            throw IntakeConfigurationException.BadRequest(
                "EMAIL_CAPTURE_SOURCE_CONTEXT_REQUIRED",
                "A trusted tenant and source context are required for email capture.");

        var source = await sourceRepository.FindTenantSourceAsync(
            command.TenantId,
            command.SourceId,
            cancellationToken)
            ?? throw IntakeConfigurationException.NotFound(
                "INTAKE_SOURCE_NOT_FOUND",
                "The registered Intake source was not found for the supplied tenant context.");

        var failureProvider = NormalizeFailureProvider(command.Provider);
        try
        {
        if (!source.IsActive)
            throw IntakeConfigurationException.BadRequest(
                "INTAKE_SOURCE_INACTIVE",
                "New captures are rejected for an inactive Intake source because delivery-time provenance is unavailable.");

        if (source.SourceType != IntakeSourceTypes.Email)
            throw IntakeConfigurationException.BadRequest(
                "EMAIL_CAPTURE_SOURCE_TYPE_UNSUPPORTED",
                "Only EMAIL Intake sources can capture inbound email.");

        if (source.ConfigurationVersion != command.SourceConfigurationVersion)
            throw IntakeConfigurationException.Conflict(
                "STALE_SOURCE_CONFIGURATION_VERSION",
                "The source configuration version is no longer active for capture.");

        var resolvedSource = await sourceResolver.ResolveByEmailAddressAsync(
            source.EmailAddress,
            cancellationToken);

        if (resolvedSource.SourceId != source.Id ||
            resolvedSource.TenantId != command.TenantId ||
            resolvedSource.SourceConfigurationVersion != command.SourceConfigurationVersion)
        {
            throw IntakeConfigurationException.Conflict(
                "EMAIL_CAPTURE_SOURCE_PROVENANCE_MISMATCH",
                "The trusted source provenance does not match the current registered source.");
        }

        var provider = NormalizeRequiredCode(command.Provider, "EMAIL_CAPTURE_PROVIDER_REQUIRED");
        var purpose = NormalizeRequiredCode(command.Purpose, "EMAIL_CAPTURE_PURPOSE_REQUIRED");
        var profileCode = NormalizeRequiredCode(
            command.ProcessingProfileCode,
            "EMAIL_CAPTURE_PROFILE_REQUIRED");

        if (!string.Equals(provider, source.Provider, StringComparison.Ordinal) ||
            !string.Equals(purpose, source.Purpose, StringComparison.Ordinal) ||
            !string.Equals(profileCode, source.ProcessingProfileCode, StringComparison.Ordinal))
        {
            throw IntakeConfigurationException.BadRequest(
                "EMAIL_CAPTURE_SOURCE_PROVENANCE_MISMATCH",
                "Capture provenance must match the registered source provider, purpose, and processing profile.");
        }

        var resolvedConfiguration = await configurationService.ResolveAsync(
            source.TenantId,
            source.ProcessingProfileCode,
            cancellationToken);

        if (command.TenantConfigurationVersion.HasValue &&
            command.TenantConfigurationVersion.Value != resolvedConfiguration.TenantConfigurationVersion)
        {
            throw IntakeConfigurationException.Conflict(
                "EMAIL_CAPTURE_CONFIGURATION_VERSION_MISMATCH",
                "The supplied tenant configuration version is not current.");
        }

        if (command.TenantProfileConfigurationVersion.HasValue &&
            command.TenantProfileConfigurationVersion.Value != resolvedConfiguration.TenantProfileConfigurationVersion)
        {
            throw IntakeConfigurationException.Conflict(
                "EMAIL_CAPTURE_PROFILE_CONFIGURATION_VERSION_MISMATCH",
                "The supplied tenant profile configuration version is not current.");
        }

        var receivedAt = RequireTimestamp(command.ReceivedAt, "ReceivedAt");
        var capturedAt = DateTimeOffset.UtcNow;
        var providerMessageId = NormalizeOptionalIdentity(command.ProviderMessageId, "ProviderMessageId");
        var providerThreadId = NormalizeOptionalIdentity(command.ProviderThreadId, "ProviderThreadId");
        var internetMessageId = NormalizeOptionalIdentity(command.InternetMessageId, "InternetMessageId");
        var inReplyToMessageId = NormalizeOptionalIdentity(command.InReplyToMessageId, "InReplyToMessageId");
        var idempotencyKey = BuildIdempotencyKey(
            source.Id,
            provider,
            providerMessageId,
            internetMessageId);

        ValidateOptionalAddress(command.FromAddress, "FromAddress");
        ValidateOptionalAddress(command.SenderAddress, "SenderAddress");
        ValidateOptionalAddress(command.ReplyToAddress, "ReplyToAddress");
        ValidateDisplayName(command.FromDisplayName, "FromDisplayName");
        ValidateDisplayName(command.SenderDisplayName, "SenderDisplayName");
        ValidateDisplayName(command.ReplyToDisplayName, "ReplyToDisplayName");
        ValidateText(command.Subject, limits.MaxSubjectLength, "Subject");

        var recipients = BuildRecipients(command.Recipients, limits);
        var attachments = BuildAttachments(command.Attachments, limits);
        var references = command.References ?? [];
        ValidateReferences(references);
        var headers = command.Headers ?? [];
        ValidateHeaders(headers);

        var textBody = ValidateBody(command.TextBody, limits.MaxTextBodyBytes, "TextBody");
        var htmlBody = ValidateBody(command.HtmlBody, limits.MaxHtmlBodyBytes, "HtmlBody");
        var headersJson = InboundEmailCaptureSerialization.SerializeHeaders(headers);
        var referencesJson = InboundEmailCaptureSerialization.SerializeReferences(references);
        var headerBytes = Encoding.UTF8.GetByteCount(headersJson);
        if (headerBytes > limits.MaxHeaderBytes)
            throw SizeLimit("headers");

        var rawBytes = command.RawMessage is null
            ? null
            : Encoding.UTF8.GetBytes(command.RawMessage);
        if (rawBytes is not null && rawBytes.LongLength > limits.MaxInboundMessageBytes)
            throw SizeLimit("raw message");

        var estimatedMessageBytes =
            (rawBytes?.LongLength ?? 0) +
            (textBody is null ? 0 : Encoding.UTF8.GetByteCount(textBody)) +
            (htmlBody is null ? 0 : Encoding.UTF8.GetByteCount(htmlBody)) +
            headerBytes;
        if (estimatedMessageBytes > limits.MaxInboundMessageBytes)
            throw SizeLimit("inbound message");

        var email = new InboundEmail
        {
            Id = Guid.CreateVersion7(),
            TenantId = source.TenantId,
            OrgId = source.OrgId,
            TenantIntakeSourceId = source.Id,
            SourceConfigurationVersion = source.ConfigurationVersion,
            Purpose = purpose,
            ProcessingProfileCode = profileCode,
            TenantConfigurationVersion = resolvedConfiguration.TenantConfigurationVersion,
            TenantProfileConfigurationVersion = resolvedConfiguration.TenantProfileConfigurationVersion,
            Provider = provider,
            ProviderMessageId = providerMessageId,
            ProviderThreadId = providerThreadId,
            InternetMessageId = internetMessageId,
            InReplyToMessageId = inReplyToMessageId,
            ReferencesJson = referencesJson,
            ReceivedAt = receivedAt,
            ProviderCreatedAt = command.ProviderCreatedAt,
            CapturedAt = capturedAt,
            FromAddress = NormalizeOptionalDisplayAddress(command.FromAddress),
            FromDisplayName = command.FromDisplayName?.Trim(),
            SenderAddress = NormalizeOptionalDisplayAddress(command.SenderAddress),
            SenderDisplayName = command.SenderDisplayName?.Trim(),
            ReplyToAddress = NormalizeOptionalDisplayAddress(command.ReplyToAddress),
            ReplyToDisplayName = command.ReplyToDisplayName?.Trim(),
            Subject = command.Subject,
            TextBody = textBody,
            HtmlBody = htmlBody,
            HeadersJson = headersJson,
            RawMessageContent = command.RawMessage,
            RawMessageHash = rawBytes is null ? null : Convert.ToHexString(SHA256.HashData(rawBytes)),
            RawMessageSizeBytes = rawBytes?.LongLength,
            HasAttachments = attachments.Count > 0,
            AttachmentCount = attachments.Count,
            CaptureStatus = InboundEmailCaptureStatuses.Captured,
            ProcessingStatus = InboundEmailProcessingStatuses.NotStarted,
            IdempotencyKey = idempotencyKey,
            CreatedAt = capturedAt,
            UpdatedAt = capturedAt,
        };

        var persistence = await repository.PersistCaptureAsync(
            email,
            recipients,
            attachments,
            cancellationToken);
        var canonical = await repository.FindTenantEmailAsync(
            source.TenantId,
            persistence.EmailId,
            cancellationToken)
            ?? throw new InvalidOperationException("Persisted inbound email could not be reloaded.");

        RecordAudit(
            canonical,
            persistence.IsDuplicate ? "EMAIL_CAPTURE_DUPLICATE" : "EMAIL_CAPTURED",
            correlationId);
        logger.LogInformation(
            "Inbound email capture {Result} CorrelationId={CorrelationId} TenantId={TenantId} SourceId={SourceId} EmailRecordId={EmailRecordId} Provider={Provider} ProviderMessageId={ProviderMessageId} InternetMessageId={InternetMessageId} Purpose={Purpose} ProcessingProfileCode={ProcessingProfileCode} CaptureStatus={CaptureStatus} AttachmentCount={AttachmentCount}",
            persistence.IsDuplicate ? "duplicate" : "succeeded",
            correlationId,
            canonical.TenantId,
            canonical.TenantIntakeSourceId,
            canonical.Id,
            canonical.Provider,
            canonical.ProviderMessageId,
            canonical.InternetMessageId,
            canonical.Purpose,
            canonical.ProcessingProfileCode,
            canonical.CaptureStatus,
            canonical.AttachmentCount);

        return new(InboundEmailDetailMapper.Map(canonical), persistence.IsDuplicate);
        }
        catch (IntakeConfigurationException exception)
        {
            await RecordCaptureFailureAsync(
                source,
                failureProvider,
                exception.Code,
                correlationId,
                cancellationToken);
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await RecordCaptureFailureAsync(
                source,
                failureProvider,
                "EMAIL_CAPTURE_PERSISTENCE_FAILED",
                correlationId,
                cancellationToken);
            throw;
        }
    }

    private static List<InboundEmailRecipient> BuildRecipients(
        IEnumerable<InboundEmailRecipientInput> inputs,
        EmailCaptureOptions limits)
    {
        var values = inputs?.ToArray() ?? [];
        if (values.Length == 0 || values.Length > limits.MaxRecipientsPerMessage)
            throw SizeLimit("recipients");

        return values.Select((input, index) =>
        {
            var type = NormalizeRequiredCode(input.RecipientType, "EMAIL_RECIPIENT_TYPE_REQUIRED");
            if (type is not InboundEmailRecipientTypes.To and
                not InboundEmailRecipientTypes.Cc and
                not InboundEmailRecipientTypes.Bcc)
            {
                throw IntakeConfigurationException.BadRequest(
                    "EMAIL_RECIPIENT_TYPE_UNSUPPORTED",
                    $"Recipient type '{input.RecipientType}' is not supported.");
            }

            var normalized = EmailAddressNormalizer.Normalize(input.EmailAddress);
            ValidateDisplayName(input.DisplayName, "RecipientDisplayName");
            return new InboundEmailRecipient
            {
                Id = Guid.CreateVersion7(),
                RecipientType = type,
                EmailAddress = input.EmailAddress.Trim(),
                NormalizedEmailAddress = normalized,
                DisplayName = input.DisplayName?.Trim(),
                Ordinal = index,
            };
        }).ToList();
    }

    private static List<InboundEmailAttachmentMetadata> BuildAttachments(
        IEnumerable<InboundEmailAttachmentInput> inputs,
        EmailCaptureOptions limits)
    {
        var values = inputs?.ToArray() ?? [];
        if (values.Length > limits.MaxAttachmentMetadataCount)
            throw SizeLimit("attachment metadata");

        return values.Select((input, index) =>
        {
            if (string.IsNullOrWhiteSpace(input.FileName) || input.FileName.Length > 1024)
                throw IntakeConfigurationException.BadRequest(
                    "INVALID_ATTACHMENT_METADATA",
                    "Attachment metadata requires a filename of at most 1024 characters.");
            if (input.SizeBytes is < 0)
                throw IntakeConfigurationException.BadRequest(
                    "INVALID_ATTACHMENT_METADATA",
                    "Attachment size cannot be negative.");
            if (input.Sha256 is not null && !Sha256Pattern.IsMatch(input.Sha256))
                throw IntakeConfigurationException.BadRequest(
                    "INVALID_ATTACHMENT_METADATA",
                    "Attachment Sha256 must be a 64-character hexadecimal value.");

            return new InboundEmailAttachmentMetadata
            {
                Id = Guid.CreateVersion7(),
                ProviderAttachmentId = LimitOptional(input.ProviderAttachmentId, 512, "ProviderAttachmentId"),
                FileName = input.FileName.Trim(),
                ContentType = LimitOptional(input.ContentType, 255, "ContentType"),
                ContentDisposition = LimitOptional(input.ContentDisposition, 255, "ContentDisposition"),
                ContentId = LimitOptional(input.ContentId, 512, "ContentId"),
                SizeBytes = input.SizeBytes,
                Sha256 = input.Sha256?.Trim().ToUpperInvariant(),
                IsInline = input.IsInline,
                Ordinal = index,
            };
        }).ToList();
    }

    private static void ValidateHeaders(IEnumerable<InboundEmailHeaderInput> headers)
    {
        foreach (var header in headers)
        {
            if (string.IsNullOrWhiteSpace(header.Name) || header.Name.Length > 256)
                throw IntakeConfigurationException.BadRequest(
                    "INVALID_EMAIL_HEADER",
                    "Header names must be non-empty and at most 256 characters.");
            if (header.Values is null || header.Values.Any(value => value is null || value.Length > 8192))
                throw IntakeConfigurationException.BadRequest(
                    "INVALID_EMAIL_HEADER",
                    "Header values must be present and at most 8192 characters.");
        }
    }

    private static void ValidateReferences(IEnumerable<string> references)
    {
        if (references.Any(reference => string.IsNullOrWhiteSpace(reference) || reference.Length > 998))
            throw IntakeConfigurationException.BadRequest(
                "INVALID_EMAIL_REFERENCE",
                "Message references must be non-empty values of at most 998 characters.");
    }

    private static string? ValidateBody(string? body, long maxBytes, string field)
    {
        if (body is null)
            return null;
        if (Encoding.UTF8.GetByteCount(body) > maxBytes)
            throw SizeLimit(field);
        return body;
    }

    private static void ValidateText(string? value, int maxLength, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength)
            throw IntakeConfigurationException.BadRequest(
                "EMAIL_CAPTURE_FIELD_TOO_LONG",
                $"{field} is required and must be at most {maxLength} characters.");
    }

    private static void ValidateOptionalAddress(string? address, string field)
    {
        if (address is null or "")
            return;
        _ = EmailAddressNormalizer.Normalize(address);
        LimitOptional(address, 320, field);
    }

    private static string? NormalizeOptionalDisplayAddress(string? address) =>
        string.IsNullOrWhiteSpace(address) ? null : address.Trim();

    private static void ValidateDisplayName(string? value, string field) =>
        _ = LimitOptional(value, 512, field);

    private static string? LimitOptional(string? value, int maxLength, string field)
    {
        if (value is not null && value.Length > maxLength)
            throw IntakeConfigurationException.BadRequest(
                "EMAIL_CAPTURE_FIELD_TOO_LONG",
                $"{field} exceeds the maximum allowed length of {maxLength} characters.");
        return value;
    }

    private static DateTimeOffset RequireTimestamp(DateTimeOffset value, string field)
    {
        if (value == default)
            throw IntakeConfigurationException.BadRequest(
                "EMAIL_CAPTURE_TIMESTAMP_REQUIRED",
                $"{field} is required.");
        return value.ToUniversalTime();
    }

    private static string NormalizeRequiredCode(string? value, string errorCode)
    {
        var normalized = value?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalized.Length == 0)
            throw IntakeConfigurationException.BadRequest(errorCode, "A required capture value was not supplied.");
        return normalized;
    }

    private static string? NormalizeFailureProvider(string? value)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : normalized[..Math.Min(normalized.Length, 64)];
    }

    private static string? NormalizeOptionalIdentity(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return LimitOptional(value.Trim(), 768, field);
    }

    private static string BuildIdempotencyKey(
        Guid sourceId,
        string provider,
        string? providerMessageId,
        string? internetMessageId)
    {
        if (providerMessageId is null && internetMessageId is null)
            throw IntakeConfigurationException.BadRequest(
                "EMAIL_IDENTITY_REQUIRED",
                "ProviderMessageId or InternetMessageId is required for idempotent capture.");

        var prefix = providerMessageId is not null ? "P" : "I";
        var identity = providerMessageId ?? internetMessageId!;
        var material = $"{prefix}|{sourceId:N}|{provider}|{identity}";
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
        return $"{prefix}:{sourceId:N}:{provider}:{digest}";
    }

    private static IntakeConfigurationException SizeLimit(string field) =>
        IntakeConfigurationException.BadRequest(
            "EMAIL_CAPTURE_SIZE_LIMIT_EXCEEDED",
            $"The {field} exceeds the configured Intake email capture limit.");

    private static void ValidateLimitsConfiguration(EmailCaptureOptions limits)
    {
        if (limits.MaxInboundMessageBytes <= 0 ||
            limits.MaxTextBodyBytes <= 0 ||
            limits.MaxHtmlBodyBytes <= 0 ||
            limits.MaxHeaderBytes <= 0 ||
            limits.MaxAttachmentMetadataCount < 0 ||
            limits.MaxRecipientsPerMessage <= 0)
        {
            throw new InvalidOperationException("Intake email capture limits must be positive.");
        }
    }

    private void RecordAudit(
        InboundEmail email,
        string operation,
        string? correlationId) =>
        _ = auditSink.RecordAsync(
            new ConfigurationAuditEntry(
                email.TenantId,
                "InboundEmail",
                email.Id.ToString(),
                operation,
                null,
                1,
                null,
                correlationId,
                new
                {
                    email.TenantIntakeSourceId,
                    email.Provider,
                    email.ProviderMessageId,
                    email.Purpose,
                    email.ProcessingProfileCode,
                    email.CaptureStatus,
                    email.DuplicateCaptureCount,
                }),
            CancellationToken.None);

    private async Task RecordCaptureFailureAsync(
        TenantIntakeSource source,
        string? provider,
        string failureCode,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var failure = new InboundEmailCaptureFailure
        {
            Id = Guid.CreateVersion7(),
            TenantId = source.TenantId,
            TenantIntakeSourceId = source.Id,
            Provider = provider,
            FailureCode = failureCode,
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = correlationId,
        };

        try
        {
            await repository.RecordCaptureFailureAsync(failure, cancellationToken);
            _ = auditSink.RecordAsync(
                new ConfigurationAuditEntry(
                    source.TenantId,
                    "InboundEmailCapture",
                    source.Id.ToString(),
                    "EMAIL_CAPTURE_FAILED",
                    null,
                    1,
                    null,
                    correlationId,
                    new
                    {
                        source.Id,
                        failure.FailureCode,
                        failure.Provider,
                    }),
                CancellationToken.None);
            logger.LogWarning(
                "Inbound email capture failed. CorrelationId={CorrelationId} TenantId={TenantId} SourceId={SourceId} Provider={Provider} FailureCode={FailureCode}",
                correlationId,
                source.TenantId,
                source.Id,
                provider,
                failureCode);
        }
        catch (Exception auditException) when (auditException is not OperationCanceledException)
        {
            logger.LogWarning(
                auditException,
                "Inbound email capture failure telemetry could not be persisted. CorrelationId={CorrelationId} TenantId={TenantId} SourceId={SourceId} FailureCode={FailureCode}",
                correlationId,
                source.TenantId,
                source.Id,
                failureCode);
        }
    }
}