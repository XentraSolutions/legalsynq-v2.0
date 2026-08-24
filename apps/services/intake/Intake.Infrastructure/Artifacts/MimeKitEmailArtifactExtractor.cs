using Intake.Application.Artifacts;
using MimeKit;

namespace Intake.Infrastructure.Artifacts;

public sealed class MimeKitEmailArtifactExtractor : IEmailArtifactExtractor
{
    public EmailArtifactExtractionResult Extract(
        string rawMessage,
        EmailArtifactProcessingOptions options)
    {
        var rawBytes = System.Text.Encoding.UTF8.GetByteCount(rawMessage);
        if (rawBytes > options.MaxMimeInputBytes)
        {
            return new(
                [],
                Intake.Domain.Artifacts.IntakeArtifactFailureCodes.MimeInputTooLarge,
                "The captured MIME message exceeds the configured processing limit.");
        }

        var parts = new List<ExtractedEmailPart>();
        long totalDecodedBytes = 0;
        try
        {
            using var input = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(rawMessage), writable: false);
            var message = MimeMessage.Load(input);
            Walk(message.Body, depth: 0, parts, options, ref totalDecodedBytes);
            return new(parts, null, null);
        }
        catch (ExtractionLimitException exception)
        {
            return new([], exception.Code, exception.Message);
        }
        catch (Exception)
        {
            return new(
                [],
                Intake.Domain.Artifacts.IntakeArtifactFailureCodes.MimeParseFailed,
                "The captured MIME message could not be parsed.");
        }
    }

    private static void Walk(
        MimeEntity? entity,
        int depth,
        ICollection<ExtractedEmailPart> parts,
        EmailArtifactProcessingOptions options,
        ref long totalDecodedBytes)
    {
        if (entity is null)
            return;

        if (depth > options.MaxMimeDepth)
        {
            throw new ExtractionLimitException(
                Intake.Domain.Artifacts.IntakeArtifactFailureCodes.MimeDepthExceeded,
                "The MIME nesting depth exceeds the configured processing limit.");
        }

        if (entity is Multipart multipart)
        {
            foreach (var child in multipart)
                Walk(child, depth + 1, parts, options, ref totalDecodedBytes);
            return;
        }

        if (entity is not MimePart part)
            return;

        var disposition = part.ContentDisposition?.Disposition;
        var isInline = string.Equals(disposition, "inline", StringComparison.OrdinalIgnoreCase) ||
                       !string.IsNullOrWhiteSpace(part.ContentId);
        var hasFileName = !string.IsNullOrWhiteSpace(part.FileName);
        var isAttachment = part.IsAttachment || isInline || hasFileName;
        if (!isAttachment)
            return;

        if (parts.Count >= options.MaxArtifactsPerEmail)
        {
            throw new ExtractionLimitException(
                Intake.Domain.Artifacts.IntakeArtifactFailureCodes.ArtifactCountExceeded,
                "The MIME message contains more artifacts than the configured limit.");
        }

        var contentType = part.ContentType?.MimeType;
        if (string.IsNullOrWhiteSpace(contentType))
            contentType = "application/octet-stream";

        using var output = new LimitedMemoryStream(options.MaxArtifactBytes);
        part.Content.DecodeTo(output);
        var content = output.ToArray();
        if (content.Length == 0)
            return;
        if (totalDecodedBytes + content.LongLength > options.MaxTotalArtifactBytesPerEmail)
        {
            throw new ExtractionLimitException(
                Intake.Domain.Artifacts.IntakeArtifactFailureCodes.ArtifactBytesExceeded,
                "The MIME artifacts exceed the configured aggregate size limit.");
        }
        totalDecodedBytes += content.LongLength;

        var ordinal = parts.Count;
        var originalFileName = string.IsNullOrWhiteSpace(part.FileName)
            ? $"attachment-{ordinal + 1:D3}"
            : part.FileName!;

        parts.Add(new ExtractedEmailPart(
            ordinal,
            Intake.Domain.Artifacts.IntakeArtifactTypes.Attachment,
            isInline
                ? Intake.Domain.Artifacts.IntakeArtifactRoles.InlineAttachment
                : Intake.Domain.Artifacts.IntakeArtifactRoles.Attachment,
            originalFileName,
            contentType,
            part.ContentId,
            isInline,
            content));
    }

    private sealed class ExtractionLimitException(string code, string message) : Exception(message)
    {
        public string Code { get; } = code;
    }

    private sealed class LimitedMemoryStream(int maxBytes) : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count)
        {
            EnsureCapacity(count);
            base.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            EnsureCapacity(buffer.Length);
            base.Write(buffer);
        }

        private void EnsureCapacity(int incomingBytes)
        {
            if (Length + incomingBytes > maxBytes)
            {
                throw new ExtractionLimitException(
                    Intake.Domain.Artifacts.IntakeArtifactFailureCodes.ArtifactBytesExceeded,
                    "An individual MIME artifact exceeds the configured size limit.");
            }
        }
    }
}