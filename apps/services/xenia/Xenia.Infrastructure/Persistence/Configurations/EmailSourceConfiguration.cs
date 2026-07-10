using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Xenia.Domain.Email;

namespace Xenia.Infrastructure.Persistence.Configurations;

internal sealed class EmailSourceConfiguration : IEntityTypeConfiguration<EmailSource>
{
    private static readonly EnumToStringConverter<EmailProviderType> _providerTypeConverter = new();
    private static readonly EnumToStringConverter<EmailAuthType> _authTypeConverter = new();
    private static readonly EnumToStringConverter<EmailSourceStatus> _statusConverter = new();
    private static readonly EnumToStringConverter<EmailHealthStatus> _healthStatusConverter = new();
    private static readonly EnumToStringConverter<EmailValidationStatus> _validationStatusConverter = new();

    public void Configure(EntityTypeBuilder<EmailSource> builder)
    {
        builder.ToTable("xn_email_sources");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasColumnType("char(36)")
            .ValueGeneratedNever();

        builder.Property(e => e.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("char(36)")
            .IsRequired();

        builder.Property(e => e.ModuleKey)
            .HasColumnName("module_key")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(EmailSource.DisplayNameMaxLength)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasColumnName("description")
            .HasMaxLength(EmailSource.DescriptionMaxLength);

        builder.Property(e => e.ProviderType)
            .HasColumnName("provider_type")
            .HasConversion(_providerTypeConverter)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.AuthType)
            .HasColumnName("auth_type")
            .HasConversion(_authTypeConverter)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.EmailAddress)
            .HasColumnName("email_address")
            .HasMaxLength(EmailSource.EmailAddressMaxLength)
            .IsRequired();

        builder.Property(e => e.Username)
            .HasColumnName("username")
            .HasMaxLength(EmailSource.UsernameMaxLength);

        builder.Property(e => e.IncomingHost)
            .HasColumnName("incoming_host")
            .HasMaxLength(EmailSource.HostMaxLength);

        builder.Property(e => e.IncomingPort)
            .HasColumnName("incoming_port");

        builder.Property(e => e.UseTls)
            .HasColumnName("use_tls")
            .IsRequired();

        builder.Property(e => e.MailboxFolder)
            .HasColumnName("mailbox_folder")
            .HasMaxLength(EmailSource.FolderMaxLength);

        builder.Property(e => e.SecretReferenceId)
            .HasColumnName("secret_reference_id")
            .HasMaxLength(EmailSource.SecretRefMaxLength);

        builder.Property(e => e.OAuthConnectionRef)
            .HasColumnName("oauth_connection_ref")
            .HasMaxLength(EmailSource.SecretRefMaxLength);

        builder.Property(e => e.Enabled)
            .HasColumnName("enabled")
            .IsRequired();

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasConversion(_statusConverter)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.HealthStatus)
            .HasColumnName("health_status")
            .HasConversion(_healthStatusConverter)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.ValidationStatus)
            .HasColumnName("validation_status")
            .HasConversion(_validationStatusConverter)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.LastValidatedAt)
            .HasColumnName("last_validated_at")
            .HasColumnType("datetime(6)");

        builder.Property(e => e.LastSuccessfulValidationAt)
            .HasColumnName("last_successful_validation_at")
            .HasColumnType("datetime(6)");

        builder.Property(e => e.LastValidationLatencyMs)
            .HasColumnName("last_validation_latency_ms");

        builder.Property(e => e.LastConnectionAt)
            .HasColumnName("last_connection_at")
            .HasColumnType("datetime(6)");

        builder.Property(e => e.LastErrorCode)
            .HasColumnName("last_error_code")
            .HasMaxLength(EmailSource.ErrorCodeMaxLength);

        builder.Property(e => e.LastErrorSummary)
            .HasColumnName("last_error_summary")
            .HasMaxLength(EmailSource.ErrorSummaryMaxLength);

        builder.Property(e => e.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("char(36)");

        builder.Property(e => e.UpdatedBy)
            .HasColumnName("updated_by")
            .HasColumnType("char(36)");

        builder.Property(e => e.RowVersion)
            .HasColumnName("row_version")
            .IsRequired();

        builder.Property(e => e.CreatedAtUtc)
            .HasColumnName("created_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(e => e.UpdatedAtUtc)
            .HasColumnName("updated_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.HasIndex(e => e.TenantId)
            .HasDatabaseName("ix_xn_email_sources_tenant_id");

        builder.HasIndex(new[] { "TenantId", "ProviderType" })
            .HasDatabaseName("ix_xn_email_sources_tenant_provider");

        builder.HasIndex(new[] { "TenantId", "Status" })
            .HasDatabaseName("ix_xn_email_sources_tenant_status");
    }
}
