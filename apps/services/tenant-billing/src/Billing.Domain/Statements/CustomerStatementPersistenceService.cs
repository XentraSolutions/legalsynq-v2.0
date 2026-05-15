using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Billing.Domain.Entities;
using Billing.Domain.StatementTemplates;

namespace Billing.Domain.Statements;

/// <summary>
/// STAT-B02 — Default <see cref="ICustomerStatementPersistenceService"/>.
/// Composes the existing STAT-B01 builder
/// (<see cref="ICustomerStatementService"/>), the new template
/// selection service, the number generator, and the repository.
///
/// Snapshot immutability is enforced here, not in the entity:
/// once <see cref="ICustomerStatementRepository.AddAsync"/> succeeds,
/// only <see cref="VoidAsync"/> may mutate the row, and only its
/// status / void columns.
/// </summary>
public sealed class CustomerStatementPersistenceService : ICustomerStatementPersistenceService
{
    /// <summary>
    /// Number-collision retry cap. Five attempts comfortably covers
    /// the worst-case admin-driven contention without becoming a
    /// silent infinite loop on a misconfigured environment.
    /// </summary>
    public const int MaxNumberRetries = 5;

    /// <summary>
    /// JSON serializer settings used for both the statement and
    /// template snapshots. Enum-as-string so a future enum rename
    /// is detectable as a deserialization mismatch instead of a
    /// silently-shifted integer.
    /// </summary>
    public static readonly JsonSerializerOptions SnapshotJsonOptions = BuildSnapshotJsonOptions();

