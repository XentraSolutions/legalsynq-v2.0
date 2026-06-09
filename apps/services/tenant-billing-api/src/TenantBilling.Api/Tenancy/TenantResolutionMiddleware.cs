using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace TenantBilling.Api.Tenancy;

/// <summary>
/// Resolves the active tenant for billing API requests from the
/// <c>X-Tenant-Id</c> request header. The parsed value is stashed in
/// <see cref="HttpContext.Items"/> for <see cref="HttpHeaderTenantContext"/>
/// to read.
///
/// Requests under <see cref="ProtectedPathPrefix"/> that omit the header,
/// pass a non-GUID value, or pass <see cref="Guid.Empty"/> are rejected with
/// HTTP 400 so handlers can never run without a tenant filter.
/// </summary>
public sealed class TenantResolutionMiddleware
{
    public const string HeaderName = "X-Tenant-Id";
    public const string HttpContextItemKey = "TenantBilling:TenantId";
    public const string ProtectedPathPrefix = "/api/";

    /// <summary>
    /// Path prefixes that live under <see cref="ProtectedPathPrefix"/>
    /// but are NOT tenant-scoped (Platform Billing surfaces). Requests
    /// matching one of these prefixes bypass the X-Tenant-Id check
    /// entirely. The controllers behind them never read
    /// <see cref="ITenantContext"/>.
    /// </summary>
    private static readonly string[] UnscopedPathPrefixes =
    {
        "/api/invoice-templates/platform",
    };

    private readonly RequestDelegate _next;
    private readonly ProblemDetailsFactory _problemDetailsFactory;

    public TenantResolutionMiddleware(RequestDelegate next, ProblemDetailsFactory problemDetailsFactory)
    {
        _next = next;
        _problemDetailsFactory = problemDetailsFactory;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var requiresTenant = path.StartsWith(ProtectedPathPrefix, StringComparison.OrdinalIgnoreCase);

        if (!requiresTenant)
        {
            await _next(context);
            return;
        }

        // Honour the unscoped-prefix whitelist BEFORE checking the
        // header so Platform Billing endpoints (e.g. platform invoice
        // templates) don't have to send a meaningless tenant id.
        foreach (var prefix in UnscopedPathPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var values)
            || values.Count == 0
            || string.IsNullOrWhiteSpace(values[0]))
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status400BadRequest,
                $"Missing required '{HeaderName}' header.");
            return;
        }

        if (!Guid.TryParse(values[0], out var tenantId) || tenantId == Guid.Empty)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status400BadRequest,
                $"Header '{HeaderName}' must be a non-empty GUID.");
            return;
        }

        context.Items[HttpContextItemKey] = tenantId;
        await _next(context);
    }

    private async Task WriteProblemAsync(HttpContext context, int statusCode, string detail)
    {
        var problem = _problemDetailsFactory.CreateProblemDetails(
            context,
            statusCode: statusCode,
            title: "Tenant resolution failed",
            detail: detail);

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem);
    }
}
