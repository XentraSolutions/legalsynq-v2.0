using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Liens.Api.Endpoints;

public static class SettlementEndpoints
{
    public static void MapSettlementEndpoints(this WebApplication app)
    {
        // ── v2 routes ─────────────────────────────────────────────────────────
        var v2 = app.MapGroup("/api/liens/settlement")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode);

        // Reductions
        v2.MapGet("/reductions/case/{caseId:guid}", GetReductionsByCase)
            .RequirePermission(LiensPermissions.LienRead);

        v2.MapGet("/reductions/lien/{lienId:guid}", GetReductionsByLien)
            .RequirePermission(LiensPermissions.LienRead);

        v2.MapPost("/reductions", CreateReduction)
            .RequirePermission(LiensPermissions.LienUpdate);

        v2.MapPut("/reductions/{id:guid}", UpdateReduction)
            .RequirePermission(LiensPermissions.LienUpdate);

        // Settlements
        v2.MapGet("/case/{caseId:guid}", GetSettlementsByCase)
            .RequirePermission(LiensPermissions.LienRead);

        v2.MapGet("/lien/{lienId:guid}", GetSettlementsByLien)
            .RequirePermission(LiensPermissions.LienRead);

        v2.MapPost("/create", CreateSettlement)
            .RequirePermission(LiensPermissions.LienUpdate);

        v2.MapPut("/{id:guid}", UpdateSettlement)
            .RequirePermission(LiensPermissions.LienUpdate);

        // Payment details
        v2.MapGet("/payments/case/{caseId:guid}", GetPaymentsByCase)
            .RequirePermission(LiensPermissions.LienRead);

        v2.MapGet("/payments/lien/{lienId:guid}", GetPaymentsByLien)
            .RequirePermission(LiensPermissions.LienRead);

        v2.MapPost("/payments", CreatePayment)
            .RequirePermission(LiensPermissions.LienUpdate);

        v2.MapDelete("/payments/{id:guid}", DeletePayment)
            .RequirePermission(LiensPermissions.LienUpdate);

        // ── Legacy routes ─────────────────────────────────────────────────────
        // Maps legacy SynqLiens controller-style paths as aliases to the same handlers.
        var legacy = app.MapGroup("/service")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode);

        // /service/liens/update/reduction  (POST, body carries CaseId+LienId)
        legacy.MapPost("/liens/update/reduction", CreateReduction)
            .RequirePermission(LiensPermissions.LienUpdate);

        // /service/liens/update/settlement  (POST)
        legacy.MapPost("/liens/update/settlement", CreateSettlement)
            .RequirePermission(LiensPermissions.LienUpdate);

        // /service/liens/settlement/payment  (POST)
        legacy.MapPost("/liens/settlement/payment", CreatePayment)
            .RequirePermission(LiensPermissions.LienUpdate);

        // /service/delete-payment/{id}
        legacy.MapDelete("/delete-payment/{id:guid}", DeletePayment)
            .RequirePermission(LiensPermissions.LienUpdate);

        // /service/settlement/history/{caseId}  — returns settlements + reductions + payments
        legacy.MapGet("/settlement/history/{caseId:guid}", GetSettlementHistory)
            .RequirePermission(LiensPermissions.LienRead);
    }

    // ── Handlers ──────────────────────────────────────────────────────────────

    private static async Task<IResult> GetReductionsByCase(
        Guid caseId,
        ISettlementService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = CaseEndpoints.RequireTenantId(ctx);
        var result = await svc.GetReductionsByCaseAsync(tenantId, caseId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetReductionsByLien(
        Guid lienId,
        ISettlementService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = CaseEndpoints.RequireTenantId(ctx);
        var result = await svc.GetReductionsByLienAsync(tenantId, lienId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> CreateReduction(
        CreateLienReductionRequest request,
        ISettlementService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = CaseEndpoints.RequireTenantId(ctx);
        var userId   = CaseEndpoints.RequireUserId(ctx);
        var result = await svc.CreateReductionAsync(tenantId, userId, request, ct);
        return Results.Created($"/api/liens/settlement/reductions/lien/{request.LienId}", result);
    }

    private static async Task<IResult> UpdateReduction(
        Guid id,
        UpdateLienReductionRequest request,
        ISettlementService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = CaseEndpoints.RequireTenantId(ctx);
        var userId   = CaseEndpoints.RequireUserId(ctx);
        var result = await svc.UpdateReductionAsync(tenantId, id, userId, request, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetSettlementsByCase(
        Guid caseId,
        ISettlementService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = CaseEndpoints.RequireTenantId(ctx);
        var result = await svc.GetSettlementsByCaseAsync(tenantId, caseId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetSettlementsByLien(
        Guid lienId,
        ISettlementService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = CaseEndpoints.RequireTenantId(ctx);
        var result = await svc.GetSettlementsByLienAsync(tenantId, lienId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> CreateSettlement(
        CreateLienSettlementRequest request,
        ISettlementService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = CaseEndpoints.RequireTenantId(ctx);
        var userId   = CaseEndpoints.RequireUserId(ctx);
        var result = await svc.CreateSettlementAsync(tenantId, userId, request, ct);
        return Results.Created($"/api/liens/settlement/lien/{request.LienId}", result);
    }

    private static async Task<IResult> UpdateSettlement(
        Guid id,
        UpdateLienSettlementRequest request,
        ISettlementService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = CaseEndpoints.RequireTenantId(ctx);
        var userId   = CaseEndpoints.RequireUserId(ctx);
        var result = await svc.UpdateSettlementAsync(tenantId, id, userId, request, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetPaymentsByCase(
        Guid caseId,
        ISettlementService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = CaseEndpoints.RequireTenantId(ctx);
        var result = await svc.GetPaymentsByCaseAsync(tenantId, caseId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetPaymentsByLien(
        Guid lienId,
        ISettlementService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = CaseEndpoints.RequireTenantId(ctx);
        var result = await svc.GetPaymentsByLienAsync(tenantId, lienId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> CreatePayment(
        CreateSettlementPaymentDetailRequest request,
        ISettlementService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = CaseEndpoints.RequireTenantId(ctx);
        var userId   = CaseEndpoints.RequireUserId(ctx);
        var result = await svc.CreatePaymentAsync(tenantId, userId, request, ct);
        return Results.Created($"/api/liens/settlement/payments/lien/{request.LienId}", result);
    }

    private static async Task<IResult> DeletePayment(
        Guid id,
        ISettlementService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = CaseEndpoints.RequireTenantId(ctx);
        var userId   = CaseEndpoints.RequireUserId(ctx);
        await svc.DeletePaymentAsync(tenantId, id, userId, ct);
        return Results.Ok(new { isSuccess = true, message = "Successfully Deleted." });
    }

    private static async Task<IResult> GetSettlementHistory(
        Guid caseId,
        ISettlementService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = CaseEndpoints.RequireTenantId(ctx);
        var settlements = await svc.GetSettlementsByCaseAsync(tenantId, caseId, ct);
        var reductions  = await svc.GetReductionsByCaseAsync(tenantId, caseId, ct);
        var payments    = await svc.GetPaymentsByCaseAsync(tenantId, caseId, ct);
        return Results.Ok(new { settlements, reductions, payments });
    }
}
