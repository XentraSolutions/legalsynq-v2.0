using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Xenia.Domain.Email;

namespace Xenia.Infrastructure.Persistence.Configurations;

internal sealed class EmailValidationHistoryConfiguration : IEntityTypeConfiguration<EmailValidationHistory>
{
    private static readonly EnumToStringConverter<EmailProviderType> _providerTypeConverter = new();
    private static readonly EnumToStringConverter<EmailValidationResult> _resultConverter = new();

    public void Configure(EntityTypeBuilder<EmailValidationHistory> builder)
    {
        builder.ToTable("xn_email_validation_history");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasColumnType("char(36)")
            .ValueGeneratedNever();

        builder.Property(e => e.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("char(36)")
            .IsRequired();

        builder.Property(e => e.EmailSourceId)
            .HasColumnName("email_source_id")
            .HasColumnType("char(36)")
            .IsRequired();

        builder.Property(e => e.ProviderType)
            .HasColumnName("provider_type")
            .HasConversion(_providerTypeConverter)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.ValidationType)
            .HasColumnName("validation_type")
            .HasMaxLength(EmailValidationHistory.ValidationTypeMaxLength)
            .IsRequired();

        builder.Property(e => e.StartedAt)
            .HasColumnName("started_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(e => e.CompletedAt)
            .HasColumnName("completed_at")
            .HasColumnType("datetime(6)");

        builder.Property(e => e.DurationMs)
            .HasColumnName("duration_ms");

        builder.Property(e => e.Result)
            .HasColumnName("result")
            .HasConversion(_resultConverter)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.ErrorCode)
            .HasColumnName("error_code")
            .HasMaxLength(EmailValidationHistory.ErrorCodeMaxLength);

        builder.Property(e => e.ErrorSummary)
            .HasColumnName("error_summary")
            .HasMaxLength(EmailValidationHistory.ErrorSummaryMaxLength);

        builder.Property(e => e.CorrelationId)
            .HasColumnName("correlation_id")
            .HasMaxLength(EmailValidationHistory.CorrelationIdMaxLength);

        builder.Property(e => e.ActorId)
            .HasColumnName("actor_id")
            .HasColumnType("char(36)");

        builder.Property(e => e.CreatedAtUtc)
            .HasColumnName("created_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.HasIndex(e => e.TenantId)
            .HasDatabaseName("ix_xn_email_val_history_tenant");

        builder.HasIndex(e => e.EmailSourceId)
            .HasDatabaseName("ix_xn_email_val_history_source");

        builder.HasIndex(e => e.StartedAt)
            .HasDatabaseName("ix_xn_email_val_history_started");
    }
}
