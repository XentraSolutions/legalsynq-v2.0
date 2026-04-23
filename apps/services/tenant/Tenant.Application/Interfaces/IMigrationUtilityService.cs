using Tenant.Application.DTOs;

namespace Tenant.Application.Interfaces;

/// <summary>
/// Block 4 foundation — dry-run migration utility.
///
/// Compares Identity tenant data against Tenant service data and produces
/// a structured reconciliation report. No writes are performed.
///
/// Write mode (actual migration execution) is deferred to Block 5.
/// </summary>
public interface IMigrationUtilityService
{
    /// <summary>
    /// Runs a dry-run reconciliation between Identity and Tenant service.
    /// Returns a structured report. Never writes to either database.
    /// </summary>
    Task<MigrationDryRunReport> RunDryRunAsync(CancellationToken ct = default);
}
