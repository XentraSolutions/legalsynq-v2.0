using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenia.Domain.Email;

namespace Xenia.Infrastructure.Persistence.Configurations;

internal sealed class EmailSourceConfiguration : IEntityTypeConfiguration<EmailSource>
{

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
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.AuthType)
            .HasColumnName("auth_type")
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
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.HealthStatus)
            .HasColumnName("health_status")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.ValidationStatus)
            .HasColumnName("validation_status")
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

        // ── Soft delete ───────────────────────────────────────────────────────
        builder.Property(e => e.IsDeleted)
            .HasColumnName("is_deleted")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.DeletedAt)
            .HasColumnName("deleted_at")
            .HasColumnType("datetime(6)");

        builder.Property(e => e.DeletedBy)
            .HasColumnName("deleted_by")
            .HasColumnType("char(36)");

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
