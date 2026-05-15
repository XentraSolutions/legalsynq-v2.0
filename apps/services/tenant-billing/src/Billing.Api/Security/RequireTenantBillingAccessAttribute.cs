using Billing.Api.Tenancy;
using Billing.Domain.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Billing.Api.Security;

/// <summary>
/// TB-ENF-01 — soft-enforcement gate for tenant-billing write/action endpoints.
///
/// Resolves the active tenant from the existing
/// <see cref="ITenantContext"/>, asks the
/// <see cref="ITenantBillingAccessPolicy"/> whether the configured operation
/// category is currently allowed, and short-circuits the action with an
/// HTTP 403 ProblemDetails when it is not.
///
/// <para>
/// Behaviour is controlled by
/// <see cref="EntitlementEnforcementOptions"/> (section
/// <c>Billing:EntitlementEnforcement</c>):
/// <list type="bullet">
///   <item><c>Enabled = false</c> (default) — every attributed action
///         passes through unchanged. The attribute is effectively a no-op
///         until an operator opts in.</item>
///   <item><c>Enabled = true</c> — the policy matrix in
///         <see cref="TenantBillingAccessPolicy"/> decides per
///         <see cref="TenantBillingOperationCategory"/>.</item>
/// </list>
/// Read endpoints, profile-admin endpoints, and entitlement-admin
/// endpoints are never attributed; they are always reachable.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class,
                AllowMultiple = false, Inherited = true)]
public sealed class RequireTenantBillingAccessAttribute : Attribute, IAsyncActionFilter
{
    public TenantBillingOperationCategory Category { get; }

    public RequireTenantBillingAccessAttribute(TenantBillingOperationCategory category)
    {
        Category = category;
    }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var sp = context.HttpContext.RequestServices;
        var policy = sp.GetService<ITenantBillingAccessPolicy>();

        // If the policy was not registered (defensive — production wiring
        // always registers it via AddBillingInfrastructure), behave as if
        // enforcement is disabled. A regression in DI must never block
        // existing surfaces.
        if (policy is null)
        {
            await next();
            return;
        }

        var tenantId = Guid.Empty;
        try
        {
            var tenant = sp.GetService<ITenantContext>();
            if (tenant is not null) tenantId = tenant.TenantId;
        }
        catch
        {
            // ITenantContext.TenantId throws when no tenant has been
            // resolved (unscoped routes). Leave tenantId empty — the
            // policy will treat that as "missing tenant context" when
            // enforcement is on, and skip evaluation when off.
            tenantId = Guid.Empty;
        }

        var decision = await policy.AuthorizeAsync(
            tenantId, Category, context.HttpContext.RequestAborted);

        if (decision.IsAllowed)
        {
            await next();
            return;
        }

        await WriteForbiddenAsync(context, decision);
    }

    private static async Task WriteForbiddenAsync(
        ActionExecutingContext context, TenantBillingEnforcementDecision decision)
    {
        var http = context.HttpContext;
        var sp = http.RequestServices;
        var problemFactory = sp.GetService<ProblemDetailsFactory>();
        var logger = sp.GetService<ILoggerFactory>()
            ?.CreateLogger<RequireTenantBillingAccessAttribute>();

        logger?.LogInformation(
            "TenantBilling enforcement blocked: Category={Category} " +
            "AccessRecommendation={AccessRecommendation} " +
            "EntitlementStatus={EntitlementStatus} Reason={Reason}",
            decision.Category, decision.AccessRecommendation,
            decision.EntitlementStatus, decision.Reason);

        ProblemDetails problem;
        if (problemFactory is not null)
        {
            problem = problemFactory.CreateProblemDetails(
                http,
                statusCode: decision.HttpStatus,
                title: decision.ProblemTitle,
                detail: decision.ProblemDetail);
        }
        else
        {
            problem = new ProblemDetails
            {
                Status = decision.HttpStatus,
                Title  = decision.ProblemTitle,
                Detail = decision.ProblemDetail,
            };
        }

        // Surface category / recommendation / status / reason as extensions
        // so a BFF can render an explanatory banner without leaking any
        // Commerce-side identifiers. Keys mirror the report §7 contract.
        problem.Extensions["category"]              = decision.Category.ToString();
        problem.Extensions["accessRecommendation"]  = decision.AccessRecommendation;
        problem.Extensions["entitlementStatus"]    = decision.EntitlementStatus;
        problem.Extensions["reason"]                = decision.Reason;

        context.Result = new ObjectResult(problem)
        {
            StatusCode  = decision.HttpStatus,
            ContentTypes = { "application/problem+json" },
        };
    }
}
