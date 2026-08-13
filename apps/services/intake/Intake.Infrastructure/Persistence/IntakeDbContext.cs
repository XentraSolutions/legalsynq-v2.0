using Microsoft.EntityFrameworkCore;
using Intake.Domain.Configuration;

namespace Intake.Infrastructure.Persistence;

/// <summary>
/// Dedicated persistence boundary for Synq Intake configuration and future
/// Intake aggregates. This context must not reuse another service's database.
/// </summary>
public sealed class IntakeDbContext(DbContextOptions<IntakeDbContext> options) : DbContext(options)
{
    public DbSet<TenantIntakeConfiguration> TenantIntakeConfigurations => Set<TenantIntakeConfiguration>();
    public DbSet<ProcessingProfileDefinition> ProcessingProfileDefinitions => Set<ProcessingProfileDefinition>();
    public DbSet<TenantProcessingProfile> TenantProcessingProfiles => Set<TenantProcessingProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IntakeDbContext).Assembly);
    }
}