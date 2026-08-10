using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public sealed class SellingPartyAliasConfiguration : IEntityTypeConfiguration<SellingPartyAlias>
{
    public void Configure(EntityTypeBuilder<SellingPartyAlias> builder)
    {
        builder.ToTable("liens_SellingPartyAliases", table => table.HasCheckConstraint(
            "CK_SellingPartyAliases_ExactlyOneTarget",
            "(`CompanyId` IS NOT NULL AND `CompanyContactPersonId` IS NULL) OR (`CompanyId` IS NULL AND `CompanyContactPersonId` IS NOT NULL)"));
        builder.HasKey(value => value.Id);
        builder.Property(value => value.TenantId).IsRequired();
        builder.Property(value => value.ScopeKind).IsRequired().HasMaxLength(30);
        builder.Property(value => value.ScopeId).IsRequired();
        builder.Property(value => value.Namespace).IsRequired().HasMaxLength(50);
        builder.Property(value => value.WorkflowProvenance).IsRequired().HasMaxLength(80);
        builder.Property(value => value.ExternalId).IsRequired();
        builder.Property(value => value.IsPreferred).IsRequired();
        builder.Property(value => value.PreferredCompanyKey)
            .HasComputedColumnSql("CASE WHEN `IsPreferred` = 1 THEN `CompanyId` ELSE NULL END", stored: true);
        builder.Property(value => value.PreferredContactPersonKey)
            .HasComputedColumnSql("CASE WHEN `IsPreferred` = 1 THEN `CompanyContactPersonId` ELSE NULL END", stored: true);
        builder.Property(value => value.CreatedAtUtc).IsRequired();
        builder.Property(value => value.UpdatedAtUtc).IsRequired();
        builder.Property(value => value.CreatedByUserId).IsRequired();
        builder.Property(value => value.UpdatedByUserId);

        builder.HasOne(value => value.Company).WithMany().HasForeignKey(value => value.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.CompanyContactPerson).WithMany()
            .HasForeignKey(value => value.CompanyContactPersonId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(value => new
            {
                value.TenantId, value.ScopeKind, value.ScopeId, value.Namespace,
                value.WorkflowProvenance, value.ExternalId,
            })
            .IsUnique()
            .HasDatabaseName("UX_SellingPartyAliases_ExternalScope");
        builder.HasIndex(value => new
            {
                value.TenantId, value.ScopeKind, value.ScopeId, value.Namespace,
                value.WorkflowProvenance, value.PreferredCompanyKey,
            })
            .IsUnique()
            .HasDatabaseName("UX_SellingPartyAliases_PreferredCompany");
        builder.HasIndex(value => new
            {
                value.TenantId, value.ScopeKind, value.ScopeId, value.Namespace,
                value.WorkflowProvenance, value.PreferredContactPersonKey,
            })
            .IsUnique()
            .HasDatabaseName("UX_SellingPartyAliases_PreferredContact");
    }
}
