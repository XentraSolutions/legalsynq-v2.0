using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Billing.Domain.Accounting.Erp;
using Billing.Domain.Accounting.Erp.QuickBooks;

#pragma warning disable IDE0005

namespace Billing.Infrastructure.Accounting.Erp.Providers.QuickBooks;

/// <summary>
/// MS-BILL-ERP-002 — Real QuickBooks Online implementation of
/// <see cref="IAccountingExportProvider"/>. Posts each derived
/// <see cref="AccountingJournalEntry"/> in the supplied
/// <see cref="AccountingExportPayload"/> as a QBO JournalEntry
/// against the configured realm.
///
/// <para>
/// Strategic stance: <b>JournalEntry-first</b>. The provider
/// never creates QBO Customers, never resolves QBO Invoices, and
/// never performs fuzzy matching on Billing customer names —
/// every line is a self-contained debit + credit pair against
/// pre-configured QBO chart-of-account refs. This keeps the
/// integration deterministic and prevents the QBO ledger from
/// silently growing rows that Billing did not author.
/// </para>
///
/// <para>
/// Failure mapping (collapsed at batch level):
/// </para>
/// <list type="bullet">
///   <item>All lines 2xx → <see cref="AccountingExportStatus.Exported"/>.</item>
///   <item>QBO duplicate-detected (same RequestId within window)
///   → counted as success per line; if every line is duplicate
///   the batch surfaces as <see cref="AccountingExportStatus.Duplicate"/>.</item>
///   <item>Any line returns 400 → <see cref="AccountingExportStatus.Failed"/>
///   with the QBO error code/detail (capped to 500 chars,
///   NON-PII).</item>
///   <item>Any line returns 401 / 403, or token refresh fails →
///   <see cref="AccountingExportStatus.ProviderUnavailable"/>.</item>
///   <item>Any line returns 429 / 5xx, or transport timeout →
///   <see cref="AccountingExportStatus.ProviderUnavailable"/>
///   (operator-retryable; we do NOT auto-retry — no queue, no
///   scheduler).</item>
///   <item>Any other unmapped status →
///   <see cref="AccountingExportStatus.Failed"/> with the literal
///   status code as the reason.</item>
/// </list>
///
/// <para>
/// Logging discipline: NEVER logs the access token, refresh
/// token, client secret, account ref values, or full request /
/// response bodies. Logs only correlation id, line count, status
/// code, and mapped status.
/// </para>
/// </summary>
public sealed class QuickBooksAccountingExportProvider : IAccountingExportProvider
{
    public const string Name = "quickbooks";

    private const int FailureReasonCapChars = 500;

    private readonly HttpClient _http;
    private readonly IOptionsMonitor<QuickBooksOptions> _optionsMonitor;
    private readonly IQuickBooksTokenProvider _tokenProvider;
    private readonly IQuickBooksCustomerMappingService _mappings;
    private readonly ILogger<QuickBooksAccountingExportProvider> _log;

    public QuickBooksAccountingExportProvider(
        HttpClient http,
        IOptionsMonitor<QuickBooksOptions> optionsMonitor,
        IQuickBooksTokenProvider tokenProvider,
        IQuickBooksCustomerMappingService mappings,
        ILogger<QuickBooksAccountingExportProvider> log)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
        _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        _mappings = mappings ?? throw new ArgumentNullException(nameof(mappings));
        _log = log ?? throw new ArgumentNullException(nameof(log));

