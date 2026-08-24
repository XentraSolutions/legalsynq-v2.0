using Intake.Domain.Snapshot;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Intake.Infrastructure.Persistence.Configurations;

public sealed class DocumentAssociationExecutionConfiguration
    : IEntityTypeConfiguration<DocumentAssociationExecution>
{
    public void Configure(EntityTypeBuilder<DocumentAssociationExecution> builder)
    {
        builder.ToTable("IntakeDocumentAssociationExecutions");
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.TenantId, x.Id })
            .HasName("AK_IntakeDocumentAssociationExecutions_TenantId_Id");
        builder.Property(x => x.PolicyCode).HasMaxLength(96).IsRequired();
        builder.Property(x => x.ExecutionKey).HasMaxLength(240).IsRequired();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(240).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.FailureCode).HasMaxLength(96);
        builder.Property(x => x.FailureMessage).HasMaxLength(1000);
        builder.Property(x => x.ResultJson).HasColumnType("longtext").IsRequired();
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.Property(x => x.RequestedAt).HasPrecision(6);
        builder.Property(x => x.StartedAt).HasPrecision(6);
        builder.Property(x => x.CompletedAt).HasPrecision(6);
        builder.Property(x => x.CreatedAt).HasPrecision(6);
        builder.Property(x => x.UpdatedAt).HasPrecision(6);
        builder.HasIndex(x => new { x.TenantId, x.ExecutionKey }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.SnapshotId, x.CreatedAt });
        builder.HasMany(x => x.Items)
            .WithOne(x => x.Execution)
            .HasForeignKey(x => new { ExecutionId = x.ExecutionId, x.TenantId })
            .HasPrincipalKey(x => new { x.Id, x.TenantId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class DocumentAssociationItemConfiguration
    : IEntityTypeConfiguration<DocumentAssociationItem>
{
    public void Configure(EntityTypeBuilder<DocumentAssociationItem> builder)
    {
        builder.ToTable("IntakeDocumentAssociationItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DocumentReference).HasMaxLength(320).IsRequired();
        builder.Property(x => x.DocumentRole).HasMaxLength(96).IsRequired();
        builder.Property(x => x.TargetType).HasMaxLength(16).IsRequired();
        builder.Property(x => x.RelatedCaseId);
        builder.Property(x => x.ItemKey).HasMaxLength(320).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.FailureCode).HasMaxLength(96);
        builder.Property(x => x.FailureMessage).HasMaxLength(1000);
        builder.Property(x => x.DestinationReference).HasMaxLength(320);
        builder.Property(x => x.CreatedAt).HasPrecision(6);
        builder.Property(x => x.UpdatedAt).HasPrecision(6);
        builder.HasIndex(x => new { x.TenantId, x.ExecutionId, x.ItemKey }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.ArtifactId });
    }
}