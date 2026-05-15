using System.Globalization;

namespace Billing.Domain.Statements;

/// <summary>
/// STAT-B02 — Default <see cref="IStatementNumberGenerator"/>. Reads
/// the latest <c>STMT-{year:D4}-NNNNNN</c> for the tenant + year and
/// returns it incremented by one with six-digit zero padding. See
/// the interface XML doc for concurrency caveats.
/// </summary>
public sealed class StatementNumberGenerator : IStatementNumberGenerator
{
    public const string Prefix = "STMT";
    public const int SequenceWidth = 6;

    private readonly ICustomerStatementRepository _repository;

    public StatementNumberGenerator(ICustomerStatementRepository repository)
    {
        _repository = repository;
    }

    public async Task<string> NextAsync(Guid tenantId, int year, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (year < 1900 || year > 2999)
            throw new ArgumentOutOfRangeException(nameof(year), "Year must be in [1900, 2999].");

        var latest = await _repository.GetLatestNumberForYearAsync(tenantId, year, ct);
        var next = NextSequence(latest);
        return Format(year, next);
    }

    /// <summary>
    /// Pure helper exposed for tests so the parsing rules can be
    /// asserted without a repository.
    /// </summary>
    internal static int NextSequence(string? latest)
    {
        if (string.IsNullOrWhiteSpace(latest)) return 1;
        var parts = latest.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3) return 1;
        return int.TryParse(parts[^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var seq)
            ? seq + 1
            : 1;
    }

    /// <summary>
    /// Pure helper exposed for tests: deterministic
    /// <c>STMT-YYYY-NNNNNN</c> formatter.
    /// </summary>
    internal static string Format(int year, int sequence) =>
        $"{Prefix}-{year.ToString("D4", CultureInfo.InvariantCulture)}-{sequence.ToString("D" + SequenceWidth, CultureInfo.InvariantCulture)}";
}