    private static JsonSerializerOptions BuildSnapshotJsonOptions()
    {
        // Reflection-based default resolver is required for
        // .NET 8's MakeReadOnly() to succeed on a freshly-built
        // JsonSerializerOptions; we don't ship a source-generated
        // resolver yet.
        var o = new JsonSerializerOptions(JsonSerializerDefaults.General)
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
            Converters = { new JsonStringEnumConverter() },
            WriteIndented = false,
        };
        o.MakeReadOnly();
        return o;
    }

    private readonly ICustomerStatementService _builder;
    private readonly ICustomerStatementHtmlRenderer _renderer;
    private readonly IStatementTemplateSelectionService _templates;
    private readonly IStatementNumberGenerator _numbers;
    private readonly ICustomerStatementRepository _repository;
    private readonly TimeProvider _time;

    public CustomerStatementPersistenceService(
        ICustomerStatementService builder,
        ICustomerStatementHtmlRenderer renderer,
        IStatementTemplateSelectionService templates,
        IStatementNumberGenerator numbers,
        ICustomerStatementRepository repository,
        TimeProvider? time = null)
    {
        _builder = builder;
        _renderer = renderer;
        _templates = templates;
        _numbers = numbers;
        _repository = repository;
        _time = time ?? TimeProvider.System;
    }

    public Task<CustomerStatement?> GenerateMonthlyAsync(
        Guid tenantId, Guid customerId, int year, int month,
        Guid? explicitTemplateId, bool renderHtml, CancellationToken ct = default)
    {
        if (year < 1900 || year > 2999)
            throw new StatementValidationException($"Year {year} is out of supported range (1900-2999).");
        if (month < 1 || month > 12)
            throw new StatementValidationException($"Month {month} must be between 1 and 12.");

        var from = new DateTime(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        return GenerateAsync(tenantId, customerId, from, to, explicitTemplateId, renderHtml, ct);
    }

    public async Task<CustomerStatement?> GenerateAsync(
        Guid tenantId, Guid customerId, DateTime periodStart, DateTime periodEnd,
        Guid? explicitTemplateId, bool renderHtml, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (customerId == Guid.Empty)
            throw new ArgumentException("CustomerId is required.", nameof(customerId));

        var document = await _builder.BuildStatementAsync(
            tenantId, customerId, periodStart, periodEnd, ct);
        if (document is null) return null;

        // Template selection happens AFTER the build so a build
        // failure (validation, missing customer) is reported
        // without consulting the template catalogue. The selection
        // service throws StatementTemplateNotFoundInScopeException /
        // StatementTemplateNotSelectableException directly — the
        // controller maps both to 400.
        var template = await _templates.SelectForStatementAsync(tenantId, explicitTemplateId, ct);

        var statementJson = JsonSerializer.Serialize(document, SnapshotJsonOptions);
        var templateJson = template is null ? null : JsonSerializer.Serialize(template, SnapshotJsonOptions);
        var html = renderHtml ? _renderer.Render(document) : null;
        var now = _time.GetUtcNow().UtcDateTime;
        var year = now.Year;

        // Retry loop: if two writers race on MAX(seq)+1 the second
        // hits the (TenantId, StatementNumber) unique index. The
        // repository raises CustomerStatementNumberConflictException
        // and we re-roll. Bounded by MaxNumberRetries.
        Exception? lastError = null;
        for (var attempt = 0; attempt < MaxNumberRetries; attempt++)
        {
            var number = await _numbers.NextAsync(tenantId, year, ct);
            var entity = new CustomerStatement
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CustomerId = customerId,
                StatementNumber = number,
                TemplateId = template?.Id,
                PeriodStart = document.PeriodStartDate,
                PeriodEnd = document.PeriodEndDate,
                GeneratedAtUtc = now,
                Status = CustomerStatementStatus.Generated,
                Currency = document.Currency,
                OpeningBalance = document.OpeningBalance,
                ClosingBalance = document.ClosingBalance,
                OutstandingBalance = document.OutstandingBalance,
                TotalInvoiced = document.TotalInvoiced,
                TotalPaid = document.TotalPaid,
                StatementSnapshotJson = statementJson,
                TemplateSnapshotJson = templateJson,
                HtmlSnapshot = html,
            };

            try
            {
                return await _repository.AddAsync(entity, ct);
            }
            catch (CustomerStatementNumberConflictException ex)
            {
                lastError = ex;
            }
        }

        throw new CustomerStatementNumberConflictException(
            $"Could not allocate a unique statement number after {MaxNumberRetries} attempts; please retry.",
            lastError!);
    }

    public Task<IReadOnlyList<CustomerStatement>> ListHistoryAsync(
        Guid tenantId, Guid customerId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (customerId == Guid.Empty)
            throw new ArgumentException("CustomerId is required.", nameof(customerId));
        return _repository.ListForCustomerAsync(tenantId, customerId, ct);
    }

    public Task<CustomerStatement?> GetHistoryAsync(
        Guid tenantId, Guid statementId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (statementId == Guid.Empty)
            throw new ArgumentException("StatementId is required.", nameof(statementId));
        return _repository.GetByIdInScopeAsync(tenantId, statementId, ct);
    }

    public async Task<string?> RenderHtmlAsync(Guid tenantId, Guid statementId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (statementId == Guid.Empty)
            throw new ArgumentException("StatementId is required.", nameof(statementId));

        var statement = await _repository.GetByIdInScopeAsync(tenantId, statementId, ct);
        if (statement is null) return null;

        if (!string.IsNullOrEmpty(statement.HtmlSnapshot))
            return statement.HtmlSnapshot;

        var document = JsonSerializer.Deserialize<CustomerStatementDocument>(
            statement.StatementSnapshotJson, SnapshotJsonOptions);
        if (document is null)
        {
            // Defensive: a corrupt snapshot is an internal-state
            // issue, not a request-shape issue. Surface with a clear
            // 400 to avoid masking the corruption.
            throw new StatementValidationException(
                $"Statement {statementId} has a missing or unreadable snapshot.");
        }

        return _renderer.Render(document);
    }

    public async Task<CustomerStatement?> RecordDeliveryAttemptAsync(
        Guid tenantId,
        Guid statementId,
        string provider,
        string deliveryStatus,
        string? failureReason,
        string? recipientEmail,
        string? sentBy,
        string? deliveryId,
        string? correlationId,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (statementId == Guid.Empty)
            throw new ArgumentException("StatementId is required.", nameof(statementId));
        if (string.IsNullOrWhiteSpace(provider))
            throw new ArgumentException("Provider is required.", nameof(provider));
        if (string.IsNullOrWhiteSpace(deliveryStatus))
            throw new ArgumentException("DeliveryStatus is required.", nameof(deliveryStatus));
        if (!Delivery.StatementDeliveryStatus.IsValid(deliveryStatus))
            throw new ArgumentException(
                $"Unknown DeliveryStatus '{deliveryStatus}'. Must be one of: Sent, Failed, ProviderUnavailable, InvalidRecipient, RetryableFailure, RetryNotAllowed.",
                nameof(deliveryStatus));
        // MS-BILL-INT-003 — RetryNotAllowed is a governance short-
        // circuit; the orchestrator MUST NOT persist it as a "last
        // attempt" because doing so would clobber the genuine prior
        // outcome (and corrupt cooldown / retry-count math). Treat
        // accidental persistence as a programmer error.
        if (deliveryStatus == Delivery.StatementDeliveryStatus.RetryNotAllowed)
            throw new InvalidOperationException(
                "RetryNotAllowed must not be persisted on the snapshot row.");

        var statement = await _repository.GetByIdInScopeAsync(tenantId, statementId, ct);
        if (statement is null) return null;

        var nowUtc = _time.GetUtcNow().UtcDateTime;

        // Append-only counter — cannot decrement. Persisted even
        // on the ProviderNotConfigured / InvalidRecipient branches
        // so operators have a single number to alert on.
        statement.DeliveryRetryCount = checked(statement.DeliveryRetryCount + 1);

        statement.DeliveryProvider = TrimToMaxOrNull(provider, 64);
        statement.DeliveryStatus = deliveryStatus;
        statement.DeliveryFailureReason = TrimToMaxOrNull(failureReason, 200);
        statement.DeliveryRecipientEmail = TrimToMaxOrNull(recipientEmail, 320);
        statement.DeliverySentBy = TrimToMaxOrNull(sentBy, 200);
        statement.DeliveryId = TrimToMaxOrNull(deliveryId, 200);
        statement.DeliveryCorrelationId = TrimToMaxOrNull(correlationId, 64);
        statement.DeliveryAttemptedAtUtc = nowUtc;
        if (deliveryStatus == Delivery.StatementDeliveryStatus.Sent)
        {
            statement.DeliveryLastSentAtUtc = nowUtc;
        }

        await _repository.UpdateAsync(statement, ct);
        return statement;
    }

    private static string? TrimToMaxOrNull(string? src, int max)
    {
        if (string.IsNullOrWhiteSpace(src)) return null;
        var trimmed = src.Trim();
        return trimmed.Length <= max ? trimmed : trimmed.Substring(0, max);
    }

    public async Task<CustomerStatement?> VoidAsync(
        Guid tenantId, Guid statementId, string? reason, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (statementId == Guid.Empty)
            throw new ArgumentException("StatementId is required.", nameof(statementId));

        var statement = await _repository.GetByIdInScopeAsync(tenantId, statementId, ct);
        if (statement is null) return null;

        // Idempotent: voiding an already-voided statement is a
        // no-op and returns the current row so the caller's UI can
        // re-converge without surfacing a 409.
        if (statement.Status == CustomerStatementStatus.Voided)
            return statement;

        statement.Status = CustomerStatementStatus.Voided;
        statement.VoidedAtUtc = _time.GetUtcNow().UtcDateTime;
        statement.VoidReason = string.IsNullOrWhiteSpace(reason)
            ? null
            : reason.Trim().Substring(0, Math.Min(reason.Trim().Length, 1000));

        await _repository.UpdateAsync(statement, ct);
        return statement;
    }
}
