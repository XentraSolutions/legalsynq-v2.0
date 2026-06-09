using Commerce.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AccountStandingEntity = Commerce.Domain.AccountStanding.AccountStanding;

namespace Commerce.Infrastructure.Persistence.Configurations;

internal sealed class AccountStandingConfiguration : IEntityTypeConfiguration<AccountStandingEntity>
{
    public void Configure(EntityTypeBuilder<AccountStandingEntity> b)
    {
        b.ToTable("account_standings");
        b.HasKey(x => x.Id);
        b.Property(x => x.BillingAccountId).IsRequired();
        b.HasIndex(x => x.BillingAccountId).IsUnique()
            .HasDatabaseName("ux_account_standings_billing_account_id");
        b.Property(x => x.Status).HasConversion<int>().IsRequired();
        b.Property(x => x.Reason).HasMaxLength(500);
        b.Property(x => x.GracePeriodEndsAtUtc);
        b.Property(x => x.PastDueSinceUtc);
        b.Property(x => x.SuspendedAtUtc);
        b.Property(x => x.LastEvaluatedAtUtc).IsRequired();
        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc).IsRequired();

        b.HasOne<BillingAccount>()
            .WithMany()
            .HasForeignKey(x => x.BillingAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
