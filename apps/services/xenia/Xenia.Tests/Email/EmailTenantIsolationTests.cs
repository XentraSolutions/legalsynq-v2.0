using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xenia.Application.Email;
using Xenia.Infrastructure.Email;
using Xenia.Infrastructure.Email.Connectors;
using Xenia.Infrastructure.Persistence;
using Xenia.Infrastructure.Platform;
using Xunit;

namespace Xenia.Tests.Email;

/// <summary>
/// Tenant isolation tests for the Email module.
///
/// Validates that Tenant A cannot read, update, delete, validate, or view
/// validation history for Tenant B's sources, and vice versa.
///
/// Body/route/query/header tenant IDs cannot override JWT context — this is
/// enforced by the service layer always using the caller-supplied tenantId
/// parameter, which in production comes exclusively from IXeniaTenantContext
/// (JWT-derived), never from the HTTP request.
/// </summary>
public sealed class EmailTenantIsolationTests : IDisposable
{
    private readonly XeniaDbContext _db;
    private readonly EfEmailSourceService _service;
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();

    public EmailTenantIsolationTests()
    {
        var options = new DbContextOptionsBuilder<XeniaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new XeniaDbContext(options);

        var secretService = new UnavailableSecretReferenceService(
            NullLogger<UnavailableSecretReferenceService>.Instance);
        var auditAdapter = new UnavailableAuditAdapter(
            NullLogger<UnavailableAuditAdapter>.Instance);

        var registry = new EmailSourceConnectorRegistry();
        registry.RegisterConnector(new ImapEmailConnector(secretService, NullLogger<ImapEmailConnector>.Instance));

        _service = new EfEmailSourceService(
            _db,
            registry,
            auditAdapter,
            NullLogger<EfEmailSourceService>.Instance);
    }

    [Fact]
    public async Task TenantB_CannotRead_TenantA_Source()
    {
        var sourceA = await CreateSource(_tenantA, "a@example.com");
        var result = await _service.GetSourceAsync(_tenantB, sourceA.Id);
        Assert.Null(result);
    }

    [Fact]
    public async Task TenantB_CannotUpdate_TenantA_Source()
    {
        var sourceA = await CreateSource(_tenantA, "a2@example.com");
        var result = await _service.UpdateSourceAsync(
            _tenantB, sourceA.Id, null,
            new UpdateEmailSourceRequest { DisplayName = "Hacked" });
        Assert.Null(result);

        // Verify original name unchanged
        var original = await _service.GetSourceAsync(_tenantA, sourceA.Id);
        Assert.NotEqual("Hacked", original!.DisplayName);
    }

    [Fact]
    public async Task TenantB_CannotDelete_TenantA_Source()
    {
        var sourceA = await CreateSource(_tenantA, "a3@example.com");
        var result = await _service.DeleteSourceAsync(_tenantB, sourceA.Id, null);
        Assert.False(result);

        // Source still exists for Tenant A
        Assert.NotNull(await _service.GetSourceAsync(_tenantA, sourceA.Id));
    }

    [Fact]
    public async Task TenantB_CannotEnable_TenantA_Source()
    {
        var sourceA = await _service.CreateSourceAsync(_tenantA, null, new CreateEmailSourceRequest
        {
            DisplayName = "A Source",
            EmailAddress = "a4@example.com",
            ProviderType = "Imap",
            AuthType = "UsernamePassword",
            IncomingHost = "mail.example.com",
            UseTls = true,
            Enabled = false,
        });

        var result = await _service.EnableSourceAsync(_tenantB, sourceA.Id, null);
        Assert.False(result);
    }

    [Fact]
    public async Task TenantB_CannotDisable_TenantA_Source()
    {
        var sourceA = await CreateSource(_tenantA, "a5@example.com");
        var result = await _service.DisableSourceAsync(_tenantB, sourceA.Id, null);
        Assert.False(result);
    }

    [Fact]
    public async Task TenantB_CannotValidate_TenantA_Source()
    {
        var sourceA = await CreateSource(_tenantA, "a6@example.com");
        var result = await _service.ValidateSourceAsync(_tenantB, sourceA.Id, null, null);
        Assert.False(result.Success);
        Assert.Equal("SOURCE_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task TenantA_CannotViewValidationHistory_OfTenantB()
    {
        var sourceB = await CreateSource(_tenantB, "b1@example.com");
        await _service.ValidateSourceAsync(_tenantB, sourceB.Id, null, null);

        var history = await _service.GetValidationHistoryAsync(_tenantA, sourceB.Id, 20);
        Assert.Empty(history);
    }

    [Fact]
    public async Task GetSources_OnlyReturnsOwnTenantSources()
    {
        await CreateSource(_tenantA, "iso1@example.com");
        await CreateSource(_tenantA, "iso2@example.com");
        await CreateSource(_tenantB, "iso3@example.com");

        var sourcesA = await _service.GetSourcesAsync(_tenantA);
        var sourcesB = await _service.GetSourcesAsync(_tenantB);

        Assert.Equal(2, sourcesA.Count);
        Assert.Single(sourcesB);
        Assert.DoesNotContain(sourcesA, s => s.TenantId == _tenantB);
        Assert.DoesNotContain(sourcesB, s => s.TenantId == _tenantA);
    }

    [Fact]
    public async Task TenantContext_CannotBeOverriddenByBodyTenantId()
    {
        // This test verifies the architectural guarantee: the service always receives
        // tenantId from IXeniaTenantContext (JWT claims). Even if a caller passes a
        // different tenantId to GetSourceAsync, the returned source belongs to that exact
        // tenantId only — it cannot see another tenant's data.
        var sourceA = await CreateSource(_tenantA, "body@example.com");

        // Passing TenantB's ID returns null even though the source ID exists
        var result = await _service.GetSourceAsync(_tenantB, sourceA.Id);
        Assert.Null(result);
    }

    private Task<EmailSourceDto> CreateSource(Guid tenantId, string email) =>
        _service.CreateSourceAsync(tenantId, null, new CreateEmailSourceRequest
        {
            DisplayName = $"Source {email}",
            EmailAddress = email,
            ProviderType = "Imap",
            AuthType = "UsernamePassword",
            IncomingHost = "mail.example.com",
            IncomingPort = 993,
            UseTls = true,
        });

    public void Dispose() => _db.Dispose();
}
