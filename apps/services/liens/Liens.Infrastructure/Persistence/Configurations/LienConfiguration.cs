using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public class LienConfiguration : IEntityTypeConfiguration<Lien>
{
    public void Configure(EntityTypeBuilder<Lien> builder)
    {
        builder.ToTable("liens_Liens");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id).IsRequired();
        builder.Property(l => l.TenantId).IsRequired();
        builder.Property(l => l.OrgId).IsRequired();

        builder.Property(l => l.LienNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(l => l.ExternalReference)
            .HasMaxLength(200);

        builder.Property(l => l.LienType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(l => l.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(l => l.CaseId);
        builder.Property(l => l.FacilityId);
        builder.Property(l => l.SubjectPartyId);

        builder.Property(l => l.SubjectFirstName)
            .HasMaxLength(100);

        builder.Property(l => l.SubjectLastName)
            .HasMaxLength(100);

        builder.Property(l => l.IsConfidential)
            .IsRequired();

        builder.Property(l => l.OriginalAmount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(l => l.CurrentBalance)
            .HasColumnType("decimal(18,2)");

        builder.Property(l => l.OfferPrice)
            .HasColumnType("decimal(18,2)");

        builder.Property(l => l.PurchasePrice)
            .HasColumnType("decimal(18,2)");

        builder.Property(l => l.PayoffAmount)
            .HasColumnType("decimal(18,2)");

        builder.Property(l => l.Jurisdiction)
            .HasMaxLength(100);

        builder.Property(l => l.Description)
            .HasMaxLength(4000);

        builder.Property(l => l.Notes)
            .HasMaxLength(4000);

        builder.Property(l => l.BuyerMessage)
            .HasMaxLength(4000);

        builder.Property(l => l.IncidentDate)
            .HasColumnType("date");

        builder.Property(l => l.PurchaseDate)
            .HasColumnType("date");

        builder.Property(l => l.ReceivableDueDate)
            .HasColumnType("date");

        builder.Property(l => l.InitialServiceDate)
            .HasColumnType("date");

        builder.Property(l => l.EndServiceDate)
            .HasColumnType("date");

        builder.Property(l => l.IsBulk)
            .HasMaxLength(10);

        builder.Property(l => l.IsServicing)
            .HasMaxLength(10);

        builder.Property(l => l.OpenedAtUtc);
        builder.Property(l => l.ClosedAtUtc);

        builder.Property(l => l.SellingOrgId);
        builder.Property(l => l.BuyingOrgId);
        builder.Property(l => l.HoldingOrgId);

        builder.Property(l => l.SellerStatus)
            .HasMaxLength(50);

        builder.Property(l => l.ListingVisibility)
            .HasMaxLength(50);

        builder.Property(l => l.FundingCompanyId);
        builder.Property(l => l.FundingCompanyContactId);
        builder.Property(l => l.FundingCompanyCompanyId);
        builder.Property(l => l.FundingCompanyContactPersonId);
        builder.Property(l => l.MedicalProviderCompanyId);
        builder.Property(l => l.MedicalFacilityCompanyId);

        builder.Property(l => l.AskAmount)
            .HasColumnType("decimal(18,2)");

        builder.Property(l => l.HighestBidAmount)
            .HasColumnType("decimal(18,2)");

        builder.Property(l => l.SubmittedForSaleAtUtc);
        builder.Property(l => l.SoldAtUtc);
        builder.Property(l => l.WithdrawnAtUtc);
        builder.Property(l => l.ArchivedAtUtc);

        builder.Property(l => l.ArchivedReason)
            .HasMaxLength(1000);

        builder.Property(l => l.CreatedByUserId).IsRequired();
        builder.Property(l => l.UpdatedByUserId);
        builder.Property(l => l.CreatedAtUtc).IsRequired();
        builder.Property(l => l.UpdatedAtUtc).IsRequired();

        builder.HasIndex(l => new { l.TenantId, l.LienNumber })
            .IsUnique()
            .HasDatabaseName("UX_Liens_TenantId_LienNumber");

        builder.HasIndex(l => new { l.TenantId, l.OrgId, l.Status })
            .HasDatabaseName("IX_Liens_TenantId_OrgId_Status");

        builder.HasIndex(l => new { l.TenantId, l.Status })
            .HasDatabaseName("IX_Liens_TenantId_Status");

        builder.HasIndex(l => new { l.TenantId, l.LienType })
            .HasDatabaseName("IX_Liens_TenantId_LienType");

        builder.HasIndex(l => l.CaseId)
            .HasDatabaseName("IX_Liens_CaseId");

        builder.HasIndex(l => l.FacilityId)
            .HasDatabaseName("IX_Liens_FacilityId");

        builder.HasIndex(l => new { l.TenantId, l.SellingOrgId, l.Status })
            .HasDatabaseName("IX_Liens_TenantId_SellingOrgId_Status");

        builder.HasIndex(l => new { l.TenantId, l.SellingOrgId, l.SellerStatus })
            .HasDatabaseName("IX_Liens_TenantId_SellingOrgId_SellerStatus");

        builder.HasIndex(l => new { l.TenantId, l.SellingOrgId, l.SellerStatus, l.SubmittedForSaleAtUtc })
            .HasDatabaseName("IX_Liens_Tenant_Seller_Status_Submitted");

        builder.HasIndex(l => new { l.TenantId, l.SellingOrgId, l.SellerStatus, l.SoldAtUtc })
            .HasDatabaseName("IX_Liens_Tenant_Seller_Status_Sold");

        builder.HasIndex(l => new { l.TenantId, l.SellingOrgId, l.InitialServiceDate })
            .HasDatabaseName("IX_Liens_Tenant_Seller_InitialService");

        builder.HasIndex(l => new { l.TenantId, l.SellingOrgId, l.FundingCompanyId, l.SellerStatus })
            .HasDatabaseName("IX_Liens_Tenant_Seller_Funding_Status");

        builder.HasIndex(l => new { l.TenantId, l.SellerStatus, l.ListingVisibility })
            .HasDatabaseName("IX_Liens_Tenant_SellerStatus_Visibility");

        builder.HasIndex(l => new { l.TenantId, l.BuyingOrgId })
            .HasDatabaseName("IX_Liens_TenantId_BuyingOrgId");

        builder.HasIndex(l => new { l.TenantId, l.HoldingOrgId, l.Status })
            .HasDatabaseName("IX_Liens_TenantId_HoldingOrgId_Status");

        builder.HasIndex(l => new { l.TenantId, l.CreatedAtUtc })
            .HasDatabaseName("IX_Liens_TenantId_CreatedAtUtc");

        builder.HasIndex(l => new { l.TenantId, l.PurchaseDate })
            .HasDatabaseName("IX_Liens_TenantId_PurchaseDate");

        builder.HasIndex(l => new { l.TenantId, l.SellingOrgId, l.ReceivableDueDate })
            .HasDatabaseName("IX_Liens_Tenant_Seller_ReceivableDueDate");

        builder.HasIndex(l => new { l.TenantId, l.SellingOrgId, l.FundingCompanyCompanyId })
            .HasDatabaseName("IX_Liens_Tenant_Seller_FundingCompanyCompanyId");

        builder.HasOne<Case>()
            .WithMany()
            .HasForeignKey(l => l.CaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Facility>()
            .WithMany()
            .HasForeignKey(l => l.FacilityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Company>().WithMany().HasForeignKey(l => l.FundingCompanyCompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CompanyContactPerson>().WithMany().HasForeignKey(l => l.FundingCompanyContactPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Company>().WithMany().HasForeignKey(l => l.MedicalProviderCompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Company>().WithMany().HasForeignKey(l => l.MedicalFacilityCompanyId).OnDelete(DeleteBehavior.Restrict);
    }
}
