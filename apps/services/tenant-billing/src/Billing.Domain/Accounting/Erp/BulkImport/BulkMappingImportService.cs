using System.Globalization;
using System.Text;
using System.Text.Json;
using Billing.Domain.Accounting.Erp.QuickBooks;
using Billing.Domain.Accounting.Erp.Remediation;

namespace Billing.Domain.Accounting.Erp.BulkImport;

/// <summary>
/// MS-BILL-ERP-006 — concrete bulk-import orchestrator.
///
/// <para>
/// Composition-only: parses via <see cref="IBulkMappingImportParser"/>,
/// validates against <see cref="IErpRemediationRepository"/> +
/// <see cref="IQuickBooksCustomerMappingRepository"/>, persists
/// each accepted row through the same
/// <see cref="IQuickBooksCustomerMappingRepository.AddAsync"/> the
/// ERP-003 single-row controller already uses (so the unique-index
/// 409 backstop applies per row), and stamps a single audit row via
/// <see cref="IBulkMappingImportHistoryRepository"/>.
/// </para>
///
/// <para>
/// Forbidden behaviours that are explicitly NOT implemented here:
/// no automatic mapping, no fuzzy matching, no QBO customer
/// creation, no bulk-INSERT path that bypasses the unique-index
/// backstop, no replay/retry of already-failed exports, no
/// background persistence.
/// </para>
/// </summary>
public sealed class BulkMappingImportService : IBulkMappingImportService
{
    /// <summary>
    /// Maximum operator-confirmed rows accepted on a single commit.
    /// Mirrors the parser's data-row cap so the commit cannot exceed
    /// what the validator could have produced.
    /// </summary>
    public const int CommitRowHardCap = 5000;

    public const int MaxHistoryPageSize = 100;
    public const int DefaultHistoryPageSize = 25;
    public const int MaxExportRows = 50_000;

    private static readonly JsonSerializerOptions SummaryJsonOptions = new()
    {
        WriteIndented = false,
    };

    private readonly IBulkMappingImportParser _parser;
    private readonly IErpRemediationRepository _remediation;
    private readonly IQuickBooksCustomerMappingRepository _mappings;
    private readonly IBulkMappingImportHistoryRepository _history;
    private readonly TimeProvider _clock;

    public BulkMappingImportService(
        IBulkMappingImportParser parser,
        IErpRemediationRepository remediation,
        IQuickBooksCustomerMappingRepository mappings,
        IBulkMappingImportHistoryRepository history,
        TimeProvider clock)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _remediation = remediation ?? throw new ArgumentNullException(nameof(remediation));
        _mappings = mappings ?? throw new ArgumentNullException(nameof(mappings));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<BulkImportPreviewResult> ValidateAsync(
        Guid tenantId,
        Stream csv,
        CancellationToken ct = default)
    {
        if (csv is null) throw new ArgumentNullException(nameof(csv));

        var parsed = await _parser.ParseAsync(csv, ct).ConfigureAwait(false);
        var rows = await ClassifyAsync(tenantId, parsed.Rows, ct).ConfigureAwait(false);
        var token = Guid.NewGuid();
        var valid = rows.Count(r => r.Classification == BulkImportRowClassification.Valid);
        var warning = rows.Count(r => r.Classification == BulkImportRowClassification.Warning);
        var rejected = rows.Count(r => r.Classification == BulkImportRowClassification.Rejected);
        return new BulkImportPreviewResult(
            PreviewToken: token,
            TotalRows: rows.Count,
            ValidCount: valid,
            WarningCount: warning,
            RejectedCount: rejected,
            Rows: rows,
            DocumentIssues: parsed.DocumentIssues);
    }

