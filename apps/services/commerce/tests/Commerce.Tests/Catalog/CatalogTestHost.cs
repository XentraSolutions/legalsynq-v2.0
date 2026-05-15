using Commerce.Application.Common.Time;
using Commerce.Infrastructure.Catalog.Services;
using Commerce.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Commerce.Tests.Catalog;

internal sealed class FixedClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
}

/// <summary>
/// Minimal in-memory host used by Catalog service tests. Avoids spinning up
/// the full WebApplicationFactory for unit-level coverage of services.
/// </summary>
internal sealed class CatalogTestHost : IDisposable
{
    public CommerceDbContext Db { get; }
    public FixedClock Clock { get; } = new();

    public ProductCatalogService ProductService { get; }
    public FeatureCatalogService FeatureService { get; }
    public PlanCatalogService PlanService { get; }
    public AddonCatalogService AddonService { get; }
    public BundleCatalogService BundleService { get; }
    public PriceCatalogService PriceService { get; }

    public CatalogTestHost()
    {
        var opts = new DbContextOptionsBuilder<CommerceDbContext>()
            .UseInMemoryDatabase($"catalog-tests-{Guid.NewGuid()}")
            .Options;
        Db = new CommerceDbContext(opts);

        // Build validator instances by scanning the Application assembly.
        var appAsm = typeof(Commerce.Application.DependencyInjection).Assembly;
        IValidator<T> Resolve<T>()
        {
            var validatorType = appAsm.GetTypes()
                .First(t => !t.IsAbstract && typeof(IValidator<T>).IsAssignableFrom(t));
            return (IValidator<T>)Activator.CreateInstance(validatorType)!;
        }

        ProductService = new ProductCatalogService(Db, Clock,
            Resolve<Contracts.Catalog.CreateProductRequest>(),
            Resolve<Contracts.Catalog.UpdateProductRequest>());

        FeatureService = new FeatureCatalogService(Db, Clock,
            Resolve<Contracts.Catalog.CreateFeatureRequest>(),
            Resolve<Contracts.Catalog.UpdateFeatureRequest>());

        PlanService = new PlanCatalogService(Db, Clock,
            Resolve<Contracts.Catalog.CreatePlanRequest>(),
            Resolve<Contracts.Catalog.UpdatePlanRequest>(),
            Resolve<Contracts.Catalog.AddPlanFeatureRequest>());

        AddonService = new AddonCatalogService(Db, Clock,
            Resolve<Contracts.Catalog.CreateAddonRequest>(),
            Resolve<Contracts.Catalog.UpdateAddonRequest>());

        BundleService = new BundleCatalogService(Db, Clock,
            Resolve<Contracts.Catalog.CreateBundleRequest>(),
            Resolve<Contracts.Catalog.UpdateBundleRequest>(),
            Resolve<Contracts.Catalog.AddBundleItemRequest>());

        PriceService = new PriceCatalogService(Db, Clock,
            Resolve<Contracts.Catalog.CreatePriceRequest>(),
            Resolve<Contracts.Catalog.UpdatePriceRequest>());
    }

    public void Dispose() => Db.Dispose();
}
