using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

public class CapabilityConfiguration : IEntityTypeConfiguration<Capability>
{
    public void Configure(EntityTypeBuilder<Capability> builder)
    {
        builder.ToTable("Capabilities");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.ProductId).IsRequired();

        builder.Property(c => c.Code)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.Description)
            .HasMaxLength(1000);

        builder.Property(c => c.IsActive).IsRequired();
        builder.Property(c => c.CreatedAtUtc).IsRequired();

        builder.HasIndex(c => c.Code).IsUnique();
        builder.HasIndex(c => c.ProductId);

        builder.HasOne(c => c.Product)
            .WithMany(p => p.Capabilities)
            .HasForeignKey(c => c.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        var cc  = SeedIds.ProductSynqCareConnect;
        var sl  = SeedIds.ProductSynqLiens;
        var sf  = SeedIds.ProductSynqFund;
        var at  = SeedIds.SeededAt;

        builder.HasData(
            // CareConnect
            new { Id = SeedIds.CapReferralCreate,        ProductId = cc, Code = "referral:create",           Name = "Create Referral",              Description = (string?)"Create a new referral",                          IsActive = true, CreatedAtUtc = at },
            new { Id = SeedIds.CapReferralReadOwn,       ProductId = cc, Code = "referral:read:own",         Name = "Read Own Referrals",           Description = (string?)"View referrals you initiated",                   IsActive = true, CreatedAtUtc = at },
            new { Id = SeedIds.CapReferralCancel,        ProductId = cc, Code = "referral:cancel",           Name = "Cancel Referral",              Description = (string?)"Cancel a referral you initiated",                IsActive = true, CreatedAtUtc = at },
            new { Id = SeedIds.CapReferralReadAddressed, ProductId = cc, Code = "referral:read:addressed",   Name = "Read Addressed Referrals",     Description = (string?)"View referrals addressed to your organization",  IsActive = true, CreatedAtUtc = at },
            new { Id = SeedIds.CapReferralAccept,        ProductId = cc, Code = "referral:accept",           Name = "Accept Referral",              Description = (string?)"Accept an incoming referral",                    IsActive = true, CreatedAtUtc = at },
            new { Id = SeedIds.CapReferralDecline,       ProductId = cc, Code = "referral:decline",          Name = "Decline Referral",             Description = (string?)"Decline an incoming referral",                   IsActive = true, CreatedAtUtc = at },
            new { Id = SeedIds.CapProviderSearch,        ProductId = cc, Code = "provider:search",           Name = "Search Providers",             Description = (string?)"Search for providers by criteria",               IsActive = true, CreatedAtUtc = at },
            new { Id = SeedIds.CapProviderMap,           ProductId = cc, Code = "provider:map",              Name = "View Provider Map",            Description = (string?)"View providers on a geographic map",             IsActive = true, CreatedAtUtc = at },
            new { Id = SeedIds.CapAppointmentCreate,     ProductId = cc, Code = "appointment:create",        Name = "Create Appointment",           Description = (string?)"Schedule an appointment",                        IsActive = true, CreatedAtUtc = at },
            new { Id = SeedIds.CapAppointmentUpdate,     ProductId = cc, Code = "appointment:update",        Name = "Update Appointment",           Description = (string?)"Modify an existing appointment",                 IsActive = true, CreatedAtUtc = at },
            new { Id = SeedIds.CapAppointmentReadOwn,   ProductId = cc, Code = "appointment:read:own",       Name = "Read Own Appointments",        Description = (string?)"View your organization's appointments",          IsActive = true, CreatedAtUtc = at },
            // SynqLien
            new { Id = SeedIds.CapLienCreate,   ProductId = sl, Code = "lien:create",    Name = "Create Lien",           Description = (string?)"Create a new lien record",          IsActive = true, CreatedAtUtc = at },
            new { Id = SeedIds.CapLienOffer,    ProductId = sl, Code = "lien:offer",     Name = "Offer Lien",            Description = (string?)"Offer a lien for sale",              IsActive = true, CreatedAtUtc = at },
            new { Id = SeedIds.CapLienReadOwn,  ProductId = sl, Code = "lien:read:own",  Name = "Read Own Liens",        Description = (string?)"View liens you created",             IsActive = true, CreatedAtUtc = at },
            new { Id = SeedIds.CapLienBrowse,   ProductId = sl, Code = "lien:browse",    Name = "Browse Liens",          Description = (string?)"Browse available liens for purchase", IsActive = true, CreatedAtUtc = at },
            new { Id = SeedIds.CapLienPurchase, ProductId = sl, Code = "lien:purchase",  Name = "Purchase Lien",         Description = (string?)"Purchase a lien",                    IsActive = true, CreatedAtUtc = at },
            new { Id = SeedIds.CapLienReadHeld, ProductId = sl, Code = "lien:read:held", Name = "Read Held Liens",       Description = (string?)"View liens you hold",                IsActive = true, CreatedAtUtc = at },
            new { Id = SeedIds.CapLienService,  ProductId = sl, Code = "lien:service",   Name = "Service Lien",          Description = (string?)"Service an active lien",             IsActive = true, CreatedAtUtc = at },
            new { Id = SeedIds.CapLienSettle,   ProductId = sl, Code = "lien:settle",    Name = "Settle Lien",           Description = (string?)"Settle and close a lien",            IsActive = true, CreatedAtUtc = at },
            // SynqFund
            new { Id = SeedIds.CapApplicationCreate,        ProductId = sf, Code = "application:create",           Name = "Create Application",           Description = (string?)"Submit a new fund application",                     IsActive = true, CreatedAtUtc = at },
            new { Id = SeedIds.CapApplicationReadOwn,       ProductId = sf, Code = "application:read:own",         Name = "Read Own Applications",        Description = (string?)"View applications you submitted",                    IsActive = true, CreatedAtUtc = at },
            new { Id = SeedIds.CapApplicationCancel,        ProductId = sf, Code = "application:cancel",           Name = "Cancel Application",           Description = (string?)"Cancel a pending application",                      IsActive = true, CreatedAtUtc = at },
            new { Id = SeedIds.CapApplicationReadAddressed, ProductId = sf, Code = "application:read:addressed",   Name = "Read Addressed Applications",  Description = (string?)"View applications addressed to your organization",   IsActive = true, CreatedAtUtc = at },
            new { Id = SeedIds.CapApplicationEvaluate,      ProductId = sf, Code = "application:evaluate",         Name = "Evaluate Application",         Description = (string?)"Perform underwriting evaluation of an application",  IsActive = true, CreatedAtUtc = at },
            new { Id = SeedIds.CapApplicationApprove,       ProductId = sf, Code = "application:approve",          Name = "Approve Application",          Description = (string?)"Approve and fund an application",                   IsActive = true, CreatedAtUtc = at },
            new { Id = SeedIds.CapApplicationDecline,       ProductId = sf, Code = "application:decline",          Name = "Decline Application",          Description = (string?)"Decline a fund application",                        IsActive = true, CreatedAtUtc = at },
            new { Id = SeedIds.CapPartyCreate,              ProductId = sf, Code = "party:create",                 Name = "Create Party",                 Description = (string?)"Create a party profile for a client",               IsActive = true, CreatedAtUtc = at },
            new { Id = SeedIds.CapPartyReadOwn,             ProductId = sf, Code = "party:read:own",               Name = "Read Own Party",               Description = (string?)"View party profiles you created",                   IsActive = true, CreatedAtUtc = at },
            new { Id = SeedIds.CapApplicationStatusView,    ProductId = sf, Code = "application:status:view",      Name = "View Application Status",      Description = (string?)"View the status of a fund application (party portal)", IsActive = true, CreatedAtUtc = at }
        );
    }
}
