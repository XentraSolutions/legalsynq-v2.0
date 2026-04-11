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

        var userId = user.FindFirst("sub")?.Value;
        var tenantId = user.FindFirst("tenant_id")?.Value;
        var accessVersion = user.FindFirst("access_version")?.Value;
        var path = httpContext.Request.Path.Value;
        var method = httpContext.Request.Method;

        if (user.IsTenantAdminOrAbove())
        {
            LogAuthzDecision(httpContext, "ALLOW", userId, tenantId, path, method,
                _productCode, null, "AdminBypass", accessVersion);
            return await next(context);
        }

        if (!user.HasProductAccess(_productCode))
        {
            LogAuthzDecision(httpContext, "DENY", userId, tenantId, path, method,
                _productCode, null, "NoProductAccess", accessVersion);

            return ProductAccessDeniedResult.Create(
                ProductAccessDeniedException.NoProductAccess(_productCode));
        }

        LogAuthzDecision(httpContext, "ALLOW", userId, tenantId, path, method,
            _productCode, null, "ProductClaim", accessVersion);

        return await next(context);
    }

    private static void LogAuthzDecision(
        HttpContext ctx, string result, string? userId, string? tenantId,
        string? path, string method, string product, string? requiredRole,
        string source, string? accessVersion)
    {
        var logger = ctx.RequestServices.GetService(typeof(ILogger<RequireProductAccessFilter>)) as ILogger;
        if (logger == null) return;

        if (result == "DENY")
        {
            logger.LogWarning(
                "AuthzDecision: result={Result} user={UserId} tenant={TenantId} method={Method} endpoint={Path} product={Product} source={Source} accessVersion={AccessVersion}",
                result, userId, tenantId, method, path, product, source, accessVersion);
        }
        else
        {
            logger.LogInformation(
                "AuthzDecision: result={Result} user={UserId} tenant={TenantId} method={Method} endpoint={Path} product={Product} source={Source} accessVersion={AccessVersion}",
                result, userId, tenantId, method, path, product, source, accessVersion);
        }
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

        var userId = user.FindFirst("sub")?.Value;
        var tenantId = user.FindFirst("tenant_id")?.Value;
        var accessVersion = user.FindFirst("access_version")?.Value;
        var path = httpContext.Request.Path.Value;
        var method = httpContext.Request.Method;
        var rolesStr = string.Join(",", _requiredRoles);

        if (user.IsTenantAdminOrAbove())
        {
            LogAuthzDecision(httpContext, "ALLOW", userId, tenantId, path, method,
                _productCode, rolesStr, "AdminBypass", accessVersion);
            return await next(context);
        }

        if (!user.HasProductAccess(_productCode))
        {
            LogAuthzDecision(httpContext, "DENY", userId, tenantId, path, method,
                _productCode, rolesStr, "NoProductAccess", accessVersion);

            return ProductAccessDeniedResult.Create(
                ProductAccessDeniedException.NoProductAccess(_productCode));
        }

        if (!user.HasProductRole(_productCode, _requiredRoles))
        {
            LogAuthzDecision(httpContext, "DENY", userId, tenantId, path, method,
                _productCode, rolesStr, "InsufficientRole", accessVersion);

            return ProductAccessDeniedResult.Create(
                ProductAccessDeniedException.InsufficientProductRole(_productCode, _requiredRoles));
        }

        LogAuthzDecision(httpContext, "ALLOW", userId, tenantId, path, method,
            _productCode, rolesStr, "RoleClaim", accessVersion);

        return await next(context);
    }

    private static void LogAuthzDecision(
        HttpContext ctx, string result, string? userId, string? tenantId,
        string? path, string method, string product, string? requiredRoles,
        string source, string? accessVersion)
    {
        var logger = ctx.RequestServices.GetService(typeof(ILogger<RequireProductRoleFilter>)) as ILogger;
        if (logger == null) return;

        if (result == "DENY")
        {
            logger.LogWarning(
                "AuthzDecision: result={Result} user={UserId} tenant={TenantId} method={Method} endpoint={Path} product={Product} requiredRoles=[{RequiredRoles}] source={Source} accessVersion={AccessVersion}",
                result, userId, tenantId, method, path, product, requiredRoles, source, accessVersion);
        }
        else
        {
            logger.LogInformation(
                "AuthzDecision: result={Result} user={UserId} tenant={TenantId} method={Method} endpoint={Path} product={Product} requiredRoles=[{RequiredRoles}] source={Source} accessVersion={AccessVersion}",
                result, userId, tenantId, method, path, product, requiredRoles, source, accessVersion);
        }
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

        var userId = user.FindFirst("sub")?.Value;
        var tenantId = user.FindFirst("tenant_id")?.Value;
        var accessVersion = user.FindFirst("access_version")?.Value;
        var path = httpContext.Request.Path.Value;
        var method = httpContext.Request.Method;

        if (user.IsTenantAdminOrAbove())
        {
            LogAuthzDecision(httpContext, "ALLOW", userId, tenantId, path, method,
                _productCode, "AdminBypass", accessVersion);
            return await next(context);
        }

        if (!user.HasProductAccess(_productCode))
        {
            LogAuthzDecision(httpContext, "DENY", userId, tenantId, path, method,
                _productCode, "NoProductAccess", accessVersion);

            return ProductAccessDeniedResult.Create(
                ProductAccessDeniedException.NoProductAccess(_productCode));
        }

        var orgIdClaim = user.FindFirst("org_id")?.Value;
        if (!Guid.TryParse(orgIdClaim, out var userOrgId))
        {
            LogAuthzDecision(httpContext, "DENY", userId, tenantId, path, method,
                _productCode, "OrgContextMissing", accessVersion);

            return ProductAccessDeniedResult.Create(
                "ORG_CONTEXT_MISSING",
                "Organization context is required for this operation.",
                _productCode);
        }

        httpContext.Items["ProductAuth:OrgId"] = userOrgId;
        httpContext.Items["ProductAuth:ProductCode"] = _productCode;

        LogAuthzDecision(httpContext, "ALLOW", userId, tenantId, path, method,
            _productCode, "OrgProductClaim", accessVersion);

        return await next(context);
    }

    private static void LogAuthzDecision(
        HttpContext ctx, string result, string? userId, string? tenantId,
        string? path, string method, string product,
        string source, string? accessVersion)
    {
        var logger = ctx.RequestServices.GetService(typeof(ILogger<RequireOrgProductAccessFilter>)) as ILogger;
        if (logger == null) return;

        if (result == "DENY")
        {
            logger.LogWarning(
                "AuthzDecision: result={Result} user={UserId} tenant={TenantId} method={Method} endpoint={Path} product={Product} source={Source} accessVersion={AccessVersion}",
                result, userId, tenantId, method, path, product, source, accessVersion);
        }
        else
        {
            logger.LogInformation(
                "AuthzDecision: result={Result} user={UserId} tenant={TenantId} method={Method} endpoint={Path} product={Product} source={Source} accessVersion={AccessVersion}",
                result, userId, tenantId, method, path, product, source, accessVersion);
        }
    }
}

