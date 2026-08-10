using Xenia.Application.Email;
using Xenia.Application.TenantContext;

namespace Xenia.Api.Endpoints;

/// <summary>
/// CRUD and management endpoints for tenant-scoped email sources.
///
/// All routes require an authenticated tenant context (resolved from JWT).
/// Tenant isolation is enforced at the service layer — cross-tenant access
/// returns 404, indistinguishable from not found.
/// </summary>
public static class XeniaEmailSourceEndpoints
{
    public static IEndpointRouteBuilder MapXeniaEmailSourceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/email/sources");

        // GET /email/sources — list all sources for the current tenant
        group.MapGet("/", async (
            XeniaTenantContextAccessor tenantAccessor,
            IEmailSourceService service,
            CancellationToken ct) =>
        {
            var ctx = tenantAccessor.Current;
            if (ctx is null || !ctx.IsResolved)
                return Results.BadRequest(new { error = "Tenant context is required." });

            var sources = await service.GetSourcesAsync(ctx.TenantId, ct);
            return Results.Ok(new { sources, total = sources.Count });
        }).RequireAuthorization(XeniaPolicies.EmailRead);

        // GET /email/sources/{id} — single source
        group.MapGet("/{id:guid}", async (
            Guid id,
            XeniaTenantContextAccessor tenantAccessor,
            IEmailSourceService service,
            CancellationToken ct) =>
        {
            var ctx = tenantAccessor.Current;
            if (ctx is null || !ctx.IsResolved)
                return Results.BadRequest(new { error = "Tenant context is required." });

            var source = await service.GetSourceAsync(ctx.TenantId, id, ct);
            return source is null
                ? Results.NotFound(new { error = "Email source not found." })
                : Results.Ok(source);
        }).RequireAuthorization(XeniaPolicies.EmailRead);

        // POST /email/sources — create a new source
        group.MapPost("/", async (
            CreateEmailSourceRequest request,
            XeniaTenantContextAccessor tenantAccessor,
            IEmailSourceService service,
            CancellationToken ct) =>
        {
            var ctx = tenantAccessor.Current;
            if (ctx is null || !ctx.IsResolved)
                return Results.BadRequest(new { error = "Tenant context is required." });

            if (string.IsNullOrWhiteSpace(request.DisplayName))
                return Results.BadRequest(new { error = "DisplayName is required." });

            if (string.IsNullOrWhiteSpace(request.EmailAddress) || !request.EmailAddress.Contains('@'))
                return Results.BadRequest(new { error = "A valid EmailAddress is required." });

            if (string.IsNullOrWhiteSpace(request.ProviderType))
                return Results.BadRequest(new { error = "ProviderType is required." });

            if (string.IsNullOrWhiteSpace(request.AuthType))
                return Results.BadRequest(new { error = "AuthType is required." });

            try
            {
                var source = await service.CreateSourceAsync(ctx.TenantId, ctx.ActorId, request, ct);
                return Results.Created($"/email/sources/{source.Id}", source);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization(XeniaPolicies.EmailManage);

        // PUT /email/sources/{id} — update
        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateEmailSourceRequest request,
            XeniaTenantContextAccessor tenantAccessor,
            IEmailSourceService service,
            CancellationToken ct) =>
        {
            var ctx = tenantAccessor.Current;
            if (ctx is null || !ctx.IsResolved)
                return Results.BadRequest(new { error = "Tenant context is required." });

            try
            {
                var updated = await service.UpdateSourceAsync(ctx.TenantId, id, ctx.ActorId, request, ct);
                return updated is null
                    ? Results.NotFound(new { error = "Email source not found." })
                    : Results.Ok(updated);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Concurrency"))
            {
                return Results.Conflict(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization(XeniaPolicies.EmailManage);

        // DELETE /email/sources/{id}
        group.MapDelete("/{id:guid}", async (
            Guid id,
            XeniaTenantContextAccessor tenantAccessor,
            IEmailSourceService service,
            CancellationToken ct) =>
        {
            var ctx = tenantAccessor.Current;
            if (ctx is null || !ctx.IsResolved)
                return Results.BadRequest(new { error = "Tenant context is required." });

            var deleted = await service.DeleteSourceAsync(ctx.TenantId, id, ctx.ActorId, ct);
            return deleted
                ? Results.NoContent()
                : Results.NotFound(new { error = "Email source not found." });
        }).RequireAuthorization(XeniaPolicies.EmailManage);

        // PUT /email/sources/{id}/enable
        group.MapPut("/{id:guid}/enable", async (
            Guid id,
            XeniaTenantContextAccessor tenantAccessor,
            IEmailSourceService service,
            CancellationToken ct) =>
        {
            var ctx = tenantAccessor.Current;
            if (ctx is null || !ctx.IsResolved)
                return Results.BadRequest(new { error = "Tenant context is required." });

            var ok = await service.EnableSourceAsync(ctx.TenantId, id, ctx.ActorId, ct);
            return ok
                ? Results.Ok(new { source_id = id, enabled = true })
                : Results.NotFound(new { error = "Email source not found." });
        }).RequireAuthorization(XeniaPolicies.EmailManage);

        // PUT /email/sources/{id}/disable
        group.MapPut("/{id:guid}/disable", async (
            Guid id,
            XeniaTenantContextAccessor tenantAccessor,
            IEmailSourceService service,
            CancellationToken ct) =>
        {
            var ctx = tenantAccessor.Current;
            if (ctx is null || !ctx.IsResolved)
                return Results.BadRequest(new { error = "Tenant context is required." });

            var ok = await service.DisableSourceAsync(ctx.TenantId, id, ctx.ActorId, ct);
            return ok
                ? Results.Ok(new { source_id = id, enabled = false })
                : Results.NotFound(new { error = "Email source not found." });
        }).RequireAuthorization(XeniaPolicies.EmailManage);

        // POST /email/sources/{id}/validate — test connectivity
        group.MapPost("/{id:guid}/validate", async (
            Guid id,
            XeniaTenantContextAccessor tenantAccessor,
            IEmailSourceService service,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var ctx = tenantAccessor.Current;
            if (ctx is null || !ctx.IsResolved)
                return Results.BadRequest(new { error = "Tenant context is required." });

            var correlationId = httpContext.Request.Headers["X-Correlation-Id"].FirstOrDefault()
                ?? httpContext.TraceIdentifier;

            var result = await service.ValidateSourceAsync(
                ctx.TenantId, id, ctx.ActorId, correlationId, ct);

            return result.Success
                ? Results.Ok(result)
                : result.ErrorCode == "SOURCE_NOT_FOUND"
                    ? Results.NotFound(new { error = "Email source not found." })
                    : Results.Ok(result); // validation failures are 200 with result details
        }).RequireAuthorization(XeniaPolicies.EmailValidate);

        // GET /email/sources/{id}/validation-history
        group.MapGet("/{id:guid}/validation-history", async (
            Guid id,
            XeniaTenantContextAccessor tenantAccessor,
            IEmailSourceService service,
            CancellationToken ct,
            int limit = 20) =>
        {
            var ctx = tenantAccessor.Current;
            if (ctx is null || !ctx.IsResolved)
                return Results.BadRequest(new { error = "Tenant context is required." });

            var history = await service.GetValidationHistoryAsync(ctx.TenantId, id, limit, ct);
            return Results.Ok(new { source_id = id, history, total = history.Count });
        }).RequireAuthorization(XeniaPolicies.EmailRead);

        return app;
    }
}
