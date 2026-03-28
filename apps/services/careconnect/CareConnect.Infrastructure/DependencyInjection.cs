using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Application.Services;
using CareConnect.Infrastructure.Data;
using CareConnect.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CareConnect.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("CareConnectDb")
            ?? throw new InvalidOperationException("Connection string 'CareConnectDb' is not configured.");

        services.AddDbContext<CareConnectDbContext>(options =>
            options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 0))));

        services.AddScoped<IProviderRepository, ProviderRepository>();
        services.AddScoped<IReferralRepository, ReferralRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IFacilityRepository, FacilityRepository>();
        services.AddScoped<IServiceOfferingRepository, ServiceOfferingRepository>();
        services.AddScoped<IAvailabilityTemplateRepository, AvailabilityTemplateRepository>();

        services.AddScoped<IProviderService, ProviderService>();
        services.AddScoped<IReferralService, ReferralService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IFacilityService, FacilityService>();
        services.AddScoped<IServiceOfferingService, ServiceOfferingService>();
        services.AddScoped<IAvailabilityTemplateService, AvailabilityTemplateService>();

        return services;
    }
}