public sealed class RequirePermissionFilter : IEndpointFilter
{
    private readonly string _permissionCode;
    private readonly string[]? _fallbackRoles;

    public RequirePermissionFilter(string permissionCode, string[]? fallbackRoles = null)
    {
        _permissionCode = permissionCode;
        _fallbackRoles = fallbackRoles;
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var user = httpContext.User;

        if (user.Identity?.IsAuthenticated != true)
            return Results.Unauthorized();

        var userId = user.FindFirst("sub")?.Value;
        var tenantId = user.FindFirst("tenant_id")?.Value;
        var accessVersion = user.FindFirst("access_version")?.Value;
        var path = httpContext.Request.Path.Value;
        var method = httpContext.Request.Method;

        if (user.IsTenantAdminOrAbove())
        {
            LogPermissionDecision(httpContext, "ALLOW", userId, tenantId, path, method,
                _permissionCode, "AdminBypass", accessVersion);
            return await next(context);
        }

        if (user.HasPermission(_permissionCode))
        {
            var policyResult = await EvaluatePoliciesIfEnabled(httpContext, user, _permissionCode);
            if (policyResult != null && !policyResult.Allowed)
            {
                LogPermissionDecision(httpContext, "DENY", userId, tenantId, path, method,
                    _permissionCode, $"PolicyDenied:{policyResult.Reason}", accessVersion);

                return ProductAccessDeniedResult.Create(
                    "POLICY_DENIED",
                    $"Permission '{_permissionCode}' denied by policy: {policyResult.Reason}",
                    ExtractProductCode(_permissionCode) ?? "");
            }

            LogPermissionDecision(httpContext, "ALLOW", userId, tenantId, path, method,
                _permissionCode, policyResult != null ? "PermissionClaim+PolicyPass" : "PermissionClaim", accessVersion);
            return await next(context);
        }

        if (_fallbackRoles is { Length: > 0 } && IsRoleFallbackEnabled(httpContext))
        {
            var productCode = ExtractProductCode(_permissionCode);
            if (productCode != null && user.HasProductRole(productCode, _fallbackRoles))
            {
                LogPermissionDecision(httpContext, "ALLOW", userId, tenantId, path, method,
                    _permissionCode, "RoleFallback", accessVersion);
                return await next(context);
            }
        }

        LogPermissionDecision(httpContext, "DENY", userId, tenantId, path, method,
            _permissionCode, "MissingPermission", accessVersion);

        return ProductAccessDeniedResult.Create(
            ProductAccessDeniedException.MissingPermission(_permissionCode));
    }

