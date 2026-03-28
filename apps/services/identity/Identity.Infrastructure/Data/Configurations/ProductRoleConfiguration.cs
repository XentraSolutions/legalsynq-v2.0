using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

public class ProductRoleConfiguration : IEntityTypeConfiguration<ProductRole>
{
    public void Configure(EntityTypeBuilder<ProductRole> builder)
    {
        builder.ToTable("ProductRoles");

        builder.HasKey(pr => pr.Id);

        builder.Property(pr => pr.ProductId).IsRequired();

        builder.Property(pr => pr.Code)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(pr => pr.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(pr => pr.Description)
            .HasMaxLength(1000);

        builder.Property(pr => pr.EligibleOrgType)
            .HasMaxLength(50);

        builder.Property(pr => pr.IsActive).IsRequired();
        builder.Property(pr => pr.CreatedAtUtc).IsRequired();

        builder.HasIndex(pr => pr.Code).IsUnique();
        builder.HasIndex(pr => new { pr.ProductId, pr.EligibleOrgType });

        builder.HasOne(pr => pr.Product)
            .WithMany(p => p.ProductRoles)
            .HasForeignKey(pr => pr.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(
            new { Id = SeedIds.PrCareConnectReferrer,     ProductId = SeedIds.ProductSynqCareConnect, Code = "CARECONNECT_REFERRER",      Name = "CareConnect Referrer",      EligibleOrgType = (string?)OrgType.LawFirm,  Description = (string?)"Law firm that refers clients to providers", IsActive = true, CreatedAtUtc = SeedIds.SeededAt },
            new { Id = SeedIds.PrCareConnectReceiver,     ProductId = SeedIds.ProductSynqCareConnect, Code = "CARECONNECT_RECEIVER",      Name = "CareConnect Receiver",      EligibleOrgType = (string?)OrgType.Provider, Description = (string?)"Provider that receives referrals",            IsActive = true, CreatedAtUtc = SeedIds.SeededAt },
            new { Id = SeedIds.PrSynqLienSeller,          ProductId = SeedIds.ProductSynqLiens,       Code = "SYNQLIEN_SELLER",           Name = "SynqLien Seller",           EligibleOrgType = (string?)OrgType.LawFirm,  Description = (string?)"Law firm that creates and offers liens",   IsActive = true, CreatedAtUtc = SeedIds.SeededAt },
            new { Id = SeedIds.PrSynqLienBuyer,           ProductId = SeedIds.ProductSynqLiens,       Code = "SYNQLIEN_BUYER",            Name = "SynqLien Buyer",            EligibleOrgType = (string?)OrgType.LienOwner, Description = (string?)"Lien owner that purchases liens",          IsActive = true, CreatedAtUtc = SeedIds.SeededAt },
            new { Id = SeedIds.PrSynqLienHolder,          ProductId = SeedIds.ProductSynqLiens,       Code = "SYNQLIEN_HOLDER",           Name = "SynqLien Holder",           EligibleOrgType = (string?)OrgType.LienOwner, Description = (string?)"Lien owner that services and settles liens", IsActive = true, CreatedAtUtc = SeedIds.SeededAt },
            new { Id = SeedIds.PrSynqFundReferrer,        ProductId = SeedIds.ProductSynqFund,        Code = "SYNQFUND_REFERRER",         Name = "SynqFund Referrer",         EligibleOrgType = (string?)OrgType.LawFirm,  Description = (string?)"Law firm that submits fund applications on behalf of clients", IsActive = true, CreatedAtUtc = SeedIds.SeededAt },
            new { Id = SeedIds.PrSynqFundFunder,          ProductId = SeedIds.ProductSynqFund,        Code = "SYNQFUND_FUNDER",           Name = "SynqFund Funder",           EligibleOrgType = (string?)OrgType.Funder,   Description = (string?)"Funder that evaluates and funds applications", IsActive = true, CreatedAtUtc = SeedIds.SeededAt },
            new { Id = SeedIds.PrSynqFundApplicantPortal, ProductId = SeedIds.ProductSynqFund,        Code = "SYNQFUND_APPLICANT_PORTAL", Name = "SynqFund Applicant Portal", EligibleOrgType = (string?)null,             Description = (string?)"Limited read-only portal access for fund applicants", IsActive = true, CreatedAtUtc = SeedIds.SeededAt }
        );
    }
}
