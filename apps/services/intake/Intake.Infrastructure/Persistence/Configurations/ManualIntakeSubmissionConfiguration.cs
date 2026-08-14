using Intake.Domain.Manual;
using Intake.Domain.Sources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Intake.Infrastructure.Persistence.Configurations;

public sealed class ManualIntakeSubmissionConfiguration
    : IEntityTypeConfiguration<ManualIntakeSubmission>
{
    public void Configure(EntityTypeBuilder<ManualIntakeSubmission> builder)
    {
        builder.ToTable("ManualIntakeSubmissions");
        builder.HasKey(submission => submission.Id);

        builder.Property(submission => submission.Id).HasColumnType("char(36)");
        builder.Property(submission => submission.TenantId).HasColumnType("char(36)").IsRequired();
        builder.Property(submission => submission.OrgId).HasColumnType("char(36)");
        builder.Property(submission => submission.TenantIntakeSourceId).HasColumnType("char(36)");
        builder.Property(submission => submission.SourceType).HasMaxLength(32).IsRequired();
        builder.Property(submission => submission.Purpose).HasMaxLength(64).IsRequired();
        builder.Property(submission => submission.ProcessingProfileCode).HasMaxLength(64).IsRequired();
        builder.Property(submission => submission.Title).HasMaxLength(512);
        builder.Property(submission => submission.ExternalReference).HasMaxLength(256);
        builder.Property(submission => submission.Notes).HasMaxLength(4000);
        builder.Property(submission => submission.ClientRequestId).HasMaxLength(256);
        builder.Property(submission => submission.SubmittedBy).HasColumnType("char(36)");
        builder.Property(submission => submission.SubmittedAt).HasPrecision(6).IsRequired();
        builder.Property(submission => submission.Status).HasMaxLength(32).IsRequired();
        builder.Property(submission => submission.FailureMessage).HasMaxLength(1000);
        builder.Property(submission => submission.ConfigurationVersion).IsRequired();
        builder.Property(submission => submission.ProfileConfigurationVersion).IsRequired();
        builder.Property(submission => submission.CreatedAt).HasPrecision(6).IsRequired();
        builder.Property(submission => submission.UpdatedAt).HasPrecision(6).IsRequired();
        builder.Property(submission => submission.CompletedAt).HasPrecision(6);
        builder.Property(submission => submission.Version).IsRequired().IsConcurrencyToken();

        builder.HasOne<TenantIntakeSource>()
            .WithMany()
            .HasForeignKey(submission => submission.TenantIntakeSourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(submission => new
        {
            submission.TenantId,
            submission.ClientRequestId,
        }).IsUnique();
        builder.HasIndex(submission => new { submission.TenantId, submission.CreatedAt });
        builder.HasIndex(submission => new { submission.TenantId, submission.Status, submission.UpdatedAt });
        builder.HasIndex(submission => new { submission.TenantId, submission.Purpose, submission.CreatedAt });
    }
}