using System.Globalization;
using System.Net.Http.Headers;
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

        var existing = await FindExistingPayoffDocumentAsync(tenantId, actingUserId, caseId, payoffStatementTypeId, ct);
        if (existing is not null)
            return PayoffQuoteResult.Success(existing.Url, existing.Base64);

        var liens = await GetOpenServicingLiensAsync(tenantId, caseId, ct);
        var pdfBytes = GeneratePdf(existingCase, liens);
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

        return PayoffQuoteResult.Success(upload.Url, base64);
    }

    private async Task<ExistingPayoffDocument?> FindExistingPayoffDocumentAsync(
        Guid tenantId,
        Guid actingUserId,
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
            {
                var base64 = document.DocumentId.HasValue
                    ? await TryReadDocumentBase64Async(tenantId, actingUserId, document.DocumentId.Value, ct)
                    : string.Empty;
                return document with { Base64 = base64 };
            }

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
        string.Equals(lien.IsServicing, "Yes", StringComparison.OrdinalIgnoreCase);

    private static byte[] GeneratePdf(CaseResponse caseInfo, IReadOnlyList<LienResponse> liens)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text("Payoff Report").Bold().FontSize(18).FontColor(Colors.Grey.Darken3);
                    col.Item().PaddingTop(3).Text($"{caseInfo.ClientFirstName} {caseInfo.ClientLastName}".Trim()).FontSize(12);
                    col.Item().Text($"Date of Loss: {FormatDate(caseInfo.DateOfIncident)}").FontSize(9).FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingVertical(14).Column(col =>
                {
                    col.Spacing(10);
                    col.Item().Text("Thank you for your inquiry regarding the payoff details for the referenced account. The current payoff amount is below for your convenience.");

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            HeaderCell(header, "Facility");
                            HeaderCell(header, "Service Date");
                            HeaderCell(header, "Amount");
                        });

                        foreach (var lien in liens)
                        {
                            BodyCell(table, FirstNonEmpty(lien.MedicalFacility, lien.FacilityId?.ToString()) ?? string.Empty);
                            BodyCell(table, FormatDate(lien.InitialServiceDate));
                            BodyCell(table, FormatMoney(lien.TotalBilling ?? lien.PayoffAmount ?? lien.CurrentBalance ?? lien.OriginalAmount));
                        }
                    });

                    col.Item().AlignRight().Text($"Total Billing Amount: {FormatMoney(liens.Sum(GetPayoffLineAmount))}").Bold();
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Generated ").FontSize(8).FontColor(Colors.Grey.Medium);
                    text.Span(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC", CultureInfo.InvariantCulture)).FontSize(8).FontColor(Colors.Grey.Medium);
                    text.Span(" - LegalSynq").FontSize(8).FontColor(Colors.Grey.Medium);
                });
            });
        });

        return document.GeneratePdf();
    }

    private static decimal GetPayoffLineAmount(LienResponse lien) =>
        lien.TotalBilling ?? lien.PayoffAmount ?? lien.CurrentBalance ?? lien.OriginalAmount;

    private static void HeaderCell(TableCellDescriptor table, string text)
    {
        table.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text(text).Bold();
    }

    private static void BodyCell(TableDescriptor table, string text)
    {
        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(text);
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

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return null;
    }

    private sealed record ExistingPayoffDocument(string Url, Guid? DocumentId)
    {
        public string Base64 { get; init; } = string.Empty;
    }
}
