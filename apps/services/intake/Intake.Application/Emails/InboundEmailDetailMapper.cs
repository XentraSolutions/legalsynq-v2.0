using System.Text.Json;
using Intake.Contracts.Emails;
using Intake.Domain.Emails;

namespace Intake.Application.Emails;

public static class InboundEmailDetailMapper
{
    public static InboundEmailDetailResponse Map(InboundEmail email) =>
        new(
            email.Id,
            email.TenantId,
            email.OrgId,
            email.TenantIntakeSourceId,
            email.SourceConfigurationVersion,
            email.Purpose,
            email.ProcessingProfileCode,
            email.TenantConfigurationVersion,
            email.TenantProfileConfigurationVersion,
            email.Provider,
            email.ProviderMessageId,
            email.ProviderThreadId,
            email.InternetMessageId,
            email.InReplyToMessageId,
            DeserializeReferences(email.ReferencesJson),
            email.ReceivedAt,
            email.ProviderCreatedAt,
            email.CapturedAt,
            email.FromAddress,
            email.FromDisplayName,
            email.SenderAddress,
            email.SenderDisplayName,
            email.ReplyToAddress,
            email.ReplyToDisplayName,
            email.Subject,
            email.TextBody,
            email.HtmlBody,
            email.HeadersJson,
            email.RawMessageSizeBytes,
            email.RawMessageHash,
            email.RawMessageContent is not null,
            email.HasAttachments,
            email.AttachmentCount,
            email.CaptureStatus,
            email.ProcessingStatus,
            email.DuplicateCaptureCount,
            email.Recipients
                .OrderBy(recipient => recipient.Ordinal)
                .Select(recipient => new InboundEmailRecipientResponse(
                    recipient.Id,
                    recipient.RecipientType,
                    recipient.EmailAddress,
                    recipient.NormalizedEmailAddress,
                    recipient.DisplayName,
                    recipient.Ordinal))
                .ToArray(),
            email.AttachmentMetadata
                .OrderBy(attachment => attachment.Ordinal)
                .Select(attachment => new InboundEmailAttachmentResponse(
                    attachment.Id,
                    attachment.ProviderAttachmentId,
                    attachment.FileName,
                    attachment.ContentType,
                    attachment.ContentDisposition,
                    attachment.ContentId,
                    attachment.SizeBytes,
                    attachment.Sha256,
                    attachment.IsInline,
                    attachment.Ordinal))
                .ToArray(),
            email.CreatedAt,
            email.UpdatedAt);

    private static IReadOnlyList<string> DeserializeReferences(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}