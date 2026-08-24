using Intake.Domain.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Intake.Infrastructure.Persistence.Configurations;

public sealed class IntakeRecoveryWorkItemConfiguration
    : IEntityTypeConfiguration<IntakeRecoveryWorkItem>
{
    public void Configure(EntityTypeBuilder<IntakeRecoveryWorkItem> builder)
    {
        builder.ToTable("IntakeRecoveryWorkItems");
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.TenantId, x.Id })
            .HasName("AK_IntakeRecoveryWorkItems_TenantId_Id");
        builder.Property(x => x.Stage).HasMaxLength(40).IsRequired();
        builder.Property(x => x.DomainStatus).HasMaxLength(40).IsRequired();
        builder.Property(x => x.RecoveryStatus).HasMaxLength(40).IsRequired();
        builder.Property(x => x.LastFailureCode).HasMaxLength(120);
        builder.Property(x => x.LastSafeMessage).HasMaxLength(500);
        builder.Property(x => x.FailureCategory).HasMaxLength(40);
        builder.Property(x => x.RecoverySource).HasMaxLength(24).IsRequired();
        builder.Property(x => x.ClaimToken).HasMaxLength(80);
        builder.Property(x => x.CorrelationId).HasMaxLength(128);
        builder.Property(x => x.Version).IsConcurrencyToken().ValueGeneratedNever();
        builder.Property(x => x.LastRecoveryAttemptAt).HasPrecision(6);
        builder.Property(x => x.NextRetryAt).HasPrecision(6);
        builder.Property(x => x.ExhaustedAt).HasPrecision(6);
        builder.Property(x => x.CancelledAt).HasPrecision(6);
        builder.Property(x => x.StaleSince).HasPrecision(6);
        builder.Property(x => x.ClaimedAt).HasPrecision(6);
        builder.Property(x => x.CreatedAt).HasPrecision(6).IsRequired();
        builder.Property(x => x.UpdatedAt).HasPrecision(6).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.Stage, x.ObjectId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.RecoveryStatus, x.NextRetryAt, x.StaleSince });
        builder.HasIndex(x => new { x.TenantId, x.Stage, x.UpdatedAt });
        builder.HasMany(x => x.Attempts)
            .WithOne()
            .HasForeignKey(x => new { x.WorkItemId, x.TenantId })
            .HasPrincipalKey(x => new { x.Id, x.TenantId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class IntakeRecoveryAttemptConfiguration
    : IEntityTypeConfiguration<IntakeRecoveryAttempt>
{
    public void Configure(EntityTypeBuilder<IntakeRecoveryAttempt> builder)
    {
        builder.ToTable("IntakeRecoveryAttempts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasMaxLength(40).IsRequired();
        builder.Property(x => x.FailureCode).HasMaxLength(120);
        builder.Property(x => x.SafeMessage).HasMaxLength(500);
        builder.Property(x => x.FailureCategory).HasMaxLength(40);
        builder.Property(x => x.RecoverySource).HasMaxLength(24).IsRequired();
        builder.Property(x => x.StartedAt).HasPrecision(6).IsRequired();
        builder.Property(x => x.CompletedAt).HasPrecision(6);
        builder.HasIndex(x => new { x.TenantId, x.WorkItemId, x.AttemptNumber }).IsUnique();
    }
}