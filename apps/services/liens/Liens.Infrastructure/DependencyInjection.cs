using BuildingBlocks.Context;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Liens.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddLiensServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("LiensDb")
            ?? throw new InvalidOperationException("Connection string 'LiensDb' is not configured.");

        services.AddDbContext<LiensDbContext>(options =>
            options.UseMySql(
                connectionString,
                new MySqlServerVersion(new Version(8, 0, 0))));

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentRequestContext, CurrentRequestContext>();

        return services;
    }
}
