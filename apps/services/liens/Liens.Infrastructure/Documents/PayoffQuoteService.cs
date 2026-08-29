using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using BuildingBlocks.Authentication.ServiceTokens;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain.Enums;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Liens.Infrastructure.Documents;

public sealed class PayoffQuoteService : IPayoffQuoteService
{
    private const string LegacyPayoffTypeId = "14";
    private const string DocumentsServiceAudience = "documents-service";
    private const string PayoffNavy = "#0B2D52";
    private const string PayoffBody = "#555667";
    private const int DocumentScanPollAttempts = 20;
    private static readonly TimeSpan DocumentScanPollInterval = TimeSpan.FromMilliseconds(250);
    private static readonly Guid LegacyFallbackDocumentTypeId =
        Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly ICaseService _caseService;
    private readonly ILienService _lienService;
    private readonly IServicingItemService _servicingItemService;
    private readonly ILookupValueService _lookupValueService;
    private readonly ILegacyDocumentUploadClient _uploadClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceTokenIssuer _serviceTokenIssuer;
    private readonly ILogger<PayoffQuoteService> _logger;

    public PayoffQuoteService(
        ICaseService caseService,
        ILienService lienService,
        IServicingItemService servicingItemService,
        ILookupValueService lookupValueService,
        ILegacyDocumentUploadClient uploadClient,
        IHttpClientFactory httpClientFactory,
        IServiceTokenIssuer serviceTokenIssuer,
        ILogger<PayoffQuoteService> logger)
    {
        _caseService = caseService;
        _lienService = lienService;
        _servicingItemService = servicingItemService;
        _lookupValueService = lookupValueService;
        _uploadClient = uploadClient;
        _httpClientFactory = httpClientFactory;
        _serviceTokenIssuer = serviceTokenIssuer;
        _logger = logger;
    }

    public async Task<PayoffQuoteResult> GetOrGenerateAsync(
        Guid tenantId,
        Guid orgId,
        Guid actingUserId,
        Guid caseId,
        string assignedTo,
        CancellationToken ct = default)
    {
        var existingCase = await _caseService.GetByIdAsync(tenantId, caseId, ct);
        if (existingCase is null)
            return PayoffQuoteResult.CaseNotFound();

        var payoffStatementType = await _lookupValueService.GetByCodeAsync(
            tenantId,
            LookupCategory.DocumentCategory,
            "PayoffStatement",
            ct);
        var payoffStatementTypeId = payoffStatementType?.Id.ToString();

        var existing = await FindExistingPayoffDocumentAsync(tenantId, caseId, payoffStatementTypeId, ct);
        if (existing is not null)
        {
            var existingBase64 = string.Empty;
            if (existing.DocumentId.HasValue)
            {
                var isClean = await WaitForDocumentCleanAsync(
                    tenantId,
                    actingUserId,
                    existing.DocumentId.Value,
                    ct);
                if (!isClean)
                    return PayoffQuoteResult.Unavailable();

                existingBase64 = await TryReadDocumentBase64Async(
                    tenantId,
                    actingUserId,
                    existing.DocumentId.Value,
                    ct);
            }

            return PayoffQuoteResult.Success(existing.Url, existingBase64);
        }

        var liens = await GetOpenServicingLiensAsync(tenantId, caseId, ct);
        var payoffLines = await BuildPayoffLinesAsync(tenantId, liens, ct);
        var pdfBytes = GeneratePdf(existingCase, payoffLines);
        var base64 = Convert.ToBase64String(pdfBytes);
        var fileName = $"PayoffQuote_{caseId}.pdf";
        var documentTypeId = payoffStatementType?.Id ?? LegacyFallbackDocumentTypeId;

        await using var content = new MemoryStream(pdfBytes);
        var upload = await _uploadClient.UploadAsync(new LegacyDocumentUploadRequest
        {
            TenantId = tenantId,
            ActingUserId = actingUserId,
            ReferenceId = caseId,
            ReferenceType = "Case",
            DocumentTypeId = documentTypeId,
            Title = Path.GetFileNameWithoutExtension(fileName),
            Description = "Payoff Statement",
            Content = content,
            FileName = fileName,
            ContentType = "application/pdf",
            Length = pdfBytes.LongLength,
        }, ct);

        if (string.IsNullOrWhiteSpace(upload.Url))
            return PayoffQuoteResult.Unavailable();

        await _servicingItemService.CreateAsync(
            tenantId,
            orgId,
            actingUserId,
            new CreateServicingItemRequest
            {
                TaskNumber = $"DOC-{Guid.CreateVersion7():N}"[..36],
                TaskType = "LegacyCaseDocument",
                Description = "Case document uploaded: PayoffQuote",
                AssignedTo = assignedTo,
                AssignedToUserId = actingUserId,
                CaseId = caseId,
                Notes = BuildLegacyDocumentNotes(
                    upload,
                    fileName,
                    Path.GetFileNameWithoutExtension(fileName),
                    documentTypeId,
                    caseId),
            },
            ct);

        if (upload.DocumentId.HasValue)
        {
            var isClean = await WaitForDocumentCleanAsync(
                tenantId,
                actingUserId,
                upload.DocumentId.Value,
                ct);
            if (!isClean)
                return PayoffQuoteResult.Unavailable();
        }

        return PayoffQuoteResult.Success(upload.Url, base64);
    }