    public async Task<BulkImportCommitResult> CommitAsync(
        Guid tenantId,
        BulkImportCommitCommand command,
        string operatorDisplayName,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        if (command is null) throw new ArgumentNullException(nameof(command));
        if (command.Rows is null) throw new ArgumentException("Rows are required.", nameof(command));
        if (command.Rows.Count == 0)
            throw new ArgumentException("At least one row is required.", nameof(command));
        if (command.Rows.Count > CommitRowHardCap)
            throw new ArgumentException(
                $"Bulk commit accepts at most {CommitRowHardCap} rows; got {command.Rows.Count}.",
                nameof(command));
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key is required.", nameof(idempotencyKey));

        var trimmedKey = idempotencyKey.Trim();
        if (trimmedKey.Length > 128) trimmedKey = trimmedKey.Substring(0, 128);

        // Fast-path replay protection: if this tenant has already
        // committed under this idempotency key, return the prior
        // outcome verbatim and DO NOT re-execute any AddAsync calls.
        // If the prior row is the reservation placeholder (its
        // finalize step failed), refuse to rehydrate so the caller
        // gets a deterministic retriable error rather than a
        // false-zero replay success.
        var existing = await _history
            .FindByIdempotencyKeyAsync(tenantId, trimmedKey, ct)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            EnsureFinalized(existing);
            return RehydrateResult(existing);
        }

        var startedAt = _clock.GetUtcNow().UtcDateTime;
        var actor = string.IsNullOrWhiteSpace(operatorDisplayName) ? "tenant-admin" : operatorDisplayName.Trim();
        var historyId = Guid.NewGuid();

        // Reserve the audit row BEFORE any mapping writes. Two
        // concurrent commits with the same idempotency key race
        // here; the loser takes the unique-index violation and we
        // return the winner's prior outcome — so duplicate side-
        // effects cannot accumulate even when the fast-path lookup
        // above is bypassed by concurrency.
        try
        {
            await _history.AppendAsync(new BulkMappingImportHistory
            {
                Id = historyId,
                TenantId = tenantId,
                StartedAtUtc = startedAt,
                CompletedAtUtc = startedAt,
                OperatorDisplayName = actor,
                TotalRows = command.Rows.Count,
                AcceptedRows = 0,
                WarningRows = 0,
                RejectedRows = 0,
                SummaryJson = "{}",
                IdempotencyKey = trimmedKey,
            }, ct).ConfigureAwait(false);
        }
        catch (BulkMappingImportReplayException)
        {
            var racing = await _history
                .FindByIdempotencyKeyAsync(tenantId, trimmedKey, ct)
                .ConfigureAwait(false);
            if (racing is not null)
            {
                EnsureFinalized(racing);
                return RehydrateResult(racing);
            }
            throw;
        }

        // Build a synthetic ParsedCsvDocument from the operator-
        // confirmed list so we can re-use the same classifier the
        // preview ran. This is the TOCTOU defence: even if the QBO
        // mapping table changed between preview and commit, the
        // re-classification picks the new state up.
        var synthetic = command.Rows.Select(r => new CsvParsedRow(
            LineNumber: r.LineNumber,
            BillingCustomerIdRaw: r.BillingCustomerId.ToString(),
            BillingCustomerName: null,
            QuickBooksCustomerIdRaw: r.QuickBooksCustomerId,
            QuickBooksDisplayName: r.QuickBooksDisplayName,
            ExportModeRaw: r.ExportMode,
            Notes: r.Notes,
            IsMalformed: false)).ToList();

        var outcomes = new List<BulkImportCommitRowResult>(command.Rows.Count);
        var persisted = 0;
        var conflicted = 0;
        var rejected = 0;
        var failed = 0;
        var aborted = false;
        Exception? primaryFailure = null;

