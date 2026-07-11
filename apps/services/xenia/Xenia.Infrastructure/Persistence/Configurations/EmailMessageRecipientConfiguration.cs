using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Xenia.Domain.Email;

namespace Xenia.Infrastructure.Persistence.Configurations;

internal sealed class EmailMessageRecipientConfiguration : IEntityTypeConfiguration<EmailMessageRecipient>
{
    private static readonly EnumToStringConverter<EmailRecipientType> _recipientTypeConverter = new();

    public void Configure(EntityTypeBuilder<EmailMessageRecipient> builder)
    {
        builder.ToTable("xn_email_recipients");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id").HasColumnType("char(36)").ValueGeneratedNever();

        builder.Property(e => e.TenantId)
            .HasColumnName("tenant_id").HasColumnType("char(36)").IsRequired();

        builder.Property(e => e.EmailMessageId)
            .HasColumnName("email_message_id").HasColumnType("char(36)").IsRequired();

        builder.Property(e => e.RecipientType)
            .HasColumnName("recipient_type")
            .HasConversion(_recipientTypeConverter).HasMaxLength(16).IsRequired();

        builder.Property(e => e.EmailAddress)
            .HasColumnName("email_address").HasMaxLength(EmailMessageRecipient.AddressMaxLength).IsRequired();

        builder.Property(e => e.DisplayName)
            .HasColumnName("display_name").HasMaxLength(EmailMessageRecipient.DisplayNameMaxLength);

        builder.Property(e => e.CreatedAtUtc)
            .HasColumnName("created_at_utc").HasColumnType("datetime(6)").IsRequired();

        // Query indexes
        builder.HasIndex(e => e.EmailMessageId).HasDatabaseName("ix_email_recipients_message");
        builder.HasIndex(e => new { e.TenantId, e.EmailAddress }).HasDatabaseName("ix_email_recipients_address");
    }
}
