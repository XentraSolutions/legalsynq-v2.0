using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using BuildingBlocks.Exceptions;
using Fund.Application.DTOs;
using Fund.Application.Interfaces;

namespace Fund.Api.Endpoints;

public static class ApplicationEndpoints
{
    public static void MapApplicationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/applications")
            .RequireProductAccess(ProductCodes.SynqFund);

        // ── GET /api/applications ─────────────────────────────────────────────
        // Both referrers and funders; service scopes by tenantId.
        group.MapGet("/", async (
            ICurrentRequestContext ctx,
            IApplicationService svc,
            CancellationToken ct,
            string? status = null) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var results  = await svc.GetAllAsync(tenantId, ct);

            if (!string.IsNullOrWhiteSpace(status))
                results = results.Where(a => a.Status == status).ToList();

            return Results.Ok(results);
        }).RequireAuthorization(Policies.AuthenticatedUser);

        // ── GET /api/applications/{id} ────────────────────────────────────────
        group.MapGet("/{id:guid}", async (
            Guid id,
            ICurrentRequestContext ctx,
            IApplicationService svc,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var result   = await svc.GetByIdAsync(tenantId, id, ct);
            if (result is null) throw new NotFoundException($"Application '{id}' was not found.");
            return Results.Ok(result);
        }).RequireAuthorization(Policies.AuthenticatedUser);

        // ── POST /api/applications ────────────────────────────────────────────
        // SYNQFUND_REFERRER creates draft applications.
        group.MapPost("/", async (
            CreateApplicationRequest request,
            ICurrentRequestContext ctx,
            IApplicationService svc,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var userId   = ctx.UserId   ?? throw new InvalidOperationException("sub claim is missing.");
            var result   = await svc.CreateAsync(tenantId, userId, request, ct);
            return Results.Created($"/api/applications/{result.Id}", result);
        })
        .RequireAuthorization(Policies.AuthenticatedUser)
        .RequireProductRole(ProductCodes.SynqFund, ProductRoleCodes.SynqFundReferrer);

        // ── PUT /api/applications/{id} ────────────────────────────────────────
        // Only SYNQFUND_REFERRER may update applications.
        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateApplicationRequest request,
            ICurrentRequestContext ctx,
            IApplicationService svc,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var userId   = ctx.UserId   ?? throw new InvalidOperationException("sub claim is missing.");
            var result   = await svc.UpdateAsync(tenantId, id, userId, request, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.AuthenticatedUser)
        .RequireProductRole(ProductCodes.SynqFund, ProductRoleCodes.SynqFundReferrer);

        // ── POST /api/applications/{id}/submit ────────────────────────────────
        // Draft → Submitted.  Performed by SYNQFUND_REFERRER.
        group.MapPost("/{id:guid}/submit", async (
            Guid id,
            SubmitApplicationRequest request,
            ICurrentRequestContext ctx,
            IApplicationService svc,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var userId   = ctx.UserId   ?? throw new InvalidOperationException("sub claim is missing.");
            var result   = await svc.SubmitAsync(tenantId, id, userId, request, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.AuthenticatedUser)
        .RequireProductRole(ProductCodes.SynqFund, ProductRoleCodes.SynqFundReferrer);

        // ── POST /api/applications/{id}/begin-review ──────────────────────────
        // Submitted → InReview.  Performed by SYNQFUND_FUNDER.
        group.MapPost("/{id:guid}/begin-review", async (
            Guid id,
            ICurrentRequestContext ctx,
            IApplicationService svc,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var userId   = ctx.UserId   ?? throw new InvalidOperationException("sub claim is missing.");
            var result   = await svc.BeginReviewAsync(tenantId, id, userId, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.AuthenticatedUser)
        .RequireProductRole(ProductCodes.SynqFund, ProductRoleCodes.SynqFundFunder);

        // ── POST /api/applications/{id}/approve ───────────────────────────────
        // InReview → Approved.  Performed by SYNQFUND_FUNDER.
        group.MapPost("/{id:guid}/approve", async (
            Guid id,
            ApproveApplicationRequest request,
            ICurrentRequestContext ctx,
            IApplicationService svc,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var userId   = ctx.UserId   ?? throw new InvalidOperationException("sub claim is missing.");
            var result   = await svc.ApproveAsync(tenantId, id, userId, request, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.AuthenticatedUser)
        .RequireProductRole(ProductCodes.SynqFund, ProductRoleCodes.SynqFundFunder);

        // ── POST /api/applications/{id}/deny ─────────────────────────────────
        // InReview → Rejected.  Performed by SYNQFUND_FUNDER.
        group.MapPost("/{id:guid}/deny", async (
            Guid id,
            DenyApplicationRequest request,
            ICurrentRequestContext ctx,
            IApplicationService svc,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var userId   = ctx.UserId   ?? throw new InvalidOperationException("sub claim is missing.");
            var result   = await svc.DenyAsync(tenantId, id, userId, request, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.AuthenticatedUser)
        .RequireProductRole(ProductCodes.SynqFund, ProductRoleCodes.SynqFundFunder);
    }
}
