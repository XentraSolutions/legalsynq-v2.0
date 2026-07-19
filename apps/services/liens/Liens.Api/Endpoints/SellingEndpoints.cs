using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using BuildingBlocks.Exceptions;
using HtmlAgilityPack;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain;
using Liens.Domain.Entities;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Liens.Api.Endpoints;

public static class SellingEndpoints
{
    private const long SellingImportMaxBytes = 50L * 1024 * 1024;
    private const string SellingPatientDetailsTemplate = "SELLING_PATIENT_DETAILS_REPORT";
    private static readonly HashSet<string> AllowedSellingImportExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xls",
        ".xlsx",
    };

    public static void MapSellingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/liens/selling")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode)
            .RequireSellMode();

        group.MapPost("/imports/patient-details", ImportPatientDetailsReport)
            .RequirePermission(LiensPermissions.LienSaleCreate)
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(SellingImportMaxBytes));

        var portfolios = group.MapGroup("/portfolios");

        portfolios.MapGet("/", SearchPortfolios)
            .RequirePermission(LiensPermissions.LienSaleRead);

        portfolios.MapGet("/{id:guid}", GetPortfolioById)
            .RequirePermission(LiensPermissions.LienSaleRead);

        portfolios.MapPost("/", CreatePortfolio)
            .RequirePermission(LiensPermissions.LienSaleCreate);

        portfolios.MapPut("/{id:guid}", UpdatePortfolio)
            .RequirePermission(LiensPermissions.LienSaleUpdate);

        portfolios.MapPost("/{id:guid}/liens", AddLiens)
            .RequirePermission(LiensPermissions.LienSaleUpdate);

        portfolios.MapPost("/{id:guid}/liens/remove", RemoveLiens)
            .RequirePermission(LiensPermissions.LienSaleUpdate);

        portfolios.MapPost("/{id:guid}/buyers", AddBuyers)
            .RequirePermission(LiensPermissions.LienSaleUpdate);

        portfolios.MapPost("/{id:guid}/liens/{lienIdOrCode}/buyer-email", SendBuyerEmail)
            .RequirePermission(LiensPermissions.LienOffer);

        portfolios.MapPost("/{id:guid}/status", TransitionStatus)
            .RequirePermission(LiensPermissions.LienSaleUpdate);

        portfolios.MapPost("/{id:guid}/publish", Publish)
            .RequirePermission(LiensPermissions.LienSalePublish);

        portfolios.MapPost("/{id:guid}/withdraw", Withdraw)
            .RequirePermission(LiensPermissions.LienSaleWithdraw);

        portfolios.MapGet("/{id:guid}/status-history", GetStatusHistory)
            .RequirePermission(LiensPermissions.LienSaleRead);

        portfolios.MapGet("/{id:guid}/activity", GetActivity)
            .RequirePermission(LiensPermissions.LienSaleRead);

        portfolios.MapGet("/{id:guid}/analytics", GetAnalytics)
            .RequirePermission(LiensPermissions.LienSaleViewAnalytics);
    }

    private static Guid RequireTenantId(ICurrentRequestContext ctx)
    {
        return ctx.TenantId
            ?? throw new UnauthorizedAccessException("Tenant context is required.");
    }

    private static Guid RequireUserId(ICurrentRequestContext ctx)
    {
        return ctx.UserId
            ?? throw new UnauthorizedAccessException("User context is required.");
    }

    private static Guid RequireOrgId(ICurrentRequestContext ctx)
    {
        return ctx.OrgId
            ?? throw new UnauthorizedAccessException("Organization context is required.");
    }

    private static async Task<IResult> SearchPortfolios(
        ISellingPortfolioService service,
        ICurrentRequestContext ctx,
        string? search = null,
        string? status = null,
        Guid? buyerOrgId = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var sellerOrgId = RequireOrgId(ctx);
        var result = await service.SearchAsync(
            tenantId, sellerOrgId, search, status, buyerOrgId, page, pageSize, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetPortfolioById(
        Guid id,
        ISellingPortfolioService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var sellerOrgId = RequireOrgId(ctx);
        var result = await service.GetByIdAsync(tenantId, id, sellerOrgId, ct);
        return result is null
            ? Results.NotFound(new { error = new { code = "not_found", message = $"Selling portfolio '{id}' not found." } })
            : Results.Ok(result);
    }

    private static async Task<IResult> CreatePortfolio(
        CreateSellingPortfolioRequest request,
        ISellingPortfolioService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var sellerOrgId = RequireOrgId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.CreateAsync(tenantId, sellerOrgId, userId, request, ct);
        return Results.Created($"/api/liens/selling/portfolios/{result.Id}", result);
    }

    private static async Task<IResult> UpdatePortfolio(
        Guid id,
        UpdateSellingPortfolioRequest request,
        ISellingPortfolioService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var sellerOrgId = RequireOrgId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.UpdateAsync(tenantId, id, sellerOrgId, userId, request, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> AddLiens(
        Guid id,
        AddSellingPortfolioLiensRequest request,
        ISellingPortfolioService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var sellerOrgId = RequireOrgId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.AddLiensAsync(tenantId, id, sellerOrgId, userId, request, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> RemoveLiens(
        Guid id,
        RemoveSellingPortfolioLiensRequest request,
        ISellingPortfolioService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var sellerOrgId = RequireOrgId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.RemoveLiensAsync(tenantId, id, sellerOrgId, userId, request, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> AddBuyers(
        Guid id,
        AddSellingPortfolioBuyersRequest request,
        ISellingPortfolioService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var sellerOrgId = RequireOrgId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.AddBuyersAsync(tenantId, id, sellerOrgId, userId, request, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> SendBuyerEmail(
        Guid id,
        string lienIdOrCode,
        SendLienBuyerEmailRequest request,
        ISellingPortfolioService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var sellerOrgId = RequireOrgId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.SendBuyerEmailAsync(tenantId, id, lienIdOrCode, sellerOrgId, userId, request, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> TransitionStatus(
        Guid id,
        TransitionSellingPortfolioStatusRequest request,
        ISellingPortfolioService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var sellerOrgId = RequireOrgId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.TransitionStatusAsync(tenantId, id, sellerOrgId, userId, request, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> Publish(
        Guid id,
        TransitionSellingPortfolioStatusRequest? request,
        ISellingPortfolioService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var sellerOrgId = RequireOrgId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.PublishAsync(tenantId, id, sellerOrgId, userId, request?.Notes, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> Withdraw(
        Guid id,
        TransitionSellingPortfolioStatusRequest? request,
        ISellingPortfolioService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var sellerOrgId = RequireOrgId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.WithdrawAsync(tenantId, id, sellerOrgId, userId, request?.Notes, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetStatusHistory(
        Guid id,
        ISellingPortfolioService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var sellerOrgId = RequireOrgId(ctx);
        var result = await service.GetStatusHistoryAsync(tenantId, id, sellerOrgId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetActivity(
        Guid id,
        ISellingPortfolioService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var sellerOrgId = RequireOrgId(ctx);
        var result = await service.GetActivityAsync(tenantId, id, sellerOrgId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetAnalytics(
        Guid id,
        ISellingPortfolioService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var sellerOrgId = RequireOrgId(ctx);
        var result = await service.GetAnalyticsAsync(tenantId, id, sellerOrgId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> ImportPatientDetailsReport(
        HttpRequest request,
        LiensDbContext db,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);

        if (!request.HasFormContentType)
        {
            return Results.BadRequest(new
            {
                error = new
                {
                    code = "invalid_content_type",
                    message = "Content-Type must be multipart/form-data.",
                },
            });
        }

        var form = await request.ReadFormAsync(ct);
        var file = form.Files["file"];
        var validationError = ValidateSellingImportFile(file);
        if (validationError is not null)
            return validationError;

        await using var stream = file!.OpenReadStream();
        var parsed = ParsePatientDetailsWorkbook(stream, file.FileName);
        var label = string.IsNullOrWhiteSpace(form["label"])
            ? Path.GetFileNameWithoutExtension(file.FileName)
            : form["label"].ToString().Trim();

        var dataContext = JsonSerializer.Serialize(parsed.Rows);
        var batch = BatchUpload.Create(
            tenantId,
            userId,
            label,
            SellingPatientDetailsTemplate,
            file.FileName,
            parsed.Rows.Count,
            dataContext);

        var details = parsed.Rows.Select((row, index) =>
            BatchUploadDetail.Create(
                tenantId,
                batch.Id,
                index + 1,
                JsonSerializer.Serialize(row),
                userId)).ToList();

        db.BatchUploads.Add(batch);
        db.BatchUploadDetails.AddRange(details);
        await db.SaveChangesAsync(ct);

        return Results.Ok(new
        {
            id = batch.Id,
            label = batch.Label,
            template = batch.Template,
            fileName = batch.FileName,
            rowCount = parsed.Rows.Count,
            columnCount = parsed.Columns.Count,
            columns = parsed.Columns,
            previewRows = parsed.Rows.Take(5).ToList(),
        });
    }

    private static IResult? ValidateSellingImportFile(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            return Results.BadRequest(new
            {
                error = new
                {
                    code = "file_required",
                    message = "A non-empty Excel file is required.",
                },
            });
        }

        if (file.Length > SellingImportMaxBytes)
        {
            return Results.BadRequest(new
            {
                error = new
                {
                    code = "file_too_large",
                    message = $"The file exceeds the maximum allowed size of {SellingImportMaxBytes / (1024 * 1024)} MB.",
                },
            });
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedSellingImportExtensions.Contains(extension))
        {
            return Results.BadRequest(new
            {
                error = new
                {
                    code = "unsupported_file_type",
                    message = "Only .xls and .xlsx files are supported.",
                },
            });
        }

        return null;
    }

    private static SellingPatientDetailsImport ParsePatientDetailsWorkbook(Stream stream, string fileName)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        if (LooksLikeHtmlSpreadsheet(buffer))
            return ParsePatientDetailsHtml(buffer);

        buffer.Position = 0;
        IWorkbook workbook = Path.GetExtension(fileName).Equals(".xls", StringComparison.OrdinalIgnoreCase)
            ? new HSSFWorkbook(buffer)
            : new XSSFWorkbook(buffer);

        var sheet = workbook.NumberOfSheets > 0
            ? workbook.GetSheetAt(0)
            : null;

        if (sheet is null)
            throw new ValidationException("Unable to parse workbook.",
                new Dictionary<string, string[]> { ["file"] = ["The workbook does not contain any worksheets."] });

        var formatter = new DataFormatter(CultureInfo.InvariantCulture);
        var rows = new List<List<string>>();
        for (var rowIndex = sheet.FirstRowNum; rowIndex <= sheet.LastRowNum; rowIndex++)
        {
            var row = sheet.GetRow(rowIndex);
            if (row is null || row.LastCellNum <= 0)
            {
                rows.Add([]);
                continue;
            }

            rows.Add(Enumerable.Range(0, row.LastCellNum)
                .Select(index => formatter.FormatCellValue(row.GetCell(index)).Trim())
                .ToList());
        }

        return ParsePatientDetailsRows(rows);
    }

    private static SellingPatientDetailsImport ParsePatientDetailsHtml(Stream stream)
    {
        stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var html = reader.ReadToEnd();
        var document = new HtmlDocument();
        document.LoadHtml(html);

        var rows = document.DocumentNode
            .SelectNodes("//tr")
            ?.Select(rowNode => rowNode.SelectNodes("./th|./td")
                ?.Select(cell => HtmlEntity.DeEntitize(cell.InnerText).Trim())
                .ToList() ?? [])
            .ToList()
            ?? [];

        return ParsePatientDetailsRows(rows);
    }

    private static SellingPatientDetailsImport ParsePatientDetailsRows(IReadOnlyList<List<string>> rawRows)
    {
        var headerRowIndex = FindHeaderRowIndex(rawRows);
        if (headerRowIndex < 0)
        {
            throw new ValidationException("Unable to parse workbook.",
                new Dictionary<string, string[]> { ["file"] = ["The patient details header row could not be found."] });
        }

        var headerRow = rawRows[headerRowIndex];
        var headerIndexes = headerRow
            .Select((header, index) => new
            {
                Index = index,
                Header = header.Trim(),
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Header))
            .ToList();

        var requiredHeaders = new[] { "#", "Last Name", "First Name", "MR#", "DOB" };
        var missingHeaders = requiredHeaders
            .Where(required => !headerIndexes.Any(item => string.Equals(item.Header, required, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (missingHeaders.Count > 0)
        {
            throw new ValidationException("Unable to parse workbook.",
                new Dictionary<string, string[]> { ["file"] = [$"The workbook is missing required columns: {string.Join(", ", missingHeaders)}."] });
        }

        var rows = new List<Dictionary<string, string>>();
        for (var rowIndex = headerRowIndex + 1; rowIndex < rawRows.Count; rowIndex++)
        {
            var row = rawRows[rowIndex];
            var parsedRow = new Dictionary<string, string>(StringComparer.Ordinal);
            var hasData = false;

            foreach (var header in headerIndexes)
            {
                var value = header.Index < row.Count
                    ? row[header.Index].Trim()
                    : string.Empty;

                parsedRow[header.Header] = value;
                hasData |= !string.IsNullOrWhiteSpace(value);
            }

            if (!hasData)
                continue;

            rows.Add(parsedRow);
        }

        if (rows.Count == 0)
        {
            throw new ValidationException("Unable to parse workbook.",
                new Dictionary<string, string[]> { ["file"] = ["The workbook did not contain any patient detail rows."] });
        }

        return new SellingPatientDetailsImport(
            headerIndexes.Select(item => item.Header).ToList(),
            rows);
    }

    private static int FindHeaderRowIndex(IReadOnlyList<List<string>> rows)
    {
        for (var rowIndex = 0; rowIndex < Math.Min(rows.Count, 50); rowIndex++)
        {
            var cells = rows[rowIndex]
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (cells.Contains("#") &&
                cells.Contains("Last Name") &&
                cells.Contains("First Name") &&
                cells.Contains("MR#") &&
                cells.Contains("DOB"))
            {
                return rowIndex;
            }
        }

        return -1;
    }

    private static bool LooksLikeHtmlSpreadsheet(MemoryStream stream)
    {
        stream.Position = 0;
        var buffer = new byte[Math.Min(stream.Length, 1024)];
        _ = stream.Read(buffer, 0, buffer.Length);
        stream.Position = 0;

        var prefix = Encoding.UTF8.GetString(buffer)
            .TrimStart('\uFEFF', '\0', '\r', '\n', '\t', ' ');

        return prefix.StartsWith("<html", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("<!doctype html", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("<table", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record SellingPatientDetailsImport(
        List<string> Columns,
        List<Dictionary<string, string>> Rows);
}
