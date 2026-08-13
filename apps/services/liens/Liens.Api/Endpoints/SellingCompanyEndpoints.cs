using System.Globalization;
using System.Text;
using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using BuildingBlocks.Exceptions;
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
        group.MapPost("/lookups/contact-person-types", CreateContactPersonType)
            .RequirePermission(LiensPermissions.LienSaleCreate);

        group.MapGet("/companies", SearchCompanies)
            .RequirePermission(LiensPermissions.LienSaleRead);
        group.MapGet("/companies/export", ExportCompanies)
            .RequirePermission(LiensPermissions.LienSaleRead);
        group.MapGet("/companies/{companyId:guid}", GetCompany)
            .RequirePermission(LiensPermissions.LienSaleRead);
        group.MapGet("/company-details/{companyId:guid}", GetCompanyDetails)
            .RequirePermission(LiensPermissions.LienSaleRead);
        group.MapPost("/companies", CreateCompany)
            .RequirePermission(LiensPermissions.LienSaleCreate);
        group.MapPut("/companies/{companyId:guid}", UpdateCompany)
            .RequirePermission(LiensPermissions.LienSaleUpdate);
        group.MapPost("/companies/{companyId:guid}/reassign", ReassignCompany)
            .RequirePermission(LiensPermissions.LienSaleUpdate);
        group.MapDelete("/companies/{companyId:guid}", DeactivateCompany)
            .RequirePermission(LiensPermissions.LienSaleUpdate);
        group.MapPut("/companies/{companyId:guid}/reactivate", ReactivateCompany)
            .RequirePermission(LiensPermissions.LienSaleUpdate);

        group.MapGet("/companies/{companyId:guid}/contacts", GetContactPersons)
            .RequirePermission(LiensPermissions.LienSaleRead);
        group.MapGet("/contact-person", GetContactPersonDirectory)
            .RequirePermission(LiensPermissions.LienSaleRead);
        group.MapGet("/contacts/export", ExportContactPersons)
            .RequirePermission(LiensPermissions.LienSaleRead);
        group.MapGet("/companies/{companyId:guid}/contacts/export", ExportContactPersonsByCompany)
            .RequirePermission(LiensPermissions.LienSaleRead);
        group.MapGet("/companies/{companyId:guid}/contacts/{contactId:guid}", GetContactPerson)
            .RequirePermission(LiensPermissions.LienSaleRead);
        group.MapPost("/companies/{companyId:guid}/contacts", CreateContactPerson)
            .RequirePermission(LiensPermissions.LienSaleCreate);
        group.MapPut("/companies/{companyId:guid}/contacts/{contactId:guid}", UpdateContactPerson)
            .RequirePermission(LiensPermissions.LienSaleUpdate);
        group.MapPost("/companies/{companyId:guid}/contacts/{contactId:guid}/reassign", ReassignContactPerson)
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
        Guid companyTypeId,
        ICompanyService service,
        ICurrentRequestContext context,
        CancellationToken ct)
    {
        var (tenantId, orgId, _) = RequireContext(context);
        return Results.Ok(new
        {
            items = await service.GetContactPersonTypesAsync(tenantId, orgId, companyTypeId, ct),
        });
    }

    private static Task<IResult> CreateContactPersonType(
        CreateContactPersonTypeRequest request,
        HttpRequest httpRequest,
        ICompanyService service,
        IAuditPublisher audit,
        LiensDbContext db,
        ICurrentRequestContext context,
        CancellationToken ct)
    {
        var (tenantId, orgId, userId) = RequireContext(context);
        return ExecuteMutationAsync(
            httpRequest, db, tenantId, userId,
            "/api/liens/selling/lookups/contact-person-types",
            "CompanyType", request.CompanyTypeId.ToString(), request,
            () => service.CreateContactPersonTypeAsync(tenantId, orgId, userId, request, ct),
            response => PublishContactPersonTypeAudit(audit, response, tenantId, userId),
            StatusCodes.Status201Created, ct);
    }

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

    private static async Task<IResult> ExportCompanies(
        ICompanyService service,
        ICurrentRequestContext context,
        string? search = null,
        Guid? companyTypeId = null,
        bool? isActive = true,
        CancellationToken ct = default)
    {
        var (tenantId, orgId, _) = RequireContext(context);
        var companies = await service.GetCompaniesForExportAsync(
            tenantId, orgId, search, companyTypeId, isActive, ct);
        return CsvFile(BuildCompaniesCsv(companies), "selling-companies.csv");
    }

    private static async Task<IResult> GetCompany(
        Guid companyId, ICompanyService service, ICurrentRequestContext context, CancellationToken ct)
    {
        var (tenantId, orgId, _) = RequireContext(context);
        var result = await service.GetCompanyAsync(tenantId, orgId, companyId, ct);
        return result is null ? NotFound("Company", companyId) : Results.Ok(result);
    }

    private static async Task<IResult> GetCompanyDetails(
        Guid companyId,
        ICompanyService service,
        ICurrentRequestContext context,
        int page = 1,
        int pageSize = 4,
        CancellationToken ct = default)
    {
        var (tenantId, orgId, _) = RequireContext(context);
        var result = await service.GetCompanyDetailsAsync(
            tenantId, orgId, companyId, page, pageSize, ct);
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

    private static Task<IResult> ReassignCompany(
        Guid companyId,
        ReassignCompanyRequest request,
        HttpRequest httpRequest,
        ICompanyService service,
        IAuditPublisher audit,
        LiensDbContext db,
        ICurrentRequestContext context,
        CancellationToken ct)
    {
        var (tenantId, orgId, userId) = RequireContext(context);
        return ExecuteMutationAsync(
            httpRequest, db, tenantId, userId, "/api/liens/selling/companies/{companyId}/reassign",
            "Company", companyId.ToString(), request,
            () => service.ReassignCompanyAsync(
                tenantId, orgId, companyId, request.TargetCompanyId, userId, ct),
            response => PublishCompanyReassignmentAudit(audit, response, tenantId, userId),
            StatusCodes.Status200OK, ct);
    }

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

    private static async Task<IResult> GetContactPersonDirectory(
        ICompanyService service,
        ICurrentRequestContext context,
        string? search = null,
        Guid? companyTypeId = null,
        string? contactPersonTypeId = null,
        bool? isActive = true,
        string? filter = null,
        int page = 1,
        int pageSize = 20,
        int? limit = null,
        CancellationToken ct = default)
    {
        var (tenantId, orgId, _) = RequireContext(context);
        return Results.Ok(await service.SearchContactPersonsAsync(
            tenantId, orgId,
            string.IsNullOrWhiteSpace(search) ? filter : search,
            companyTypeId,
            ParseOptionalContactPersonTypeId(contactPersonTypeId),
            isActive,
            page,
            limit ?? pageSize,
            ct));
    }

    private static Task<IResult> ExportContactPersons(
        ICompanyService service,
        ICurrentRequestContext context,
        string? search = null,
        Guid? companyTypeId = null,
        string? contactPersonTypeId = null,
        bool? isActive = true,
        CancellationToken ct = default)
        => ExportContactPersonsCore(
            null, service, context, search, companyTypeId,
            ParseOptionalContactPersonTypeId(contactPersonTypeId), isActive,
            "selling-contact-persons.csv", ct);

    private static Task<IResult> ExportContactPersonsByCompany(
        Guid companyId,
        ICompanyService service,
        ICurrentRequestContext context,
        string? search = null,
        string? contactPersonTypeId = null,
        bool? isActive = true,
        CancellationToken ct = default)
        => ExportContactPersonsCore(
            companyId, service, context, search, null,
            ParseOptionalContactPersonTypeId(contactPersonTypeId), isActive,
            $"selling-company-{companyId:D}-contact-persons.csv", ct);

    private static Guid? ParseOptionalContactPersonTypeId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var candidate = value.Trim();
        if (candidate.Equals("null", StringComparison.OrdinalIgnoreCase)) return null;
        if (Guid.TryParse(candidate, out var id)) return id;

        const string message = "contactPersonTypeId must be a valid GUID, empty, or null.";
        throw new ValidationException(
            "Contact-person type filter is invalid.",
            new Dictionary<string, string[]> { ["contactPersonTypeId"] = [message] });
    }

    private static async Task<IResult> ExportContactPersonsCore(
        Guid? companyId,
        ICompanyService service,
        ICurrentRequestContext context,
        string? search,
        Guid? companyTypeId,
        Guid? contactPersonTypeId,
        bool? isActive,
        string fileName,
        CancellationToken ct)
    {
        var (tenantId, orgId, _) = RequireContext(context);
        var contacts = await service.GetContactPersonsForExportAsync(
            tenantId, orgId, companyId, search, companyTypeId, contactPersonTypeId, isActive, ct);
        return CsvFile(BuildContactPersonsCsv(contacts), fileName);
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

    private static Task<IResult> ReassignContactPerson(
        Guid companyId,
        Guid contactId,
        ReassignCompanyContactPersonRequest request,
        HttpRequest httpRequest,
        ICompanyService service,
        IAuditPublisher audit,
        LiensDbContext db,
        ICurrentRequestContext context,
        CancellationToken ct)
    {
        var (tenantId, orgId, userId) = RequireContext(context);
        return ExecuteMutationAsync(
            httpRequest, db, tenantId, userId,
            "/api/liens/selling/companies/{companyId}/contacts/{contactId}/reassign",
            "CompanyContactPerson", contactId.ToString(), request,
            () => service.ReassignContactPersonAsync(
                tenantId, orgId, companyId, contactId,
                request.TargetContactPersonId, userId, ct),
            response => PublishContactPersonReassignmentAudit(audit, response, tenantId, userId),
            StatusCodes.Status200OK, ct);
    }

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

    private static Task PublishCompanyReassignmentAudit(
        IAuditPublisher audit,
        CompanyReassignmentResponse response,
        Guid tenantId,
        Guid userId)
    {
        audit.Publish("liens.company.reassigned", "update",
            $"Company '{response.SourceCompanyName}' reassigned to '{response.TargetCompanyName}' " +
            $"({response.TotalReassignedCount} records)",
            tenantId, userId, "Company", response.SourceCompanyId.ToString());
        return Task.CompletedTask;
    }

    private static Task PublishContactPersonTypeAudit(
        IAuditPublisher audit,
        ContactPersonTypeResponse response,
        Guid tenantId,
        Guid userId)
    {
        audit.Publish("liens.company.contact_type.created", "create",
            $"Contact-person type '{response.Name}' created",
            tenantId, userId, "ContactPersonType", response.Id.ToString());
        return Task.CompletedTask;
    }

    private static Task PublishContactPersonReassignmentAudit(
        IAuditPublisher audit,
        CompanyContactPersonReassignmentResponse response,
        Guid tenantId,
        Guid userId)
    {
        audit.Publish("liens.company.contact.reassigned", "update",
            $"Contact '{response.SourceContactPersonName}' reassigned to " +
            $"'{response.TargetContactPersonName}' ({response.TotalReassignedCount} records)",
            tenantId, userId, "CompanyContactPerson", response.SourceContactPersonId.ToString());
        return Task.CompletedTask;
    }

    private static string BuildCompaniesCsv(IReadOnlyList<CompanyResponse> companies)
    {
        var csv = new StringBuilder();
        AppendCsvRow(csv,
        [
            "Id", "CompanyTypeId", "CompanyTypeCode", "CompanyTypeName", "LinkedTenantId",
            "Name", "AddressLine1", "City", "State", "PostalCode", "Phone", "Email",
            "IsActive", "CreatedAtUtc", "UpdatedAtUtc",
        ]);
        foreach (var company in companies)
        {
            AppendCsvRow(csv,
            [
                company.Id.ToString("D"), company.CompanyTypeId.ToString("D"),
                company.CompanyTypeCode, company.CompanyTypeName, company.LinkedTenantId?.ToString("D"),
                company.Name, company.AddressLine1, company.City, company.State, company.PostalCode,
                company.Phone, company.Email, company.IsActive ? "true" : "false",
                company.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture),
                company.UpdatedAtUtc.ToString("O", CultureInfo.InvariantCulture),
            ]);
        }
        return csv.ToString();
    }

    private static string BuildContactPersonsCsv(IReadOnlyList<CompanyContactPersonExportResponse> contacts)
    {
        var csv = new StringBuilder();
        AppendCsvRow(csv,
        [
            "Id", "CompanyId", "CompanyName", "CompanyTypeId", "CompanyTypeCode", "CompanyTypeName",
            "ContactPersonTypeId", "ContactPersonTypeCode", "ContactPersonTypeName", "FirstName",
            "LastName", "AddressLine1", "City", "State", "PostalCode", "Phone", "Email",
            "IsActive", "CreatedAtUtc", "UpdatedAtUtc",
        ]);
        foreach (var contact in contacts)
        {
            AppendCsvRow(csv,
            [
                contact.Id.ToString("D"), contact.CompanyId.ToString("D"), contact.CompanyName,
                contact.CompanyTypeId.ToString("D"), contact.CompanyTypeCode, contact.CompanyTypeName,
                contact.ContactPersonTypeId.ToString("D"), contact.ContactPersonTypeCode,
                contact.ContactPersonTypeName, contact.FirstName, contact.LastName, contact.AddressLine1,
                contact.City, contact.State, contact.PostalCode, contact.Phone, contact.Email,
                contact.IsActive ? "true" : "false",
                contact.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture),
                contact.UpdatedAtUtc.ToString("O", CultureInfo.InvariantCulture),
            ]);
        }
        return csv.ToString();
    }

    private static void AppendCsvRow(StringBuilder csv, IEnumerable<string?> values)
        => csv.AppendLine(string.Join(',', values.Select(EscapeCsvField)));

    private static string EscapeCsvField(string? value)
    {
        if (value is null) return string.Empty;

        var candidate = value.TrimStart();
        var safeValue = candidate.Length > 0 && candidate[0] is '=' or '+' or '-' or '@' or '\t' or '\r'
            ? $"'{value}"
            : value;
        return safeValue.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $"\"{safeValue.Replace("\"", "\"\"")}\""
            : safeValue;
    }

    private static IResult CsvFile(string csv, string fileName)
        => Results.File(Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", fileName);

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
