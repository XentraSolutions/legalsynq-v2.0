using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public sealed class LegacyImportExceptionConfiguration : IEntityTypeConfiguration<LegacyImportException>
{
    public void Configure(EntityTypeBuilder<LegacyImportException> builder)
    {
        builder.ToTable("liens_LegacyImportExceptions");
        builder.HasKey(exception => exception.Id);

        builder.Property(exception => exception.TenantId).IsRequired();
        builder.Property(exception => exception.ImportRunId).IsRequired();
        builder.Property(exception => exception.SourceTable).IsRequired().HasMaxLength(100);
        builder.Property(exception => exception.LegacyId).IsRequired().HasMaxLength(100);
        builder.Property(exception => exception.Severity).IsRequired().HasMaxLength(20);
        builder.Property(exception => exception.ErrorCode).IsRequired().HasMaxLength(100);
        builder.Property(exception => exception.Message).IsRequired().HasMaxLength(2000);
        builder.Property(exception => exception.SourceHash).HasMaxLength(128);
        builder.Property(exception => exception.CreatedAtUtc).IsRequired();

        builder.HasIndex(exception => new { exception.TenantId, exception.ImportRunId, exception.Severity })
            .HasDatabaseName("IX_LegacyImportExceptions_Tenant_Run_Severity");

        builder.HasOne<LegacyImportRun>()
            .WithMany()
            .HasForeignKey(exception => exception.ImportRunId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
