using Microsoft.EntityFrameworkCore;

namespace Intake.Infrastructure.Persistence;

/// <summary>
/// Dedicated persistence boundary for Synq Intake.
///
/// The foundation intentionally has no business entities yet. Future Intake
/// aggregates must be persisted here and must not reuse another service's
/// DbContext or database.
/// </summary>
public sealed class IntakeDbContext(DbContextOptions<IntakeDbContext> options) : DbContext(options)
{
}