using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xenia.Application.Adapters.Interfaces;
using Xenia.Application.Email.Ingestion;
using Xenia.Domain.Email;
using Xenia.Infrastructure.Email;
using Xenia.Infrastructure.Persistence;

namespace Xenia.Tests.Email;

/// <summary>
/// Tests for EfMessagePersistenceService.
/// Uses InMemory EF provider — no database required.
/// </summary>
public sealed class MessagePersistenceServiceTests : IDisposable
{
    private readonly XeniaDbContext _db;
    private readonly EfMessagePersistenceService _sut;

    private static readonly Guid TenantId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly Guid SourceId = Guid.Parse("22222222-0000-0000-0000-000000000002");

    private sealed class NoopAuditAdapter : IAuditAdapter
    {
        public bool IsConfigured => false;
        public Task RecordEventAsync(XeniaAuditEvent auditEvent, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    public MessagePersistenceServiceTests()
    {
        var options = new DbContextOptionsBuilder<XeniaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db  = new XeniaDbContext(options);
        _sut = new EfMessagePersistenceService(_db, new NoopAuditAdapter(), NullLogger<EfMessagePersistenceService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static NormalizedMessage BuildMessage(
        string providerMessageId = "msg-001",
        string? internetMessageId = "<abc@test.example>",
        string? subject = "Hello World",
        string? fromAddress = "sender@example.com",
        string? contentHash = null) =>
        new()
        {
            ProviderMessageId  = providerMessageId,
            InternetMessageId  = internetMessageId,
            Subject            = subject,
            FromAddress        = fromAddress,
            FromName           = "Test Sender",
            SentAt             = DateTime.UtcNow.AddMinutes(-5),
            ReceivedAt         = DateTime.UtcNow,
            Importance         = EmailImportance.Normal,
            BodyType           = EmailMessageBodyType.Plain,
            BodyText           = "Test body",
            BodyPreview        = "Test body",
            ContentHash        = contentHash ?? $"hash-{providerMessageId}",
            HasAttachments     = false,
            AttachmentCount    = 0,
            HeadersJson        = "{}",
            Recipients         = [new NormalizedRecipient(EmailRecipientType.To, "to@example.com", "Recipient")],
            Attachments        = [],
        };

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PersistMessageAsync_NewMessage_CreatesEmailMessageAndRecipient()
    {
        var msg    = BuildMessage("msg-new");
        var runId  = Guid.CreateVersion7();
        var dupCheck = DuplicateCheckResult.NotDuplicate();

        var result = await _sut.PersistMessageAsync(
            TenantId, SourceId, EmailProviderType.Imap, msg, runId, dupCheck);

        Assert.True(result.Success);
        Assert.NotNull(result.MessageId);
        Assert.Equal(MessageImportStatus.Imported, result.ImportStatus);

        var saved = await _db.EmailMessages.FirstOrDefaultAsync(m => m.Id == result.MessageId!.Value);
        Assert.NotNull(saved);
        Assert.Equal(TenantId, saved.TenantId);
        Assert.Equal(SourceId, saved.EmailSourceId);
        Assert.Equal("msg-new", saved.ProviderMessageId);
        Assert.Equal("Hello World", saved.Subject);
        Assert.Equal(MessageImportStatus.Imported, saved.ImportStatus);

        var recipients = await _db.EmailMessageRecipients
            .Where(r => r.EmailMessageId == result.MessageId!.Value)
            .ToListAsync();
        Assert.Single(recipients);
        Assert.Equal("to@example.com", recipients[0].EmailAddress);
        Assert.Equal(EmailRecipientType.To, recipients[0].RecipientType);
    }

    [Fact]
    public async Task PersistMessageAsync_Duplicate_ReturnsDeduplicatedStatus()
    {
        var msg   = BuildMessage("msg-dup");
        var runId = Guid.CreateVersion7();

        var first = await _sut.PersistMessageAsync(
            TenantId, SourceId, EmailProviderType.Imap, msg, runId, DuplicateCheckResult.NotDuplicate());
        Assert.True(first.Success);

        var dupCheck = DuplicateCheckResult.Duplicate(first.MessageId!.Value, "ProviderMessageId");
        var result   = await _sut.PersistMessageAsync(
            TenantId, SourceId, EmailProviderType.Imap, msg, Guid.CreateVersion7(), dupCheck);

        Assert.True(result.Success);
        Assert.Equal(MessageImportStatus.Duplicate, result.ImportStatus);
    }

    [Fact]
    public async Task PersistMessageAsync_MessageWithAttachments_CreatesAttachmentStubs()
    {
        var msg = BuildMessage("msg-att") with
        {
            HasAttachments  = true,
            AttachmentCount = 2,
            Attachments =
            [
                new ProviderAttachmentDescriptor { ProviderAttachmentId = "att-1", FileName = "doc.pdf",   MimeType = "application/pdf", SizeBytes = 1024, IsInline = false },
                new ProviderAttachmentDescriptor { ProviderAttachmentId = "att-2", FileName = "image.png", MimeType = "image/png",       SizeBytes = 2048, IsInline = true  },
            ],
        };
        var runId = Guid.CreateVersion7();

        var result = await _sut.PersistMessageAsync(
            TenantId, SourceId, EmailProviderType.Imap, msg, runId, DuplicateCheckResult.NotDuplicate());

        Assert.True(result.Success);
        Assert.Equal(2, result.AttachmentReferenceIds.Count);

        var attachments = await _db.EmailAttachmentReferences
            .Where(a => a.EmailMessageId == result.MessageId!.Value)
            .ToListAsync();
        Assert.Equal(2, attachments.Count);
        Assert.All(attachments, a => Assert.Equal(AttachmentDispatchStatus.Pending, a.DispatchStatus));
        Assert.Contains(attachments, a => a.FileName == "doc.pdf");
        Assert.Contains(attachments, a => a.FileName == "image.png");
    }

    [Fact]
    public async Task PersistMessageAsync_TenantIsolation_DoesNotSeeOtherTenantMessages()
    {
        var otherTenantId = Guid.Parse("99999999-0000-0000-0000-000000000099");
        var msg   = BuildMessage("msg-iso");
        var runId = Guid.CreateVersion7();

        await _sut.PersistMessageAsync(
            otherTenantId, SourceId, EmailProviderType.Imap, msg, runId, DuplicateCheckResult.NotDuplicate());

        var messages = await _db.EmailMessages
            .Where(m => m.TenantId == TenantId)
            .ToListAsync();
        Assert.Empty(messages);
    }
}
