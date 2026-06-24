using Commerce.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Commerce.Infrastructure.Persistence.Configurations;

internal sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> b)
    {
        b.ToTable("subscriptions");
        b.HasKey(x => x.Id);
        b.Property(x => x.BillingAccountId).IsRequired();
        b.Property(x => x.SubscriptionNumber).HasMaxLength(32).IsRequired();
        b.HasIndex(x => x.SubscriptionNumber).IsUnique()
            .HasDatabaseName("ux_subscriptions_subscription_number");
        b.Property(x => x.Status).HasConversion<int>().IsRequired();
        b.Property(x => x.StartDateUtc).IsRequired();
        b.Property(x => x.CurrentPeriodStartUtc).IsRequired();
        b.Property(x => x.CurrentPeriodEndUtc).IsRequired();
        b.Property(x => x.TrialStartUtc);
        b.Property(x => x.TrialEndUtc);
        b.Property(x => x.CancelAtPeriodEnd).IsRequired();
        b.Property(x => x.CancelledAtUtc);
        b.Property(x => x.CancellationReason).HasMaxLength(500);
        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc).IsRequired();

        b.HasIndex(x => x.BillingAccountId).HasDatabaseName("ix_subscriptions_billing_account_id");
        b.HasIndex(x => x.Status).HasDatabaseName("ix_subscriptions_status");

        b.HasOne<Commerce.Domain.Billing.BillingAccount>()
            .WithMany()
            .HasForeignKey(x => x.BillingAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SubscriptionItemConfiguration : IEntityTypeConfiguration<SubscriptionItem>
{
    public void Configure(EntityTypeBuilder<SubscriptionItem> b)
    {
        b.ToTable("subscription_items");
        b.HasKey(x => x.Id);
        b.Property(x => x.SubscriptionId).IsRequired();
        b.Property(x => x.PlanId).IsRequired();
        b.Property(x => x.PriceId).IsRequired();
        b.Property(x => x.Quantity).IsRequired();
        b.Property(x => x.UnitAmountMinor).IsRequired();
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.BillingInterval).HasConversion<int>().IsRequired();
        b.Property(x => x.Status).HasConversion<int>().IsRequired();
        b.Property(x => x.EffectiveFromUtc).IsRequired();
        b.Property(x => x.EffectiveToUtc);
        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc).IsRequired();

        b.HasIndex(x => x.SubscriptionId).HasDatabaseName("ix_subscription_items_subscription_id");
        b.HasIndex(x => new { x.SubscriptionId, x.Status })
            .HasDatabaseName("ix_subscription_items_subscription_status");

        b.HasOne<Subscription>()
            .WithMany()
            .HasForeignKey(x => x.SubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne<Commerce.Domain.Catalog.Plan>()
            .WithMany()
            .HasForeignKey(x => x.PlanId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne<Commerce.Domain.Catalog.Price>()
            .WithMany()
            .HasForeignKey(x => x.PriceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SubscriptionChangeConfiguration : IEntityTypeConfiguration<SubscriptionChange>
{
    public void Configure(EntityTypeBuilder<SubscriptionChange> b)
    {
        b.ToTable("subscription_changes");
        b.HasKey(x => x.Id);
        b.Property(x => x.SubscriptionId).IsRequired();
        b.Property(x => x.ChangeType).HasConversion<int>().IsRequired();
        b.Property(x => x.FromPlanId);
        b.Property(x => x.ToPlanId);
        b.Property(x => x.FromPriceId);
        b.Property(x => x.ToPriceId);
        b.Property(x => x.EffectiveAtUtc).IsRequired();
        b.Property(x => x.ProrationBehavior).HasConversion<int>().IsRequired();
        b.Property(x => x.Reason).HasMaxLength(500);
        b.Property(x => x.MetadataJson).HasColumnType("text");
        b.Property(x => x.CreatedAtUtc).IsRequired();

        b.HasIndex(x => new { x.SubscriptionId, x.CreatedAtUtc })
            .HasDatabaseName("ix_subscription_changes_subscription_created");

        b.HasOne<Subscription>()
            .WithMany()
            .HasForeignKey(x => x.SubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
