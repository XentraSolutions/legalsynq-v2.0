using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xenia.Application.Email.Ingestion;
using Xenia.Domain.Email;
using Xenia.Infrastructure.Email;
using Xenia.Infrastructure.Persistence;
using Xunit;

namespace Xenia.Tests.Email;

public sealed class EmailMessageNormalizerTests
{
    private sealed class NoopHtmlSanitizer : IEmailHtmlSanitizer
    {
        public bool BlocksRemoteImages => false;
        public string Sanitize(string? html) => html ?? string.Empty;
    }

    private static EmailMessageNormalizer CreateNormalizer(XeniaIngestionOptions? opts = null)
    {
        opts ??= new XeniaIngestionOptions();
        return new EmailMessageNormalizer(Options.Create(opts), new NoopHtmlSanitizer(), NullLogger<EmailMessageNormalizer>.Instance);
    }

    private static ProviderMessageEnvelope MinimalEnvelope(string msgId = "prov-001") =>
        new()
        {
            ProviderMessageId = msgId,
            Subject           = "Test subject",
            FromAddress       = "SENDER@Example.COM",
            SentAt            = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc),
            ReceivedAt        = new DateTime(2026, 1, 15, 10, 31, 0, DateTimeKind.Utc),
        };

    [Fact]
    public void Normalize_ValidEnvelope_ProducesNormalizedMessage()
    {
        var normalizer = CreateNormalizer();
        var result = normalizer.Normalize(MinimalEnvelope(), EmailProviderType.Microsoft365);

        Assert.True(result.IsValid);
        Assert.NotNull(result.Message);
        Assert.Equal("prov-001", result.Message.ProviderMessageId);
        Assert.Equal("sender@example.com", result.Message.FromAddress);
    }

    [Fact]
    public void Normalize_MissingProviderMessageId_Fails()
    {
        var normalizer = CreateNormalizer();
        var envelope = new ProviderMessageEnvelope { ProviderMessageId = "" };
        var result = normalizer.Normalize(envelope, EmailProviderType.Microsoft365);

        Assert.False(result.IsValid);
        Assert.Equal("PROVIDER_MESSAGE_ID_REQUIRED", result.ErrorCode);
    }

    [Fact]
    public void Normalize_LowerCasesFromAddress()
    {
        var normalizer = CreateNormalizer();
        var envelope = MinimalEnvelope();
        envelope = envelope with { FromAddress = "TEST@EXAMPLE.COM" };
        var result = normalizer.Normalize(envelope, EmailProviderType.Microsoft365);

        Assert.True(result.IsValid);
        Assert.Equal("test@example.com", result.Message!.FromAddress);
    }

    [Fact]
    public void Normalize_NormalizesTimestampsToUtc()
    {
        var normalizer = CreateNormalizer();
        var local = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Local);
        var envelope = MinimalEnvelope() with { SentAt = local };
        var result = normalizer.Normalize(envelope, EmailProviderType.Microsoft365);

        Assert.True(result.IsValid);
        Assert.Equal(DateTimeKind.Utc, result.Message!.SentAt!.Value.Kind);
    }

    [Fact]
    public void Normalize_TruncatesBodyToLimit()
    {
        var opts = new XeniaIngestionOptions { MaxMessageBodyBytes = 100 };
        var normalizer = CreateNormalizer(opts);
        var bigBody = new string('x', 500);
        var envelope = MinimalEnvelope() with { BodyText = bigBody };
        var result = normalizer.Normalize(envelope, EmailProviderType.Imap);

        Assert.True(result.IsValid);
        Assert.True(result.Message!.BodyText!.Length <= 100);
    }

    [Fact]
    public void Normalize_RemovesSensitiveHeaders()
    {
        var normalizer = CreateNormalizer();
        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer secret-token",
            ["X-Auth-Token"]  = "another-secret",
            ["X-Mailer"]      = "Outlook",
        };
        var envelope = MinimalEnvelope() with { Headers = headers };
        var result = normalizer.Normalize(envelope, EmailProviderType.Microsoft365);

        Assert.True(result.IsValid);
        Assert.NotNull(result.Message!.HeadersJson);
        Assert.DoesNotContain("secret-token", result.Message.HeadersJson);
        Assert.DoesNotContain("Authorization", result.Message.HeadersJson);
        Assert.Contains("X-Mailer", result.Message.HeadersJson);
    }

    [Fact]
    public void Normalize_GeneratesBodyPreviewFromPlainText()
    {
        var normalizer = CreateNormalizer();
        var envelope = MinimalEnvelope() with { BodyText = "Hello world, this is a test message." };
        var result = normalizer.Normalize(envelope, EmailProviderType.Google);

        Assert.True(result.IsValid);
        Assert.NotNull(result.Message!.BodyPreview);
        Assert.Contains("Hello world", result.Message.BodyPreview);
    }

    [Fact]
    public void Normalize_GeneratesBodyPreviewFromHtml_StripsTagsFirst()
    {
        var normalizer = CreateNormalizer();
        var envelope = MinimalEnvelope() with { BodyHtml = "<p>Hello <b>world</b></p><script>alert(1)</script>" };
        var result = normalizer.Normalize(envelope, EmailProviderType.Imap);

        Assert.True(result.IsValid);
        Assert.NotNull(result.Message!.BodyPreview);
        Assert.DoesNotContain("<", result.Message.BodyPreview);
        Assert.DoesNotContain("script", result.Message.BodyPreview);
    }

    [Fact]
    public void Normalize_ComputesContentHash()
    {
        var normalizer = CreateNormalizer();
        var result1 = normalizer.Normalize(MinimalEnvelope("id-001"), EmailProviderType.Microsoft365);
        var result2 = normalizer.Normalize(MinimalEnvelope("id-001"), EmailProviderType.Microsoft365);

        Assert.True(result1.IsValid);
        Assert.NotNull(result1.Message!.ContentHash);
        Assert.Equal(result1.Message.ContentHash, result2.Message!.ContentHash);
    }

    [Fact]
    public void Normalize_DifferentProviderMessageIds_ProduceDifferentHashes()
    {
        var normalizer = CreateNormalizer();
        var result1 = normalizer.Normalize(MinimalEnvelope("id-001"), EmailProviderType.Microsoft365);
        var result2 = normalizer.Normalize(MinimalEnvelope("id-002"), EmailProviderType.Microsoft365);

        Assert.NotEqual(result1.Message!.ContentHash, result2.Message!.ContentHash);
    }

    [Fact]
    public void Normalize_NormalizesRecipients()
    {
        var normalizer = CreateNormalizer();
        var envelope = MinimalEnvelope() with
        {
            To = [new ProviderRecipient("TO@EXAMPLE.COM", "To User")],
            Cc = [new ProviderRecipient("CC@EXAMPLE.COM", "Cc User")],
        };
        var result = normalizer.Normalize(envelope, EmailProviderType.Microsoft365);

        Assert.True(result.IsValid);
        Assert.Equal(2, result.Message!.Recipients.Count);
        Assert.All(result.Message.Recipients, r =>
            Assert.Equal(r.EmailAddress, r.EmailAddress.ToLowerInvariant()));
    }

    [Fact]
    public void StripHtmlTags_RemovesScriptAndTags()
    {
        var input  = "<html><body><p>Hello</p><script>evil()</script></body></html>";
        var result = EmailMessageNormalizer.StripHtmlTags(input);
        Assert.DoesNotContain("<", result);
        Assert.DoesNotContain("evil()", result);
        Assert.Contains("Hello", result);
    }

    [Fact]
    public void Normalize_EmptyHtmlBody_DoesNotThrow()
    {
        var normalizer = CreateNormalizer();
        var envelope = MinimalEnvelope() with { BodyHtml = "" };
        var result = normalizer.Normalize(envelope, EmailProviderType.Imap);
        Assert.True(result.IsValid);
    }
}
