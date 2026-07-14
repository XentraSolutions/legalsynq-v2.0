using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Liens.Domain;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liens.Api.Endpoints;

public static class BatchUploadEndpoints
{
    private sealed class BatchUploadRequest
    {
        public string? id { get; init; }
        public string? label { get; init; }
        public string? template { get; init; }
        public string? caseId { get; init; }
        public string? file { get; init; }
        public string? date { get; init; }
        public int rows { get; init; }
        public string? dataContext { get; init; }
    }

    private sealed class BatchUploadProcessRequest
    {
        public string? batchUploadId { get; init; }
        public string? templateId { get; init; }
        public string? caseId { get; init; }
    }

    private sealed class BatchUploadFilterRequest
    {
        public int page { get; init; } = 1;
        public int limit { get; init; } = 20;
        public string? keyword { get; init; }
        public string? template { get; init; }
        public string? status { get; init; }
        public string? sortBy { get; init; }
        public string? sortDirection { get; init; }
    }

    private sealed class BatchUploadDataContextFilterRequest
    {
        public string? id { get; init; }
        public int page { get; init; } = 1;
        public int limit { get; init; } = 20;
    }

    public static void MapBatchUploadEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/Batch")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode);

        group.MapGet("/list/{id}", GetBatchUploadById)
            .RequirePermission(LiensPermissions.CaseRead);
        group.MapPost("/list", GetBatchUploads)
            .RequirePermission(LiensPermissions.CaseRead);
        group.MapPost("/data-context", GetDataContext)
            .RequirePermission(LiensPermissions.CaseRead);
        group.MapPost("/create", CreateBatchUpload)
            .RequirePermission(LiensPermissions.CaseUpdate);
        group.MapPost("/Upload", UploadBatchFile)
            .RequirePermission(LiensPermissions.CaseUpdate)
            .DisableAntiforgery();
        group.MapPost("/update", UpdateBatchUpload)
            .RequirePermission(LiensPermissions.CaseUpdate);
        group.MapPost("/process", ProcessBatchUpload)
            .RequirePermission(LiensPermissions.CaseUpdate);
        group.MapGet("/details/{batchUploadId}", GetBatchUploadDetails)
            .RequirePermission(LiensPermissions.CaseRead);
        group.MapGet("/download-template/{id}", DownloadTemplate)
            .RequirePermission(LiensPermissions.CaseRead);
        group.MapDelete("/delete/{id}", DeleteBatchUpload)
            .RequirePermission(LiensPermissions.CaseUpdate);
        group.MapDelete("/details/delete/{id}", DeleteBatchUploadDetail)
            .RequirePermission(LiensPermissions.CaseUpdate);
    }

    private static Guid RequireTenantId(ICurrentRequestContext ctx) =>
        ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");

    private static Guid RequireUserId(ICurrentRequestContext ctx) =>
        ctx.UserId ?? throw new UnauthorizedAccessException("User context is required.");

    private static Guid RequireOrgId(ICurrentRequestContext ctx) =>
        ctx.OrgId ?? throw new UnauthorizedAccessException("Organization context is required.");

    private static async Task SeedDefaultTemplatesAsync(LiensDbContext db, Guid userId, CancellationToken ct)
    {
        if (await db.BatchTemplates.AnyAsync(ct))
            return;

        db.BatchTemplates.AddRange(
            BatchTemplate.Create(
                "ADD_LIENS_EXISTING_CASE",
                "Add Liens To Existing Case",
                "Case Code*|Lien Status*|Purchase Date*|Initial Service Date*|End Service Date|Notes|Is Bulk|Funding Company|Facility Name*|Contact Person|Facility Email Address|Medical Provider Name|Medical Code & Description*|Medicare Cost|Billing Amount*|Purchase Amount*|Payee|Outbound Check Number|Document Type*|Attachment",
                userId,
                isSystem: true),
            BatchTemplate.Create(
                "ADD_PAYMENTS_EXISTING_LIENS",
                "Add Payments To Existing Liens",
                "Lien Code*|Payment Number*|Payment Date*|Amount*|Payee|Check Number|Note",
                userId,
                isSystem: true),
            BatchTemplate.Create(
                "INITIAL_CASE_IMPORT",
                "Initial Case Import",
                "Case Code*|First Name*|Last Name*|Date Of Loss|Date Of Birth|Address|City|State|Zipcode|Note|External Case Id",
                userId,
                isSystem: true),
            BatchTemplate.Create(
                "UPDATE_CASE_TRACKING_STATUS",
                "Update Case Tracking Status",
                "Case Code*|Case Status*|Note|Switched Date",
                userId,
                isSystem: true));

        await db.SaveChangesAsync(ct);
    }

    private static async Task<IResult> GetBatchUploadById(
        string id,
        LiensDbContext db,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        if (!Guid.TryParse(id, out var batchUploadId))
            return Results.NotFound(new { isSuccess = false, message = "No batch upload found." });

        var item = await db.BatchUploads.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Status == "A" && x.Id == batchUploadId, ct);
        if (item is null)
            return Results.NotFound(new { isSuccess = false, message = "No batch upload found." });

        return Results.Ok(new
        {
            isSuccess = true,
            message = "Batch upload found.",
            data = new[] { MapBatchUpload(item) },
        });
    }

    private static async Task<IResult> GetBatchUploads(
        BatchUploadFilterRequest filter,
        LiensDbContext db,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        var query = db.BatchUploads.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Status == "A");

        if (!string.IsNullOrWhiteSpace(filter.keyword))
        {
            var keyword = filter.keyword.Trim();
            query = query.Where(x =>
                x.Label.Contains(keyword) ||
                x.FileName.Contains(keyword) ||
                x.Template.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(filter.template))
            query = query.Where(x => x.Template == filter.template.Trim());

        if (!string.IsNullOrWhiteSpace(filter.status))
            query = query.Where(x => x.ProcessStatus == filter.status.Trim());

        query = string.Equals(filter.sortBy, "processStatus", StringComparison.OrdinalIgnoreCase)
            ? (string.Equals(filter.sortDirection, "ASC", StringComparison.OrdinalIgnoreCase)
                ? query.OrderBy(x => x.ProcessStatus).ThenBy(x => x.CreatedAtUtc)
                : query.OrderByDescending(x => x.ProcessStatus).ThenByDescending(x => x.CreatedAtUtc))
            : (string.Equals(filter.sortDirection, "ASC", StringComparison.OrdinalIgnoreCase)
                ? query.OrderBy(x => x.CreatedAtUtc)
                : query.OrderByDescending(x => x.CreatedAtUtc));

        var page = Math.Max(filter.page, 1);
        var limit = Math.Max(filter.limit, 1);
        var totalCount = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * limit).Take(limit).ToListAsync(ct);

        if (items.Count == 0)
        {
            return Results.NotFound(new
            {
                isSuccess = false,
                message = "No batch upload found.",
                data = Array.Empty<object>(),
                page,
                limit,
                totalCount,
            });
        }

        return Results.Ok(new
        {
            isSuccess = true,
            message = "Batch upload found.",
            data = items.Select(MapBatchUpload).ToList(),
            page,
            limit,
            totalCount,
        });
    }

    private static async Task<IResult> GetDataContext(
        BatchUploadDataContextFilterRequest filter,
        LiensDbContext db,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        if (!Guid.TryParse(filter.id, out var batchUploadId))
            return Results.NotFound(new { isSuccess = false, message = "No batch upload found." });

        var batch = await db.BatchUploads.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Status == "A" && x.Id == batchUploadId, ct);
        if (batch is null)
            return Results.NotFound(new { isSuccess = false, message = "No batch upload found." });

        var page = Math.Max(filter.page, 1);
        var limit = Math.Max(filter.limit, 1);
        var detailQuery = db.BatchUploadDetails.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.BatchUploadId == batchUploadId && x.RecordStatus == "A")
            .OrderBy(x => x.RowNumber);

        var totalCount = await detailQuery.CountAsync(ct);
        var items = await detailQuery.Skip((page - 1) * limit).Take(limit).ToListAsync(ct);

        return Results.Ok(new
        {
            isSuccess = true,
            message = "Data context retrieved.",
            id = batch.Id.ToString(),
            caseId = batch.CaseId?.ToString() ?? string.Empty,
            data = items.Select(MapBatchUploadDataContextRow).ToList(),
            page,
            limit,
            totalCount,
        });
    }

    private static async Task<IResult> CreateBatchUpload(
        BatchUploadRequest request,
        LiensDbContext db,
        ICaseService caseService,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        var orgId = RequireOrgId(ctx);
        await SeedDefaultTemplatesAsync(db, userId, ct);

        var validated = await BuildBatchUploadAsync(request.label, request.template, request.caseId, request.file, request.date, request.rows, request.dataContext, tenantId, userId, db, ct);
        if (validated.Error is not null)
            return validated.Error;

        db.BatchUploads.Add(validated.Batch!);
        db.BatchUploadDetails.AddRange(validated.Details!);
        await db.SaveChangesAsync(ct);
        var createdBatch = validated.Batch!;
        var importResult = await ImportBatchRowsAsync(
            createdBatch,
            validated.Details!,
            validated.ParsedDataContext,
            request.template,
            tenantId,
            orgId,
            userId,
            db,
            caseService,
            ct);

        return Results.Ok(new
        {
            isSuccess = true,
            message = importResult.Message,
            id = createdBatch.Id.ToString(),
            totalRows = importResult.TotalRows,
            importedCount = importResult.ImportedCount,
            createdCount = importResult.CreatedCount,
            updatedCount = importResult.UpdatedCount,
            failedCount = importResult.FailedCount,
            data = validated.ParsedDataContext,
        });
    }

    private static async Task<IResult> UploadBatchFile(
        HttpRequest httpRequest,
        LiensDbContext db,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        await SeedDefaultTemplatesAsync(db, userId, ct);

        if (!httpRequest.HasFormContentType)
            return Results.BadRequest(new { isSuccess = false, message = "Content-Type must be multipart/form-data." });

        var form = await httpRequest.ReadFormAsync(ct);
        var file = form.Files["file"];
        if (file is null || file.Length == 0)
            return Results.BadRequest(new { isSuccess = false, message = "No file uploaded" });
        if (file.Length > 20 * 1024 * 1024)
            return Results.BadRequest(new { isSuccess = false, message = "File size exceeds the allowed limit (20 MB)" });

        var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
        if (ext != ".csv")
            return Results.BadRequest(new { isSuccess = false, message = "Only CSV files are allowed." });

        string dataContext;
        using (var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8))
            dataContext = await reader.ReadToEndAsync(ct);

        var rows = Math.Max(dataContext.Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries).Length - 1, 0);
        var validated = await BuildBatchUploadAsync(
            form["label"].ToString(),
            form["template"].ToString(),
            form["caseId"].ToString(),
            Path.GetFileName(file.FileName),
            form["date"].ToString(),
            rows,
            dataContext,
            tenantId,
            userId,
            db,
            ct);
        if (validated.Error is not null)
            return validated.Error;

        db.BatchUploads.Add(validated.Batch!);
        db.BatchUploadDetails.AddRange(validated.Details!);
        await db.SaveChangesAsync(ct);
        var createdBatch = validated.Batch!;

        return Results.Ok(new
        {
            isSuccess = true,
            message = "Successfully Created.",
            id = createdBatch.Id.ToString(),
            label = createdBatch.Label,
            template = createdBatch.Template,
            file = createdBatch.FileName,
            processStatus = createdBatch.ProcessStatus,
        });
    }

    private static async Task<IResult> UpdateBatchUpload(
        BatchUploadRequest request,
        LiensDbContext db,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        await SeedDefaultTemplatesAsync(db, userId, ct);

        if (!Guid.TryParse(request.id, out var batchUploadId))
            return Results.BadRequest(new { isSuccess = false, message = "Unable to update batch upload." });

        var existing = await db.BatchUploads.Include(x => x.Details)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Status == "A" && x.Id == batchUploadId, ct);
        if (existing is null)
            return Results.BadRequest(new { isSuccess = false, message = "Unable to update batch upload." });

        var templateId = await ResolveTemplateIdAsync(db, tenantId, request.template, ct);
        Guid? caseId = Guid.TryParse(request.caseId, out var parsedCaseId) ? parsedCaseId : null;
        existing.Update(
            userId,
            request.label?.Trim() ?? existing.Label,
            request.template?.Trim() ?? existing.Template,
            request.file?.Trim() ?? existing.FileName,
            request.rows,
            request.dataContext ?? string.Empty,
            caseId,
            templateId,
            request.date);

        var parsedRows = ParseDataContext(existing.DataContext);
        db.BatchUploadDetails.RemoveRange(existing.Details);
        var details = parsedRows.Select((row, index) =>
            BatchUploadDetail.Create(tenantId, existing.Id, index + 1, (row as JsonObject ?? new JsonObject()).ToJsonString(), userId)).ToList();
        db.BatchUploadDetails.AddRange(details);
        await db.SaveChangesAsync(ct);

        return Results.Ok(new { isSuccess = true, message = "Successfully Updated." });
    }

    private static async Task<IResult> ProcessBatchUpload(
        BatchUploadProcessRequest request,
        LiensDbContext db,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        await SeedDefaultTemplatesAsync(db, userId, ct);

        if (!Guid.TryParse(request.batchUploadId, out var batchUploadId))
            return Results.BadRequest(new { isSuccess = false, message = "Unable to process batch upload." });

        var batch = await db.BatchUploads.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Status == "A" && x.Id == batchUploadId, ct);
        if (batch is null)
            return Results.BadRequest(new { isSuccess = false, message = "Unable to process batch upload." });

        var template = await ResolveTemplateAsync(db, tenantId, request.templateId, batch.Template, ct);
        if (template is null)
            return Results.BadRequest(new { isSuccess = false, message = "No batch template found." });

        var details = await db.BatchUploadDetails
            .Where(x => x.TenantId == tenantId && x.BatchUploadId == batchUploadId && x.RecordStatus == "A")
            .OrderBy(x => x.RowNumber)
            .ToListAsync(ct);
        if (details.Count == 0)
            return Results.BadRequest(new { isSuccess = false, message = "No detail rows found to process." });

        var requiredHeaders = template.ColumnsHeader
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(h => h.EndsWith('*'))
            .ToList();
        var allowsGeneratedCaseCode = string.Equals(
            template.Code,
            "INITIAL_CASE_IMPORT",
            StringComparison.OrdinalIgnoreCase);

        var processed = new List<object>();
        var successCount = 0;
        var failedCount = 0;

        foreach (var detail in details)
        {
            var row = JsonNode.Parse(detail.DataJson)?.AsObject() ?? new JsonObject();
            var reasons = new List<string>();

            foreach (var required in requiredHeaders)
            {
                if (allowsGeneratedCaseCode &&
                    string.Equals(required, "Case Code*", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var value = ResolveRowValue(row, required);
                if (string.IsNullOrWhiteSpace(value))
                    reasons.Add($"{required} is required.");
            }

            foreach (var property in row)
            {
                if (property.Key.Contains("DATE", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(property.Value?.ToString()) &&
                    !DateTime.TryParseExact(property.Value!.ToString(), ["M/d/yyyy", "MM/dd/yyyy", "yyyy-MM-dd"], CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                {
                    reasons.Add($"{property.Key} has invalid date format. Expected format: MM/DD/YYYY.");
                }
            }

            var status = reasons.Count == 0 ? "SUCCESS" : "FAILED";
            var reason = reasons.Count == 0 ? null : string.Join("; ", reasons);
            detail.SetResult(status, reason, userId);

            if (status == "SUCCESS")
                successCount++;
            else
                failedCount++;

            processed.Add(new
            {
                id = detail.Id.ToString(),
                batchUploadId = detail.BatchUploadId.ToString(),
                row = detail.RowNumber,
                status,
                reason = reason ?? string.Empty,
                data = row,
            });
        }

        batch.SetProcessStatus(failedCount == 0 ? "PROCESSED" : successCount == 0 ? "FAILED" : "PARTIAL", userId);
        await db.SaveChangesAsync(ct);

        return Results.Ok(new
        {
            isSuccess = true,
            message = $"Processing complete. {successCount} succeeded, {failedCount} failed out of {details.Count} rows.",
            totalRows = details.Count,
            successCount,
            failedCount,
            data = processed,
        });
    }

    private static async Task<IResult> GetBatchUploadDetails(
        string batchUploadId,
        LiensDbContext db,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        if (!Guid.TryParse(batchUploadId, out var parsedId))
            return Results.NotFound(new { isSuccess = false, message = "No detail rows found." });

        var details = await db.BatchUploadDetails.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.BatchUploadId == parsedId && x.RecordStatus == "A")
            .OrderBy(x => x.RowNumber)
            .ToListAsync(ct);
        if (details.Count == 0)
            return Results.NotFound(new { isSuccess = false, message = "No detail rows found." });

        return Results.Ok(new
        {
            isSuccess = true,
            message = "Batch upload details found.",
            totalRows = details.Count,
            successCount = details.Count(x => x.Status == "SUCCESS"),
            failedCount = details.Count(x => x.Status == "FAILED"),
            data = details.Select(MapBatchUploadDetail).ToList(),
        });
    }

    private static async Task<IResult> DownloadTemplate(
        string id,
        LiensDbContext db,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var userId = RequireUserId(ctx);
        await SeedDefaultTemplatesAsync(db, userId, ct);

        BatchTemplate? template = null;
        if (Guid.TryParse(id, out var templateId))
            template = await db.BatchTemplates.AsNoTracking().FirstOrDefaultAsync(x => x.Id == templateId && x.IsActive, ct);
        template ??= await db.BatchTemplates.AsNoTracking().FirstOrDefaultAsync(x => x.Code == id && x.IsActive, ct);

        if (template is null)
            return Results.NotFound(new { isSuccess = false, message = "No batch template found." });

        var bytes = Encoding.UTF8.GetBytes(string.Join(",", template.ColumnsHeader.Split('|', StringSplitOptions.TrimEntries)));
        return Results.Ok(new
        {
            isSuccess = true,
            message = "CSV generated successfully.",
            data = new object[]
            {
                new
                {
                    base64 = Convert.ToBase64String(bytes),
                    filename = $"{template.Code}.csv",
                    export_format = "csv",
                },
            },
        });
    }

    private static async Task<IResult> DeleteBatchUpload(
        string id,
        LiensDbContext db,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        if (!Guid.TryParse(id, out var batchUploadId))
            return Results.BadRequest(new { isSuccess = false, message = "Unable to delete batch upload." });

        var batch = await db.BatchUploads.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Status == "A" && x.Id == batchUploadId, ct);
        if (batch is null)
            return Results.BadRequest(new { isSuccess = false, message = "Unable to delete batch upload." });

        batch.Deactivate(userId);
        var details = await db.BatchUploadDetails.Where(x => x.TenantId == tenantId && x.BatchUploadId == batchUploadId && x.RecordStatus == "A").ToListAsync(ct);
        foreach (var detail in details)
            detail.Deactivate(userId);
        await db.SaveChangesAsync(ct);

        return Results.Ok(new { isSuccess = true, message = "Successfully Deleted." });
    }

    private static async Task<IResult> DeleteBatchUploadDetail(
        string id,
        LiensDbContext db,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        if (!Guid.TryParse(id, out var detailId))
            return Results.BadRequest(new { isSuccess = false, message = "Unable to delete batch upload detail." });

        var detail = await db.BatchUploadDetails.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.RecordStatus == "A" && x.Id == detailId, ct);
        if (detail is null)
            return Results.BadRequest(new { isSuccess = false, message = "Unable to delete batch upload detail." });

        detail.Deactivate(userId);
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { isSuccess = true, message = "Successfully Deleted." });
    }

    private static async Task<(BatchUpload? Batch, List<BatchUploadDetail>? Details, JsonArray ParsedDataContext, IResult? Error)> BuildBatchUploadAsync(
        string? label,
        string? template,
        string? caseId,
        string? fileName,
        string? batchDate,
        int rows,
        string? dataContext,
        Guid tenantId,
        Guid userId,
        LiensDbContext db,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(template))
            return (null, null, [], Results.BadRequest(new { isSuccess = false, message = "Unable to save batch upload." }));

        Guid? parsedCaseId = Guid.TryParse(caseId, out var resolvedCaseId) ? resolvedCaseId : null;
        if (parsedCaseId.HasValue)
        {
            var existingCase = await db.Cases.AsNoTracking().AnyAsync(x => x.Id == parsedCaseId.Value && x.TenantId == tenantId, ct);
            if (!existingCase)
                return (null, null, [], Results.BadRequest(new { isSuccess = false, message = "Case not found." }));
        }

        var templateId = await ResolveTemplateIdAsync(db, tenantId, template, ct);
        var parsedDataContext = ParseDataContext(dataContext);
        var batch = BatchUpload.Create(
            tenantId,
            userId,
            label.Trim(),
            template.Trim(),
            fileName?.Trim() ?? string.Empty,
            rows > 0 ? rows : parsedDataContext.Count,
            dataContext ?? string.Empty,
            parsedCaseId,
            templateId,
            batchDate);

        var details = parsedDataContext.Select((row, index) =>
            BatchUploadDetail.Create(tenantId, batch.Id, index + 1, (row as JsonObject ?? new JsonObject()).ToJsonString(), userId)).ToList();

        return (batch, details, parsedDataContext, null);
    }

    private sealed record BatchImportResult(
        string Message,
        int TotalRows,
        int ImportedCount,
        int CreatedCount,
        int UpdatedCount,
        int FailedCount);

    private static async Task<BatchImportResult> ImportBatchRowsAsync(
        BatchUpload batch,
        List<BatchUploadDetail> details,
        JsonArray parsedRows,
        string? templateCode,
        Guid tenantId,
        Guid orgId,
        Guid userId,
        LiensDbContext db,
        ICaseService caseService,
        CancellationToken ct)
    {
        var normalizedTemplate = templateCode?.Trim() ?? batch.Template;
        if (!SupportsDirectImport(normalizedTemplate))
        {
            return new BatchImportResult(
                "Successfully Created.",
                details.Count,
                0,
                0,
                0,
                0);
        }

        var importedCount = 0;
        var createdCount = 0;
        var updatedCount = 0;
        var failedCount = 0;

        for (var i = 0; i < details.Count; i++)
        {
            var detail = details[i];
            var row = (parsedRows.ElementAtOrDefault(i) as JsonObject) ?? new JsonObject();

            try
            {
                switch (normalizedTemplate)
                {
                    case "INITIAL_CASE_IMPORT":
                        await ImportInitialCaseRowAsync(row, tenantId, orgId, userId, caseService, ct);
                        detail.SetResult("SUCCESS", null, userId);
                        importedCount++;
                        createdCount++;
                        break;
                    case "UPDATE_CASE_TRACKING_STATUS":
                        await ImportCaseTrackingRowAsync(row, tenantId, userId, caseService, ct);
                        detail.SetResult("SUCCESS", null, userId);
                        importedCount++;
                        updatedCount++;
                        break;
                    default:
                        detail.SetResult("FAILED", "Template import is not supported yet.", userId);
                        failedCount++;
                        break;
                }
            }
            catch (Exception ex)
            {
                detail.SetResult("FAILED", ex.Message, userId);
                failedCount++;
            }
        }

        batch.SetProcessStatus(
            failedCount == 0 ? "PROCESSED" : importedCount == 0 ? "FAILED" : "PARTIAL",
            userId);
        await db.SaveChangesAsync(ct);

        return new BatchImportResult(
            $"Import complete. {importedCount} imported, {failedCount} failed out of {details.Count} rows.",
            details.Count,
            importedCount,
            createdCount,
            updatedCount,
            failedCount);
    }

    private static bool SupportsDirectImport(string? templateCode) =>
        string.Equals(templateCode, "INITIAL_CASE_IMPORT", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(templateCode, "UPDATE_CASE_TRACKING_STATUS", StringComparison.OrdinalIgnoreCase);

    private static async Task ImportInitialCaseRowAsync(
        JsonObject row,
        Guid tenantId,
        Guid orgId,
        Guid userId,
        ICaseService caseService,
        CancellationToken ct)
    {
        var request = new CreateCaseRequest
        {
            CaseNumber = ResolveRowValue(row, "Case Code*")?.Trim() ?? string.Empty,
            ClientFirstName = RequireRowValue(row, "First Name*"),
            ClientLastName = RequireRowValue(row, "Last Name*"),
            DateOfIncident = ParseBatchDate(ResolveRowValue(row, "Date Of Loss")),
            ClientDob = ParseBatchDate(ResolveRowValue(row, "Date Of Birth")),
            ClientAddress = BuildAddress(
                ResolveRowValue(row, "Address"),
                ResolveRowValue(row, "City"),
                ResolveRowValue(row, "State"),
                ResolveRowValue(row, "Zipcode")),
            Notes = ResolveRowValue(row, "Note"),
            ExternalReference = ResolveRowValue(row, "External Case Id"),
        };

        await caseService.CreateAsync(tenantId, orgId, userId, request, ct);
    }

    private static async Task ImportCaseTrackingRowAsync(
        JsonObject row,
        Guid tenantId,
        Guid userId,
        ICaseService caseService,
        CancellationToken ct)
    {
        var caseNumber = RequireRowValue(row, "Case Code*");
        var existing = await caseService.GetByCaseNumberAsync(tenantId, caseNumber, ct)
            ?? throw new InvalidOperationException($"Case '{caseNumber}' not found.");

        var request = new UpdateCaseRequest
        {
            ClientFirstName = existing.ClientFirstName,
            ClientLastName = existing.ClientLastName,
            Title = existing.Title,
            ExternalReference = existing.ExternalReference,
            ClientDob = existing.ClientDob,
            ClientPhone = existing.ClientPhone,
            ClientEmail = existing.ClientEmail,
            ClientAddress = existing.ClientAddress,
            DateOfIncident = existing.DateOfIncident,
            InsuranceCarrier = existing.InsuranceCarrier,
            PolicyNumber = existing.PolicyNumber,
            ClaimNumber = existing.ClaimNumber,
            Description = existing.Description,
            Notes = FirstNonEmpty(ResolveRowValue(row, "Note"), existing.Notes),
            Status = NormalizeImportedCaseStatus(RequireRowValue(row, "Case Status*")),
            DemandAmount = existing.DemandAmount,
            SettlementAmount = existing.SettlementAmount,
            Sex = existing.Sex,
            CaseType = existing.CaseType,
            CurrentMedicalStatus = existing.CurrentMedicalStatus,
            StateOfIncident = existing.StateOfIncident,
            TrackingFollowUpDate = existing.TrackingFollowUpDate,
            LeadId = existing.LeadId,
        };

        await caseService.UpdateAsync(tenantId, existing.Id, userId, request, ct);
    }

    private static string RequireRowValue(JsonObject row, string key)
    {
        var value = ResolveRowValue(row, key);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"{key} is required.");

        return value.Trim();
    }

    private static DateOnly? ParseBatchDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return DateOnly.TryParseExact(
            value.Trim(),
            ["M/d/yyyy", "MM/dd/yyyy", "yyyy-MM-dd"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var date)
            ? date
            : throw new InvalidOperationException($"Invalid date value '{value}'. Expected MM/DD/YYYY.");
    }

    private static string? BuildAddress(string? address, string? city, string? state, string? zipcode)
    {
        var parts = new[] { address, city, state, zipcode }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToList();

        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    private static string NormalizeImportedCaseStatus(string value)
    {
        var normalized = value.Trim();
        var compact = normalized
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

        return compact.ToUpperInvariant() switch
        {
            "OPEN" or "PREDEMAND" => CaseStatus.PreDemand,
            "DEMANDSENT" => CaseStatus.DemandSent,
            "INNEGOTIATION" => CaseStatus.InNegotiation,
            "CASESETTLED" => CaseStatus.CaseSettled,
            "CLOSED" => CaseStatus.Closed,
            _ when CaseStatus.All.Contains(normalized) => normalized,
            _ => throw new InvalidOperationException($"Invalid case status '{value}'."),
        };
    }

    private static JsonArray ParseDataContext(string? dataContext)
    {
        var result = new JsonArray();
        if (string.IsNullOrWhiteSpace(dataContext))
            return result;

        var lines = dataContext
            .Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();
        if (lines.Count == 0)
            return result;

        var headers = ParseCsvLine(lines[0]);
        for (var i = 1; i < lines.Count; i++)
        {
            var values = ParseCsvLine(lines[i]);
            var row = new JsonObject();
            for (var h = 0; h < headers.Count; h++)
                row[headers[h].Trim()] = h < values.Count ? values[h].Trim() : string.Empty;
            result.Add(row);
        }

        return result;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else
            {
                if (c == '"')
                    inQuotes = true;
                else if (c == ',')
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else
                    current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields;
    }

    private static object MapBatchUpload(BatchUpload item) => new
    {
        id = item.Id.ToString(),
        label = item.Label,
        template = item.Template,
        file = item.FileName,
        date = item.BatchDate ?? string.Empty,
        rows = item.Rows,
        dataContext = item.DataContext,
        status = item.Status,
        processStatus = item.ProcessStatus,
        createdDate = item.CreatedAtUtc.ToString("MM/dd/yyyy hh:mm:ss tt", CultureInfo.InvariantCulture),
        createdBy = item.CreatedByUserId?.ToString() ?? string.Empty,
        updatedDate = item.UpdatedAtUtc.ToString("MM/dd/yyyy hh:mm:ss tt", CultureInfo.InvariantCulture),
        updatedBy = item.UpdatedByUserId?.ToString() ?? string.Empty,
    };

    private static object MapBatchUploadDetail(BatchUploadDetail item) => new
    {
        id = item.Id.ToString(),
        batchUploadId = item.BatchUploadId.ToString(),
        row = item.RowNumber,
        status = item.Status,
        reason = item.Reason ?? string.Empty,
        data = JsonNode.Parse(item.DataJson),
        createdDate = item.CreatedAtUtc.ToString("MM/dd/yyyy hh:mm:ss tt", CultureInfo.InvariantCulture),
        createdBy = item.CreatedByUserId?.ToString() ?? string.Empty,
        updatedDate = item.UpdatedAtUtc.ToString("MM/dd/yyyy hh:mm:ss tt", CultureInfo.InvariantCulture),
        updatedBy = item.UpdatedByUserId?.ToString() ?? string.Empty,
    };

    private static Dictionary<string, string> MapBatchUploadDataContextRow(BatchUploadDetail item)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var row = JsonNode.Parse(item.DataJson)?.AsObject();
        if (row is not null)
        {
            foreach (var property in row)
                result[property.Key] = property.Value?.ToString() ?? string.Empty;
        }

        result["id"] = item.Id.ToString();
        result["row"] = item.RowNumber.ToString(CultureInfo.InvariantCulture);
        result["status"] = item.Status;
        result["reason"] = item.Reason ?? string.Empty;
        return result;
    }

    private static async Task<Guid?> ResolveTemplateIdAsync(LiensDbContext db, Guid tenantId, string? template, CancellationToken ct)
    {
        if (Guid.TryParse(template, out var templateId))
            return templateId;

        if (string.IsNullOrWhiteSpace(template))
            return null;

        var existing = await db.BatchTemplates.AsNoTracking()
            .FirstOrDefaultAsync(x => x.IsActive && x.Code == template && (x.TenantId == tenantId || x.TenantId == null), ct);
        return existing?.Id;
    }

    private static async Task<BatchTemplate?> ResolveTemplateAsync(LiensDbContext db, Guid tenantId, string? templateIdOrCode, string batchTemplate, CancellationToken ct)
    {
        if (Guid.TryParse(templateIdOrCode, out var templateId))
        {
            return await db.BatchTemplates.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == templateId && x.IsActive, ct);
        }

        var code = !string.IsNullOrWhiteSpace(templateIdOrCode) ? templateIdOrCode.Trim() : batchTemplate;
        return await db.BatchTemplates.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Code == code && x.IsActive && (x.TenantId == tenantId || x.TenantId == null), ct);
    }

    private static string? ResolveRowValue(JsonObject row, string key)
    {
        var normalizedKey = NormalizeHeaderKey(key);
        foreach (var property in row)
        {
            if (string.Equals(NormalizeHeaderKey(property.Key), normalizedKey, StringComparison.OrdinalIgnoreCase))
                return property.Value?.ToString();
        }

        return null;
    }

    private static string NormalizeHeaderKey(string value) =>
        value.Replace("*", string.Empty, StringComparison.Ordinal)
            .Trim();

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }
}
