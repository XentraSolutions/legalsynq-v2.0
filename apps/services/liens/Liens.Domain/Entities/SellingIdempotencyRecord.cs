using System.Security.Cryptography;
using System.Text;
using BuildingBlocks.Domain;

namespace Liens.Domain.Entities;

/// <summary>
/// Tenant- and caller-scoped replay record for Selling V2 state-changing requests.
/// The unique database key is deliberately independent of request hash so a reused
/// idempotency key with a changed body can be rejected deterministically.
/// </summary>
public sealed class SellingIdempotencyRecord : AuditableEntity
{
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string SubjectType { get; private set; } = string.Empty;
    public Guid SubjectId { get; private set; }
    public string Route { get; private set; } = string.Empty;
    public string ResourceType { get; private set; } = string.Empty;
    public string ResourceKey { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string IdempotencyKeyHash { get; private set; } = string.Empty;
    public string RequestHash { get; private set; } = string.Empty;
    public string ProcessingState { get; private set; } = InProgress;
    public int? ResponseStatusCode { get; private set; }
    public string? ResponseContentType { get; private set; }
    public string? ResponseBody { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    private SellingIdempotencyRecord() { }

    public static SellingIdempotencyRecord Create(
        Guid tenantId,
        string subjectType,
        Guid subjectId,
        string route,
        string resourceType,
        string resourceKey,
        string idempotencyKey,
        string requestHash)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (subjectId == Guid.Empty) throw new ArgumentException("SubjectId is required.", nameof(subjectId));
        RequireValue(subjectType, nameof(subjectType));
        RequireValue(route, nameof(route));
        RequireValue(resourceType, nameof(resourceType));
        RequireValue(resourceKey, nameof(resourceKey));
        RequireValue(idempotencyKey, nameof(idempotencyKey));
        RequireSha256Hash(requestHash, nameof(requestHash));

        var now = DateTime.UtcNow;
        return new SellingIdempotencyRecord
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            SubjectType = subjectType.Trim(),
            SubjectId = subjectId,
            Route = route.Trim(),
            ResourceType = resourceType.Trim(),
            ResourceKey = resourceKey.Trim(),
            IdempotencyKey = idempotencyKey.Trim(),
            IdempotencyKeyHash = ComputeSha256Hex(idempotencyKey),
            RequestHash = requestHash.Trim().ToLowerInvariant(),
            ProcessingState = InProgress,
            CreatedByUserId = subjectId,
            UpdatedByUserId = subjectId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    public void Complete(
        int responseStatusCode,
        string? responseBody,
        string? responseContentType,
        Guid updatedByUserId)
    {
        if (responseStatusCode is < 100 or > 599)
            throw new ArgumentOutOfRangeException(nameof(responseStatusCode), "Response status code must be a valid HTTP status code.");
        if (updatedByUserId == Guid.Empty)
            throw new ArgumentException("UpdatedByUserId is required.", nameof(updatedByUserId));

        ResponseStatusCode = responseStatusCode;
        ResponseBody = responseBody;
        ResponseContentType = string.IsNullOrWhiteSpace(responseContentType) ? null : responseContentType.Trim();
        ProcessingState = Completed;
        CompletedAtUtc = DateTime.UtcNow;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = CompletedAtUtc.Value;
    }

    private static void RequireValue(string? value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
    }

    private static void RequireSha256Hash(string value, string parameterName)
    {
        if (value.Length != 64 || !value.All(Uri.IsHexDigit))
            throw new ArgumentException("RequestHash must be a 64-character SHA-256 hexadecimal digest.", parameterName);
    }

    private static string ComputeSha256Hex(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim()))).ToLowerInvariant();
}
