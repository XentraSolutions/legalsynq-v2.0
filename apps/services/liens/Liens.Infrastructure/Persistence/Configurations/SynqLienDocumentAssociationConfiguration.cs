using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public sealed class SynqLienDocumentAssociationConfiguration
    : IEntityTypeConfiguration<SynqLienDocumentAssociation>
{
    public void Configure(EntityTypeBuilder<SynqLienDocumentAssociation> builder)
    {
        builder.ToTable("liens_SynqDocumentAssociations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DocumentReference).HasMaxLength(320).IsRequired();
        builder.Property(x => x.DocumentRole).HasMaxLength(96).IsRequired();
        builder.Property(x => x.TargetType).HasMaxLength(16).IsRequired();
        builder.Property(x => x.RelatedCaseId);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(320).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.TargetType, x.TargetId });
        builder.HasIndex(x => new { x.TenantId, x.DocumentId });
    }
}