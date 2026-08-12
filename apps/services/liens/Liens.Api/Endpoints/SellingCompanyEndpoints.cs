using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Liens.Api.Endpoints;

public static class SellingCompanyEndpoints
{
    public static void MapSellingCompanyEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/liens/selling")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode)
            .RequireSellMode();

        group.MapGet("/lookups/company-types", GetCompanyTypes)
            .RequirePermission(LiensPermissions.LienSaleRead);
        group.MapGet("/lookups/contact-person-types", GetContactPersonTypes)
            .RequirePermission(LiensPermissions.LienSaleRead);

        group.MapGet("/companies", SearchCompanies)
            .RequirePermission(LiensPermissions.LienSaleRead);
        group.MapGet("/companies/{companyId:guid}", GetCompany)
            .RequirePermission(LiensPermissions.LienSaleRead);
        group.MapPost("/companies", CreateCompany)
            .RequirePermission(LiensPermissions.LienSaleCreate);
        group.MapPut("/companies/{companyId:guid}", UpdateCompany)
            .RequirePermission(LiensPermissions.LienSaleUpdate);
        group.MapDelete("/companies/{companyId:guid}", DeactivateCompany)
            .RequirePermission(LiensPermissions.LienSaleUpdate);
        group.MapPut("/companies/{companyId:guid}/reactivate", ReactivateCompany)
            .RequirePermission(LiensPermissions.LienSaleUpdate);

