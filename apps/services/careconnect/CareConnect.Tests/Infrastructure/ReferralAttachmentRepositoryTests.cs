using CareConnect.Application.DTOs;
using CareConnect.Domain;
using CareConnect.Infrastructure.Data;
using CareConnect.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CareConnect.Tests.Infrastructure;

public sealed class ReferralAttachmentRepositoryTests
{
    [Fact]
    public async Task GetByReferralAsync_ExcludesMessageScopedAttachments()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.CreateVersion7();
        var referralId = Guid.CreateVersion7();
        var commentId = Guid.CreateVersion7();

        var referralDocument = ReferralAttachment.Create(
            tenantId,
            referralId,
            "records.pdf",
            "application/pdf",
            1024,
            externalDocumentId: "doc-general",
            externalStorageProvider: AttachmentScope.Shared,
            status: "Uploaded",
            notes: null,
            createdByUserId: null);

        var messageAttachment = ReferralAttachment.Create(
            tenantId,
            referralId,
            "scan.png",
            "image/png",
            2048,
            externalDocumentId: "doc-message",
            externalStorageProvider: AttachmentScope.Shared,
            status: "Uploaded",
            notes: null,
            createdByUserId: null,
            referralCommentId: commentId);

        db.ReferralAttachments.AddRange(referralDocument, messageAttachment);
        await db.SaveChangesAsync();

        var repository = new ReferralAttachmentRepository(db);

        var generalDocuments = await repository.GetByReferralAsync(tenantId, referralId);
        var allAttachments = await repository.GetByReferralIncludingMessageAttachmentsAsync(tenantId, referralId);

        var document = Assert.Single(generalDocuments);
        Assert.Equal(referralDocument.Id, document.Id);
        Assert.Contains(allAttachments, attachment => attachment.Id == referralDocument.Id);
        Assert.Contains(allAttachments, attachment => attachment.Id == messageAttachment.Id);
    }

    private static CareConnectDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CareConnectDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new CareConnectDbContext(options);
    }
}
