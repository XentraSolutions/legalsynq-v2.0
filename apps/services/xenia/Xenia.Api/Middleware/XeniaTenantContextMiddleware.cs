using Xenia.Application.TenantContext;

namespace Xenia.Api.Middleware;

/// <summary>
/// Resolves the Xenia tenant context at the start of each authenticated request
/// and stores it in the scoped <see cref="XeniaTenantContextAccessor"/>.
///
/// This middleware runs after authentication/authorization so that JWT claims
/// are available. It calls <see cref="ITenantContextResolver.ResolveAsync"/>
/// which reads the <c>tenant_id</c> claim from the verified JWT — never from
/// caller-supplied headers or query strings.
///
/// Endpoints that require tenant context check <see cref="XeniaTenantContextAccessor.Current"/>.
/// Unauthenticated or platform-level requests (no tenant_id claim) leave Current as null.
/// </summary>
public sealed class XeniaTenantContextMiddleware
{
    private readonly RequestDelegate _next;

    public XeniaTenantContextMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        ITenantContextResolver resolver,
        XeniaTenantContextAccessor accessor)
    {
        var tenantContext = await resolver.ResolveAsync(context, context.RequestAborted);

        if (tenantContext is not null)
        {
            accessor.Set(tenantContext);
        }

        await _next(context);
    }
}
