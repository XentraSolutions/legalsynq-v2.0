using Xenia.Application.Email;

namespace Xenia.Api.Endpoints;

/// <summary>
/// Read-only endpoints for the email provider catalog.
/// Returns safe, UI-ready provider definitions. No credentials or internal metadata.
/// </summary>
public static class XeniaEmailProviderEndpoints
{
    public static IEndpointRouteBuilder MapXeniaEmailProviderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/email/providers").RequireAuthorization(XeniaPolicies.EmailRead);

        // GET /email/providers — all supported provider definitions
        group.MapGet("/", () =>
        {
            var providers = EmailProviderDefinitions.GetAll();
            return Results.Ok(new { providers, total = providers.Count });
        });

        // GET /email/providers/{key} — single provider definition
        group.MapGet("/{key}", (string key) =>
        {
            var provider = EmailProviderDefinitions.Get(key);
            return provider is null
                ? Results.NotFound(new { error = $"Provider '{key}' is not supported." })
                : Results.Ok(provider);
        });

        return app;
    }
}
