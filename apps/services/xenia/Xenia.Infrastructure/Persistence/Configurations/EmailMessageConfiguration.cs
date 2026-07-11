using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Xenia.Domain.Email;

namespace Xenia.Infrastructure.Persistence.Configurations;

internal sealed class EmailMessageConfiguration : IEntityTypeConfiguration<EmailMessage>
{
    private static readonly EnumToStringConverter<EmailProviderType>     _providerTypeConverter = new();
    private static readonly EnumToStringConverter<MessageImportStatus>   _importStatusConverter = new();
    private static readonly EnumToStringConverter<MessageProcessingState>_processingStateConverter = new();
    private static readonly EnumToStringConverter<EmailMessageBodyType>  _bodyTypeConverter = new();
    private static readonly EnumToStringConverter<EmailImportance>       _importanceConverter = new();

    public void Configure(EntityTypeBuilder<EmailMessage> builder)
    {
        builder.ToTable("xn_email_messages");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id").HasColumnType("char(36)").ValueGeneratedNever();

        builder.Property(e => e.TenantId)
            .HasColumnName("tenant_id").HasColumnType("char(36)").IsRequired();

        builder.Property(e => e.EmailSourceId)
            .HasColumnName("email_source_id").HasColumnType("char(36)").IsRequired();

        builder.Property(e => e.ProviderType)
            .HasColumnName("provider_type")
            .HasConversion(_providerTypeConverter).HasMaxLength(32).IsRequired();

        builder.Property(e => e.ProviderMessageId)
            .HasColumnName("provider_message_id").HasMaxLength(EmailMessage.ProviderMessageIdMaxLength).IsRequired();

        builder.Property(e => e.InternetMessageId)
            .HasColumnName("internet_message_id").HasMaxLength(EmailMessage.InternetMessageIdMaxLength);

        builder.Property(e => e.ThreadId)
            .HasColumnName("thread_id").HasMaxLength(EmailMessage.ThreadIdMaxLength);

        builder.Property(e => e.ConversationId)
            .HasColumnName("conversation_id").HasMaxLength(EmailMessage.ConversationIdMaxLength);

        builder.Property(e => e.Subject)
            .HasColumnName("subject").HasMaxLength(EmailMessage.SubjectMaxLength);

        builder.Property(e => e.FromAddress)
            .HasColumnName("from_address").HasMaxLength(EmailMessage.AddressMaxLength);

        builder.Property(e => e.FromName)
            .HasColumnName("from_name").HasMaxLength(EmailMessage.DisplayNameMaxLength);

        builder.Property(e => e.SenderAddress)
            .HasColumnName("sender_address").HasMaxLength(EmailMessage.AddressMaxLength);

        builder.Property(e => e.SenderName)
            .HasColumnName("sender_name").HasMaxLength(EmailMessage.DisplayNameMaxLength);

        builder.Property(e => e.ReplyToAddresses)
            .HasColumnName("reply_to_addresses").HasMaxLength(EmailMessage.ReplyToAddressesMaxLength);

        builder.Property(e => e.SentAt)
            .HasColumnName("sent_at").HasColumnType("datetime(6)");

        builder.Property(e => e.ReceivedAt)
            .HasColumnName("received_at").HasColumnType("datetime(6)");

        builder.Property(e => e.Importance)
            .HasColumnName("importance")
            .HasConversion(_importanceConverter).HasMaxLength(16).IsRequired();

        builder.Property(e => e.IsRead)
            .HasColumnName("is_read");

        builder.Property(e => e.HasAttachments)
            .HasColumnName("has_attachments").IsRequired();

        builder.Property(e => e.AttachmentCount)
            .HasColumnName("attachment_count").IsRequired();

        builder.Property(e => e.BodyType)
            .HasColumnName("body_type")
            .HasConversion(_bodyTypeConverter).HasMaxLength(16).IsRequired();

        builder.Property(e => e.BodyText)
            .HasColumnName("body_text").HasColumnType("mediumtext");

        builder.Property(e => e.BodyHtml)
            .HasColumnName("body_html").HasColumnType("mediumtext");

        builder.Property(e => e.BodyPreview)
            .HasColumnName("body_preview").HasMaxLength(EmailMessage.BodyPreviewMaxLength);

        builder.Property(e => e.HeadersJson)
            .HasColumnName("headers_json").HasColumnType("text");

        builder.Property(e => e.ProviderMetadataJson)
            .HasColumnName("provider_metadata_json").HasMaxLength(EmailMessage.ProviderMetadataMaxLength);

        builder.Property(e => e.ContentHash)
            .HasColumnName("content_hash").HasMaxLength(EmailMessage.ContentHashMaxLength);

        builder.Property(e => e.ImportStatus)
            .HasColumnName("import_status")
            .HasConversion(_importStatusConverter).HasMaxLength(32).IsRequired();

        builder.Property(e => e.ProcessingState)
            .HasColumnName("processing_state")
            .HasConversion(_processingStateConverter).HasMaxLength(32).IsRequired();

        builder.Property(e => e.ImportedAt)
            .HasColumnName("imported_at").HasColumnType("datetime(6)");

        builder.Property(e => e.LastObservedAt)
            .HasColumnName("last_observed_at").HasColumnType("datetime(6)");

        builder.Property(e => e.LastIngestionRunId)
            .HasColumnName("last_ingestion_run_id").HasColumnType("char(36)");

        builder.Property(e => e.Version)
            .HasColumnName("version").IsRequired();

        builder.Property(e => e.CreatedAtUtc)
            .HasColumnName("created_at_utc").HasColumnType("datetime(6)").IsRequired();

        builder.Property(e => e.UpdatedAtUtc)
            .HasColumnName("updated_at_utc").HasColumnType("datetime(6)").IsRequired();

        // Unique constraint: one provider message per tenant/source/provider
        builder.HasIndex(e => new { e.TenantId, e.EmailSourceId, e.ProviderType, e.ProviderMessageId })
            .IsUnique()
            .HasDatabaseName("ux_email_messages_provider_unique");

        // Secondary duplicate signal
        builder.HasIndex(e => new { e.TenantId, e.InternetMessageId })
            .HasDatabaseName("ix_email_messages_internet_message_id");

        // Query indexes
        builder.HasIndex(e => e.TenantId).HasDatabaseName("ix_email_messages_tenant");
        builder.HasIndex(e => new { e.TenantId, e.EmailSourceId }).HasDatabaseName("ix_email_messages_source");
        builder.HasIndex(e => new { e.TenantId, e.ReceivedAt }).HasDatabaseName("ix_email_messages_received_at");
        builder.HasIndex(e => new { e.TenantId, e.ImportStatus }).HasDatabaseName("ix_email_messages_import_status");
        builder.HasIndex(e => new { e.TenantId, e.HasAttachments }).HasDatabaseName("ix_email_messages_has_attachments");
    }
}
