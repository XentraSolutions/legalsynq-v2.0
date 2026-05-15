using Commerce.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Commerce.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.ToTable("catalog_products");
        b.HasKey(x => x.Id);
        b.Property(x => x.Key).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.Key).IsUnique().HasDatabaseName("ux_catalog_products_key");
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.Status).HasConversion<int>().IsRequired();
        b.Property(x => x.SortOrder).IsRequired();
        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc).IsRequired();
    }
}

internal sealed class FeatureConfiguration : IEntityTypeConfiguration<Feature>
{
    public void Configure(EntityTypeBuilder<Feature> b)
    {
        b.ToTable("catalog_features");
        b.HasKey(x => x.Id);
        b.Property(x => x.ProductId).IsRequired();
        b.HasIndex(x => x.ProductId).HasDatabaseName("ix_catalog_features_product_id");
        b.HasIndex(x => new { x.ProductId, x.Key })
            .IsUnique()
            .HasDatabaseName("ux_catalog_features_product_key");
        b.Property(x => x.Key).HasMaxLength(64).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.FeatureType).HasConversion<int>().IsRequired();
        b.Property(x => x.Status).HasConversion<int>().IsRequired();
        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc).IsRequired();

        b.HasOne<Product>()
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> b)
    {
        b.ToTable("catalog_plans");
        b.HasKey(x => x.Id);
        b.Property(x => x.ProductId);
        b.HasIndex(x => x.ProductId).HasDatabaseName("ix_catalog_plans_product_id");
        b.Property(x => x.Key).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.Key).IsUnique().HasDatabaseName("ux_catalog_plans_key");
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.Status).HasConversion<int>().IsRequired();
        b.Property(x => x.BillingInterval).HasConversion<int>().IsRequired();
        b.Property(x => x.TrialDays);
        b.Property(x => x.SortOrder).IsRequired();
        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc).IsRequired();

        b.HasOne<Product>()
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PlanFeatureConfiguration : IEntityTypeConfiguration<PlanFeature>
{
    public void Configure(EntityTypeBuilder<PlanFeature> b)
    {
        b.ToTable("catalog_plan_features");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.PlanId, x.FeatureId })
            .IsUnique()
            .HasDatabaseName("ux_catalog_plan_features_plan_feature");
        b.Property(x => x.IsEnabled).IsRequired();
        b.Property(x => x.LimitValue);
        b.Property(x => x.MeteredIncludedUnits);
        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc).IsRequired();

        b.HasOne<Plan>()
            .WithMany()
            .HasForeignKey(x => x.PlanId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Feature>()
            .WithMany()
            .HasForeignKey(x => x.FeatureId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AddonConfiguration : IEntityTypeConfiguration<Addon>
{
    public void Configure(EntityTypeBuilder<Addon> b)
    {
        b.ToTable("catalog_addons");
        b.HasKey(x => x.Id);
        b.Property(x => x.ProductId);
        b.HasIndex(x => x.ProductId).HasDatabaseName("ix_catalog_addons_product_id");
        b.Property(x => x.Key).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.Key).IsUnique().HasDatabaseName("ux_catalog_addons_key");
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.Status).HasConversion<int>().IsRequired();
        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc).IsRequired();

        b.HasOne<Product>()
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class BundleConfiguration : IEntityTypeConfiguration<Bundle>
{
    public void Configure(EntityTypeBuilder<Bundle> b)
    {
        b.ToTable("catalog_bundles");
        b.HasKey(x => x.Id);
        b.Property(x => x.Key).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.Key).IsUnique().HasDatabaseName("ux_catalog_bundles_key");
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.Status).HasConversion<int>().IsRequired();
        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc).IsRequired();
    }
}

internal sealed class BundleItemConfiguration : IEntityTypeConfiguration<BundleItem>
{
    public void Configure(EntityTypeBuilder<BundleItem> b)
    {
        b.ToTable("catalog_bundle_items");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.BundleId).HasDatabaseName("ix_catalog_bundle_items_bundle_id");
        b.Property(x => x.CreatedAtUtc).IsRequired();

        b.HasOne<Bundle>()
            .WithMany()
            .HasForeignKey(x => x.BundleId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne<Product>()
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Plan>()
            .WithMany()
            .HasForeignKey(x => x.PlanId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Addon>()
            .WithMany()
            .HasForeignKey(x => x.AddonId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PriceConfiguration : IEntityTypeConfiguration<Price>
{
    public void Configure(EntityTypeBuilder<Price> b)
    {
        b.ToTable("catalog_prices");
        b.HasKey(x => x.Id);
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.AmountMinor).IsRequired();
        b.Property(x => x.BillingInterval).HasConversion<int>().IsRequired();
        b.Property(x => x.Status).HasConversion<int>().IsRequired();
        b.Property(x => x.EffectiveFromUtc).IsRequired();
        b.Property(x => x.EffectiveToUtc);
        b.Property(x => x.CreatedAtUtc).IsRequired();
        b.Property(x => x.UpdatedAtUtc).IsRequired();

        b.HasIndex(x => new { x.PlanId, x.Currency, x.BillingInterval, x.Status })
            .HasDatabaseName("ix_catalog_prices_plan_lookup");
        b.HasIndex(x => new { x.AddonId, x.Currency, x.BillingInterval, x.Status })
            .HasDatabaseName("ix_catalog_prices_addon_lookup");
        b.HasIndex(x => new { x.BundleId, x.Currency, x.BillingInterval, x.Status })
            .HasDatabaseName("ix_catalog_prices_bundle_lookup");

        b.HasOne<Plan>()
            .WithMany()
            .HasForeignKey(x => x.PlanId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Addon>()
            .WithMany()
            .HasForeignKey(x => x.AddonId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Bundle>()
            .WithMany()
            .HasForeignKey(x => x.BundleId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
