using Microsoft.Extensions.Logging.Abstractions;
using Xenia.Application.Email;
using Xenia.Domain.Email;
using Xenia.Infrastructure.Email;
using Xenia.Infrastructure.Email.Connectors;
using Xunit;

namespace Xenia.Tests.Email;

/// <summary>
/// Tests for the email connector registry and individual connector validation.
/// No database or network access required.
/// </summary>
public sealed class EmailConnectorTests
{
    private readonly UnavailableSecretReferenceService _secretService =
        new(NullLogger<UnavailableSecretReferenceService>.Instance);

    private Microsoft365EmailConnector M365() =>
        new(_secretService, NullLogger<Microsoft365EmailConnector>.Instance);

    private GoogleEmailConnector Google() =>
        new(_secretService, NullLogger<GoogleEmailConnector>.Instance);

    private ImapEmailConnector Imap() =>
        new(_secretService, NullLogger<ImapEmailConnector>.Instance);

    private Pop3EmailConnector Pop3() =>
        new(_secretService, NullLogger<Pop3EmailConnector>.Instance);

    private ExchangeImapEmailConnector ExchangeImap() =>
        new(_secretService, NullLogger<ExchangeImapEmailConnector>.Instance);

    // ── Connector registry ────────────────────────────────────────────────────

    [Fact]
    public void Registry_RegistersAllFiveConnectors()
    {
        var registry = BuildRegistry();
        Assert.Equal(5, registry.GetAllConnectors().Count);
    }

    [Theory]
    [InlineData(EmailProviderType.Microsoft365)]
    [InlineData(EmailProviderType.Google)]
    [InlineData(EmailProviderType.Imap)]
    [InlineData(EmailProviderType.Pop3)]
    [InlineData(EmailProviderType.ExchangeImap)]
    public void Registry_HasConnector_ForAllSupportedProviders(EmailProviderType providerType)
    {
        var registry = BuildRegistry();
        Assert.True(registry.HasConnector(providerType));
    }

    [Fact]
    public void Registry_GetConnector_UnknownProvider_Throws()
    {
        var registry = new EmailSourceConnectorRegistry();
        Assert.Throws<KeyNotFoundException>(() => registry.GetConnector(EmailProviderType.Microsoft365));
    }

    [Fact]
    public void Registry_DuplicateRegistration_Throws()
    {
        var registry = new EmailSourceConnectorRegistry();
        registry.RegisterConnector(M365());
        Assert.Throws<InvalidOperationException>(() => registry.RegisterConnector(M365()));
    }

    // ── Microsoft365 validation ───────────────────────────────────────────────

