using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Capabilities");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.ProductId).IsRequired();

        builder.Property(c => c.Code)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.Description)
            .HasMaxLength(1000);

        builder.Property(c => c.Category)
            .HasMaxLength(100);

        builder.Property(c => c.IsActive).IsRequired();
        builder.Property(c => c.CreatedAtUtc).IsRequired();
        builder.Property(c => c.UpdatedAtUtc);
        builder.Property(c => c.CreatedBy);
        builder.Property(c => c.UpdatedBy);

        builder.HasIndex(c => c.Code).IsUnique();
        builder.HasIndex(c => c.ProductId);

        builder.HasOne(c => c.Product)
            .WithMany(p => p.Permissions)
            .HasForeignKey(c => c.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        var cc  = SeedIds.ProductSynqCareConnect;
        var sl  = SeedIds.ProductSynqLiens;
        var sf  = SeedIds.ProductSynqFund;
        var at  = SeedIds.SeededAt;

        builder.HasData(
            // CareConnect — Referral
            new { Id = SeedIds.PermReferralCreate,        ProductId = cc, Code = "SYNQ_CARECONNECT.referral:create",         Name = "Create Referral",              Description = (string?)"Create a new referral",                          Category = (string?)"Referral",   IsActive = true, CreatedAtUtc = at, UpdatedAtUtc = (DateTime?)null, CreatedBy = (Guid?)null, UpdatedBy = (Guid?)null },
            new { Id = SeedIds.PermReferralReadOwn,       ProductId = cc, Code = "SYNQ_CARECONNECT.referral:read:own",       Name = "Read Own Referrals",           Description = (string?)"View referrals you initiated",                   Category = (string?)"Referral",   IsActive = true, CreatedAtUtc = at, UpdatedAtUtc = (DateTime?)null, CreatedBy = (Guid?)null, UpdatedBy = (Guid?)null },
            new { Id = SeedIds.PermReferralCancel,        ProductId = cc, Code = "SYNQ_CARECONNECT.referral:cancel",         Name = "Cancel Referral",              Description = (string?)"Cancel a referral you initiated",                Category = (string?)"Referral",   IsActive = true, CreatedAtUtc = at, UpdatedAtUtc = (DateTime?)null, CreatedBy = (Guid?)null, UpdatedBy = (Guid?)null },
            new { Id = SeedIds.PermReferralReadAddressed, ProductId = cc, Code = "SYNQ_CARECONNECT.referral:read:addressed", Name = "Read Addressed Referrals",     Description = (string?)"View referrals addressed to your organization",  Category = (string?)"Referral",   IsActive = true, CreatedAtUtc = at, UpdatedAtUtc = (DateTime?)null, CreatedBy = (Guid?)null, UpdatedBy = (Guid?)null },
            new { Id = SeedIds.PermReferralAccept,        ProductId = cc, Code = "SYNQ_CARECONNECT.referral:accept",         Name = "Accept Referral",              Description = (string?)"Accept an incoming referral",                    Category = (string?)"Referral",   IsActive = true, CreatedAtUtc = at, UpdatedAtUtc = (DateTime?)null, CreatedBy = (Guid?)null, UpdatedBy = (Guid?)null },
            new { Id = SeedIds.PermReferralDecline,       ProductId = cc, Code = "SYNQ_CARECONNECT.referral:decline",        Name = "Decline Referral",             Description = (string?)"Decline an incoming referral",                   Category = (string?)"Referral",   IsActive = true, CreatedAtUtc = at, UpdatedAtUtc = (DateTime?)null, CreatedBy = (Guid?)null, UpdatedBy = (Guid?)null },
            // CareConnect — Provider
            new { Id = SeedIds.PermProviderSearch,        ProductId = cc, Code = "SYNQ_CARECONNECT.provider:search",         Name = "Search Providers",             Description = (string?)"Search for providers by criteria",               Category = (string?)"Provider",   IsActive = true, CreatedAtUtc = at, UpdatedAtUtc = (DateTime?)null, CreatedBy = (Guid?)null, UpdatedBy = (Guid?)null },
            new { Id = SeedIds.PermProviderMap,           ProductId = cc, Code = "SYNQ_CARECONNECT.provider:map",            Name = "View Provider Map",            Description = (string?)"View providers on a geographic map",             Category = (string?)"Provider",   IsActive = true, CreatedAtUtc = at, UpdatedAtUtc = (DateTime?)null, CreatedBy = (Guid?)null, UpdatedBy = (Guid?)null },
            // CareConnect — Appointment
            new { Id = SeedIds.PermAppointmentCreate,     ProductId = cc, Code = "SYNQ_CARECONNECT.appointment:create",      Name = "Create Appointment",           Description = (string?)"Schedule an appointment",                        Category = (string?)"Appointment", IsActive = true, CreatedAtUtc = at, UpdatedAtUtc = (DateTime?)null, CreatedBy = (Guid?)null, UpdatedBy = (Guid?)null },
            new { Id = SeedIds.PermAppointmentUpdate,     ProductId = cc, Code = "SYNQ_CARECONNECT.appointment:update",      Name = "Update Appointment",           Description = (string?)"Modify an existing appointment",                 Category = (string?)"Appointment", IsActive = true, CreatedAtUtc = at, UpdatedAtUtc = (DateTime?)null, CreatedBy = (Guid?)null, UpdatedBy = (Guid?)null },
            new { Id = SeedIds.PermAppointmentReadOwn,    ProductId = cc, Code = "SYNQ_CARECONNECT.appointment:read:own",    Name = "Read Own Appointments",        Description = (string?)"View your organization's appointments",          Category = (string?)"Appointment", IsActive = true, CreatedAtUtc = at, UpdatedAtUtc = (DateTime?)null, CreatedBy = (Guid?)null, UpdatedBy = (Guid?)null },
            // SynqLien
            new { Id = SeedIds.PermLienCreate,   ProductId = sl, Code = "SYNQ_LIENS.lien:create",    Name = "Create Lien",           Description = (string?)"Create a new lien record",          Category = (string?)"Lien", IsActive = true, CreatedAtUtc = at, UpdatedAtUtc = (DateTime?)null, CreatedBy = (Guid?)null, UpdatedBy = (Guid?)null },
            new { Id = SeedIds.PermLienOffer,    ProductId = sl, Code = "SYNQ_LIENS.lien:offer",     Name = "Offer Lien",            Description = (string?)"Offer a lien for sale",              Category = (string?)"Lien", IsActive = true, CreatedAtUtc = at, UpdatedAtUtc = (DateTime?)null, CreatedBy = (Guid?)null, UpdatedBy = (Guid?)null },
            new { Id = SeedIds.PermLienReadOwn,  ProductId = sl, Code = "SYNQ_LIENS.lien:read:own",  Name = "Read Own Liens",        Description = (string?)"View liens you created",             Category = (string?)"Lien", IsActive = true, CreatedAtUtc = at, UpdatedAtUtc = (DateTime?)null, CreatedBy = (Guid?)null, UpdatedBy = (Guid?)null },
            new { Id = SeedIds.PermLienBrowse,   ProductId = sl, Code = "SYNQ_LIENS.lien:browse",    Name = "Browse Liens",          Description = (string?)"Browse available liens for purchase", Category = (string?)"Lien", IsActive = true, CreatedAtUtc = at, UpdatedAtUtc = (DateTime?)null, CreatedBy = (Guid?)null, UpdatedBy = (Guid?)null },
            new { Id = SeedIds.PermLienPurchase, ProductId = sl, Code = "SYNQ_LIENS.lien:purchase",  Name = "Purchase Lien",         Description = (string?)"Purchase a lien",                    Category = (string?)"Lien", IsActive = true, CreatedAtUtc = at, UpdatedAtUtc = (DateTime?)null, CreatedBy = (Guid?)null, UpdatedBy = (Guid?)null },
            new { Id = SeedIds.PermLienReadHeld, ProductId = sl, Code = "SYNQ_LIENS.lien:read:held", Name = "Read Held Liens",       Description = (string?)"View liens you hold",                Category = (string?)"Lien", IsActive = true, CreatedAtUtc = at, UpdatedAtUtc = (DateTime?)null, CreatedBy = (Guid?)null, UpdatedBy = (Guid?)null },
            new { Id = SeedIds.PermLienService,  ProductId = sl, Code = "SYNQ_LIENS.lien:service",   Name = "Service Lien",          Description = (string?)"Service an active lien",             Category = (string?)"Lien", IsActive = true, CreatedAtUtc = at, UpdatedAtUtc = (DateTime?)null, CreatedBy = (Guid?)null, UpdatedBy = (Guid?)null },
            new { Id = SeedIds.PermLienSettle,   ProductId = sl, Code = "SYNQ_LIENS.lien:settle",    Name = "Settle Lien",           Description = (string?)"Settle and close a lien",            Category = (string?)"Lien", IsActive = true, CreatedAtUtc = at, UpdatedAtUtc = (DateTime?)null, CreatedBy = (Guid?)null, UpdatedBy = (Guid?)null },
            // SynqFund — Application
            new { Id = SeedIds.PermApplicationCreate,        ProductId = sf, Code = "SYNQ_FUND.application:create",         Name = "Create Application",           Description = (string?)"Submit a new fund application",                     Category = (string?)"Application", IsActive = true, CreatedAtUtc = at, UpdatedAtUtc = (DateTime?)null, CreatedBy = (Guid?)null, UpdatedBy = (Guid?)null },
            new { Id = SeedIds.PermApplicationReadOwn,       ProductId = sf, Code = "SYNQ_FUND.application:read:own",       Name = "Read Own Applications",        Description = (string?)"View applications you submitted",                    Category = (string?)"Application", IsActive = true, CreatedAtUtc = at, UpdatedAtUtc = (DateTime?)null, CreatedBy = (Guid?)null, UpdatedBy = (Guid?)null },
            new { Id = SeedIds.PermApplicationCancel,        ProductId = sf, Code = "SYNQ_FUND.application:cancel",         Name = "Cancel Application",           Description = (string?)"Cancel a pending application",                      Category = (string?)"Application", IsActive = true, CreatedAtUtc = at, UpdatedAtUtc = (DateTime?)null, CreatedBy = (Guid?)null, UpdatedBy = (Guid?)null },
            new { Id = SeedIds.PermApplicationReadAddressed, ProductId = sf, Code = "SYNQ_FUND.application:read:addressed", Name = "Read Addressed Applications",  Description = (string?)"View applications addressed to your organization",   Category = (string?)"Application", IsActive = true, CreatedAtUtc = at, UpdatedAtUtc = (DateTime?)null, CreatedBy = (Guid?)null, UpdatedBy = (Guid?)null },
            new { Id = SeedIds.PermApplicationEvaluate,      ProductId = sf, Code = "SYNQ_FUND.application:evaluate",       Name = "Evaluate Application",         Description = (string?)"Perform underwriting evaluation of an application",  Category = (string?)"Application", IsActive = true, CreatedAtUtc = at, UpdatedAtUtc = (DateTime?)null, CreatedBy = (Guid?)null, UpdatedBy = (Guid?)null },
            new { Id = SeedIds.PermApplicationApprove,       ProductId = sf, Code = "SYNQ_FUND.application:approve",        Name = "Approve Application",          Description = (string?)"Approve and fund an application",                   Category = (string?)"Application", IsActive = true, CreatedAtUtc = at, UpdatedAtUtc = (DateTime?)null, CreatedBy = (Guid?)null, UpdatedBy = (Guid?)null },
            new { Id = SeedIds.PermApplicationDecline,       ProductId = sf, Code = "SYNQ_FUND.application:decline",        Name = "Decline Application",          Description = (string?)"Decline a fund application",                        Category = (string?)"Application", IsActive = true, CreatedAtUtc = at, UpdatedAtUtc = (DateTime?)null, CreatedBy = (Guid?)null, UpdatedBy = (Guid?)null },
            // SynqFund — Party
            new { Id = SeedIds.PermPartyCreate,              ProductId = sf, Code = "SYNQ_FUND.party:create",               Name = "Create Party",                 Description = (string?)"Create a party profile for a client",               Category = (string?)"Party",       IsActive = true, CreatedAtUtc = at, UpdatedAtUtc = (DateTime?)null, CreatedBy = (Guid?)null, UpdatedBy = (Guid?)null },
            new { Id = SeedIds.PermPartyReadOwn,             ProductId = sf, Code = "SYNQ_FUND.party:read:own",             Name = "Read Own Party",               Description = (string?)"View party profiles you created",                   Category = (string?)"Party",       IsActive = true, CreatedAtUtc = at, UpdatedAtUtc = (DateTime?)null, CreatedBy = (Guid?)null, UpdatedBy = (Guid?)null },
            new { Id = SeedIds.PermApplicationStatusView,    ProductId = sf, Code = "SYNQ_FUND.application:status:view",    Name = "View Application Status",      Description = (string?)"View the status of a fund application (party portal)", Category = (string?)"Application", IsActive = true, CreatedAtUtc = at, UpdatedAtUtc = (DateTime?)null, CreatedBy = (Guid?)null, UpdatedBy = (Guid?)null }
        );
    }
}
