using Documents.Application.DTOs;
using Documents.Domain.Entities;
using Documents.Domain.Enums;
using Xunit;

namespace Documents.Tests;

public sealed class DocumentResponseTimezoneTests
{
    [Fact]
    public void DocumentResponse_From_converts_document_timestamps_to_pacific_time()
    {
        var createdUtc = new DateTime(2026, 7, 21, 14, 35, 0, DateTimeKind.Utc);
        var updatedUtc = createdUtc.AddMinutes(5);

        var document = new Document
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ProductId = "SYNQ_LIENS",
            ReferenceId = "CASE-1",
            ReferenceType = "Case",
            DocumentTypeId = Guid.NewGuid(),
            Title = "Upload",
            Status = DocumentStatus.Active,
            MimeType = "application/pdf",
            FileSizeBytes = 1024,
            ScanStatus = ScanStatus.Clean,
            CreatedAt = createdUtc,
            CreatedBy = Guid.NewGuid(),
            UpdatedAt = updatedUtc,
            UpdatedBy = Guid.NewGuid(),
        };

        var response = DocumentResponse.From(document);

        Assert.Equal(new DateTimeOffset(2026, 7, 21, 7, 35, 0, TimeSpan.FromHours(-7)), response.CreatedAt);
        Assert.Equal(new DateTimeOffset(2026, 7, 21, 7, 40, 0, TimeSpan.FromHours(-7)), response.UpdatedAt);
    }

    [Fact]
    public void DocumentVersionResponse_From_converts_uploaded_at_to_pacific_time()
    {
        var uploadedUtc = new DateTime(2026, 7, 21, 14, 35, 0, DateTimeKind.Utc);

        var version = new DocumentVersion
        {
            Id = Guid.NewGuid(),
            DocumentId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            VersionNumber = 2,
            MimeType = "application/pdf",
            FileSizeBytes = 2048,
            ScanStatus = ScanStatus.Clean,
            UploadedAt = uploadedUtc,
            UploadedBy = Guid.NewGuid(),
        };

        var response = DocumentVersionResponse.From(version);

        Assert.Equal(new DateTimeOffset(2026, 7, 21, 7, 35, 0, TimeSpan.FromHours(-7)), response.UploadedAt);
    }

    [Fact]
    public void DocumentResponse_From_uses_standard_time_offset_outside_dst()
    {
        var createdUtc = new DateTime(2026, 12, 21, 15, 35, 0, DateTimeKind.Utc);

        var document = new Document
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ProductId = "SYNQ_LIENS",
            ReferenceId = "CASE-1",
            ReferenceType = "Case",
            DocumentTypeId = Guid.NewGuid(),
            Title = "Upload",
            Status = DocumentStatus.Active,
            MimeType = "application/pdf",
            FileSizeBytes = 1024,
            ScanStatus = ScanStatus.Clean,
            CreatedAt = createdUtc,
            CreatedBy = Guid.NewGuid(),
            UpdatedAt = createdUtc,
            UpdatedBy = Guid.NewGuid(),
        };

        var response = DocumentResponse.From(document);

        Assert.Equal(new DateTimeOffset(2026, 12, 21, 7, 35, 0, TimeSpan.FromHours(-8)), response.CreatedAt);
    }
}