    [Fact]
    public async Task M365_ValidateConfiguration_ValidConfig_Succeeds()
    {
        var ctx = new EmailSourceConnectorContext
        {
            SourceId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            EmailAddress = "user@contoso.com",
            AuthType = EmailAuthType.SecretReference,
            IncomingHost = "outlook.office365.com",
            IncomingPort = 993,
            UseTls = true,
        };
        var result = await M365().ValidateConfigurationAsync(ctx);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task M365_ValidateConfiguration_MissingEmail_Fails()
    {
        var ctx = new EmailSourceConnectorContext
        {
            SourceId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            EmailAddress = "",
            AuthType = EmailAuthType.SecretReference,
            UseTls = true,
        };
        var result = await M365().ValidateConfigurationAsync(ctx);
        Assert.False(result.Success);
        Assert.Equal("EMAIL_ADDRESS_REQUIRED", result.ErrorCode);
    }

    [Fact]
    public async Task M365_ValidateConfiguration_UnsupportedAuthType_Fails()
    {
        var ctx = new EmailSourceConnectorContext
        {
            SourceId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            EmailAddress = "user@contoso.com",
            AuthType = EmailAuthType.UsernamePassword,
            UseTls = true,
        };
        var result = await M365().ValidateConfigurationAsync(ctx);
        Assert.False(result.Success);
        Assert.Equal("AUTH_TYPE_UNSUPPORTED", result.ErrorCode);
    }

    // ── Google validation ─────────────────────────────────────────────────────

    [Fact]
    public async Task Google_ValidateConfiguration_NoTls_Fails()
    {
        var ctx = new EmailSourceConnectorContext
        {
            SourceId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            EmailAddress = "user@gmail.com",
            AuthType = EmailAuthType.AppPassword,
            UseTls = false,
        };
        var result = await Google().ValidateConfigurationAsync(ctx);
        Assert.False(result.Success);
        Assert.Equal("TLS_REQUIRED", result.ErrorCode);
    }

    [Fact]
    public async Task Google_ValidateConfiguration_UnsupportedAuth_Fails()
    {
        var ctx = new EmailSourceConnectorContext
        {
            SourceId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            EmailAddress = "user@gmail.com",
            AuthType = EmailAuthType.UsernamePassword,
            UseTls = true,
        };
        var result = await Google().ValidateConfigurationAsync(ctx);
        Assert.False(result.Success);
        Assert.Equal("AUTH_TYPE_UNSUPPORTED", result.ErrorCode);
    }

    // ── IMAP validation ───────────────────────────────────────────────────────

    [Fact]
    public async Task Imap_ValidateConfiguration_MissingHost_Fails()
    {
        var ctx = new EmailSourceConnectorContext
        {
            SourceId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            EmailAddress = "user@example.com",
            AuthType = EmailAuthType.UsernamePassword,
            IncomingHost = null,
            UseTls = true,
        };
        var result = await Imap().ValidateConfigurationAsync(ctx);
        Assert.False(result.Success);
        Assert.Equal("IMAP_HOST_REQUIRED", result.ErrorCode);
    }

    [Fact]
    public async Task Imap_ValidateConfiguration_NoTls_Fails()
    {
        var ctx = new EmailSourceConnectorContext
        {
            SourceId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            EmailAddress = "user@example.com",
            AuthType = EmailAuthType.UsernamePassword,
            IncomingHost = "mail.example.com",
            UseTls = false,
        };
        var result = await Imap().ValidateConfigurationAsync(ctx);
        Assert.False(result.Success);
        Assert.Equal("TLS_REQUIRED", result.ErrorCode);
    }

    [Fact]
    public async Task Imap_ValidateConfiguration_ValidConfig_Succeeds()
    {
        var ctx = new EmailSourceConnectorContext
        {
            SourceId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            EmailAddress = "user@example.com",
            AuthType = EmailAuthType.UsernamePassword,
            IncomingHost = "mail.example.com",
            IncomingPort = 993,
            UseTls = true,
        };
        var result = await Imap().ValidateConfigurationAsync(ctx);
        Assert.True(result.Success);
    }

    // ── SSRF / unsafe host rejection ──────────────────────────────────────────

    [Theory]
    [InlineData("localhost")]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.1")]
    [InlineData("192.168.1.1")]
    [InlineData("172.16.0.1")]
    [InlineData("169.254.169.254")]
    [InlineData("metadata.google.internal")]
    public async Task Imap_ValidateConfiguration_UnsafeHost_Fails(string host)
    {
        var ctx = new EmailSourceConnectorContext
        {
            SourceId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            EmailAddress = "user@example.com",
            AuthType = EmailAuthType.UsernamePassword,
            IncomingHost = host,
            UseTls = true,
        };
        var result = await Imap().ValidateConfigurationAsync(ctx);
        Assert.False(result.Success);
        Assert.Equal("HOST_NOT_ALLOWED", result.ErrorCode);
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("192.168.1.100")]
    [InlineData("10.10.10.10")]
    public async Task Pop3_ValidateConfiguration_UnsafeHost_Fails(string host)
    {
        var ctx = new EmailSourceConnectorContext
        {
            SourceId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            EmailAddress = "user@example.com",
            AuthType = EmailAuthType.UsernamePassword,
            IncomingHost = host,
            UseTls = true,
        };
        var result = await Pop3().ValidateConfigurationAsync(ctx);
        Assert.False(result.Success);
        Assert.Equal("HOST_NOT_ALLOWED", result.ErrorCode);
    }

    // ── Secret reference service ──────────────────────────────────────────────

    [Fact]
    public void SecretReferenceService_IsNotConfigured_InDevelopment()
    {
        Assert.False(_secretService.IsConfigured);
    }

    [Fact]
    public async Task SecretReferenceService_Resolve_ReturnsUnavailable()
    {
        var result = await _secretService.ResolveAsync("ref:some-secret");
        Assert.False(result.Success);
        Assert.Equal("SECRET_SERVICE_UNAVAILABLE", result.ErrorCode);
    }

    [Fact]
    public void SecretReferenceService_ValidReferenceFormat_Empty_ReturnsFalse()
    {
        Assert.False(_secretService.IsValidReferenceFormat(""));
        Assert.False(_secretService.IsValidReferenceFormat("   "));
    }

    [Fact]
    public void SecretReferenceService_ValidReferenceFormat_NormalRef_ReturnsTrue()
    {
        Assert.True(_secretService.IsValidReferenceFormat("arn:aws:secretsmanager:us-east-1:123:secret:my-secret"));
    }

    // ── Connector capabilities ────────────────────────────────────────────────

    [Fact]
    public void AllConnectors_GetCapabilities_ReturnsNonNull()
    {
        var registry = BuildRegistry();
        foreach (var connector in registry.GetAllConnectors())
        {
            var caps = connector.GetCapabilities();
            Assert.NotNull(caps);
            Assert.Equal(connector.ProviderType, caps.ProviderType);
        }
    }

    // ── TLS enforcement ───────────────────────────────────────────────────────

    [Fact]
    public async Task ExchangeImap_ValidateConfiguration_NoTls_Fails()
    {
        var ctx = new EmailSourceConnectorContext
        {
            SourceId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            EmailAddress = "user@exchange.local",
            AuthType = EmailAuthType.UsernamePassword,
            IncomingHost = "mail.exchange.local",
            UseTls = false,
        };
        var result = await ExchangeImap().ValidateConfigurationAsync(ctx);
        Assert.False(result.Success);
        Assert.Equal("TLS_REQUIRED", result.ErrorCode);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private EmailSourceConnectorRegistry BuildRegistry()
    {
        var registry = new EmailSourceConnectorRegistry();
        registry.RegisterConnector(M365());
        registry.RegisterConnector(Google());
        registry.RegisterConnector(Imap());
        registry.RegisterConnector(Pop3());
        registry.RegisterConnector(ExchangeImap());
        return registry;
    }
}