        // try/finally guarantees the reserved audit row is finalized
        // with the actual partial-progress counters even if the
        // request is cancelled or a row write throws an unexpected
        // exception. Without this, a poisoned `{}` audit row would
        // be the rehydration source for any future replay under the
        // same Idempotency-Key.
        try
        {
            var classified = await ClassifyAsync(tenantId, synthetic, ct).ConfigureAwait(false);
            var byLine = classified.ToDictionary(r => r.LineNumber);

            foreach (var row in command.Rows)
            {
                ct.ThrowIfCancellationRequested();
                if (!byLine.TryGetValue(row.LineNumber, out var validated)
                    || validated.Classification == BulkImportRowClassification.Rejected
                    || validated.BillingCustomerId is null
                    || string.IsNullOrWhiteSpace(validated.QuickBooksCustomerId))
                {
                    rejected++;
                    outcomes.Add(new BulkImportCommitRowResult(
                        row.LineNumber,
                        row.BillingCustomerId,
                        row.QuickBooksCustomerId ?? string.Empty,
                        BulkImportCommitOutcome.Rejected,
                        null,
                        validated is null
                            ? "Row was not part of the validated preview."
                            : string.Join("; ", validated.Issues.Select(i => i.Code))));
                    continue;
                }

                var mapping = new QuickBooksCustomerMapping
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    BillingCustomerId = validated.BillingCustomerId.Value,
                    QuickBooksCustomerId = validated.QuickBooksCustomerId!,
                    QuickBooksDisplayName = NullIfBlank(validated.QuickBooksDisplayName),
                    MappingStatus = QuickBooksCustomerMappingStatus.Active,
                    ExportMode = NullIfBlank(validated.ExportMode),
                    CreatedBy = actor,
                    CreatedAtUtc = _clock.GetUtcNow().UtcDateTime,
                    UpdatedAtUtc = _clock.GetUtcNow().UtcDateTime,
                    LastExportedAtUtc = null,
                };

                try
                {
                    await _mappings.AddAsync(mapping, ct).ConfigureAwait(false);
                    persisted++;
                    outcomes.Add(new BulkImportCommitRowResult(
                        row.LineNumber,
                        mapping.BillingCustomerId,
                        mapping.QuickBooksCustomerId,
                        BulkImportCommitOutcome.Persisted,
                        mapping.Id,
                        null));
                }
                catch (QuickBooksCustomerMappingConflictException ex)
                {
                    conflicted++;
                    outcomes.Add(new BulkImportCommitRowResult(
                        row.LineNumber,
                        mapping.BillingCustomerId,
                        mapping.QuickBooksCustomerId,
                        BulkImportCommitOutcome.Conflict,
                        null,
                        ex.Message));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failed++;
                    outcomes.Add(new BulkImportCommitRowResult(
                        row.LineNumber,
                        mapping.BillingCustomerId,
                        mapping.QuickBooksCustomerId,
                        BulkImportCommitOutcome.Failed,
                        null,
                        "Persistence failed; row was skipped."));
                }
            }
        }
        catch (Exception ex)
        {
            aborted = true;
            primaryFailure = ex;
        }

        var completedAt = _clock.GetUtcNow().UtcDateTime;
        var summary = new
        {
            previewToken = command.PreviewToken,
            persisted,
            conflicted,
            rejected,
            failed,
            aborted,
            outcomes = outcomes.Select(o => new
            {
                line = o.LineNumber,
                outcome = o.Outcome,
                billingCustomerId = o.BillingCustomerId,
                quickBooksCustomerId = o.QuickBooksCustomerId,
                mappingId = o.MappingId,
                error = o.Error,
            }),
        };
        // Finalize the reserved audit row with the ACTUAL partial-
        // progress counters. CancellationToken.None ensures a client
        // disconnect after some rows have already landed cannot
        // leave the audit row poisoned with the `{}` reservation
        // payload (which would otherwise cause future replays to
        // rehydrate zero outcomes).
        try
        {
            await _history.FinalizeAsync(
                tenantId,
                historyId,
                completedAt,
                acceptedRows: persisted,
                rejectedRows: rejected + conflicted + failed,
                summaryJson: JsonSerializer.Serialize(summary, SummaryJsonOptions),
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception finalizeEx)
        {
            // Suppress finalize failure ONLY if a primary exception
            // is already in flight — that one is the higher-signal
            // failure for the caller. On the success path, propagate
            // so the caller never receives a false-success response
            // while the audit row remains poisoned at placeholder
            // values.
            if (primaryFailure is null) throw;
            // primary takes precedence; finalize error is dropped.
            _ = finalizeEx;
        }

        if (primaryFailure is not null)
        {
            // Re-throw the original failure preserving its stack.
            System.Runtime.ExceptionServices.ExceptionDispatchInfo
                .Capture(primaryFailure).Throw();
        }

        return new BulkImportCommitResult(
            HistoryId: historyId,
            TotalRequested: command.Rows.Count,
            Persisted: persisted,
            Conflicted: conflicted,
            Rejected: rejected,
            Failed: failed,
            Rows: outcomes);
    }

    public async Task<IReadOnlyList<BulkImportHistorySnapshot>> ListHistoryAsync(
        Guid tenantId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = DefaultHistoryPageSize;
        if (pageSize > MaxHistoryPageSize) pageSize = MaxHistoryPageSize;
        var rows = await _history.ListAsync(tenantId, page, pageSize, ct).ConfigureAwait(false);
        return rows.Select(r => new BulkImportHistorySnapshot(
            r.Id,
            r.StartedAtUtc,
            r.CompletedAtUtc,
            r.OperatorDisplayName,
            r.TotalRows,
            r.AcceptedRows,
            r.WarningRows,
            r.RejectedRows)).ToList();
    }

    public async Task<byte[]> ExportMappingsAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        var rows = await _remediation.ListMappingExportAsync(tenantId, MaxExportRows, ct).ConfigureAwait(false);
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", new[]
        {
            "BillingCustomerId",
            "BillingCustomerName",
            "QuickBooksCustomerId",
            "QuickBooksDisplayName",
            "MappingStatus",
            "ExportMode",
            "CreatedBy",
            "CreatedAtUtc",
            "UpdatedAtUtc",
            "LastExportedAtUtc",
        }));
        foreach (var r in rows)
        {
            sb.Append(EscapeCsv(r.BillingCustomerId.ToString())).Append(',')
              .Append(EscapeCsv(r.BillingCustomerName)).Append(',')
              .Append(EscapeCsv(r.QuickBooksCustomerId)).Append(',')
              .Append(EscapeCsv(r.QuickBooksDisplayName ?? string.Empty)).Append(',')
              .Append(EscapeCsv(r.MappingStatus)).Append(',')
              .Append(EscapeCsv(r.ExportMode ?? string.Empty)).Append(',')
              .Append(EscapeCsv(r.CreatedBy)).Append(',')
              .Append(r.CreatedAtUtc.ToString("o", CultureInfo.InvariantCulture)).Append(',')
              .Append(r.UpdatedAtUtc.ToString("o", CultureInfo.InvariantCulture)).Append(',')
              .Append(r.LastExportedAtUtc?.ToString("o", CultureInfo.InvariantCulture) ?? string.Empty)
              .Append('\n');
        }
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    // ---- internals ----------------------------------------------------

    /// <summary>
    /// Pure, deterministic classifier reused by both the preview
    /// and the TOCTOU re-check at commit time. Order of operations
    /// matters and is explicit: surface → intra-upload dedup →
    /// existing-mapping conflict probe.
    /// </summary>
    private async Task<IReadOnlyList<ValidatedBulkImportRow>> ClassifyAsync(
        Guid tenantId,
        IReadOnlyList<CsvParsedRow> raw,
        CancellationToken ct)
    {
        // ----- 1. Surface validation -----
        var stage1 = new List<(CsvParsedRow Raw, List<BulkImportRowIssue> Issues, Guid? BillingId, string? QboId, string? ExportMode)>();
        foreach (var row in raw)
        {
            var issues = new List<BulkImportRowIssue>();
            Guid? billingId = null;
            string? qboId = null;
            string? exportMode = null;

            if (row.IsMalformed)
            {
                issues.Add(new BulkImportRowIssue(
                    BulkImportRowIssueCode.MalformedCsvRow,
                    "Row could not be parsed; verify quoting and column count."));
                stage1.Add((row, issues, null, null, null));
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.BillingCustomerIdRaw))
            {
                issues.Add(new BulkImportRowIssue(
                    BulkImportRowIssueCode.MissingRequiredField,
                    "BillingCustomerId is required."));
            }
            else if (!Guid.TryParse(row.BillingCustomerIdRaw.Trim(), out var parsedBilling))
            {
                issues.Add(new BulkImportRowIssue(
                    BulkImportRowIssueCode.MissingRequiredField,
                    "BillingCustomerId must be a GUID."));
            }
            else
            {
                billingId = parsedBilling;
            }

            if (string.IsNullOrWhiteSpace(row.QuickBooksCustomerIdRaw))
            {
                issues.Add(new BulkImportRowIssue(
                    BulkImportRowIssueCode.MissingRequiredField,
                    "QuickBooksCustomerId is required."));
            }
            else
            {
                var trimmed = row.QuickBooksCustomerIdRaw.Trim();
                if (trimmed.Length == 0 || trimmed.Length > 100)
                {
                    issues.Add(new BulkImportRowIssue(
                        BulkImportRowIssueCode.InvalidQuickBooksCustomerId,
                        "QuickBooksCustomerId must be 1–100 characters."));
                }
                else
                {
                    qboId = trimmed;
                }
            }

            if (!string.IsNullOrWhiteSpace(row.ExportModeRaw))
            {
                var mode = row.ExportModeRaw.Trim();
                if (!IsAllowedExportMode(mode))
                {
                    issues.Add(new BulkImportRowIssue(
                        BulkImportRowIssueCode.InvalidExportMode,
                        $"ExportMode must be one of: {QuickBooksCustomerMappingExportMode.JournalEntry}, {QuickBooksCustomerMappingExportMode.InvoiceFirst}."));
                }
                else
                {
                    exportMode = mode;
                }
            }

            stage1.Add((row, issues, billingId, qboId, exportMode));
        }

        // ----- 2. Intra-upload dedup -----
        var billingSeen = new Dictionary<Guid, int>();
        var qboSeen = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < stage1.Count; i++)
        {
            var entry = stage1[i];
            if (entry.BillingId is Guid bid)
            {
                if (billingSeen.TryGetValue(bid, out _))
                {
                    entry.Issues.Add(new BulkImportRowIssue(
                        BulkImportRowIssueCode.DuplicateBillingCustomerInUpload,
                        "BillingCustomerId appears more than once in this upload."));
                }
                else
                {
                    billingSeen[bid] = entry.Raw.LineNumber;
                }
            }
            if (!string.IsNullOrEmpty(entry.QboId))
            {
                if (qboSeen.TryGetValue(entry.QboId, out _))
                {
                    entry.Issues.Add(new BulkImportRowIssue(
                        BulkImportRowIssueCode.DuplicateQuickBooksCustomerInUpload,
                        "QuickBooksCustomerId appears more than once in this upload."));
                }
                else
                {
                    qboSeen[entry.QboId] = entry.Raw.LineNumber;
                }
            }
        }

        // ----- 3. Cross-table conflict probe (per row) -----
        var classified = new List<ValidatedBulkImportRow>(stage1.Count);
        foreach (var entry in stage1)
        {
            ct.ThrowIfCancellationRequested();
            var issues = entry.Issues;
            BulkImportRowClassification classification;

            if (entry.BillingId is Guid billingId)
            {
                var customer = await _remediation
                    .GetCustomerAsync(tenantId, billingId, ct).ConfigureAwait(false);
                if (customer is null)
                {
                    issues.Add(new BulkImportRowIssue(
                        BulkImportRowIssueCode.BillingCustomerNotFound,
                        "Billing customer was not found in this tenant."));
                }

                var existingByBilling = await _mappings
                    .GetByBillingCustomerAsync(tenantId, billingId, ct).ConfigureAwait(false);
                if (existingByBilling is not null)
                {
                    if (string.Equals(existingByBilling.MappingStatus,
                        QuickBooksCustomerMappingStatus.Active, StringComparison.Ordinal))
                    {
                        issues.Add(new BulkImportRowIssue(
                            BulkImportRowIssueCode.BillingCustomerAlreadyMapped,
                            "An active mapping already exists for this Billing customer."));
                    }
                    else
                    {
                        // Disabled mapping on the same Billing
                        // customer — surface as a Warning rather
                        // than a Rejected so the operator can
                        // re-enable manually via the ERP-003 PUT.
                        issues.Add(new BulkImportRowIssue(
                            BulkImportRowIssueCode.ExistingDisabledMapping,
                            "A disabled mapping already exists for this Billing customer; re-enable it via the per-customer screen instead of creating a new one."));
                    }
                }
            }

            if (!string.IsNullOrEmpty(entry.QboId)
                && issues.All(i => i.Code != BulkImportRowIssueCode.InvalidQuickBooksCustomerId))
            {
                var existingByQbo = await _mappings
                    .GetByQuickBooksCustomerIdAsync(tenantId, entry.QboId, ct).ConfigureAwait(false);
                if (existingByQbo is not null
                    && (entry.BillingId is null || existingByQbo.BillingCustomerId != entry.BillingId.Value)
                    && string.Equals(existingByQbo.MappingStatus,
                        QuickBooksCustomerMappingStatus.Active, StringComparison.Ordinal))
                {
                    issues.Add(new BulkImportRowIssue(
                        BulkImportRowIssueCode.QuickBooksCustomerAlreadyMapped,
                        "This QuickBooks customer is already mapped to a different Billing customer."));
                }
            }

            classification = ClassifyRow(issues);
            classified.Add(new ValidatedBulkImportRow(
                LineNumber: entry.Raw.LineNumber,
                BillingCustomerId: entry.BillingId,
                BillingCustomerName: entry.Raw.BillingCustomerName,
                QuickBooksCustomerId: entry.QboId,
                QuickBooksDisplayName: entry.Raw.QuickBooksDisplayName,
                ExportMode: entry.ExportMode,
                Notes: entry.Raw.Notes,
                Classification: classification,
                Issues: issues));
        }

        return classified;
    }

    private static BulkImportRowClassification ClassifyRow(IReadOnlyList<BulkImportRowIssue> issues)
    {
        if (issues.Count == 0) return BulkImportRowClassification.Valid;
        foreach (var issue in issues)
        {
            switch (issue.Code)
            {
                case BulkImportRowIssueCode.ExistingDisabledMapping:
                    continue;
                default:
                    return BulkImportRowClassification.Rejected;
            }
        }
        return BulkImportRowClassification.Warning;
    }

    private static bool IsAllowedExportMode(string mode)
        => string.Equals(mode, QuickBooksCustomerMappingExportMode.JournalEntry, StringComparison.Ordinal)
        || string.Equals(mode, QuickBooksCustomerMappingExportMode.InvoiceFirst, StringComparison.Ordinal);

    private static string? NullIfBlank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// CSV cell escaper hardened against spreadsheet formula
    /// injection (CWE-1236): if the first character is one of
    /// <c>= + - @ TAB CR</c>, prefix with a leading apostrophe so
    /// Excel / Sheets / LibreOffice render the cell as text rather
    /// than evaluating it as a formula. The apostrophe is then
    /// quoted per RFC-4180 if the cell otherwise needs quoting.
    /// </summary>
    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var safe = value;
        var first = safe[0];
        if (first == '=' || first == '+' || first == '-' || first == '@'
            || first == '\t' || first == '\r')
        {
            safe = "'" + safe;
        }
        var needsQuoting = safe.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
        if (!needsQuoting) return safe;
        return "\"" + safe.Replace("\"", "\"\"") + "\"";
    }

    /// <summary>
    /// Reservation placeholders (where <see cref="BulkMappingImportHistory.SummaryJson"/>
    /// is still the literal "{}") indicate a prior commit reserved
    /// the idempotency key but its FinalizeAsync step failed before
    /// the totals/outcomes could be written. Rehydrating from such a
    /// row would lie about the actual outcome — instead we surface
    /// the incomplete state so the caller can repair (or rotate the
    /// key) deliberately.
    /// </summary>
    private static void EnsureFinalized(BulkMappingImportHistory existing)
    {
        if (string.Equals(existing.SummaryJson, "{}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "A prior bulk-import attempt under this Idempotency-Key did not finalize its audit row. "
                + "The replay cannot be rehydrated safely; rotate the Idempotency-Key to retry, "
                + "or finalize history row " + existing.Id + " out-of-band before reusing the key.");
        }
    }

    /// <summary>
    /// Re-build the <see cref="BulkImportCommitResult"/> for a
    /// replayed idempotency-key hit using the persisted SummaryJson.
    /// On JSON-shape regression we fall back to a row-less envelope
    /// containing the audit totals so the caller still sees the
    /// "already-applied" outcome rather than a re-execution.
    /// </summary>
    private static BulkImportCommitResult RehydrateResult(BulkMappingImportHistory existing)
    {
        var rows = new List<BulkImportCommitRowResult>();
        var persisted = existing.AcceptedRows;
        var rejected = 0;
        var conflicted = 0;
        var failed = 0;
        try
        {
            using var doc = JsonDocument.Parse(existing.SummaryJson);
            if (doc.RootElement.TryGetProperty("persisted", out var p) && p.TryGetInt32(out var pv)) persisted = pv;
            if (doc.RootElement.TryGetProperty("conflicted", out var c) && c.TryGetInt32(out var cv)) conflicted = cv;
            if (doc.RootElement.TryGetProperty("rejected", out var r) && r.TryGetInt32(out var rv)) rejected = rv;
            if (doc.RootElement.TryGetProperty("failed", out var f) && f.TryGetInt32(out var fv)) failed = fv;
            if (doc.RootElement.TryGetProperty("outcomes", out var outcomes) && outcomes.ValueKind == JsonValueKind.Array)
            {
                foreach (var o in outcomes.EnumerateArray())
                {
                    var line = o.TryGetProperty("line", out var ln) && ln.TryGetInt32(out var lv) ? lv : 0;
                    var outcome = o.TryGetProperty("outcome", out var oc) && oc.ValueKind == JsonValueKind.String
                        ? oc.GetString() ?? string.Empty
                        : string.Empty;
                    var billing = o.TryGetProperty("billingCustomerId", out var bc) && bc.TryGetGuid(out var bg)
                        ? bg : Guid.Empty;
                    var qbo = o.TryGetProperty("quickBooksCustomerId", out var qc) && qc.ValueKind == JsonValueKind.String
                        ? qc.GetString() ?? string.Empty
                        : string.Empty;
                    Guid? mappingId = null;
                    if (o.TryGetProperty("mappingId", out var mc) && mc.ValueKind == JsonValueKind.String
                        && Guid.TryParse(mc.GetString(), out var mg)) mappingId = mg;
                    var error = o.TryGetProperty("error", out var ec) && ec.ValueKind == JsonValueKind.String
                        ? ec.GetString() : null;
                    rows.Add(new BulkImportCommitRowResult(line, billing, qbo, outcome, mappingId, error));
                }
            }
        }
        catch (JsonException)
        {
            // Fall through with the totals we already have; rows stays empty.
        }
        return new BulkImportCommitResult(
            HistoryId: existing.Id,
            TotalRequested: existing.TotalRows,
            Persisted: persisted,
            Conflicted: conflicted,
            Rejected: rejected,
            Failed: failed,
            Rows: rows);
    }
}
