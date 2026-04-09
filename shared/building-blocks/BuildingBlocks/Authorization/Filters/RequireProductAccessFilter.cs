using BuildingBlocks.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Authorization.Filters;

public sealed class RequireProductAccessFilter : IEndpointFilter
{
    private readonly string _productCode;

    public RequireProductAccessFilter(string productCode)
    {
        _productCode = productCode;
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var user = httpContext.User;

        if (user.Identity?.IsAuthenticated != true)
            return Results.Unauthorized();

        if (user.IsTenantAdminOrAbove())
            return await next(context);

        if (!user.HasProductAccess(_productCode))
        {
            var logger = httpContext.RequestServices
                .GetService(typeof(ILogger<RequireProductAccessFilter>)) as ILogger;
            logger?.LogWarning(
                "Product access denied: user={UserId} product={ProductCode} path={Path}",
                user.FindFirst("sub")?.Value, _productCode, httpContext.Request.Path);

            return ProductAccessDeniedResult.Create(
                ProductAccessDeniedException.NoProductAccess(_productCode));
        }

        return await next(context);
    }
}

public sealed class RequireProductRoleFilter : IEndpointFilter
{
    private readonly string _productCode;
    private readonly IReadOnlyList<string> _requiredRoles;

    public RequireProductRoleFilter(string productCode, IReadOnlyList<string> requiredRoles)
    {
        _productCode = productCode;
        _requiredRoles = requiredRoles;
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var user = httpContext.User;

        if (user.Identity?.IsAuthenticated != true)
            return Results.Unauthorized();

        if (user.IsTenantAdminOrAbove())
            return await next(context);

        if (!user.HasProductAccess(_productCode))
        {
            var logger = httpContext.RequestServices
                .GetService(typeof(ILogger<RequireProductRoleFilter>)) as ILogger;
            logger?.LogWarning(
                "Product access denied: user={UserId} product={ProductCode} path={Path}",
                user.FindFirst("sub")?.Value, _productCode, httpContext.Request.Path);

            return ProductAccessDeniedResult.Create(
                ProductAccessDeniedException.NoProductAccess(_productCode));
        }

        if (!user.HasProductRole(_productCode, _requiredRoles))
        {
            var logger = httpContext.RequestServices
                .GetService(typeof(ILogger<RequireProductRoleFilter>)) as ILogger;
            logger?.LogWarning(
                "Product role denied: user={UserId} product={ProductCode} required=[{Roles}] path={Path}",
                user.FindFirst("sub")?.Value, _productCode,
                string.Join(",", _requiredRoles), httpContext.Request.Path);

            return ProductAccessDeniedResult.Create(
                ProductAccessDeniedException.InsufficientProductRole(_productCode, _requiredRoles));
        }

        return await next(context);
    }
}

public sealed class RequireOrgProductAccessFilter : IEndpointFilter
{
    private readonly string _productCode;

    public RequireOrgProductAccessFilter(string productCode)
    {
        _productCode = productCode;
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var user = httpContext.User;

        if (user.Identity?.IsAuthenticated != true)
            return Results.Unauthorized();

        if (user.IsTenantAdminOrAbove())
            return await next(context);

        if (!user.HasProductAccess(_productCode))
        {
            var logger = httpContext.RequestServices
                .GetService(typeof(ILogger<RequireOrgProductAccessFilter>)) as ILogger;
            logger?.LogWarning(
                "Product access denied: user={UserId} product={ProductCode} path={Path}",
                user.FindFirst("sub")?.Value, _productCode, httpContext.Request.Path);

            return ProductAccessDeniedResult.Create(
                ProductAccessDeniedException.NoProductAccess(_productCode));
        }

        var orgIdClaim = user.FindFirst("org_id")?.Value;
        if (!Guid.TryParse(orgIdClaim, out var userOrgId))
        {
            return ProductAccessDeniedResult.Create(
                "ORG_CONTEXT_MISSING",
                "Organization context is required for this operation.",
                _productCode);
        }

        httpContext.Items["ProductAuth:OrgId"] = userOrgId;
        httpContext.Items["ProductAuth:ProductCode"] = _productCode;

        return await next(context);
    }
}
