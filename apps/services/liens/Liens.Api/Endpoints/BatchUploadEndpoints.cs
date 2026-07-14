using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Liens.Domain;
using Liens.Domain.Entities;
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
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        await SeedDefaultTemplatesAsync(db, userId, ct);

        var validated = await BuildBatchUploadAsync(request.label, request.template, request.caseId, request.file, request.date, request.rows, request.dataContext, tenantId, userId, db, ct);
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

        var processed = new List<object>();
        var successCount = 0;
        var failedCount = 0;

        foreach (var detail in details)
        {
            var row = JsonNode.Parse(detail.DataJson)?.AsObject() ?? new JsonObject();
            var reasons = new List<string>();

            foreach (var required in requiredHeaders)
            {
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
        foreach (var property in row)
        {
            if (string.Equals(property.Key.Trim(), key.Trim(), StringComparison.OrdinalIgnoreCase))
                return property.Value?.ToString();
        }

        return null;
    }
}
