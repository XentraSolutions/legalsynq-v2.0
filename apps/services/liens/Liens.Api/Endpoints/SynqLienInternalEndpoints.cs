using BuildingBlocks.Context;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain.Entities;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;

namespace Liens.Api.Endpoints;

public static class SynqLienInternalEndpoints
{
    public static void MapSynqLienInternalEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/internal/synqlien")
            .RequireAuthorization("SynqLienInternal");

        group.MapGet("/cases/{id:guid}", async (
            Guid id, ICaseService service, ICurrentRequestContext context, CancellationToken ct) =>
        {
            var tenantId = RequireTenant(context);
            var result = await service.GetByIdAsync(tenantId, id, ct);
            return result is null ? Results.NotFound() : Results.Ok(new { id = result.Id, caseNumber = result.CaseNumber });
        });

        group.MapPost("/cases", async (
            CreateCaseRequest request, ICaseService service, ICurrentRequestContext context,
            HttpRequest httpRequest, CancellationToken ct) =>
        {
            var tenantId = RequireTenant(context);
            var orgId = RequireOrg(httpRequest);
            var userId = context.UserId ?? throw new UnauthorizedAccessException("Actor context is required.");
            var idempotencyKey = httpRequest.Headers["Idempotency-Key"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(request.ExternalReference) && !string.IsNullOrWhiteSpace(idempotencyKey))
                request = request with { ExternalReference = idempotencyKey };
            var result = await service.CreateAsync(tenantId, orgId, userId, request, ct);
            return Results.Created($"/api/liens/cases/{result.Id}", new { id = result.Id, caseNumber = result.CaseNumber });
        });

        group.MapPost("/liens", async (
            CreateLienRequest request, ILienService service, ICurrentRequestContext context,
            HttpRequest httpRequest, CancellationToken ct) =>
        {
            var tenantId = RequireTenant(context);
            var orgId = RequireOrg(httpRequest);
            var userId = context.UserId ?? throw new UnauthorizedAccessException("Actor context is required.");
            var idempotencyKey = httpRequest.Headers["Idempotency-Key"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(request.ExternalReference) && !string.IsNullOrWhiteSpace(idempotencyKey))
                request = request with { ExternalReference = idempotencyKey };
            var result = await service.CreateAsync(tenantId, orgId, userId, request, ct);
            return Results.Created($"/api/liens/liens/{result.Id}", new
            {
                id = result.Id, lienNumber = result.LienNumber, caseId = result.CaseId
            });
        });

        group.MapPost("/document-associations", async (
            SynqLienDocumentAssociationRequest request,
            LiensDbContext db,
            ICurrentRequestContext context,
            HttpRequest httpRequest,
            CancellationToken ct) =>
        {
            var tenantId = RequireTenant(context);
            if (request.DocumentId == Guid.Empty || request.TargetId == Guid.Empty ||
                !request.TargetType.Equals("CASE", StringComparison.OrdinalIgnoreCase) &&
                !request.TargetType.Equals("LIEN", StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest(new { error = new { code = "invalid_association_request" } });

            var key = httpRequest.Headers["Idempotency-Key"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(key))
                return Results.BadRequest(new { error = new { code = "idempotency_key_required" } });

            var existing = await db.SynqLienDocumentAssociations.AsNoTracking()
                .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.IdempotencyKey == key, ct);
            if (existing is not null)
                return Results.Ok(new { data = new { associationId = existing.Id } });

            var targetExists = request.TargetType.Equals("CASE", StringComparison.OrdinalIgnoreCase)
                ? await db.Cases.AsNoTracking().AnyAsync(x => x.TenantId == tenantId && x.Id == request.TargetId, ct)
                : await db.Liens.AsNoTracking().AnyAsync(x => x.TenantId == tenantId && x.Id == request.TargetId, ct);
            if (!targetExists)
                return Results.NotFound(new { error = new { code = "target_not_found" } });

            if (request.TargetType.Equals("LIEN", StringComparison.OrdinalIgnoreCase) &&
                request.RelatedCaseId.HasValue)
            {
                var relationshipValid = await db.Liens.AsNoTracking().AnyAsync(
                    x => x.TenantId == tenantId &&
                         x.Id == request.TargetId &&
                         x.CaseId == request.RelatedCaseId.Value,
                    ct);
                if (!relationshipValid)
                    return Results.Conflict(new { error = new { code = "lien_case_relationship_mismatch" } });
            }

            var actor = context.UserId ?? Guid.Empty;
            if (actor == Guid.Empty)
                return Results.Unauthorized();
            var association = new SynqLienDocumentAssociation
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenantId,
                DocumentId = request.DocumentId,
                DocumentReference = request.DocumentReference?.Trim() ?? string.Empty,
                DocumentRole = request.DocumentRole.Trim(),
                TargetType = request.TargetType.Trim().ToUpperInvariant(),
                TargetId = request.TargetId,
                RelatedCaseId = request.RelatedCaseId,
                IdempotencyKey = key.Trim(),
                CreatedByUserId = actor,
                CreatedAtUtc = DateTime.UtcNow,
            };
            db.SynqLienDocumentAssociations.Add(association);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                var concurrent = await db.SynqLienDocumentAssociations.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.IdempotencyKey == key, ct);
                if (concurrent is not null)
                    return Results.Ok(new { data = new { associationId = concurrent.Id } });
                throw;
            }

            return Results.Created($"/api/internal/synqlien/document-associations/{association.Id}",
                new { data = new { associationId = association.Id } });
        });
    }

    private static Guid RequireTenant(ICurrentRequestContext context) =>
        context.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");

    private static Guid RequireOrg(HttpRequest request) =>
        Guid.TryParse(request.Headers["X-Org-Id"].FirstOrDefault(), out var value) && value != Guid.Empty
            ? value
            : throw new BadHttpRequestException("X-Org-Id is required.");

    private sealed record SynqLienDocumentAssociationRequest(
        Guid DocumentId,
        string TargetType,
        Guid TargetId,
        string DocumentRole,
        string? DocumentReference,
        Guid? RelatedCaseId);
}