        group.MapGet("/companies/{companyId:guid}/contacts", GetContactPersons)
            .RequirePermission(LiensPermissions.LienSaleRead);
        group.MapGet("/companies/{companyId:guid}/contacts/{contactId:guid}", GetContactPerson)
            .RequirePermission(LiensPermissions.LienSaleRead);
        group.MapPost("/companies/{companyId:guid}/contacts", CreateContactPerson)
            .RequirePermission(LiensPermissions.LienSaleCreate);
        group.MapPut("/companies/{companyId:guid}/contacts/{contactId:guid}", UpdateContactPerson)
            .RequirePermission(LiensPermissions.LienSaleUpdate);
        group.MapDelete("/companies/{companyId:guid}/contacts/{contactId:guid}", DeactivateContactPerson)
            .RequirePermission(LiensPermissions.LienSaleUpdate);
        group.MapPut("/companies/{companyId:guid}/contacts/{contactId:guid}/reactivate", ReactivateContactPerson)
            .RequirePermission(LiensPermissions.LienSaleUpdate);
    }

    private static async Task<IResult> GetCompanyTypes(
        ICompanyService service, CancellationToken ct)
        => Results.Ok(new { items = await service.GetCompanyTypesAsync(ct) });

    private static async Task<IResult> GetContactPersonTypes(
        Guid companyTypeId, ICompanyService service, CancellationToken ct)
        => Results.Ok(new { items = await service.GetContactPersonTypesAsync(companyTypeId, ct) });

    private static async Task<IResult> SearchCompanies(
        ICompanyService service,
        ICurrentRequestContext context,
        string? search = null,
        Guid? companyTypeId = null,
        bool? isActive = true,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var (tenantId, orgId, _) = RequireContext(context);
        return Results.Ok(await service.SearchCompaniesAsync(
            tenantId, orgId, search, companyTypeId, isActive, page, pageSize, ct));
    }

    private static async Task<IResult> GetCompany(
        Guid companyId, ICompanyService service, ICurrentRequestContext context, CancellationToken ct)
    {
        var (tenantId, orgId, _) = RequireContext(context);
        var result = await service.GetCompanyAsync(tenantId, orgId, companyId, ct);
        return result is null ? NotFound("Company", companyId) : Results.Ok(result);
    }

    private static Task<IResult> CreateCompany(
        CreateCompanyRequest request,
        HttpRequest httpRequest,
        ICompanyService service,
        IAuditPublisher audit,
        LiensDbContext db,
        ICurrentRequestContext context,
        CancellationToken ct)
    {
        var (tenantId, orgId, userId) = RequireContext(context);
        return ExecuteMutationAsync(
            httpRequest, db, tenantId, userId, "/api/liens/selling/companies",
            "SellerOrganization", orgId.ToString(), request,
            () => service.CreateCompanyAsync(tenantId, orgId, userId, request, ct),
            response => PublishCompanyAudit(audit, "created", "create", response, tenantId, userId),
            StatusCodes.Status201Created, ct);
    }

    private static Task<IResult> UpdateCompany(
        Guid companyId,
        UpdateCompanyRequest request,
        HttpRequest httpRequest,
        ICompanyService service,
        IAuditPublisher audit,
        LiensDbContext db,
        ICurrentRequestContext context,
        CancellationToken ct)
    {
        var (tenantId, orgId, userId) = RequireContext(context);
        return ExecuteMutationAsync(
            httpRequest, db, tenantId, userId, "/api/liens/selling/companies/{companyId}",
            "Company", companyId.ToString(), request,
            () => service.UpdateCompanyAsync(tenantId, orgId, companyId, userId, request, ct),
            response => PublishCompanyAudit(audit, "updated", "update", response, tenantId, userId),
            StatusCodes.Status200OK, ct);
    }

    private static Task<IResult> DeactivateCompany(
        Guid companyId,
        HttpRequest httpRequest,
        ICompanyService service,
        IAuditPublisher audit,
        LiensDbContext db,
        ICurrentRequestContext context,
        CancellationToken ct)
        => SetCompanyActive(companyId, false, httpRequest, service, audit, db, context, ct);

    private static Task<IResult> ReactivateCompany(
        Guid companyId,
        HttpRequest httpRequest,
        ICompanyService service,
        IAuditPublisher audit,
        LiensDbContext db,
        ICurrentRequestContext context,
        CancellationToken ct)
        => SetCompanyActive(companyId, true, httpRequest, service, audit, db, context, ct);

    private static Task<IResult> SetCompanyActive(
        Guid companyId,
        bool isActive,
        HttpRequest httpRequest,
        ICompanyService service,
        IAuditPublisher audit,
        LiensDbContext db,
        ICurrentRequestContext context,
        CancellationToken ct)
    {
        var (tenantId, orgId, userId) = RequireContext(context);
        var route = isActive
            ? "/api/liens/selling/companies/{companyId}/reactivate"
            : "/api/liens/selling/companies/{companyId}";
        var request = new { isActive };
        return ExecuteMutationAsync(
            httpRequest, db, tenantId, userId, route, "Company", companyId.ToString(), request,
            () => service.SetCompanyActiveAsync(tenantId, orgId, companyId, userId, isActive, ct),
            response => PublishCompanyAudit(audit, isActive ? "reactivated" : "deactivated",
                "update", response, tenantId, userId),
            StatusCodes.Status200OK, ct);
    }

    private static async Task<IResult> GetContactPersons(
        Guid companyId,
        ICompanyService service,
        ICurrentRequestContext context,
        bool? isActive = true,
        CancellationToken ct = default)
    {
        var (tenantId, orgId, _) = RequireContext(context);
        return Results.Ok(new
        {
            items = await service.GetContactPersonsAsync(tenantId, orgId, companyId, isActive, ct),
        });
    }

    private static async Task<IResult> GetContactPerson(
        Guid companyId,
        Guid contactId,
        ICompanyService service,
        ICurrentRequestContext context,
        CancellationToken ct)
    {
        var (tenantId, orgId, _) = RequireContext(context);
        var result = await service.GetContactPersonAsync(tenantId, orgId, companyId, contactId, ct);
        return result is null ? NotFound("Company contact", contactId) : Results.Ok(result);
    }

    private static Task<IResult> CreateContactPerson(
        Guid companyId,
        CreateCompanyContactPersonRequest request,
        HttpRequest httpRequest,
        ICompanyService service,
        IAuditPublisher audit,
        LiensDbContext db,
        ICurrentRequestContext context,
        CancellationToken ct)
    {
        var (tenantId, orgId, userId) = RequireContext(context);
        return ExecuteMutationAsync(
            httpRequest, db, tenantId, userId, "/api/liens/selling/companies/{companyId}/contacts",
            "Company", companyId.ToString(), request,
            () => service.CreateContactPersonAsync(tenantId, orgId, companyId, userId, request, ct),
            response => PublishContactAuditAsync(audit, service, "created", "create", response,
                tenantId, orgId, userId, ct),
            StatusCodes.Status201Created, ct);
    }

    private static Task<IResult> UpdateContactPerson(
        Guid companyId,
        Guid contactId,
        UpdateCompanyContactPersonRequest request,
        HttpRequest httpRequest,
        ICompanyService service,
        IAuditPublisher audit,
        LiensDbContext db,
        ICurrentRequestContext context,
        CancellationToken ct)
    {
        var (tenantId, orgId, userId) = RequireContext(context);
        return ExecuteMutationAsync(
            httpRequest, db, tenantId, userId, "/api/liens/selling/companies/{companyId}/contacts/{contactId}",
            "CompanyContactPerson", contactId.ToString(), request,
            () => service.UpdateContactPersonAsync(tenantId, orgId, companyId, contactId, userId, request, ct),
            response => PublishContactAuditAsync(audit, service, "updated", "update", response,
                tenantId, orgId, userId, ct),
            StatusCodes.Status200OK, ct);
    }

    private static Task<IResult> DeactivateContactPerson(
        Guid companyId,
        Guid contactId,
        HttpRequest httpRequest,
        ICompanyService service,
        IAuditPublisher audit,
        LiensDbContext db,
        ICurrentRequestContext context,
        CancellationToken ct)
        => SetContactPersonActive(companyId, contactId, false, httpRequest, service, audit, db, context, ct);

    private static Task<IResult> ReactivateContactPerson(
        Guid companyId,
        Guid contactId,
        HttpRequest httpRequest,
        ICompanyService service,
        IAuditPublisher audit,
        LiensDbContext db,
        ICurrentRequestContext context,
        CancellationToken ct)
        => SetContactPersonActive(companyId, contactId, true, httpRequest, service, audit, db, context, ct);

    private static Task<IResult> SetContactPersonActive(
        Guid companyId,
        Guid contactId,
        bool isActive,
        HttpRequest httpRequest,
        ICompanyService service,
        IAuditPublisher audit,
        LiensDbContext db,
        ICurrentRequestContext context,
        CancellationToken ct)
    {
        var (tenantId, orgId, userId) = RequireContext(context);
        var route = isActive
            ? "/api/liens/selling/companies/{companyId}/contacts/{contactId}/reactivate"
            : "/api/liens/selling/companies/{companyId}/contacts/{contactId}";
        var request = new { isActive };
        return ExecuteMutationAsync(
            httpRequest, db, tenantId, userId, route, "CompanyContactPerson", contactId.ToString(), request,
            () => service.SetContactPersonActiveAsync(
                tenantId, orgId, companyId, contactId, userId, isActive, ct),
            response => PublishContactAuditAsync(audit, service,
                isActive ? "reactivated" : "deactivated", "update", response,
                tenantId, orgId, userId, ct),
            StatusCodes.Status200OK, ct);
    }

    private static async Task<IResult> ExecuteMutationAsync<TResponse>(
        HttpRequest request,
        LiensDbContext db,
        Guid tenantId,
        Guid userId,
        string route,
        string resourceType,
        string resourceKey,
        object requestBody,
        Func<Task<TResponse>> operation,
        Func<TResponse, Task> afterCommit,
        int statusCode,
        CancellationToken ct)
    {
        if (!SellingIdempotency.TryGetKey(request, out var key, out var error)) return error!;
        IDbContextTransaction? transaction = null;
        Liens.Domain.Entities.SellingIdempotencyRecord? startedRecord = null;
        TResponse? response = default;
        IResult? result = null;
        try
        {
            if (db.Database.IsRelational())
                transaction = await db.Database.BeginTransactionAsync(ct);

            var started = await SellingIdempotency.TryBeginAsync(
                db, tenantId, "User", userId, route, resourceType, resourceKey, key!, requestBody, ct);
            if (started.Result is not null)
            {
                if (transaction is not null) await transaction.CommitAsync(ct);
                return started.Result;
            }

            startedRecord = started.Record;
            response = await operation();
            result = await SellingIdempotency.CompleteAsync(
                db, startedRecord!, userId, statusCode, response!, ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
        }
        catch
        {
            if (transaction is not null)
                await transaction.RollbackAsync(CancellationToken.None);
            else if (startedRecord is not null)
            {
                db.ChangeTracker.Clear();
                db.SellingIdempotencyRecords.Remove(startedRecord);
                await db.SaveChangesAsync(CancellationToken.None);
            }
            else
            {
                db.ChangeTracker.Clear();
            }
            throw;
        }
        finally
        {
            if (transaction is not null) await transaction.DisposeAsync();
        }

        await afterCommit(response!);
        return result!;
    }

    private static Task PublishCompanyAudit(
        IAuditPublisher audit, string suffix, string action, CompanyResponse company,
        Guid tenantId, Guid userId)
    {
        audit.Publish($"liens.company.{suffix}", action, $"Company '{company.Name}' {suffix}",
            tenantId, userId, "Company", company.Id.ToString());
        return Task.CompletedTask;
    }

    private static async Task PublishContactAuditAsync(
        IAuditPublisher audit, ICompanyService service, string suffix, string action,
        CompanyContactPersonResponse contact, Guid tenantId, Guid orgId, Guid userId,
        CancellationToken ct)
    {
        var company = await service.GetCompanyAsync(tenantId, orgId, contact.CompanyId, ct);
        var companyName = company?.Name ?? contact.CompanyId.ToString();
        audit.Publish($"liens.company.contact.{suffix}", action,
            $"Contact '{contact.FirstName} {contact.LastName}' {suffix} for company '{companyName}'",
            tenantId, userId, "CompanyContactPerson", contact.Id.ToString());
    }

    private static (Guid TenantId, Guid OrgId, Guid UserId) RequireContext(ICurrentRequestContext context)
        => (
            context.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required."),
            context.OrgId ?? throw new UnauthorizedAccessException("Organization context is required."),
            context.UserId ?? throw new UnauthorizedAccessException("User context is required."));

    private static IResult NotFound(string resource, Guid id)
        => Results.NotFound(new
        {
            error = new { code = "not_found", message = $"{resource} '{id}' not found." },
        });
}
