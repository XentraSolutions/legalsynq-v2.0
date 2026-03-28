using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

public class RoleCapabilityConfiguration : IEntityTypeConfiguration<RoleCapability>
{
    public void Configure(EntityTypeBuilder<RoleCapability> builder)
    {
        builder.ToTable("RoleCapabilities");

        builder.HasKey(rc => new { rc.ProductRoleId, rc.CapabilityId });

        builder.HasIndex(rc => rc.CapabilityId);

        builder.HasOne(rc => rc.ProductRole)
            .WithMany(pr => pr.RoleCapabilities)
            .HasForeignKey(rc => rc.ProductRoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rc => rc.Capability)
            .WithMany(c => c.RoleCapabilities)
            .HasForeignKey(rc => rc.CapabilityId)
            .OnDelete(DeleteBehavior.Cascade);

        var referrer  = SeedIds.PrCareConnectReferrer;
        var receiver  = SeedIds.PrCareConnectReceiver;
        var seller    = SeedIds.PrSynqLienSeller;
        var buyer     = SeedIds.PrSynqLienBuyer;
        var holder    = SeedIds.PrSynqLienHolder;
        var fReferrer = SeedIds.PrSynqFundReferrer;
        var funder    = SeedIds.PrSynqFundFunder;
        var portal    = SeedIds.PrSynqFundApplicantPortal;

        builder.HasData(
            // CARECONNECT_REFERRER
            new { ProductRoleId = referrer, CapabilityId = SeedIds.CapReferralCreate },
            new { ProductRoleId = referrer, CapabilityId = SeedIds.CapReferralReadOwn },
            new { ProductRoleId = referrer, CapabilityId = SeedIds.CapReferralCancel },
            new { ProductRoleId = referrer, CapabilityId = SeedIds.CapProviderSearch },
            new { ProductRoleId = referrer, CapabilityId = SeedIds.CapProviderMap },
            new { ProductRoleId = referrer, CapabilityId = SeedIds.CapAppointmentReadOwn },

            // CARECONNECT_RECEIVER
            new { ProductRoleId = receiver, CapabilityId = SeedIds.CapReferralReadAddressed },
            new { ProductRoleId = receiver, CapabilityId = SeedIds.CapReferralAccept },
            new { ProductRoleId = receiver, CapabilityId = SeedIds.CapReferralDecline },
            new { ProductRoleId = receiver, CapabilityId = SeedIds.CapAppointmentCreate },
            new { ProductRoleId = receiver, CapabilityId = SeedIds.CapAppointmentUpdate },
            new { ProductRoleId = receiver, CapabilityId = SeedIds.CapAppointmentReadOwn },

            // SYNQLIEN_SELLER
            new { ProductRoleId = seller, CapabilityId = SeedIds.CapLienCreate },
            new { ProductRoleId = seller, CapabilityId = SeedIds.CapLienOffer },
            new { ProductRoleId = seller, CapabilityId = SeedIds.CapLienReadOwn },

            // SYNQLIEN_BUYER
            new { ProductRoleId = buyer, CapabilityId = SeedIds.CapLienBrowse },
            new { ProductRoleId = buyer, CapabilityId = SeedIds.CapLienPurchase },
            new { ProductRoleId = buyer, CapabilityId = SeedIds.CapLienReadHeld },

            // SYNQLIEN_HOLDER
            new { ProductRoleId = holder, CapabilityId = SeedIds.CapLienReadHeld },
            new { ProductRoleId = holder, CapabilityId = SeedIds.CapLienService },
            new { ProductRoleId = holder, CapabilityId = SeedIds.CapLienSettle },

            // SYNQFUND_REFERRER
            new { ProductRoleId = fReferrer, CapabilityId = SeedIds.CapApplicationCreate },
            new { ProductRoleId = fReferrer, CapabilityId = SeedIds.CapApplicationReadOwn },
            new { ProductRoleId = fReferrer, CapabilityId = SeedIds.CapApplicationCancel },
            new { ProductRoleId = fReferrer, CapabilityId = SeedIds.CapPartyCreate },
            new { ProductRoleId = fReferrer, CapabilityId = SeedIds.CapPartyReadOwn },

            // SYNQFUND_FUNDER
            new { ProductRoleId = funder, CapabilityId = SeedIds.CapApplicationReadAddressed },
            new { ProductRoleId = funder, CapabilityId = SeedIds.CapApplicationEvaluate },
            new { ProductRoleId = funder, CapabilityId = SeedIds.CapApplicationApprove },
            new { ProductRoleId = funder, CapabilityId = SeedIds.CapApplicationDecline },

            // SYNQFUND_APPLICANT_PORTAL
            new { ProductRoleId = portal, CapabilityId = SeedIds.CapApplicationStatusView },
            new { ProductRoleId = portal, CapabilityId = SeedIds.CapPartyReadOwn }
        );
    }
}
