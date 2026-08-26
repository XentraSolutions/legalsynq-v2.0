using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public sealed class SellingCaseDraftConfiguration : IEntityTypeConfiguration<SellingCaseDraft>
{
    public void Configure(EntityTypeBuilder<SellingCaseDraft> builder)
    {
        builder.ToTable("liens_SellingCaseDrafts");

        builder.HasKey(draft => draft.Id);

        builder.Property(draft => draft.Id).IsRequired();
        builder.Property(draft => draft.TenantId).IsRequired();
        builder.Property(draft => draft.OrgId).IsRequired();
        builder.Property(draft => draft.CaseStatus).IsRequired().HasMaxLength(50);
        builder.Property(draft => draft.AccidentTypeId).HasMaxLength(100);
        builder.Property(draft => draft.AccidentState).HasMaxLength(100);
        builder.Property(draft => draft.DateOfLoss).HasColumnType("date");
        builder.Property(draft => draft.CaseTrackingNotes).HasMaxLength(4000);
        builder.Property(draft => draft.FinalizedAtUtc);
        builder.Property(draft => draft.CreatedByUserId).IsRequired();
        builder.Property(draft => draft.UpdatedByUserId);
        builder.Property(draft => draft.CreatedAtUtc).IsRequired();
        builder.Property(draft => draft.UpdatedAtUtc).IsRequired();

        builder.HasIndex(draft => new { draft.TenantId, draft.OrgId, draft.CreatedAtUtc })
            .HasDatabaseName("IX_SellingCaseDrafts_Tenant_Org_CreatedAtUtc");

        builder.HasIndex(draft => new { draft.TenantId, draft.OrgId, draft.FinalizedAtUtc })
            .HasDatabaseName("IX_SellingCaseDrafts_Tenant_Org_FinalizedAtUtc");

        builder.HasIndex(draft => draft.CaseId)
            .IsUnique()
            .HasDatabaseName("UX_SellingCaseDrafts_CaseId");

        builder.HasOne<Case>()
            .WithMany()
            .HasForeignKey(draft => draft.CaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(draft => draft.HandlingLawFirmCompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<CompanyContactPerson>()
            .WithMany()
            .HasForeignKey(draft => draft.CaseManagerContactPersonId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
