using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xenia.Application.Email.Ingestion;
using Xenia.Domain.Email;
using Xenia.Infrastructure.Email;
using Xenia.Infrastructure.Persistence;
using Xunit;

namespace Xenia.Tests.Email;

public sealed class DuplicateDetectionServiceTests
{
    private static XeniaDbContext CreateInMemoryDb(string name) =>
        new(new DbContextOptionsBuilder<XeniaDbContext>()
            .UseInMemoryDatabase(name)
            .Options);

    private sealed class NoopHtmlSanitizer : IEmailHtmlSanitizer
    {
        public bool BlocksRemoteImages => false;
        public string Sanitize(string? html) => html ?? string.Empty;
    }

    private static EmailMessageNormalizer CreateNormalizer() =>
        new(Options.Create(new XeniaIngestionOptions()), new NoopHtmlSanitizer(), NullLogger<EmailMessageNormalizer>.Instance);

    private static NormalizedMessage NormalMessage(string provId, string? internetId = null, string? hash = null) =>
        new()
        {
            ProviderMessageId = provId,
            InternetMessageId = internetId,
            ContentHash       = hash ?? $"hash-{provId}",
            FromAddress       = "from@example.com",
            Subject           = "Test",
        };

    private static async Task<EmailMessage> PersistMsg(XeniaDbContext db, Guid tenantId, Guid sourceId,
        string provId, string? internetId = null)
    {
        var msg = EmailMessage.Create(tenantId, sourceId, EmailProviderType.Microsoft365, provId);
        msg.SetAddressing("Subject", "from@test.com", null, null, null, null, internetId, null, null);
        msg.SetHeadersAndMetadata(null, null, $"hash-{provId}");
        msg.MarkImported(Guid.CreateVersion7());
        db.EmailMessages.Add(msg);
        await db.SaveChangesAsync();
        return msg;
    }

    [Fact]
    public async Task NoDuplicate_WhenMessageIsNew()
    {
        using var db = CreateInMemoryDb(nameof(NoDuplicate_WhenMessageIsNew));
        var svc = new EfDuplicateDetectionService(db, NullLogger<EfDuplicateDetectionService>.Instance);
        var tenant = Guid.NewGuid(); var source = Guid.NewGuid();

        var result = await svc.CheckAsync(tenant, source, NormalMessage("new-prov-id"), CancellationToken.None);
        Assert.False(result.IsDuplicate);
    }

    [Fact]
    public async Task Duplicate_ByProviderMessageId()
    {
        using var db = CreateInMemoryDb(nameof(Duplicate_ByProviderMessageId));
        var svc = new EfDuplicateDetectionService(db, NullLogger<EfDuplicateDetectionService>.Instance);
        var tenant = Guid.NewGuid(); var source = Guid.NewGuid();

        var existing = await PersistMsg(db, tenant, source, "prov-abc");
        var result = await svc.CheckAsync(tenant, source, NormalMessage("prov-abc"), CancellationToken.None);

        Assert.True(result.IsDuplicate);
        Assert.Equal(existing.Id, result.ExistingMessageId);
        Assert.Equal("ProviderMessageId", result.DuplicateSignal);
    }

    [Fact]
    public async Task Duplicate_ByInternetMessageId_AcrossSources()
    {
        using var db = CreateInMemoryDb(nameof(Duplicate_ByInternetMessageId_AcrossSources));
        var svc = new EfDuplicateDetectionService(db, NullLogger<EfDuplicateDetectionService>.Instance);
        var tenant = Guid.NewGuid();
        var source1 = Guid.NewGuid(); var source2 = Guid.NewGuid();

        var existing = await PersistMsg(db, tenant, source1, "prov-1", "<msgid@example.com>");
        var result = await svc.CheckAsync(tenant, source2, NormalMessage("prov-2", "<msgid@example.com>"), CancellationToken.None);

        Assert.True(result.IsDuplicate);
        Assert.Equal("InternetMessageId", result.DuplicateSignal);
    }

    [Fact]
    public async Task NotDuplicate_DifferentTenants_SameProviderMessageId()
    {
        using var db = CreateInMemoryDb(nameof(NotDuplicate_DifferentTenants_SameProviderMessageId));
        var svc = new EfDuplicateDetectionService(db, NullLogger<EfDuplicateDetectionService>.Instance);
        var tenantA = Guid.NewGuid(); var tenantB = Guid.NewGuid();
        var source = Guid.NewGuid();

        await PersistMsg(db, tenantA, source, "prov-shared");
        var result = await svc.CheckAsync(tenantB, source, NormalMessage("prov-shared"), CancellationToken.None);

        Assert.False(result.IsDuplicate);
    }

    [Fact]
    public async Task Duplicate_ByContentHash_FallbackSignal()
    {
        using var db = CreateInMemoryDb(nameof(Duplicate_ByContentHash_FallbackSignal));
        var svc = new EfDuplicateDetectionService(db, NullLogger<EfDuplicateDetectionService>.Instance);
        var tenant = Guid.NewGuid(); var source = Guid.NewGuid();

        var msg = EmailMessage.Create(tenant, source, EmailProviderType.Imap, "prov-X");
        msg.SetHeadersAndMetadata(null, null, "shared-hash-abc");
        msg.MarkImported(Guid.CreateVersion7());
        db.EmailMessages.Add(msg);
        await db.SaveChangesAsync();

        var result = await svc.CheckAsync(tenant, source,
            NormalMessage("prov-Y", null, "shared-hash-abc"), CancellationToken.None);

        Assert.True(result.IsDuplicate);
        Assert.Equal("ContentHash", result.DuplicateSignal);
    }

    [Fact]
    public async Task NoDuplicate_WhenInternetMessageIdIsNull()
    {
        using var db = CreateInMemoryDb(nameof(NoDuplicate_WhenInternetMessageIdIsNull));
        var svc = new EfDuplicateDetectionService(db, NullLogger<EfDuplicateDetectionService>.Instance);
        var tenant = Guid.NewGuid(); var source = Guid.NewGuid();

        var result = await svc.CheckAsync(tenant, source, NormalMessage("prov-fresh", null, "unique-hash-xxx"), CancellationToken.None);
        Assert.False(result.IsDuplicate);
    }
}
