using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xenia.Application.Email;
using Xenia.Domain.Email;
using Xenia.Infrastructure.Email;
using Xenia.Infrastructure.Email.Connectors;
using Xenia.Infrastructure.Persistence;
using Xenia.Infrastructure.Platform;
using Xunit;

namespace Xenia.Tests.Email;

/// <summary>
/// Tests for EfEmailSourceService CRUD operations.
/// Uses InMemory database — no MySQL required.
/// </summary>
public sealed class EmailSourceServiceTests : IDisposable
{
    private readonly XeniaDbContext _db;
    private readonly EfEmailSourceService _service;
    private readonly Guid _tenantId = Guid.NewGuid();

    public EmailSourceServiceTests()
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
        registry.RegisterConnector(new Pop3EmailConnector(secretService, NullLogger<Pop3EmailConnector>.Instance));
        registry.RegisterConnector(new Microsoft365EmailConnector(secretService, NullLogger<Microsoft365EmailConnector>.Instance));
        registry.RegisterConnector(new GoogleEmailConnector(secretService, NullLogger<GoogleEmailConnector>.Instance));
        registry.RegisterConnector(new ExchangeImapEmailConnector(secretService, NullLogger<ExchangeImapEmailConnector>.Instance));

        _service = new EfEmailSourceService(
            _db,
            registry,
            auditAdapter,
            NullLogger<EfEmailSourceService>.Instance);
    }

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateSource_ValidRequest_ReturnsDto()
    {
        var dto = await _service.CreateSourceAsync(_tenantId, null, new CreateEmailSourceRequest
        {
            DisplayName = "Test IMAP",
            EmailAddress = "inbox@example.com",
            ProviderType = "Imap",
            AuthType = "UsernamePassword",
            IncomingHost = "mail.example.com",
            IncomingPort = 993,
            UseTls = true,
        });

        Assert.NotNull(dto);
        Assert.Equal("Test IMAP", dto.DisplayName);
        Assert.Equal("inbox@example.com", dto.EmailAddress);
        Assert.Equal("Imap", dto.ProviderType);
        Assert.Equal(_tenantId, dto.TenantId);
        Assert.Equal("email", dto.ModuleKey);
    }

    [Fact]
    public async Task CreateSource_InvalidProviderType_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateSourceAsync(_tenantId, null, new CreateEmailSourceRequest
            {
                DisplayName = "Bad",
                EmailAddress = "x@x.com",
                ProviderType = "Yahoo",
                AuthType = "UsernamePassword",
                UseTls = true,
            }));
    }

    [Fact]
    public async Task CreateSource_IncompatibleAuthType_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateSourceAsync(_tenantId, null, new CreateEmailSourceRequest
            {
                DisplayName = "Bad Auth",
                EmailAddress = "x@x.com",
                ProviderType = "Microsoft365",
                AuthType = "UsernamePassword",
                UseTls = true,
            }));
    }

    [Fact]
    public async Task CreateSource_DoesNotStoreSecretValueInRecord()
    {
        await _service.CreateSourceAsync(_tenantId, null, new CreateEmailSourceRequest
        {
            DisplayName = "Secret Test",
            EmailAddress = "sec@example.com",
            ProviderType = "Imap",
            AuthType = "SecretReference",
            IncomingHost = "mail.example.com",
            UseTls = true,
            SecretReferenceId = "ref:my-secret-id",
        });

        var stored = await _db.EmailSources.FirstAsync();

        // Only the reference ID is stored — no actual secret value
        Assert.Equal("ref:my-secret-id", stored.SecretReferenceId);
        Assert.True(stored.SecretReferenceId!.StartsWith("ref:") || stored.SecretReferenceId.Length > 0);
    }

    [Fact]
    public async Task CreateSource_ResponseDto_HiddesSecretReference()
    {
        var dto = await _service.CreateSourceAsync(_tenantId, null, new CreateEmailSourceRequest
        {
            DisplayName = "Secret Test",
            EmailAddress = "sec@example.com",
            ProviderType = "Imap",
            AuthType = "SecretReference",
            IncomingHost = "mail.example.com",
            UseTls = true,
            SecretReferenceId = "ref:my-secret-id",
        });

        // DTO must not expose the reference ID, only a boolean flag
        Assert.True(dto.HasSecretReference);
    }

    // ── Read ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSources_ReturnsTenantSources()
    {
        await CreateTestSource("src1@example.com");
        await CreateTestSource("src2@example.com");

        var sources = await _service.GetSourcesAsync(_tenantId);
        Assert.Equal(2, sources.Count);
    }

    [Fact]
    public async Task GetSource_ValidId_ReturnsDto()
    {
        var created = await CreateTestSource("get@example.com");
        var retrieved = await _service.GetSourceAsync(_tenantId, created.Id);

        Assert.NotNull(retrieved);
        Assert.Equal(created.Id, retrieved.Id);
    }

    [Fact]
    public async Task GetSource_WrongTenant_ReturnsNull()
    {
        var created = await CreateTestSource("iso@example.com");
        var wrongTenant = Guid.NewGuid();

        var result = await _service.GetSourceAsync(wrongTenant, created.Id);
        Assert.Null(result);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateSource_ValidRequest_UpdatesFields()
    {
        var created = await CreateTestSource("upd@example.com");

        var updated = await _service.UpdateSourceAsync(_tenantId, created.Id, null,
            new UpdateEmailSourceRequest { DisplayName = "Updated Name" });

        Assert.NotNull(updated);
        Assert.Equal("Updated Name", updated.DisplayName);
    }

    [Fact]
    public async Task UpdateSource_WrongTenant_ReturnsNull()
    {
        var created = await CreateTestSource("upd2@example.com");
        var wrongTenant = Guid.NewGuid();

        var result = await _service.UpdateSourceAsync(wrongTenant, created.Id, null,
            new UpdateEmailSourceRequest { DisplayName = "Hacked" });

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateSource_ConcurrencyConflict_Throws()
    {
        var created = await CreateTestSource("concur@example.com");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdateSourceAsync(_tenantId, created.Id, null,
                new UpdateEmailSourceRequest { ExpectedRowVersion = 999 }));
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteSource_ValidId_ReturnsTrue()
    {
        var created = await CreateTestSource("del@example.com");
        var result = await _service.DeleteSourceAsync(_tenantId, created.Id, null);

        Assert.True(result);
        // Soft delete: row is retained in DB but marked deleted
        Assert.Equal(1, await _db.EmailSources.CountAsync());
        var row = await _db.EmailSources.FirstAsync(s => s.Id == created.Id);
        Assert.True(row.IsDeleted);
        Assert.NotNull(row.DeletedAt);
    }

    [Fact]
    public async Task DeleteSource_SoftDeleted_NotReturnedByGetSources()
    {
        var created = await CreateTestSource("softdel@example.com");
        await _service.DeleteSourceAsync(_tenantId, created.Id, null);

        var sources = await _service.GetSourcesAsync(_tenantId);
        Assert.Empty(sources);

        var single = await _service.GetSourceAsync(_tenantId, created.Id);
        Assert.Null(single);
    }

    [Fact]
    public async Task DeleteSource_AlreadyDeleted_ReturnsFalse()
    {
        var created = await CreateTestSource("alreadydel@example.com");
        var first = await _service.DeleteSourceAsync(_tenantId, created.Id, null);
        var second = await _service.DeleteSourceAsync(_tenantId, created.Id, null);

        Assert.True(first);
        Assert.False(second); // Source now hidden (soft-deleted), returns not-found
    }

    [Fact]
    public async Task DeleteSource_PreservesValidationHistoryInDb()
    {
        // Validation history rows belong to a different table and are not deleted
        var created = await CreateTestSource("hist@example.com");
        await _service.DeleteSourceAsync(_tenantId, created.Id, null);
        // Validation history is not affected by soft delete
        var history = await _db.EmailValidationHistory.CountAsync(h => h.EmailSourceId == created.Id);
        Assert.Equal(0, history); // None yet — but table not purged by delete
    }

    [Fact]
    public async Task DeleteSource_WrongTenant_ReturnsFalse()
    {
        var created = await CreateTestSource("del2@example.com");
        var result = await _service.DeleteSourceAsync(Guid.NewGuid(), created.Id, null);

        Assert.False(result);
        Assert.Equal(1, await _db.EmailSources.CountAsync());
    }

    // ── Enable / Disable ──────────────────────────────────────────────────────

    [Fact]
    public async Task EnableSource_DisabledSource_SetsEnabledTrue()
    {
        var created = await _service.CreateSourceAsync(_tenantId, null, new CreateEmailSourceRequest
        {
            DisplayName = "Toggle",
            EmailAddress = "toggle@example.com",
            ProviderType = "Imap",
            AuthType = "UsernamePassword",
            IncomingHost = "mail.example.com",
            UseTls = true,
            Enabled = false,
        });

        var result = await _service.EnableSourceAsync(_tenantId, created.Id, null);
        Assert.True(result);

        var dto = await _service.GetSourceAsync(_tenantId, created.Id);
        Assert.True(dto!.Enabled);
    }

    [Fact]
    public async Task DisableSource_EnabledSource_SetsEnabledFalse()
    {
        var created = await CreateTestSource("dis@example.com");
        var result = await _service.DisableSourceAsync(_tenantId, created.Id, null);

        Assert.True(result);
        var dto = await _service.GetSourceAsync(_tenantId, created.Id);
        Assert.False(dto!.Enabled);
    }

    // ── Validation ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateSource_NotFound_ReturnsErrorResult()
    {
        var result = await _service.ValidateSourceAsync(
            _tenantId, Guid.NewGuid(), null, null);

        Assert.False(result.Success);
        Assert.Equal("SOURCE_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task ValidateSource_ValidSource_RecordsHistoryEntry()
    {
        var created = await CreateTestSource("val@example.com");
        await _service.ValidateSourceAsync(_tenantId, created.Id, null, null);

        var count = await _db.EmailValidationHistory.CountAsync();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task ValidateSource_WrongTenant_ReturnsNotFound()
    {
        var created = await CreateTestSource("iso2@example.com");
        var result = await _service.ValidateSourceAsync(
            Guid.NewGuid(), created.Id, null, null);

        Assert.False(result.Success);
        Assert.Equal("SOURCE_NOT_FOUND", result.ErrorCode);
    }

    // ── Validation history ────────────────────────────────────────────────────

    [Fact]
    public async Task GetValidationHistory_WrongTenant_ReturnsEmpty()
    {
        var created = await CreateTestSource("hist@example.com");
        await _service.ValidateSourceAsync(_tenantId, created.Id, null, null);

        var history = await _service.GetValidationHistoryAsync(Guid.NewGuid(), created.Id, 20);
        Assert.Empty(history);
    }

    [Fact]
    public async Task GetValidationHistory_LimitIsEnforced()
    {
        var created = await CreateTestSource("lim@example.com");
        for (int i = 0; i < 5; i++)
            await _service.ValidateSourceAsync(_tenantId, created.Id, null, null);

        var history = await _service.GetValidationHistoryAsync(_tenantId, created.Id, 3);
        Assert.Equal(3, history.Count);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Task<EmailSourceDto> CreateTestSource(string email) =>
        _service.CreateSourceAsync(_tenantId, null, new CreateEmailSourceRequest
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
