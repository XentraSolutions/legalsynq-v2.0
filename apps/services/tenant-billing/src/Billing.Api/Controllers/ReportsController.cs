using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Billing.Api.Contracts;
using Billing.Domain.Csv;
using Billing.Api.Tenancy;
using Billing.Domain.Reporting;

namespace Billing.Api.Controllers;

/// <summary>
/// MS-BILL-WRITE-007 — read-only accounting / reconciliation
/// reporting surface. All four endpoints are tenant-admin-only at
/// the BFF (`requireAdminSession`); Billing.Api itself trusts the
/// `X-Tenant-Id` header injected by the BFF and applies it via
/// <see cref="ITenantContext"/>. Each endpoint accepts
/// <c>?format=csv</c> for export and JSON otherwise. Page/pageSize
/// are clamped here (default 100, hard cap 1000) so the service
/// layer can trust the inputs.
///
/// Read-only by contract — no controller method on this class
/// mutates state. CSV export uses the centralised
/// <see cref="CsvWriter"/> with stable column order and
/// formula-injection neutralisation. Cross-tenant probes leak
/// nothing: an unknown <c>customerId</c> simply yields zero rows.
/// </summary>
[ApiController]
[Route("api/reports")]
public sealed class ReportsController : ControllerBase
{
    private const int DefaultPageSize = 100;
    private const int MaxPageSize = 1000;

    private readonly IBillingReportingService _reports;
    private readonly ITenantContext _tenant;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(
        IBillingReportingService reports,
        ITenantContext tenant,
        ILogger<ReportsController> logger)
    {
        _reports = reports;
        _tenant = tenant;
        _logger = logger;
    }

    private static (int Page, int Size) ClampPage(int? page, int? pageSize)
    {
        var p = page is int pp && pp > 0 ? pp : 1;
        var s = pageSize is int ss && ss > 0 ? ss : DefaultPageSize;
        if (s > MaxPageSize) s = MaxPageSize;
        return (p, s);
    }

    private static bool WantsCsv(string? format)
        => string.Equals(format?.Trim(), "csv", StringComparison.OrdinalIgnoreCase);

    private FileContentResult CsvFile(string body, string filename)
    {
        // Prepend UTF-8 BOM so Excel autodetects encoding correctly
        // when the CSV contains non-ASCII characters (customer names,
        // accent marks). Browsers and downstream parsers ignore the
        // BOM.
        var bytes = new System.IO.MemoryStream();
        bytes.Write(new byte[] { 0xEF, 0xBB, 0xBF }, 0, 3);
        var payload = System.Text.Encoding.UTF8.GetBytes(body);
        bytes.Write(payload, 0, payload.Length);
        return File(bytes.ToArray(), "text/csv; charset=utf-8", filename);
    }

    // ---- /reports/accounting-summary ----------------------------------

    [HttpGet("accounting-summary")]
    public async Task<IActionResult> AccountingSummary(
        [FromQuery] Guid? customerId,
        [FromQuery] string? status,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? format,
        CancellationToken ct)
    {
        var (p, s) = ClampPage(page, pageSize);
        var rows = await _reports.GetAccountingSummaryAsync(
            _tenant.TenantId, NormaliseId(customerId), status, from, to, p, s, ct);
        _logger.LogInformation(
            "billing_report.accounting_summary tenant={TenantId} rows={Count} page={Page} pageSize={PageSize}",
            _tenant.TenantId, rows.Count, p, s);

        if (WantsCsv(format))
        {
            var header = new[]
            {
                "InvoiceId","InvoiceNumber","CustomerId","CustomerName","Status","Currency",
                "InvoiceTotal","PaidSum","AdjustmentCreditSum","AdjustmentDebitSum",
                "EffectiveTotal","EffectiveOutstanding","IssueDate","DueDate",
            };
            var data = rows.Select(r => (IReadOnlyList<string?>)new string?[]
            {
                r.InvoiceId.ToString(), r.InvoiceNumber, r.CustomerId.ToString(), r.CustomerName,
                r.Status, r.Currency,
                CsvWriter.FormatDecimal(r.InvoiceTotal), CsvWriter.FormatDecimal(r.PaidSum),
                CsvWriter.FormatDecimal(r.AdjustmentCreditSum), CsvWriter.FormatDecimal(r.AdjustmentDebitSum),
                CsvWriter.FormatDecimal(r.EffectiveTotal), CsvWriter.FormatDecimal(r.EffectiveOutstanding),
                CsvWriter.FormatDate(r.IssueDate), CsvWriter.FormatDate(r.DueDate),
            });
            return CsvFile(CsvWriter.Write(header, data), "accounting-summary.csv");
        }

        return Ok(new
        {
            isSuccess = true,
            data = rows.Select(AccountingSummaryReportRowDto.From).ToList(),
            page = p,
            pageSize = s,
            count = rows.Count,
        });
    }

    // ---- /reports/invoice-aging ---------------------------------------

