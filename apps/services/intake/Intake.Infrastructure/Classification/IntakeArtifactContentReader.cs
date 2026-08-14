using System.Text;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Intake.Application.Classification;
using Intake.Domain.Artifacts;
using Intake.Domain.Classification;

namespace Intake.Infrastructure.Classification;

public sealed class IntakeArtifactContentReader(
    IIntakeDocumentContentClient documentsClient) : IIntakeArtifactContentReader
{
    private static readonly HashSet<string> SupportedTextTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text/plain",
        "text/csv",
        "text/html",
    };

    public async Task<ArtifactContentReadResult> ReadAsync(
        Guid tenantId,
        IntakeArtifact artifact,
        int maxCharacters,
        CancellationToken cancellationToken)
    {
        if (!SupportedTextTypes.Contains(artifact.DeclaredContentType))
        {
            return new(
                false,
                null,
                0,
                ClassificationFailureCodes.UnsupportedContent,
                "Classification currently accepts bounded text artifacts only; OCR and binary extraction are not enabled.",
                false);
        }
        if (!artifact.DocumentsServiceDocumentId.HasValue)
        {
            return new(
                false,
                null,
                0,
                ClassificationFailureCodes.ArtifactNotEligible,
                "The artifact has no Documents Service document reference.",
                true);
        }

        await using var stream = await documentsClient.DownloadAsync(
            tenantId,
            artifact.DocumentsServiceDocumentId.Value,
            cancellationToken);
        if (stream is null)
        {
            return new(
                false,
                null,
                0,
                ClassificationFailureCodes.ProviderUnavailable,
                "The artifact content could not be retrieved from Documents Service.",
                true);
        }

        using var bytes = new MemoryStream();
        var buffer = new byte[81920];
        var maxBytes = Math.Min((long)maxCharacters * 4 + 4, 4_000_004);
        while (true)
        {
            var count = await stream.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (count == 0)
                break;
            if (bytes.Length + count > maxBytes)
                return new(
                    false,
                    null,
                    (int)Math.Min(bytes.Length + count, int.MaxValue),
                    ClassificationFailureCodes.InputTooLarge,
                    "The bounded classification input limit was exceeded.",
                    false);
            await bytes.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
        }

        var observedHash = Convert.ToHexString(SHA256.HashData(bytes.ToArray())).ToLowerInvariant();
        string text;
        try
        {
            text = new UTF8Encoding(false, true).GetString(bytes.ToArray());
        }
        catch (DecoderFallbackException)
        {
            return new(
                false,
                null,
                0,
                ClassificationFailureCodes.UnsupportedContent,
                "The text artifact is not valid UTF-8.",
                false,
                observedHash);
        }
        if (artifact.DeclaredContentType.Equals("text/html", StringComparison.OrdinalIgnoreCase))
            text = Regex.Replace(text, "<[^>]+>", " ", RegexOptions.CultureInvariant);
        text = ClassificationInputPolicy.BuildBoundedDocumentText(text, maxCharacters);
        if (text.Length > maxCharacters)
            return new(
                false,
                null,
                text.Length,
                ClassificationFailureCodes.InputTooLarge,
                "The bounded classification input limit was exceeded.",
                false,
                observedHash);
        return new(true, text, text.Length, null, null, false, observedHash);
    }
}