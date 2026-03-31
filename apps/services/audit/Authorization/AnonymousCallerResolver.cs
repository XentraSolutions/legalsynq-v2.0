namespace PlatformAuditEventService.Authorization;

/// <summary>
/// Caller resolver for <c>QueryAuth:Mode = "None"</c> (development / test only).
///
/// Returns a <see cref="QueryCallerContext"/> with <see cref="CallerScope.PlatformAdmin"/>
/// scope so that all records are accessible during local development without any identity
/// provider configuration.
///
/// WARNING: This resolver must never be active in non-development environments.
///          The <c>QueryAuth:Mode</c> setting controls which resolver is registered.
/// </summary>
public sealed class AnonymousCallerResolver : IQueryCallerResolver
{
    public string Mode => "None";

    public Task<IQueryCallerContext> ResolveAsync(HttpContext context, CancellationToken ct = default) =>
        Task.FromResult<IQueryCallerContext>(QueryCallerContext.Anonymous());
}
