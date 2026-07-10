namespace Xenia.Application.TenantContext;

/// <summary>
/// Scoped store for the resolved <see cref="IXeniaTenantContext"/>.
///
/// Populated by the <c>XeniaTenantContextMiddleware</c> at the start of each request.
/// Consumed by services, repositories, and modules that need tenant context.
///
/// The accessor is registered as Scoped — one instance per HTTP request.
/// </summary>
public sealed class XeniaTenantContextAccessor
{
    private IXeniaTenantContext? _context;

    /// <summary>
    /// The resolved tenant context. Null until
    /// <see cref="Set"/> is called by the middleware.
    /// </summary>
    public IXeniaTenantContext? Current => _context;

    /// <summary>
    /// Stores the resolved context. Called once per request by
    /// <c>XeniaTenantContextMiddleware</c>.
    /// </summary>
    public void Set(IXeniaTenantContext context)
        => _context = context ?? throw new ArgumentNullException(nameof(context));

    /// <summary>
    /// Clears the stored context. Used in tests.
    /// </summary>
    public void Clear() => _context = null;
}