    [HttpGet("invoice-aging")]
    public async Task<IActionResult> InvoiceAging(
        [FromQuery] Guid? customerId,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? format,
        CancellationToken ct)
    {
        var (p, s) = ClampPage(page, pageSize);
        var rows = await _reports.GetInvoiceAgingAsync(
            _tenant.TenantId, NormaliseId(customerId), p, s, ct);
        _logger.LogInformation(
            "billing_report.invoice_aging tenant={TenantId} rows={Count} page={Page} pageSize={PageSize}",
            _tenant.TenantId, rows.Count, p, s);

        if (WantsCsv(format))
        {
            var header = new[]
            {
                "InvoiceId","InvoiceNumber","CustomerId","CustomerName","Status","Currency",
                "InvoiceTotal","PaidSum","EffectiveTotal","EffectiveOutstanding",
                "DueDate","DaysOverdue","AgingBucket",
            };
            var data = rows.Select(r => (IReadOnlyList<string?>)new string?[]
            {
                r.InvoiceId.ToString(), r.InvoiceNumber, r.CustomerId.ToString(), r.CustomerName,
                r.Status, r.Currency,
                CsvWriter.FormatDecimal(r.InvoiceTotal), CsvWriter.FormatDecimal(r.PaidSum),
                CsvWriter.FormatDecimal(r.EffectiveTotal), CsvWriter.FormatDecimal(r.EffectiveOutstanding),
                CsvWriter.FormatDate(r.DueDate),
                r.DaysOverdue.ToString(System.Globalization.CultureInfo.InvariantCulture),
                r.AgingBucket,
            });
            return CsvFile(CsvWriter.Write(header, data), "invoice-aging.csv");
        }

        return Ok(new
        {
            isSuccess = true,
            data = rows.Select(InvoiceAgingReportRowDto.From).ToList(),
            page = p,
            pageSize = s,
            count = rows.Count,
        });
    }

    // ---- /reports/adjustments -----------------------------------------

    [HttpGet("adjustments")]
    public async Task<IActionResult> Adjustments(
        [FromQuery] Guid? customerId,
        [FromQuery] string? type,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? format,
        CancellationToken ct)
    {
        var (p, s) = ClampPage(page, pageSize);
        var rows = await _reports.GetAdjustmentsAsync(
            _tenant.TenantId, NormaliseId(customerId), type, from, to, p, s, ct);
        _logger.LogInformation(
            "billing_report.adjustments tenant={TenantId} rows={Count} page={Page} pageSize={PageSize}",
            _tenant.TenantId, rows.Count, p, s);

        if (WantsCsv(format))
        {
            var header = new[]
            {
                "AdjustmentId","InvoiceId","InvoiceNumber","CustomerId","CustomerName",
                "Type","Amount","Currency","Reason","ReferenceNumber","CreatedAt",
            };
            var data = rows.Select(r => (IReadOnlyList<string?>)new string?[]
            {
                r.AdjustmentId.ToString(), r.InvoiceId.ToString(), r.InvoiceNumber,
                r.CustomerId.ToString(), r.CustomerName,
                r.Type, CsvWriter.FormatDecimal(r.Amount), r.Currency,
                r.Reason, r.ReferenceNumber,
                CsvWriter.FormatDateTime(r.CreatedAt),
            });
            return CsvFile(CsvWriter.Write(header, data), "adjustments.csv");
        }

        return Ok(new
        {
            isSuccess = true,
            data = rows.Select(AdjustmentReportRowDto.From).ToList(),
            page = p,
            pageSize = s,
            count = rows.Count,
        });
    }

    // ---- /reports/payments --------------------------------------------

    [HttpGet("payments")]
    public async Task<IActionResult> Payments(
        [FromQuery] Guid? customerId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? format,
        CancellationToken ct)
    {
        var (p, s) = ClampPage(page, pageSize);
        var rows = await _reports.GetPaymentsAsync(
            _tenant.TenantId, NormaliseId(customerId), from, to, p, s, ct);
        _logger.LogInformation(
            "billing_report.payments tenant={TenantId} rows={Count} page={Page} pageSize={PageSize}",
            _tenant.TenantId, rows.Count, p, s);

        if (WantsCsv(format))
        {
            var header = new[]
            {
                "PaymentId","InvoiceId","InvoiceNumber","CustomerId","CustomerName",
                "Amount","Currency","Method","Status","TransactionReference","PaidAt",
                "Reversed","ReversedAt",
            };
            var data = rows.Select(r => (IReadOnlyList<string?>)new string?[]
            {
                r.PaymentId.ToString(), r.InvoiceId.ToString(), r.InvoiceNumber,
                r.CustomerId.ToString(), r.CustomerName,
                CsvWriter.FormatDecimal(r.Amount), r.Currency, r.Method, r.Status,
                r.TransactionReference,
                CsvWriter.FormatDateTime(r.PaidAt),
                r.Reversed ? "true" : "false",
                r.ReversedAt is DateTime rv ? CsvWriter.FormatDateTime(rv) : null,
            });
            return CsvFile(CsvWriter.Write(header, data), "payments.csv");
        }

        return Ok(new
        {
            isSuccess = true,
            data = rows.Select(PaymentReportRowDto.From).ToList(),
            page = p,
            pageSize = s,
            count = rows.Count,
        });
    }

    /// <summary>
    /// Treat <c>Guid.Empty</c> as "not supplied" so a browser that
    /// sends `?customerId=00000000-0000-0000-0000-000000000000`
    /// does not silently match an empty-id record.
    /// </summary>
    private static Guid? NormaliseId(Guid? id)
        => id is Guid g && g != Guid.Empty ? g : null;
}