    private static async Task<Authorization.PolicyEvaluationResult?> EvaluatePoliciesIfEnabled(
        HttpContext httpContext, System.Security.Claims.ClaimsPrincipal user, string permissionCode)
    {
        if (!IsPolicyEvaluationEnabled(httpContext))
            return null;

        var evaluationService = httpContext.RequestServices.GetService(
            typeof(Authorization.IPolicyEvaluationService)) as Authorization.IPolicyEvaluationService;

        if (evaluationService == null)
            return null;

        var resourceContext = httpContext.Items.TryGetValue("PolicyResourceContext", out var ctx)
            ? ctx as Dictionary<string, object?>
            : null;

        return await evaluationService.EvaluateAsync(
            user,
            permissionCode,
            resourceContext,
            httpContext);
    }

    private static bool IsPolicyEvaluationEnabled(HttpContext ctx)
    {
        var config = ctx.RequestServices.GetService(typeof(Microsoft.Extensions.Configuration.IConfiguration))
            as Microsoft.Extensions.Configuration.IConfiguration;
        if (config == null) return false;
        return string.Equals(config["Authorization:EnablePolicyEvaluation"], "true", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRoleFallbackEnabled(HttpContext ctx)
    {
        var config = ctx.RequestServices.GetService(typeof(Microsoft.Extensions.Configuration.IConfiguration))
            as Microsoft.Extensions.Configuration.IConfiguration;
        if (config == null) return false;
        return string.Equals(config["Authorization:EnableRoleFallback"], "true", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractProductCode(string permissionCode)
    {
        var dotIndex = permissionCode.IndexOf('.');
        return dotIndex > 0 ? permissionCode[..dotIndex] : null;
    }

    private static void LogPermissionDecision(
        HttpContext ctx, string result, string? userId, string? tenantId,
        string? path, string method, string permission,
        string source, string? accessVersion)
    {
        var logger = ctx.RequestServices.GetService(typeof(ILogger<RequirePermissionFilter>)) as ILogger;
        if (logger == null) return;

        if (result == "DENY")
        {
            logger.LogWarning(
                "PermissionDecision: result={Result} user={UserId} tenant={TenantId} method={Method} endpoint={Path} permission={Permission} source={Source} accessVersion={AccessVersion}",
                result, userId, tenantId, method, path, permission, source, accessVersion);
        }
        else
        {
            logger.LogInformation(
                "PermissionDecision: result={Result} user={UserId} tenant={TenantId} method={Method} endpoint={Path} permission={Permission} source={Source} accessVersion={AccessVersion}",
                result, userId, tenantId, method, path, permission, source, accessVersion);
        }
    }
}
