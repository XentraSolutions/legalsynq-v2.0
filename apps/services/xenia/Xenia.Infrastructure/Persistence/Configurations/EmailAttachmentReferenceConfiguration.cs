using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Xenia.Domain.Email;

namespace Xenia.Infrastructure.Persistence.Configurations;

internal sealed class EmailAttachmentReferenceConfiguration : IEntityTypeConfiguration<EmailAttachmentReference>
{
    private static readonly EnumToStringConverter<AttachmentDispatchStatus> _statusConverter = new();

    public void Configure(EntityTypeBuilder<EmailAttachmentReference> builder)
    {
        builder.ToTable("xn_email_attachment_references");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id").HasColumnType("char(36)").ValueGeneratedNever();

        builder.Property(e => e.TenantId)
            .HasColumnName("tenant_id").HasColumnType("char(36)").IsRequired();

        builder.Property(e => e.EmailMessageId)
            .HasColumnName("email_message_id").HasColumnType("char(36)").IsRequired();

        builder.Property(e => e.ProviderAttachmentId)
            .HasColumnName("provider_attachment_id").HasMaxLength(EmailAttachmentReference.ProviderAttachmentIdMaxLength);

        builder.Property(e => e.DocumentReferenceId)
            .HasColumnName("document_reference_id").HasColumnType("char(36)");

        builder.Property(e => e.FileName)
            .HasColumnName("file_name").HasMaxLength(EmailAttachmentReference.FileNameMaxLength).IsRequired();

        builder.Property(e => e.MimeType)
            .HasColumnName("mime_type").HasMaxLength(EmailAttachmentReference.MimeTypeMaxLength);

        builder.Property(e => e.SizeBytes)
            .HasColumnName("size_bytes");

        builder.Property(e => e.ContentHash)
            .HasColumnName("content_hash").HasMaxLength(EmailAttachmentReference.ContentHashMaxLength);

        builder.Property(e => e.IsInline)
            .HasColumnName("is_inline").IsRequired();

        builder.Property(e => e.ContentId)
            .HasColumnName("content_id").HasMaxLength(EmailAttachmentReference.ContentIdMaxLength);

        builder.Property(e => e.Disposition)
            .HasColumnName("disposition").HasMaxLength(EmailAttachmentReference.DispositionMaxLength);

        builder.Property(e => e.DispatchStatus)
            .HasColumnName("dispatch_status")
            .HasConversion(_statusConverter).HasMaxLength(32).IsRequired();

        builder.Property(e => e.ErrorCode)
            .HasColumnName("error_code").HasMaxLength(EmailAttachmentReference.ErrorCodeMaxLength);

        builder.Property(e => e.SafeErrorSummary)
            .HasColumnName("safe_error_summary").HasMaxLength(EmailAttachmentReference.SafeErrorSummaryMaxLength);

        builder.Property(e => e.CreatedAtUtc)
            .HasColumnName("created_at_utc").HasColumnType("datetime(6)").IsRequired();

        builder.Property(e => e.UpdatedAtUtc)
            .HasColumnName("updated_at_utc").HasColumnType("datetime(6)").IsRequired();

        // Partial unique: provider attachment ID per message (if not null)
        builder.HasIndex(e => new { e.TenantId, e.EmailMessageId, e.ProviderAttachmentId })
            .HasDatabaseName("ix_email_attachments_provider_id");

        builder.HasIndex(e => e.EmailMessageId).HasDatabaseName("ix_email_attachments_message");
        builder.HasIndex(e => new { e.TenantId, e.DispatchStatus }).HasDatabaseName("ix_email_attachments_dispatch_status");
    }
}