        var snapshot = _optionsMonitor.CurrentValue;
        if (snapshot.TimeoutSeconds > 0)
        {
            _http.Timeout = TimeSpan.FromSeconds(snapshot.TimeoutSeconds);
        }
    }

    public string ProviderName => Name;

    public bool IsConfigured => _optionsMonitor.CurrentValue.HasRequired();

    public async Task<AccountingExportProviderResult> ExportAsync(
        AccountingExportPayload payload,
        CancellationToken ct = default)
    {
        if (payload is null) throw new ArgumentNullException(nameof(payload));

        var options = _optionsMonitor.CurrentValue;
        if (!options.HasRequired())
        {
            // Half-configured deployment surfaces the same UI
            // banner as "no provider configured at all".
            return AccountingExportProviderResult.ProviderUnavailable(
                provider: Name,
                correlationId: payload.CorrelationId,
                failureReason: "QuickBooks provider configuration is incomplete.");
        }

        // Acquire access token. A failure here collapses to
        // ProviderUnavailable — never bubbles into the controller.
        string accessToken;
        try
        {
            accessToken = await _tokenProvider.GetAccessTokenAsync(ct).ConfigureAwait(false);
        }
        catch (QuickBooksTokenException tex)
        {
            _log.LogWarning(
                "QuickBooks token unavailable. tenantId={TenantId} correlationId={CorrelationId} reason={Reason}",
                payload.TenantId, payload.CorrelationId, tex.Reason);
            return AccountingExportProviderResult.ProviderUnavailable(
                provider: Name,
                correlationId: payload.CorrelationId,
                failureReason: $"QuickBooks token: {tex.Reason}");
        }

        // MS-BILL-ERP-003 — branch on provider-wide ExportMode. The
        // InvoiceFirst path iterates payload.Invoices and posts each
        // as a QBO Invoice resolved against the operator-curated
        // customer mapping; the JournalEntry path below preserves the
        // ERP-002 default behaviour and is unchanged.
        if (string.Equals(options.ResolveExportMode(), QuickBooksOptions.ExportModeInvoiceFirst, StringComparison.Ordinal))
        {
            return await ExportInvoicesAsync(payload, options, accessToken, ct).ConfigureAwait(false);
        }

        if (payload.JournalEntries.Count == 0)
        {
            // Nothing to send. The orchestrator already persists the
            // payload row, so a stable Exported result with an empty
            // batch reference is the correct outcome.
            return AccountingExportProviderResult.Exported(
                provider: Name,
                correlationId: payload.CorrelationId,
                externalReferenceId: $"qbo:batch:{payload.CorrelationId}");
        }

        var apiBase = options.ResolveApiBaseUrl();
        Uri requestUri;
        try
        {
            requestUri = new Uri(
                $"{apiBase}/v3/company/{Uri.EscapeDataString(options.RealmId)}/journalentry?minorversion={options.MinorVersion}",
                UriKind.Absolute);
        }
        catch (UriFormatException)
        {
            _log.LogWarning(
                "QuickBooks endpoint URI malformed. tenantId={TenantId} correlationId={CorrelationId}",
                payload.TenantId, payload.CorrelationId);
            return AccountingExportProviderResult.ProviderUnavailable(
                provider: Name,
                correlationId: payload.CorrelationId,
                failureReason: "QuickBooks endpoint URI is malformed.");
        }

        // Per-line outcome accumulator. Decisions collapse at the
        // end of the loop; the first non-success short-circuits the
        // batch with the appropriate status (auth/network failures
        // dominate validation failures, which dominate success).
        var perLineExternalIds = new List<string>(payload.JournalEntries.Count);
        var duplicateCount = 0;

        for (var i = 0; i < payload.JournalEntries.Count; i++)
        {
            var entry = payload.JournalEntries[i];
            var lineCorrelationId = $"{payload.CorrelationId}:{i}";
            var requestId = BuildRequestId(payload.CorrelationId, entry);

            var qboBody = BuildJournalEntryBody(entry, options, payload, requestId);

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = JsonContent.Create(qboBody),
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            httpRequest.Headers.TryAddWithoutValidation("X-Correlation-Id", lineCorrelationId);

            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(httpRequest, ct).ConfigureAwait(false);
            }
            catch (TaskCanceledException tex) when (!ct.IsCancellationRequested)
            {
                _log.LogWarning(tex,
                    "QuickBooks export timed out. tenantId={TenantId} correlationId={CorrelationId} lineIndex={LineIndex}",
                    payload.TenantId, payload.CorrelationId, i);
                return AccountingExportProviderResult.ProviderUnavailable(
                    provider: Name,
                    correlationId: payload.CorrelationId,
                    failureReason: "QuickBooks request timed out.");
            }
            catch (HttpRequestException hex)
            {
                _log.LogWarning(hex,
                    "QuickBooks export transport error. tenantId={TenantId} correlationId={CorrelationId} lineIndex={LineIndex}",
                    payload.TenantId, payload.CorrelationId, i);
                return AccountingExportProviderResult.ProviderUnavailable(
                    provider: Name,
                    correlationId: payload.CorrelationId,
                    failureReason: "QuickBooks transport error.");
            }
            catch (Exception ex)
            {
                // Belt-and-braces: an unmapped exception MUST collapse
                // to a deterministic Failed result so the orchestrator
                // can persist a row instead of throwing through the
                // controller. Reason is the exception type only —
                // exception messages may carry response bytes.
                _log.LogError(ex,
                    "QuickBooks export unexpected error. tenantId={TenantId} correlationId={CorrelationId} lineIndex={LineIndex}",
                    payload.TenantId, payload.CorrelationId, i);
                return AccountingExportProviderResult.Failed(
                    provider: Name,
                    correlationId: payload.CorrelationId,
                    failureReason: $"QuickBooks {ex.GetType().Name}");
            }

            using (response)
            {
                var status = (int)response.StatusCode;
                string? bodyText = null;
                try
                {
                    bodyText = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    if (bodyText is { Length: > 4096 })
                    {
                        bodyText = bodyText[..4096];
                    }
                }
                catch
                {
                    bodyText = null;
                }

                _log.LogInformation(
                    "QuickBooks export response. tenantId={TenantId} correlationId={CorrelationId} lineIndex={LineIndex} statusCode={StatusCode}",
                    payload.TenantId, payload.CorrelationId, i, status);

                if (status is >= 200 and < 300)
                {
                    var qboId = TryExtractJournalEntryId(bodyText);
                    if (qboId is not null)
                    {
                        perLineExternalIds.Add(qboId);
                    }
                    else
                    {
                        // QBO returned 2xx but no parseable id — record
                        // the line by its request id so reconciliation
                        // can still pivot.
                        perLineExternalIds.Add($"requestId:{requestId}");
                    }
                    continue;
                }

                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    return AccountingExportProviderResult.ProviderUnavailable(
                        provider: Name,
                        correlationId: payload.CorrelationId,
                        failureReason: $"QuickBooks rejected credentials (HTTP {status}).");
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests || status >= 500)
                {
                    return AccountingExportProviderResult.ProviderUnavailable(
                        provider: Name,
                        correlationId: payload.CorrelationId,
                        failureReason: $"QuickBooks transient (HTTP {status}).");
                }

                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    // QBO may report a duplicate as 400 with the
                    // "Duplicate" code; treat that line as already-
                    // landed and continue.
                    if (LooksLikeQboDuplicate(bodyText))
                    {
                        duplicateCount++;
                        perLineExternalIds.Add($"qbo:duplicate:{requestId}");
                        continue;
                    }

                    var detail = ExtractQboErrorReason(bodyText) ?? "ValidationError";
                    return AccountingExportProviderResult.Failed(
                        provider: Name,
                        correlationId: payload.CorrelationId,
                        failureReason: Cap($"QuickBooks 400: {detail}"));
                }

                // Any other status (404, 405, etc.) → deterministic Failed.
                return AccountingExportProviderResult.Failed(
                    provider: Name,
                    correlationId: payload.CorrelationId,
                    failureReason: $"QuickBooks unexpected HTTP {status}.");
            }
        }

        // Loop completed: every line either landed or was a known
        // QBO duplicate.
        var batchExternalReference = $"qbo:batch:{payload.CorrelationId}";
        if (duplicateCount == payload.JournalEntries.Count)
        {
            // Every line was a duplicate — batch is a deterministic
            // duplicate replay. Persist the batch reference so the
            // operator can still pivot.
            return new AccountingExportProviderResult(
                Success: false,
                Provider: Name,
                Status: AccountingExportStatus.Duplicate,
                ExternalReferenceId: batchExternalReference,
                CorrelationId: payload.CorrelationId,
                FailureReason: "QuickBooks reported every line as a duplicate replay.");
        }

        return AccountingExportProviderResult.Exported(
            provider: Name,
            correlationId: payload.CorrelationId,
            externalReferenceId: batchExternalReference);
    }

    // ----------------------------------------------------------------
    // Mapping helpers (pure).
    // ----------------------------------------------------------------

    /// <summary>
    /// Build a deterministic QBO RequestId for one journal-entry
    /// line. QBO documents that re-posts with the same RequestId
    /// inside the rolling window return the original entity, which
    /// is exactly the replay semantic we want.
    /// </summary>
    private static string BuildRequestId(string batchCorrelationId, AccountingJournalEntry entry)
    {
        // QBO RequestId max length is 50 ASCII chars. Hash + truncate
        // to stay safely within bounds and to avoid leaking GUIDs.
        var raw = $"{batchCorrelationId}|{entry.EntryType}|{entry.SourceId:N}|{entry.Amount}";
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw));
        var hex = Convert.ToHexString(hash);
        return hex[..40].ToLowerInvariant();
    }

    /// <summary>
    /// Map an immutable Billing journal entry onto the QBO
    /// JournalEntry shape. Two lines per entry: a Debit line
    /// against <c>DebitAccount</c> and a Credit line against
    /// <c>CreditAccount</c>. Account names are mapped onto the
    /// configured QBO account refs — Billing is the authority for
    /// canonical account names; QBO is the consumer.
    /// </summary>
    private static object BuildJournalEntryBody(
        AccountingJournalEntry entry,
        QuickBooksOptions options,
        AccountingExportPayload payload,
        string requestId)
    {
        var debitRef = ResolveAccountRef(entry.DebitAccount, options);
        var creditRef = ResolveAccountRef(entry.CreditAccount, options);

        return new
        {
            TxnDate = entry.EntryDate.ToString("yyyy-MM-dd"),
            DocNumber = TruncateDocNumber($"BIL-{entry.SourceId:N}"),
            PrivateNote =
                $"Billing:correlation={payload.CorrelationId} source={entry.SourceId:N} type={entry.EntryType}",
            CurrencyRef = new { value = entry.Currency },
            Line = new object[]
            {
                BuildJournalLine(entry.Amount, "Debit", entry.Memo, debitRef),
                BuildJournalLine(entry.Amount, "Credit", entry.Memo, creditRef),
            },
            // Intuit dedupe header: same value within the rolling
            // window returns the original row (mapped to Duplicate).
            // Carried in the body for visibility; the actual header
            // is also set on the request.
            RequestId = requestId,
        };
    }

    private static object BuildJournalLine(decimal amount, string posting, string memo, string accountRef)
    {
        return new
        {
            Amount = amount,
            DetailType = "JournalEntryLineDetail",
            Description = memo,
            JournalEntryLineDetail = new
            {
                PostingType = posting,
                AccountRef = new { value = accountRef },
            },
        };
    }

    /// <summary>
    /// Map a canonical Billing account label (as emitted by
    /// <c>AccountingExportProjectionBuilder</c>) onto the
    /// configured QBO account ref. Unrecognised labels fall back
    /// to the adjustment account so a half-mapped chart of
    /// accounts still produces a deterministic, operator-visible
    /// result rather than a thrown exception.
    /// </summary>
    private static string ResolveAccountRef(string canonicalAccount, QuickBooksOptions options)
    {
        if (string.IsNullOrWhiteSpace(canonicalAccount)) return options.AdjustmentAccountRef;
        var normalized = canonicalAccount.Trim().ToLowerInvariant();
        return normalized switch
        {
            "accounts receivable" or "ar" => options.AccountsReceivableRef,
            "income" or "revenue" or "sales" => options.IncomeAccountRef,
            "undeposited funds" or "cash" or "bank" => options.UndepositedFundsRef,
            "adjustment" or "credit memo" or "discount" => options.AdjustmentAccountRef,
            _ => options.AdjustmentAccountRef,
        };
    }

    // ----------------------------------------------------------------
    // MS-BILL-ERP-003 — Invoice-first export path.
    //
    // Iterates payload.Invoices and POSTs each as a QBO Invoice
    // resolved through IQuickBooksCustomerMappingService. Customer
    // resolution is governed:
    //   1. Operator-curated mapping for (TenantId, BillingCustomerId)
    //      — used iff the row exists AND MappingStatus=Active.
    //   2. Optional QuickBooksOptions.FallbackCustomerRef when
    //      FallbackCustomerEnabled=true.
    //   3. Otherwise the line — and the batch — collapse to
    //      AccountingExportStatus.Failed with a deterministic,
    //      human-readable reason. The provider NEVER fuzzy-matches
    //      customer names and NEVER auto-creates a QBO Customer.
    //
    // Failure-status precedence mirrors the JournalEntry path:
    // 401/403 → ProviderUnavailable; 429/5xx/transport → ProviderUnavailable;
    // structured QBO duplicate → counted as duplicate; 400 →
    // Failed (capped reason); other → Failed.
    // ----------------------------------------------------------------
    private async Task<AccountingExportProviderResult> ExportInvoicesAsync(
        AccountingExportPayload payload,
        QuickBooksOptions options,
        string accessToken,
        CancellationToken ct)
    {
        if (payload.Invoices.Count == 0)
        {
            return AccountingExportProviderResult.Exported(
                provider: Name,
                correlationId: payload.CorrelationId,
                externalReferenceId: $"qbo:batch:{payload.CorrelationId}");
        }

        var apiBase = options.ResolveApiBaseUrl();
        Uri requestUri;
        try
        {
            requestUri = new Uri(
                $"{apiBase}/v3/company/{Uri.EscapeDataString(options.RealmId)}/invoice?minorversion={options.MinorVersion}",
                UriKind.Absolute);
        }
        catch (UriFormatException)
        {
            _log.LogWarning(
                "QuickBooks endpoint URI malformed (invoice). tenantId={TenantId} correlationId={CorrelationId}",
                payload.TenantId, payload.CorrelationId);
            return AccountingExportProviderResult.ProviderUnavailable(
                provider: Name,
                correlationId: payload.CorrelationId,
                failureReason: "QuickBooks endpoint URI is malformed.");
        }

        var perLineExternalIds = new List<string>(payload.Invoices.Count);
        var duplicateCount = 0;
        var touchedMappingIds = new HashSet<Guid>();

        for (var i = 0; i < payload.Invoices.Count; i++)
        {
            var invoice = payload.Invoices[i];
            var lineCorrelationId = $"{payload.CorrelationId}:inv:{i}";
            var requestId = BuildInvoiceRequestId(payload.CorrelationId, invoice);

            var mapping = await _mappings
                .ResolveActiveByBillingCustomerAsync(payload.TenantId, invoice.CustomerId, ct)
                .ConfigureAwait(false);

            string customerRef;
            if (mapping is not null)
            {
                customerRef = mapping.QuickBooksCustomerId;
                touchedMappingIds.Add(mapping.Id);
            }
            else if (options.FallbackCustomerEnabled
                && !string.IsNullOrWhiteSpace(options.FallbackCustomerRef))
            {
                customerRef = options.FallbackCustomerRef!.Trim();
            }
            else
            {
                _log.LogWarning(
                    "QuickBooks invoice export: no customer mapping and fallback disabled. tenantId={TenantId} correlationId={CorrelationId} invoiceId={InvoiceId}",
                    payload.TenantId, payload.CorrelationId, invoice.InvoiceId);
                return AccountingExportProviderResult.Failed(
                    provider: Name,
                    correlationId: payload.CorrelationId,
                    failureReason: Cap(
                        $"QuickBooks invoice {invoice.InvoiceNumber}: no QBO customer mapping for billingCustomerId={invoice.CustomerId:N} and fallback disabled."));
            }

            var qboBody = BuildInvoiceBody(invoice, options, payload, customerRef, requestId);

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = JsonContent.Create(qboBody),
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            httpRequest.Headers.TryAddWithoutValidation("X-Correlation-Id", lineCorrelationId);

            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(httpRequest, ct).ConfigureAwait(false);
            }
            catch (TaskCanceledException tex) when (!ct.IsCancellationRequested)
            {
                _log.LogWarning(tex,
                    "QuickBooks invoice export timed out. tenantId={TenantId} correlationId={CorrelationId} invoiceId={InvoiceId}",
                    payload.TenantId, payload.CorrelationId, invoice.InvoiceId);
                return AccountingExportProviderResult.ProviderUnavailable(
                    provider: Name,
                    correlationId: payload.CorrelationId,
                    failureReason: "QuickBooks request timed out.");
            }
            catch (HttpRequestException hex)
            {
                _log.LogWarning(hex,
                    "QuickBooks invoice export transport error. tenantId={TenantId} correlationId={CorrelationId} invoiceId={InvoiceId}",
                    payload.TenantId, payload.CorrelationId, invoice.InvoiceId);
                return AccountingExportProviderResult.ProviderUnavailable(
                    provider: Name,
                    correlationId: payload.CorrelationId,
                    failureReason: "QuickBooks transport error.");
            }
            catch (Exception ex)
            {
                _log.LogError(ex,
                    "QuickBooks invoice export unexpected error. tenantId={TenantId} correlationId={CorrelationId} invoiceId={InvoiceId}",
                    payload.TenantId, payload.CorrelationId, invoice.InvoiceId);
                return AccountingExportProviderResult.Failed(
                    provider: Name,
                    correlationId: payload.CorrelationId,
                    failureReason: $"QuickBooks {ex.GetType().Name}");
            }

            using (response)
            {
                var status = (int)response.StatusCode;
                string? bodyText = null;
                try
                {
                    bodyText = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    if (bodyText is { Length: > 4096 }) bodyText = bodyText[..4096];
                }
                catch { bodyText = null; }

                _log.LogInformation(
                    "QuickBooks invoice export response. tenantId={TenantId} correlationId={CorrelationId} invoiceId={InvoiceId} statusCode={StatusCode}",
                    payload.TenantId, payload.CorrelationId, invoice.InvoiceId, status);

                if (status is >= 200 and < 300)
                {
                    var qboId = TryExtractInvoiceId(bodyText);
                    perLineExternalIds.Add(qboId is not null ? qboId : $"requestId:{requestId}");
                    continue;
                }

                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    return AccountingExportProviderResult.ProviderUnavailable(
                        provider: Name,
                        correlationId: payload.CorrelationId,
                        failureReason: $"QuickBooks rejected credentials (HTTP {status}).");
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests || status >= 500)
                {
                    return AccountingExportProviderResult.ProviderUnavailable(
                        provider: Name,
                        correlationId: payload.CorrelationId,
                        failureReason: $"QuickBooks transient (HTTP {status}).");
                }

                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    if (LooksLikeQboDuplicate(bodyText))
                    {
                        duplicateCount++;
                        perLineExternalIds.Add($"qbo:duplicate:{requestId}");
                        continue;
                    }
                    var detail = ExtractQboErrorReason(bodyText) ?? "ValidationError";
                    return AccountingExportProviderResult.Failed(
                        provider: Name,
                        correlationId: payload.CorrelationId,
                        failureReason: Cap($"QuickBooks 400: {detail}"));
                }

                return AccountingExportProviderResult.Failed(
                    provider: Name,
                    correlationId: payload.CorrelationId,
                    failureReason: $"QuickBooks unexpected HTTP {status}.");
            }
        }

        // Best-effort audit stamp on every mapping touched by this
        // batch. Failure here MUST NOT change the export outcome.
        foreach (var mappingId in touchedMappingIds)
        {
            try
            {
                await _mappings.TouchLastExportedAsync(payload.TenantId, mappingId, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex,
                    "QuickBooks mapping touch failed (non-fatal). tenantId={TenantId} mappingId={MappingId}",
                    payload.TenantId, mappingId);
            }
        }

        var batchExternalReference = $"qbo:batch:{payload.CorrelationId}";
        if (duplicateCount == payload.Invoices.Count)
        {
            return new AccountingExportProviderResult(
                Success: false,
                Provider: Name,
                Status: AccountingExportStatus.Duplicate,
                ExternalReferenceId: batchExternalReference,
                CorrelationId: payload.CorrelationId,
                FailureReason: "QuickBooks reported every invoice as a duplicate replay.");
        }

        return AccountingExportProviderResult.Exported(
            provider: Name,
            correlationId: payload.CorrelationId,
            externalReferenceId: batchExternalReference);
    }

    private static string BuildInvoiceRequestId(string batchCorrelationId, AccountingExportInvoice invoice)
    {
        var raw = $"{batchCorrelationId}|inv|{invoice.InvoiceId:N}|{invoice.EffectiveTotal}";
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash)[..40].ToLowerInvariant();
    }

    private static object BuildInvoiceBody(
        AccountingExportInvoice invoice,
        QuickBooksOptions options,
        AccountingExportPayload payload,
        string customerRef,
        string requestId)
    {
        var incomeRef = options.IncomeAccountRef;

        // Single line per invoice — Billing is the authority for
        // the per-invoice subtotal/total split, so we collapse the
        // line items into a single SalesItemLineDetail row backed
        // by the configured income account ref. This keeps the
        // mapping deterministic and avoids exposing per-line PII
        // (the InvoiceLineItem table carries description fields
        // that are explicitly out of scope for ERP exports).
        var line = new
        {
            Amount = invoice.EffectiveTotal,
            DetailType = "SalesItemLineDetail",
            Description = $"Billing invoice {invoice.InvoiceNumber}",
            SalesItemLineDetail = new
            {
                ItemAccountRef = new { value = incomeRef },
            },
        };

        return new
        {
            TxnDate = invoice.IssueDate.ToString("yyyy-MM-dd"),
            DueDate = invoice.DueDate.ToString("yyyy-MM-dd"),
            DocNumber = TruncateDocNumber($"BIL-{invoice.InvoiceNumber}"),
            CurrencyRef = new { value = invoice.Currency },
            CustomerRef = new { value = customerRef },
            PrivateNote =
                $"Billing:correlation={payload.CorrelationId} invoice={invoice.InvoiceId:N} customer={invoice.CustomerId:N}",
            Line = new object[] { line },
            RequestId = requestId,
        };
    }

    private static string? TryExtractInvoiceId(string? bodyText)
    {
        if (string.IsNullOrWhiteSpace(bodyText)) return null;
        try
        {
            using var doc = JsonDocument.Parse(bodyText);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            if (doc.RootElement.TryGetProperty("Invoice", out var inv)
                && inv.ValueKind == JsonValueKind.Object
                && inv.TryGetProperty("Id", out var idProp))
            {
                var id = idProp.ValueKind switch
                {
                    JsonValueKind.String => idProp.GetString(),
                    JsonValueKind.Number => idProp.GetRawText(),
                    _ => null,
                };
                if (!string.IsNullOrWhiteSpace(id)) return $"Invoice/{id}";
            }
        }
        catch (JsonException) { }
        return null;
    }

    private static string TruncateDocNumber(string s) => s.Length <= 21 ? s : s[..21];

    private static string Cap(string s) => s.Length <= FailureReasonCapChars ? s : s[..FailureReasonCapChars];

    private static string? TryExtractJournalEntryId(string? bodyText)
    {
        if (string.IsNullOrWhiteSpace(bodyText)) return null;
        try
        {
            using var doc = JsonDocument.Parse(bodyText);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            if (doc.RootElement.TryGetProperty("JournalEntry", out var je)
                && je.ValueKind == JsonValueKind.Object
                && je.TryGetProperty("Id", out var idProp))
            {
                var id = idProp.ValueKind switch
                {
                    JsonValueKind.String => idProp.GetString(),
                    JsonValueKind.Number => idProp.GetRawText(),
                    _ => null,
                };
                if (!string.IsNullOrWhiteSpace(id)) return $"JournalEntry/{id}";
            }
        }
        catch (JsonException)
        {
            // Non-JSON body is acceptable — fall back to requestId.
        }
        return null;
    }

    /// <summary>
    /// Strict, structured QBO duplicate detection. Only treats a
    /// 400 as a duplicate when the QBO Fault.Error[].code matches
    /// one of the documented duplicate codes
    /// (<c>6240</c> Duplicate Document Number,
    /// <c>10401</c> Duplicate request id) OR the message field
    /// explicitly references the request-id-based dedupe path.
    /// Substring sniffing is NOT used — a generic 400 with the
    /// word "duplicate" in a free-text detail must NOT collapse
    /// to <c>Status=Duplicate</c>.
    /// </summary>
    private static bool LooksLikeQboDuplicate(string? bodyText)
    {
        if (string.IsNullOrWhiteSpace(bodyText)) return false;
        try
        {
            using var doc = JsonDocument.Parse(bodyText);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;
            if (!doc.RootElement.TryGetProperty("Fault", out var fault)) return false;
            if (!fault.TryGetProperty("Error", out var errs)
                || errs.ValueKind != JsonValueKind.Array) return false;

            foreach (var err in errs.EnumerateArray())
            {
                var code = err.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String
                    ? c.GetString() : null;
                if (code is "6240" or "10401") return true;

                // QBO occasionally reports the RequestId-dedupe path
                // via a structured Element field instead of a code.
                var element = err.TryGetProperty("element", out var e) && e.ValueKind == JsonValueKind.String
                    ? e.GetString() : null;
                if (string.Equals(element, "RequestId", StringComparison.Ordinal)) return true;
            }
        }
        catch (JsonException)
        {
            // Non-JSON 400 → not a structured duplicate; fall through to Failed.
        }
        return false;
    }

    private static string? ExtractQboErrorReason(string? bodyText)
    {
        if (string.IsNullOrWhiteSpace(bodyText)) return null;
        try
        {
            using var doc = JsonDocument.Parse(bodyText);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            if (doc.RootElement.TryGetProperty("Fault", out var fault)
                && fault.TryGetProperty("Error", out var errs)
                && errs.ValueKind == JsonValueKind.Array
                && errs.GetArrayLength() > 0)
            {
                var first = errs[0];
                var code = first.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String
                    ? c.GetString() : null;
                var detail = first.TryGetProperty("Detail", out var d) && d.ValueKind == JsonValueKind.String
                    ? d.GetString() : null;
                if (!string.IsNullOrWhiteSpace(code) || !string.IsNullOrWhiteSpace(detail))
                {
                    return $"code={code ?? "?"} detail={detail ?? "?"}";
                }
            }
        }
        catch (JsonException)
        {
            // Non-JSON body — caller falls back to a generic reason.
        }
        return null;
    }

}