    private async Task<ExistingPayoffDocument?> FindExistingPayoffDocumentAsync(
        Guid tenantId,
        Guid caseId,
        string? payoffStatementTypeId,
        CancellationToken ct)
    {
        const int pageSize = 200;
        var page = 1;

        while (true)
        {
            var result = await _servicingItemService.SearchAsync(
                tenantId,
                search: null,
                status: null,
                priority: null,
                assignedTo: null,
                caseId: caseId,
                lienId: null,
                page: page,
                pageSize: pageSize,
                ct);

            var document = result.Items
                .Where(i => string.Equals(i.TaskType, "LegacyCaseDocument", StringComparison.Ordinal))
                .OrderByDescending(i => i.CreatedAtUtc)
                .Select(i => ParseLegacyNoteFields(i.Notes))
                .Where(fields => IsLegacyPayoffQuoteDocument(fields, payoffStatementTypeId))
                .Select(fields => new ExistingPayoffDocument(
                    GetLegacyDocumentUrl(fields),
                    ResolveDocumentId(fields)))
                .FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate.Url));
            if (document is not null)
                return document;

            if (result.Items.Count == 0 || page * pageSize >= result.TotalCount)
                return null;

            page++;
        }
    }

    private async Task<List<LienResponse>> GetOpenServicingLiensAsync(
        Guid tenantId,
        Guid caseId,
        CancellationToken ct)
    {
        const int pageSize = 200;
        var page = 1;
        var liens = new List<LienResponse>();

        while (true)
        {
            var result = await _lienService.SearchAsync(
                tenantId,
                search: null,
                status: null,
                lienType: null,
                caseId: caseId,
                facilityId: null,
                page: page,
                pageSize: pageSize,
                ct);

            liens.AddRange(result.Items.Where(IsOpenServicingLien));
            if (result.Items.Count == 0 || page * pageSize >= result.TotalCount)
                return liens;

            page++;
        }
    }

    private static bool IsOpenServicingLien(LienResponse lien) =>
        LienStatus.Open.Any(status => string.Equals(status, lien.Status, StringComparison.OrdinalIgnoreCase)) &&
        IsLegacyTrue(lien.IsServicing);

    private async Task<List<PayoffQuoteLine>> BuildPayoffLinesAsync(
        Guid tenantId,
        IReadOnlyList<LienResponse> liens,
        CancellationToken ct)
    {
        var lines = new List<PayoffQuoteLine>(liens.Count);

        foreach (var lien in liens)
        {
            var facilityFields = await GetLatestLegacyFieldsAsync(
                tenantId,
                "LegacyMedicalFacilityInfo",
                lien.CaseId,
                lien.Id,
                ct);
            var codeFields = await GetLegacyMedicalCodeFieldsAsync(tenantId, lien.CaseId, lien.Id, ct);

            var legacyBillingAmount = codeFields.Count == 0
                ? (decimal?)null
                : codeFields.Sum(fields => ParseMoney(fields.GetValueOrDefault("billingAmount", string.Empty)));

            lines.Add(new PayoffQuoteLine(
                MedicalFacility: FirstNonEmpty(
                    facilityFields.GetValueOrDefault("facilityName", string.Empty),
                    lien.MedicalFacility,
                    lien.FacilityId?.ToString()) ?? string.Empty,
                DateOfService: FormatDate(lien.InitialServiceDate),
                Amount: legacyBillingAmount ?? GetPayoffLineAmount(lien)));
        }

        return lines;
    }

    private async Task<Dictionary<string, string>> GetLatestLegacyFieldsAsync(
        Guid tenantId,
        string taskType,
        Guid? caseId,
        Guid lienId,
        CancellationToken ct)
    {
        var result = await _servicingItemService.SearchAsync(
            tenantId,
            search: taskType,
            status: null,
            priority: null,
            assignedTo: null,
            caseId: null,
            lienId: lienId,
            page: 1,
            pageSize: 100,
            ct);

        var item = result.Items.FirstOrDefault(i =>
            string.Equals(i.TaskType, taskType, StringComparison.Ordinal) &&
            i.LienId == lienId);
        if (item is not null)
            return ParseLegacyNoteFields(item.Notes);

        if (!caseId.HasValue)
            return [];

        result = await _servicingItemService.SearchAsync(
            tenantId,
            search: taskType,
            status: null,
            priority: null,
            assignedTo: null,
            caseId: caseId,
            lienId: null,
            page: 1,
            pageSize: 100,
            ct);

        item = result.Items.FirstOrDefault(i =>
            string.Equals(i.TaskType, taskType, StringComparison.Ordinal) &&
            i.LienId == lienId);

        return item is null ? [] : ParseLegacyNoteFields(item.Notes);
    }

    private async Task<List<Dictionary<string, string>>> GetLegacyMedicalCodeFieldsAsync(
        Guid tenantId,
        Guid? caseId,
        Guid lienId,
        CancellationToken ct)
    {
        var result = await _servicingItemService.SearchAsync(
            tenantId,
            search: "LegacyMedicalCode",
            status: null,
            priority: null,
            assignedTo: null,
            caseId: null,
            lienId: lienId,
            page: 1,
            pageSize: 100,
            ct);

        var items = result.Items
            .Where(i => string.Equals(i.TaskType, "LegacyMedicalCode", StringComparison.Ordinal) &&
                        i.LienId == lienId)
            .ToList();

        if (items.Count == 0 && caseId.HasValue)
        {
            result = await _servicingItemService.SearchAsync(
                tenantId,
                search: "LegacyMedicalCode",
                status: null,
                priority: null,
                assignedTo: null,
                caseId: caseId,
                lienId: null,
                page: 1,
                pageSize: 100,
                ct);

            items = result.Items
                .Where(i => string.Equals(i.TaskType, "LegacyMedicalCode", StringComparison.Ordinal) &&
                            i.LienId == lienId)
                .ToList();
        }

        return items.Select(item => ParseLegacyNoteFields(item.Notes)).ToList();
    }

    private static byte[] GeneratePdf(CaseResponse caseInfo, IReadOnlyList<PayoffQuoteLine> lines)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(16);
                page.MarginVertical(25);
                page.DefaultTextStyle(x => x
                    .FontFamily("Times New Roman", "Georgia", "Liberation Serif", "DejaVu Serif")
                    .FontSize(14)
                    .FontColor(PayoffBody)
                    .LineHeight(1.15f));

                page.Content().Column(col =>
                {
                    col.Spacing(18);

                    col.Item().Text($"RE: {BuildClientReference(caseInfo)} / DOL {FormatDate(caseInfo.DateOfIncident)}")
                        .Bold()
                        .FontSize(14)
                        .FontColor(PayoffNavy);

                    col.Item().Column(intro =>
                    {
                        intro.Spacing(8);
                        intro.Item().Text("PAYOUT INFORMATION")
                            .Bold()
                            .FontSize(18)
                            .FontColor(PayoffNavy);
                        intro.Item().Text("Thank you for your inquiry regarding the payoff details for the referenced individual. We are pleased to provide the current payoff amount below for your convenience:");
                    });

                    col.Item().PaddingTop(14).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2.15f);
                            columns.RelativeColumn(1.05f);
                            columns.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            PayoffHeaderCell(header, "Medical Facility");
                            PayoffHeaderCell(header, "Date of Service");
                            PayoffHeaderCell(header, "Total Amount");
                        });

                        if (lines.Count == 0)
                        {
                            PayoffBodyCell(table, "No open servicing liens found");
                            PayoffBodyCell(table, FormatDate(caseInfo.DateOfIncident));
                            PayoffBodyCell(table, FormatMoney(0m));
                        }
                        else
                        {
                            foreach (var line in lines)
                            {
                                PayoffBodyCell(table, line.MedicalFacility);
                                PayoffBodyCell(table, line.DateOfService);
                                PayoffBodyCell(table, FormatMoney(line.Amount));
                            }
                        }
                    });

                    col.Item().PaddingTop(4).Element(container => PayoffTotalBox(
                        container,
                        FormatMoney(lines.Sum(line => line.Amount))));

                    col.Item().PaddingTop(10).Text(text =>
                    {
                        text.Span("To ensure timely and accurate processing of your payments, please direct all invoices payable to Guardian Liens LLC to our lockbox address or submit electronic payments as outlined below. ");
                        text.Span("Please send all remittances to team@guardianliens.com").Bold();
                    });

                    col.Item().PaddingLeft(15).PaddingRight(35).Row(row =>
                    {
                        row.RelativeItem().Column(address =>
                        {
                            address.Spacing(10);
                            address.Item().Text("Physical payment address:").Bold().FontSize(14).FontColor(PayoffNavy);
                            address.Item().Text("Guardian Liens LLC\nP.O. BOX 150111\nOgden, UT 84415");
                        });

                        row.RelativeItem().Column(payment =>
                        {
                            payment.Spacing(10);
                            payment.Item().Text("Electronic payments information:").Bold().FontSize(14).FontColor(PayoffNavy);
                            payment.Item().Text("Guardian Liens LLC\nAccount #: 380003977\nRouting #: 124384657");
                        });
                    });

                    col.Item().Text("If you have any questions, please feel free to contact us at team@guardianliens.com.");

                    col.Item().Column(signature =>
                    {
                        signature.Spacing(10);
                        signature.Item().Text("Sincerely,");
                        signature.Item().Text("Guardian Liens Team").Bold().FontSize(24).FontColor("#174A82");
                    });
                });
            });
        });

        return document.GeneratePdf();
    }

    private static decimal GetPayoffLineAmount(LienResponse lien) =>
        lien.TotalBilling ?? lien.PayoffAmount ?? lien.CurrentBalance ?? lien.OriginalAmount;

    private static void PayoffHeaderCell(TableCellDescriptor table, string text)
    {
        table.Cell().PaddingBottom(22).Text(text).Bold().FontSize(14).FontColor(PayoffNavy);
    }

    private static void PayoffBodyCell(TableDescriptor table, string text)
    {
        table.Cell().BorderBottom(0.75f).BorderColor(Colors.Grey.Lighten1).PaddingBottom(14).Text(text);
    }

    private static void PayoffTotalBox(IContainer container, string total)
    {
        container
            .Border(0.75f)
            .BorderColor(PayoffNavy)
            .PaddingHorizontal(10)
            .PaddingVertical(14)
            .Row(row =>
            {
                row.RelativeItem().Text("TOTAL OUTSTANDING BALANCE:").FontSize(15).FontColor(PayoffNavy);
                row.AutoItem().Text(total).Bold().FontSize(14).FontColor(PayoffNavy);
            });
    }

    private static string BuildLegacyDocumentNotes(
        LegacyDocumentUploadResult result,
        string fileName,
        string title,
        Guid documentTypeId,
        Guid caseId)
    {
        return string.Join("; ", new Dictionary<string, string?>
            {
                ["documentId"] = result.DocumentId?.ToString(),
                ["documentUrl"] = result.Url,
                ["url"] = result.Url,
                ["filename"] = title,
                ["originalFileName"] = fileName,
                ["typeId"] = LegacyPayoffTypeId,
                ["documentTypeId"] = documentTypeId.ToString(),
                ["referenceType"] = "Case",
                ["referenceId"] = caseId.ToString(),
                ["description"] = "Payoff Statement",
            }
            .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value))
            .Select(kvp => $"{kvp.Key}={SanitizeLegacyNoteValue(kvp.Value!)}"));
    }

    private static Dictionary<string, string> ParseLegacyNoteFields(string? notes)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(notes))
            return result;

        const string marker = "[legacy-meta]";
        var rawMetadata = notes;
        var markerIndex = notes.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex >= 0)
            rawMetadata = notes[(markerIndex + marker.Length)..].Trim();
        else if (!notes.Contains('=', StringComparison.Ordinal))
            return result;

        foreach (var segment in rawMetadata.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = segment.IndexOf('=');
            if (eq > 0)
            {
                var key = segment[..eq].Trim();
                var value = segment[(eq + 1)..].Trim();
                if (!string.IsNullOrWhiteSpace(key))
                    result[key] = value;
            }
        }

        return result;
    }

    private static bool IsLegacyPayoffQuoteDocument(
        Dictionary<string, string> fields,
        string? payoffStatementTypeId)
    {
        var typeIds = new[]
        {
            fields.GetValueOrDefault("typeId", string.Empty),
            fields.GetValueOrDefault("docTypeId", string.Empty),
            fields.GetValueOrDefault("documentTypeId", string.Empty),
        };

        if (typeIds.Any(typeId => string.Equals(typeId, LegacyPayoffTypeId, StringComparison.OrdinalIgnoreCase)))
            return true;

        if (!string.IsNullOrWhiteSpace(payoffStatementTypeId) &&
            typeIds.Any(typeId => string.Equals(typeId, payoffStatementTypeId, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return LegacyValueIndicatesPayoffStatement(fields.GetValueOrDefault("category", string.Empty)) ||
               LegacyValueIndicatesPayoffStatement(fields.GetValueOrDefault("code", string.Empty)) ||
               LegacyValueIndicatesPayoffStatement(fields.GetValueOrDefault("name", string.Empty)) ||
               LegacyValueIndicatesPayoffStatement(fields.GetValueOrDefault("description", string.Empty)) ||
               LegacyValueIndicatesPayoffStatement(fields.GetValueOrDefault("filename", string.Empty)) ||
               LegacyValueIndicatesPayoffStatement(fields.GetValueOrDefault("originalFileName", string.Empty));
    }

    private static string GetLegacyDocumentUrl(Dictionary<string, string> fields)
    {
        var url = fields.GetValueOrDefault("url", string.Empty);
        if (string.IsNullOrWhiteSpace(url))
            url = fields.GetValueOrDefault("documentUrl", string.Empty);
        return url;
    }

    private async Task<string> TryReadDocumentBase64Async(
        Guid tenantId,
        Guid actingUserId,
        Guid documentId,
        CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DocumentsService");
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"/documents/{documentId}/content?type=download");
            ApplyDocumentsAuthorization(request, tenantId, actingUserId);

            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                return string.Empty;

            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            return Convert.ToBase64String(bytes);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to read payoff quote document content for {DocumentId}", documentId);
            return string.Empty;
        }
    }

    internal async Task<bool> WaitForDocumentCleanAsync(
        Guid tenantId,
        Guid actingUserId,
        Guid documentId,
        CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("DocumentsService");

        for (var attempt = 1; attempt <= DocumentScanPollAttempts; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, $"/documents/{documentId}");
                ApplyDocumentsAuthorization(request, tenantId, actingUserId);

                using var response = await client.SendAsync(request, ct);
                if (response.IsSuccessStatusCode)
                {
                    await using var stream = await response.Content.ReadAsStreamAsync(ct);
                    using var payload = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                    if (!payload.RootElement.TryGetProperty("data", out var data) ||
                        !data.TryGetProperty("scanStatus", out var scanStatusElement))
                    {
                        _logger.LogWarning(
                            "Documents service omitted scan status for payoff document {DocumentId}",
                            documentId);
                        return false;
                    }

                    var scanStatus = scanStatusElement.GetString();
                    if (string.Equals(scanStatus, "CLEAN", StringComparison.OrdinalIgnoreCase))
                        return true;

                    if (!string.Equals(scanStatus, "PENDING", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning(
                            "Payoff document {DocumentId} cannot be accessed because its scan status is {ScanStatus}",
                            documentId,
                            scanStatus);
                        return false;
                    }
                }
                else
                {
                    _logger.LogWarning(
                        "Unable to read scan status for payoff document {DocumentId}: Documents service returned {StatusCode}",
                        documentId,
                        response.StatusCode);

                    if ((int)response.StatusCode < 500 && response.StatusCode != HttpStatusCode.TooManyRequests)
                        return false;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex,
                    "Documents service returned invalid scan metadata for payoff document {DocumentId}",
                    documentId);
                return false;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex,
                    "Unable to read scan status for payoff document {DocumentId}",
                    documentId);
            }

            if (attempt < DocumentScanPollAttempts)
                await Task.Delay(DocumentScanPollInterval, ct);
        }

        _logger.LogWarning(
            "Timed out waiting for payoff document {DocumentId} to complete its security scan",
            documentId);
        return false;
    }

    private void ApplyDocumentsAuthorization(HttpRequestMessage request, Guid tenantId, Guid actorUserId)
    {
        if (!_serviceTokenIssuer.IsConfigured)
            return;

        try
        {
            var token = _serviceTokenIssuer.IssueToken(
                tenantId.ToString(),
                actorUserId.ToString(),
                DocumentsServiceAudience);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to mint Documents service token for tenant {TenantId}", tenantId);
        }
    }

    private static Guid? ResolveDocumentId(Dictionary<string, string> fields)
    {
        if (Guid.TryParse(fields.GetValueOrDefault("documentId", string.Empty), out var documentId))
            return documentId;

        var url = GetLegacyDocumentUrl(fields);
        var lastSegment = url
            .Split('?', StringSplitOptions.RemoveEmptyEntries)[0]
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault();
        return Guid.TryParse(lastSegment, out documentId) ? documentId : null;
    }

    private static bool LegacyValueIndicatesPayoffStatement(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

        return string.Equals(normalized, "PayoffStatement", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "PayoffQuote", StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeLegacyNoteValue(string value) =>
        value.Replace(";", ",", StringComparison.Ordinal).Trim();

    private static string FormatDate(DateOnly? date) =>
        date.HasValue ? date.Value.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture) : string.Empty;

    private static string FormatMoney(decimal amount) =>
        amount.ToString("C2", CultureInfo.GetCultureInfo("en-US"));

    private static decimal ParseMoney(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0m;

        var normalized = value.Trim().Replace("$", string.Empty, StringComparison.Ordinal);
        return decimal.TryParse(
            normalized,
            NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out var amount)
            ? amount
            : 0m;
    }

    private static bool IsLegacyTrue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.Trim().ToUpperInvariant() switch
        {
            "YES" or "Y" or "TRUE" or "1" => true,
            _ => false,
        };
    }

    private static string BuildClientReference(CaseResponse caseInfo)
    {
        var displayName = FirstNonEmpty(
            caseInfo.ClientDisplayName,
            $"{caseInfo.ClientFirstName} {caseInfo.ClientLastName}") ?? "UNKNOWN";

        return displayName.ToUpperInvariant();
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return null;
    }

    private sealed record ExistingPayoffDocument(string Url, Guid? DocumentId);

    private sealed record PayoffQuoteLine(string MedicalFacility, string DateOfService, decimal Amount);
}
