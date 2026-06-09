namespace Billing.Domain.Accounting.Erp.QuickBooks;

/// <summary>
/// MS-BILL-ERP-003 — Default <see cref="IQuickBooksCustomerMappingService"/>.
///
/// <para>
/// Validates allow-listed enum values (<see cref="QuickBooksCustomerMappingStatus"/>,
/// <see cref="QuickBooksCustomerMappingExportMode"/>), normalises trim-bounded
/// string fields, stamps server-side timestamps via <see cref="TimeProvider"/>,
/// and delegates persistence to the repository.
/// </para>
///
/// <para>
/// The resolver returns NULL whenever no row exists OR the row is
/// <c>Disabled</c>; callers (the QB provider) interpret NULL as
/// "no mapping" and fall through to the configured fallback path.
/// The service NEVER fuzzy-matches names and NEVER creates a QBO
/// customer.
/// </para>
/// </summary>
public sealed class QuickBooksCustomerMappingService : IQuickBooksCustomerMappingService
{
    private const int MaxQuickBooksCustomerIdLength = 100;
    private const int MaxDisplayNameLength = 200;
    private const int MaxActorLength = 200;

    private readonly IQuickBooksCustomerMappingRepository _repo;
    private readonly TimeProvider _clock;

    public QuickBooksCustomerMappingService(
        IQuickBooksCustomerMappingRepository repo,
        TimeProvider clock)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<QuickBooksCustomerMapping> CreateAsync(
        Guid tenantId,
        CreateQuickBooksCustomerMappingCommand command,
        string actor,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("tenantId required.", nameof(tenantId));
        if (command is null) throw new ArgumentNullException(nameof(command));
        if (command.BillingCustomerId == Guid.Empty)
            throw new ArgumentException("billingCustomerId required.", nameof(command));

        var qboId = NormaliseQuickBooksCustomerId(command.QuickBooksCustomerId);
        var displayName = NormaliseDisplayName(command.QuickBooksDisplayName);
        var status = NormaliseStatus(command.MappingStatus);
        var exportMode = NormaliseExportMode(command.ExportMode);
        var actorNormalised = NormaliseActor(actor);

        var now = _clock.GetUtcNow().UtcDateTime;
        var entity = new QuickBooksCustomerMapping
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            BillingCustomerId = command.BillingCustomerId,
            QuickBooksCustomerId = qboId,
            QuickBooksDisplayName = displayName,
            MappingStatus = status,
            ExportMode = exportMode,
            CreatedBy = actorNormalised,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            LastExportedAtUtc = null,
        };

        return await _repo.AddAsync(entity, ct).ConfigureAwait(false);
    }

    public async Task<QuickBooksCustomerMapping> UpdateAsync(
        Guid tenantId,
        Guid id,
        UpdateQuickBooksCustomerMappingCommand command,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("tenantId required.", nameof(tenantId));
        if (id == Guid.Empty) throw new ArgumentException("id required.", nameof(id));
        if (command is null) throw new ArgumentNullException(nameof(command));

        // Read tracked (NOT AsNoTracking via GetByIdAsync) by re-querying through
        // the repo's tenant-scoped contract. We keep the entity write-tracked by
        // mutating the returned instance and calling UpdateAsync on the repo,
        // which performs the SaveChanges. Cross-tenant id → null → KeyNotFound.
        var existing = await _repo.GetByIdAsync(tenantId, id, ct).ConfigureAwait(false);
        if (existing is null)
            throw new KeyNotFoundException("QuickBooks customer mapping not found.");

        existing.QuickBooksCustomerId = NormaliseQuickBooksCustomerId(command.QuickBooksCustomerId);
        existing.QuickBooksDisplayName = NormaliseDisplayName(command.QuickBooksDisplayName);
        existing.MappingStatus = NormaliseStatus(command.MappingStatus);
        existing.ExportMode = NormaliseExportMode(command.ExportMode);
        existing.UpdatedAtUtc = _clock.GetUtcNow().UtcDateTime;

        await _repo.UpdateAsync(existing, ct).ConfigureAwait(false);
        return existing;
    }

    public Task<bool> DeleteAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _repo.DeleteAsync(tenantId, id, ct);

    public Task<QuickBooksCustomerMapping?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _repo.GetByIdAsync(tenantId, id, ct);

    public Task<IReadOnlyList<QuickBooksCustomerMapping>> ListAsync(
        Guid tenantId, int page, int pageSize, CancellationToken ct = default)
        => _repo.ListAsync(tenantId, page, pageSize, ct);

    public async Task<QuickBooksCustomerMapping?> ResolveActiveByBillingCustomerAsync(
        Guid tenantId,
        Guid billingCustomerId,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || billingCustomerId == Guid.Empty) return null;
        var row = await _repo
            .GetByBillingCustomerAsync(tenantId, billingCustomerId, ct)
            .ConfigureAwait(false);
        if (row is null) return null;
        if (!string.Equals(row.MappingStatus, QuickBooksCustomerMappingStatus.Active, StringComparison.Ordinal))
            return null;
        return row;
    }

    public Task TouchLastExportedAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _repo.TouchLastExportedAsync(tenantId, id, _clock.GetUtcNow().UtcDateTime, ct);

    // ----------------------------------------------------------------
    // Normalisation helpers (pure).
    // ----------------------------------------------------------------

    private static string NormaliseQuickBooksCustomerId(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new ArgumentException("QuickBooks customer id is required.", nameof(raw));
        var trimmed = raw.Trim();
        if (trimmed.Length > MaxQuickBooksCustomerIdLength)
            throw new ArgumentException(
                $"QuickBooks customer id exceeds {MaxQuickBooksCustomerIdLength} characters.",
                nameof(raw));
        return trimmed;
    }

    private static string? NormaliseDisplayName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var trimmed = raw.Trim();
        if (trimmed.Length > MaxDisplayNameLength)
            trimmed = trimmed[..MaxDisplayNameLength];
        return trimmed;
    }

    private static string NormaliseStatus(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return QuickBooksCustomerMappingStatus.Active;
        return raw switch
        {
            QuickBooksCustomerMappingStatus.Active => QuickBooksCustomerMappingStatus.Active,
            QuickBooksCustomerMappingStatus.Disabled => QuickBooksCustomerMappingStatus.Disabled,
            _ => throw new ArgumentException(
                $"mappingStatus must be one of: {QuickBooksCustomerMappingStatus.Active}, {QuickBooksCustomerMappingStatus.Disabled}.",
                nameof(raw)),
        };
    }

    private static string? NormaliseExportMode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return raw switch
        {
            QuickBooksCustomerMappingExportMode.JournalEntry
                => QuickBooksCustomerMappingExportMode.JournalEntry,
            QuickBooksCustomerMappingExportMode.InvoiceFirst
                => QuickBooksCustomerMappingExportMode.InvoiceFirst,
            _ => throw new ArgumentException(
                $"exportMode must be NULL, '{QuickBooksCustomerMappingExportMode.JournalEntry}', or '{QuickBooksCustomerMappingExportMode.InvoiceFirst}'.",
                nameof(raw)),
        };
    }

    private static string NormaliseActor(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "tenant-admin";
        var trimmed = raw.Trim();
        return trimmed.Length > MaxActorLength ? trimmed[..MaxActorLength] : trimmed;
    }
}
