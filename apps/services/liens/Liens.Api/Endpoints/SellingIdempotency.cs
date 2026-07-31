using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Liens.Domain.Entities;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liens.Api.Endpoints;

/// <summary>
/// Stores deterministic, tenant- and principal-scoped Selling responses so a
/// retried state-changing request cannot create a second side effect.  The
/// request body itself is never retained; only its SHA-256 digest is stored.
/// </summary>
internal static class SellingIdempotency
{
    private const int MaxKeyLength = 280;
    private const string JsonContentType = "application/json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static bool TryGetKey(HttpRequest request, out string? key, out IResult? error)
    {
        key = request.Headers["Idempotency-Key"].FirstOrDefault()?.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            error = Results.BadRequest(new { error = new { code = "idempotency_key_required", message = "Idempotency-Key header is required." } });
            return false;
        }

        if (key.Length > MaxKeyLength)
        {
            error = Results.BadRequest(new { error = new { code = "idempotency_key_invalid", message = $"Idempotency-Key must not exceed {MaxKeyLength} characters." } });
            return false;
        }

        error = null;
        return true;
    }

    public static async Task<IResult?> GetReplayAsync(
        LiensDbContext db,
        Guid tenantId,
        string subjectType,
        Guid subjectId,
        string route,
        string resourceType,
        string resourceKey,
        string idempotencyKey,
        object? request,
        CancellationToken ct)
    {
        var keyHash = ComputeHash(idempotencyKey);
        var record = await db.SellingIdempotencyRecords.AsNoTracking().FirstOrDefaultAsync(item =>
            item.TenantId == tenantId &&
            item.SubjectType == subjectType &&
            item.SubjectId == subjectId &&
            item.Route == route &&
            item.ResourceType == resourceType &&
            item.ResourceKey == resourceKey &&
            item.IdempotencyKeyHash == keyHash,
            ct);
        if (record is null)
            return null;

        if (!string.Equals(record.RequestHash, ComputeRequestHash(request), StringComparison.Ordinal))
        {
            return Results.Conflict(new
            {
                error = new
                {
                    code = "idempotency_key_reused",
                    message = "Idempotency-Key was already used with a different request payload.",
                },
            });
        }

        if (!string.Equals(record.ProcessingState, SellingIdempotencyRecord.Completed, StringComparison.Ordinal) ||
            !record.ResponseStatusCode.HasValue ||
            record.ResponseBody is null)
        {
            return Results.Conflict(new
            {
                error = new
                {
                    code = "idempotency_request_in_progress",
                    message = "An identical request is still being processed. Retry shortly.",
                },
            });
        }

        return Results.Content(record.ResponseBody, record.ResponseContentType ?? JsonContentType, statusCode: record.ResponseStatusCode.Value);
    }

    public static async Task<IdempotencyStart> TryBeginAsync(
        LiensDbContext db,
        Guid tenantId,
        string subjectType,
        Guid subjectId,
        string route,
        string resourceType,
        string resourceKey,
        string idempotencyKey,
        object? request,
        CancellationToken ct)
    {
        // Fast-path lookup makes the semantic lock work with providers that do
        // not enforce relational unique indexes (notably the test provider),
        // while the database constraint remains the cross-process race guard.
        var existing = await GetReplayAsync(
            db, tenantId, subjectType, subjectId, route, resourceType, resourceKey, idempotencyKey, request, ct);
        if (existing is not null)
            return new IdempotencyStart(null, existing);

        var record = SellingIdempotencyRecord.Create(
            tenantId,
            subjectType,
            subjectId,
            route,
            resourceType,
            resourceKey,
            idempotencyKey,
            ComputeRequestHash(request));
        db.SellingIdempotencyRecords.Add(record);

        try
        {
            await db.SaveChangesAsync(ct);
            return new IdempotencyStart(record, null);
        }
        catch (DbUpdateException)
        {
            db.Entry(record).State = EntityState.Detached;
            var replay = await GetReplayAsync(
                db, tenantId, subjectType, subjectId, route, resourceType, resourceKey, idempotencyKey, request, ct);
            return new IdempotencyStart(null, replay ?? Results.Conflict(new
            {
                error = new
                {
                    code = "idempotency_request_in_progress",
                    message = "An identical request is already being processed. Retry shortly.",
                },
            }));
        }
    }

    public static async Task<IResult> CompleteAsync(
        LiensDbContext db,
        SellingIdempotencyRecord record,
        Guid updatedByUserId,
        int statusCode,
        object response,
        CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(response, JsonOptions);
        record.Complete(statusCode, body, JsonContentType, updatedByUserId);
        await db.SaveChangesAsync(ct);
        return Results.Content(body, JsonContentType, statusCode: statusCode);
    }

    public static string ComputeRequestHash(object? request)
        => ComputeHash(JsonSerializer.Serialize(request, JsonOptions));

    private static string ComputeHash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    internal sealed record IdempotencyStart(SellingIdempotencyRecord? Record, IResult? Result);
}
