using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Tenant.Application.DTOs;
using Tenant.Application.Interfaces;
using Tenant.Infrastructure.Data;

namespace Tenant.Infrastructure.Services;

/// <summary>
/// Block 4 foundation — dry-run migration utility.
///
/// Compares Identity tenant rows with Tenant service rows and generates
/// a structured reconciliation report. No writes are performed in this block.
///
/// Identity data is accessed via a separate read-only MySqlConnection using
/// the "IdentityDb" connection string. If the connection string is absent or
/// the database is unreachable, the report flags IdentityAccessible = false
/// and carries an error message.
/// </summary>
public class MigrationUtilityService : IMigrationUtilityService
{
    private readonly TenantDbContext   _db;
    private readonly IConfiguration    _configuration;
    private readonly ILogger<MigrationUtilityService> _logger;

    public MigrationUtilityService(
        TenantDbContext   db,
        IConfiguration    configuration,
        ILogger<MigrationUtilityService> logger)
    {
        _db            = db;
        _configuration = configuration;
        _logger        = logger;
    }

    public async Task<MigrationDryRunReport> RunDryRunAsync(CancellationToken ct = default)
    {
        var generatedAt = DateTime.UtcNow;
        _logger.LogInformation("Migration dry-run started at {Time}", generatedAt);

        // ── Step 1: load Tenant service tenants ───────────────────────────────

        var tenantRows = await _db.Tenants.AsNoTracking()
            .Select(t => new
            {
                t.Id,
                t.Code,
                t.DisplayName,
                Status    = t.Status.ToString(),
                t.Subdomain
            })
            .ToListAsync(ct);

        _logger.LogInformation("Tenant service has {Count} tenants", tenantRows.Count);

        // ── Step 2: try to load Identity tenants ─────────────────────────────

        var identityCs = _configuration.GetConnectionString("IdentityDb");
        if (string.IsNullOrWhiteSpace(identityCs))
        {
            _logger.LogWarning("IdentityDb connection string is not configured — dry-run will report as unavailable.");
            return new MigrationDryRunReport(
                GeneratedAtUtc:       generatedAt,
                IdentityAccessible:   false,
                IdentityAccessError:  "ConnectionStrings:IdentityDb is not configured.",
                IdentityTenantCount:  0,
                TenantServiceCount:   tenantRows.Count,
                MissingInTenantService: 0,
                CodeMismatches:       0,
                NameMismatches:       0,
                StatusMismatches:     0,
                SubdomainGaps:        0,
                LogoGaps:             0,
                Differences:          Array.Empty<MigrationTenantDiff>());
        }

        List<IdentityTenantRow> identityTenants;
        string? accessError = null;

        try
        {
            identityTenants = await LoadIdentityTenantsAsync(identityCs, ct);
            _logger.LogInformation("Identity database has {Count} tenants", identityTenants.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read Identity tenant data for dry-run.");
            return new MigrationDryRunReport(
                GeneratedAtUtc:       generatedAt,
                IdentityAccessible:   false,
                IdentityAccessError:  ex.Message,
                IdentityTenantCount:  0,
                TenantServiceCount:   tenantRows.Count,
                MissingInTenantService: 0,
                CodeMismatches:       0,
                NameMismatches:       0,
                StatusMismatches:     0,
                SubdomainGaps:        0,
                LogoGaps:             0,
                Differences:          Array.Empty<MigrationTenantDiff>());
        }

        // ── Step 3: reconcile ─────────────────────────────────────────────────

        // Index Tenant service rows by code for fast lookup.
        var tenantByCode = tenantRows
            .GroupBy(t => t.Code.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First());

        var differences = new List<MigrationTenantDiff>();

        foreach (var identity in identityTenants)
        {
            var key = identity.Code.ToLowerInvariant();
            tenantByCode.TryGetValue(key, out var matched);

            var isMissing          = matched is null;
            var hasCodeMismatch    = matched is not null
                && !string.Equals(identity.Code, matched.Code, StringComparison.OrdinalIgnoreCase);
            var hasNameMismatch    = matched is not null
                && !string.Equals(identity.DisplayName, matched.DisplayName, StringComparison.OrdinalIgnoreCase);
            var hasStatusMismatch  = matched is not null
                && !string.Equals(NormalizeStatus(identity.Status), matched.Status, StringComparison.OrdinalIgnoreCase);
            var hasSubdomainGap    = matched is not null
                && !string.IsNullOrWhiteSpace(identity.Subdomain)
                && string.IsNullOrWhiteSpace(matched.Subdomain);
            var hasLogoGap         = matched is not null
                && identity.HasLogo
                && matched.Subdomain is null;  // proxy for no logo migrated

            if (isMissing || hasCodeMismatch || hasNameMismatch ||
                hasStatusMismatch || hasSubdomainGap || hasLogoGap)
            {
                differences.Add(new MigrationTenantDiff(
                    IdentityTenantId:    identity.Id,
                    IdentityCode:        identity.Code,
                    IdentityName:        identity.DisplayName,
                    IdentityStatus:      identity.Status,
                    IdentitySubdomain:   identity.Subdomain,
                    IdentityHasLogo:     identity.HasLogo,
                    TenantServiceId:     matched?.Id.ToString(),
                    TenantServiceCode:   matched?.Code,
                    TenantServiceName:   matched?.DisplayName,
                    TenantServiceStatus: matched?.Status,
                    IsMissing:           isMissing,
                    HasCodeMismatch:     hasCodeMismatch,
                    HasNameMismatch:     hasNameMismatch,
                    HasStatusMismatch:   hasStatusMismatch,
                    HasSubdomainGap:     hasSubdomainGap,
                    HasLogoGap:          hasLogoGap));
            }
        }

        var report = new MigrationDryRunReport(
            GeneratedAtUtc:         generatedAt,
            IdentityAccessible:     true,
            IdentityAccessError:    null,
            IdentityTenantCount:    identityTenants.Count,
            TenantServiceCount:     tenantRows.Count,
            MissingInTenantService: differences.Count(d => d.IsMissing),
            CodeMismatches:         differences.Count(d => d.HasCodeMismatch),
            NameMismatches:         differences.Count(d => d.HasNameMismatch),
            StatusMismatches:       differences.Count(d => d.HasStatusMismatch),
            SubdomainGaps:          differences.Count(d => d.HasSubdomainGap),
            LogoGaps:               differences.Count(d => d.HasLogoGap),
            Differences:            differences.AsReadOnly());

        _logger.LogInformation(
            "Migration dry-run complete. Missing={Missing}, CodeMismatches={Code}, NameMismatches={Name}",
            report.MissingInTenantService, report.CodeMismatches, report.NameMismatches);

        return report;
    }

    // ── Identity data access (read-only, raw SQL) ─────────────────────────────

    private static async Task<List<IdentityTenantRow>> LoadIdentityTenantsAsync(
        string            connectionString,
        CancellationToken ct)
    {
        const string sql = """
            SELECT
                CAST(Id AS CHAR)   AS Id,
                Code,
                DisplayName,
                Status,
                Subdomain,
                LogoDocumentId IS NOT NULL AS HasLogo
            FROM tenants
            ORDER BY Code;
            """;

        await using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new MySqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var rows = new List<IdentityTenantRow>();
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new IdentityTenantRow(
                Id:          reader.GetString("Id"),
                Code:        reader.GetString("Code"),
                DisplayName: reader.GetString("DisplayName"),
                Status:      reader.GetString("Status"),
                Subdomain:   reader.IsDBNull(reader.GetOrdinal("Subdomain"))
                    ? null
                    : reader.GetString("Subdomain"),
                HasLogo:     reader.GetBoolean("HasLogo")));
        }

        return rows;
    }

    // ── Status normalization ──────────────────────────────────────────────────

    /// <summary>
    /// Maps Identity tenant status values to Tenant service equivalents for comparison.
    /// Identity uses "Active"/"Inactive"/"Suspended" — Tenant service uses the same strings
    /// (see TenantStatus enum in Tenant.Domain). Minor differences handled here.
    /// </summary>
    private static string NormalizeStatus(string identityStatus) =>
        identityStatus?.Trim() ?? "Unknown";

    // ── Internal projection type ──────────────────────────────────────────────

    private sealed record IdentityTenantRow(
        string  Id,
        string  Code,
        string  DisplayName,
        string  Status,
        string? Subdomain,
        bool    HasLogo);
}
