using Intake.Domain.Emails;
using Intake.Domain.Sources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Intake.Infrastructure.Persistence.Configurations;

public sealed class InboundEmailCaptureFailureConfiguration
    : IEntityTypeConfiguration<InboundEmailCaptureFailure>
{
    public void Configure(EntityTypeBuilder<InboundEmailCaptureFailure> builder)
    {
        builder.ToTable("InboundEmailCaptureFailures");
        builder.HasKey(failure => failure.Id);

        builder.Property(failure => failure.Id).HasColumnType("char(36)");
        builder.Property(failure => failure.TenantId).HasColumnType("char(36)").IsRequired();
        builder.Property(failure => failure.TenantIntakeSourceId).HasColumnType("char(36)").IsRequired();
        builder.Property(failure => failure.Provider).HasMaxLength(64);
        builder.Property(failure => failure.FailureCode).HasMaxLength(128).IsRequired();
        builder.Property(failure => failure.OccurredAt).HasPrecision(6).IsRequired();
        builder.Property(failure => failure.CorrelationId).HasMaxLength(128);

        builder.HasOne<TenantIntakeSource>()
            .WithMany()
            .HasForeignKey(failure => failure.TenantIntakeSourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(failure => new { failure.TenantId, failure.OccurredAt });
        builder.HasIndex(failure => new
        {
            failure.TenantId,
            failure.TenantIntakeSourceId,
            failure.OccurredAt,
        });
        builder.HasIndex(failure => new { failure.TenantId, failure.FailureCode, failure.OccurredAt });
    }
}