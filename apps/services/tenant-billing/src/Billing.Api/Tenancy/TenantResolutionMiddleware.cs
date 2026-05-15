using Billing.Api.LegalSynq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Options;

namespace Billing.Api.Tenancy;

/// <summary>
/// Resolves the active tenant for billing API requests.
///
/// **Standalone / default mode** (<c>LegalSynq:TenantContext:Enabled = false</c>):
/// reads the <c>X-Tenant-Id</c> request header, unchanged from the original
/// implementation. Zero behavior change on deploy.
///
/// **LegalSynq integration mode** (<c>LegalSynq:TenantContext:Enabled = true</c>):
/// delegates to <see cref="ITenantIdentityContextResolver"/>, which applies
/// the dual-mode hierarchy (JWT claim → internal-service header → header fallback).
///
/// In both modes the resolved <see cref="Guid"/> is stashed in
/// <see cref="HttpContext.Items"/> under <see cref="HttpContextItemKey"/> so
/// <see cref="HttpHeaderTenantContext"/> can read it.
/// </summary>
public sealed class TenantResolutionMiddleware
{
    public const string HeaderName = "X-Tenant-Id";
    public const string HttpContextItemKey = "Billing:TenantId";
    public const string ProtectedPathPrefix = "/api/";

    /// <summary>
    /// Path prefixes that live under <see cref="ProtectedPathPrefix"/>
    /// but are NOT tenant-scoped (Platform Billing surfaces). Requests
    /// matching one of these prefixes bypass the tenant check entirely.
    /// </summary>
    private static readonly string[] UnscopedPathPrefixes =
    {
        "/api/invoice-templates/platform",
    };

    private readonly RequestDelegate _next;
    private readonly ProblemDetailsFactory _problemDetailsFactory;
    private readonly ITenantIdentityContextResolver? _resolver;
    private readonly bool _legalSynqEnabled;

    public TenantResolutionMiddleware(
        RequestDelegate next,
        ProblemDetailsFactory problemDetailsFactory,
        IOptions<LegalSynqTenantContextOptions> legalSynqOpts,
        ITenantIdentityContextResolver? resolver = null)
    {
        _next = next;
        _problemDetailsFactory = problemDetailsFactory;
        _legalSynqEnabled = legalSynqOpts.Value.Enabled;
        _resolver = resolver;
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

        foreach (var prefix in UnscopedPathPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }
        }

        if (_legalSynqEnabled && _resolver is not null)
        {
            await ResolveViaLegalSynqAsync(context);
        }
        else
        {
            await ResolveViaHeaderAsync(context);
        }
    }

    private async Task ResolveViaLegalSynqAsync(HttpContext context)
    {
        var result = await _resolver!.ResolveAsync(context);
        if (!result.IsResolved)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status400BadRequest,
                result.FailureReason ?? "Tenant could not be resolved.");
            return;
        }

        context.Items[HttpContextItemKey] = result.TenantId;
        await _next(context);
    }

    private async Task ResolveViaHeaderAsync(HttpContext context)
    {
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
