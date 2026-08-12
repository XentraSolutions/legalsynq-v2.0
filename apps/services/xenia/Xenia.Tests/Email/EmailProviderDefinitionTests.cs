using Xenia.Application.Email;
using Xenia.Domain.Email;
using Xunit;

namespace Xenia.Tests.Email;

/// <summary>
/// Tests for the email provider catalog definitions and auth-type compatibility.
/// No database required.
/// </summary>
public sealed class EmailProviderDefinitionTests
{
    [Fact]
    public void GetAll_ReturnsFiveProviders()
    {
        var providers = EmailProviderDefinitions.GetAll();
        Assert.Equal(5, providers.Count);
    }

    [Theory]
    [InlineData("Microsoft365")]
    [InlineData("Google")]
    [InlineData("Imap")]
    [InlineData("Pop3")]
    [InlineData("ExchangeImap")]
    public void Get_KnownProvider_ReturnsDefinition(string key)
    {
        var def = EmailProviderDefinitions.Get(key);
        Assert.NotNull(def);
        Assert.Equal(key, def.ProviderKey);
    }

    [Fact]
    public void Get_UnknownProvider_ReturnsNull()
    {
        var def = EmailProviderDefinitions.Get("Yahoo");
        Assert.Null(def);
    }

    [Theory]
    [InlineData(EmailProviderType.Microsoft365, EmailAuthType.OAuth2, true)]
    [InlineData(EmailProviderType.Microsoft365, EmailAuthType.UsernamePassword, false)]
    [InlineData(EmailProviderType.Google, EmailAuthType.OAuth2, true)]
    [InlineData(EmailProviderType.Google, EmailAuthType.AppPassword, true)]
    [InlineData(EmailProviderType.Google, EmailAuthType.UsernamePassword, false)]
    [InlineData(EmailProviderType.Imap, EmailAuthType.UsernamePassword, true)]
    [InlineData(EmailProviderType.Imap, EmailAuthType.AppPassword, true)]
    [InlineData(EmailProviderType.Imap, EmailAuthType.OAuth2, false)]
    [InlineData(EmailProviderType.Pop3, EmailAuthType.UsernamePassword, true)]
    [InlineData(EmailProviderType.Pop3, EmailAuthType.OAuth2, false)]
    [InlineData(EmailProviderType.ExchangeImap, EmailAuthType.OAuth2, true)]
    [InlineData(EmailProviderType.ExchangeImap, EmailAuthType.UsernamePassword, true)]
    [InlineData(EmailProviderType.ExchangeImap, EmailAuthType.AppPassword, false)]
    public void IsAuthTypeSupported_ReturnsCorrectResult(
        EmailProviderType provider, EmailAuthType authType, bool expected)
    {
        var result = EmailProviderDefinitions.IsAuthTypeSupported(provider, authType);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Microsoft365_RequiresTls()
    {
        var def = EmailProviderDefinitions.Get("Microsoft365")!;
        Assert.True(def.RequiresTls);
        Assert.True(def.SupportsOAuth);
    }

    [Fact]
    public void Google_RequiresTls()
    {
        var def = EmailProviderDefinitions.Get("Google")!;
        Assert.True(def.RequiresTls);
        Assert.True(def.SupportsOAuth);
    }

    [Fact]
    public void Imap_DoesNotSupportOAuth()
    {
        var def = EmailProviderDefinitions.Get("Imap")!;
        Assert.False(def.SupportsOAuth);
        Assert.True(def.SupportsUsernamePassword);
    }

    [Fact]
    public void Pop3_DoesNotSupportOAuth()
    {
        var def = EmailProviderDefinitions.Get("Pop3")!;
        Assert.False(def.SupportsOAuth);
        Assert.True(def.SupportsUsernamePassword);
    }

    [Fact]
    public void AllProviders_HaveNonEmptyDisplayName()
    {
        foreach (var p in EmailProviderDefinitions.GetAll())
            Assert.False(string.IsNullOrWhiteSpace(p.DisplayName));
    }

    [Fact]
    public void AllProviders_HaveAtLeastOneSupportedAuthType()
    {
        foreach (var p in EmailProviderDefinitions.GetAll())
            Assert.NotEmpty(p.SupportedAuthTypes);
    }
}
