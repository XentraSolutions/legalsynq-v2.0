using System.Net.Http.Headers;
using System.Text;
using BuildingBlocks.Authentication.ServiceTokens;
using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using Liens.Domain.Entities;
using Liens.Infrastructure.Persistence;

namespace Liens.Api.Endpoints;

/// <summary>
/// Public product-owned facade. Identity remains the source of truth; browsers
/// never call its internal user-management surface directly.
/// </summary>
public static class SynqLienUserManagementEndpoints
{
    private const string IdentityAudience = "identity-service";

    public static void MapSynqLienUserManagementEndpoints(this WebApplication app)
    {
        app.MapMethods(
                "/api/liens/user-management/{**path}",
                [HttpMethods.Get, HttpMethods.Post, HttpMethods.Put, HttpMethods.Patch, HttpMethods.Delete],
                ProxyAsync)
            .RequireAuthorization(Policies.AuthenticatedUser);
    }

    private static async Task<IResult> ProxyAsync(
        HttpContext context,
        string? path,
        ICurrentRequestContext requestContext,
        IHttpClientFactory httpClientFactory,
        IServiceTokenIssuer serviceTokenIssuer,
        LiensDbContext db,
        CancellationToken ct)
    {
        if (requestContext.TenantId is not Guid tenantId ||
            requestContext.UserId is not Guid actorUserId ||
            requestContext.OrgId is not Guid organizationId)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "SynqLien user management requires tenant, user, and organization context.",
                extensions: new Dictionary<string, object?> { ["code"] = "synqlien.missing_scope" });
        }

        var requestBody = string.Empty;
        if (context.Request.ContentLength > 0 || context.Request.Headers.ContainsKey("Transfer-Encoding"))
        {
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
            requestBody = await reader.ReadToEndAsync(ct);
        }

        SellingIdempotencyRecord? idempotencyRecord = null;
        if (!HttpMethods.IsGet(context.Request.Method))
        {
            if (!SellingIdempotency.TryGetKey(context.Request, out var idempotencyKey, out var keyError))
                return keyError!;
            var route = $"{context.Request.Method}:{context.Request.Path.Value}";
            var fingerprint = new { context.Request.Method, Path = context.Request.Path.Value, Query = context.Request.QueryString.Value, Body = requestBody };
            var start = await SellingIdempotency.TryBeginAsync(
                db, tenantId, "User", actorUserId, route, "SynqLienUserManagement",
                organizationId.ToString(), idempotencyKey!, fingerprint, ct);
            if (start.Result is not null) return start.Result;
            idempotencyRecord = start.Record;
        }

        var relativePath = $"/api/internal/synqlien/user-management/{path?.TrimStart('/')}{context.Request.QueryString}";
        using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), relativePath);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", serviceTokenIssuer.IssueToken(tenantId.ToString(), actorUserId.ToString(), IdentityAudience));
        request.Headers.TryAddWithoutValidation("X-Organization-Id", organizationId.ToString());
        request.Headers.TryAddWithoutValidation("X-Correlation-Id",
            context.Request.Headers["X-Correlation-Id"].FirstOrDefault() ?? context.TraceIdentifier);
        if (context.Request.Headers.TryGetValue("Idempotency-Key", out var forwardedIdempotencyKey))
            request.Headers.TryAddWithoutValidation("Idempotency-Key", forwardedIdempotencyKey.ToString());

        if (requestBody.Length > 0)
        {
            request.Content = new StringContent(requestBody, Encoding.UTF8);
            if (!string.IsNullOrWhiteSpace(context.Request.ContentType))
                request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(context.Request.ContentType);
        }

        try
        {
            var client = httpClientFactory.CreateClient("Identity");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            var content = response.StatusCode == System.Net.HttpStatusCode.NoContent
                ? null
                : await response.Content.ReadAsStringAsync(ct);
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
            if (idempotencyRecord is not null)
                return await SellingIdempotency.CompleteRawAsync(
                    db, idempotencyRecord, actorUserId, (int)response.StatusCode, content, contentType, ct);
            return content is null ? Results.StatusCode((int)response.StatusCode) :
                Results.Content(content, contentType, statusCode: (int)response.StatusCode);
        }
        catch (HttpRequestException)
        {
            return await CompleteUnknownOutcomeAsync();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return await CompleteUnknownOutcomeAsync();
        }

        async Task<IResult> CompleteUnknownOutcomeAsync()
        {
            var problem = new
            {
                type = "about:blank",
                title = "Identity service did not return a response. Check the current user or role state before retrying.",
                status = StatusCodes.Status503ServiceUnavailable,
                code = "synqlien.identity_outcome_unknown",
            };
            return idempotencyRecord is null
                ? Results.Json(problem, statusCode: StatusCodes.Status503ServiceUnavailable)
                : await SellingIdempotency.CompleteAsync(
                    db, idempotencyRecord, actorUserId,
                    StatusCodes.Status503ServiceUnavailable, problem, CancellationToken.None);
        }
    }
}
