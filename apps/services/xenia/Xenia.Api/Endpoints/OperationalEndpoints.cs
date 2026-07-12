namespace Xenia.Api.Endpoints;

public static class OperationalEndpoints
{
    public static IEndpointRouteBuilder MapOperationalEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/health", () => Results.Ok(new { status = "ok", service = "xenia" }))
            .AllowAnonymous();

        routes.MapGet("/ready", () => Results.Ok(new { status = "ready", service = "xenia" }))
            .AllowAnonymous();

        routes.MapGet("/info", () => Results.Ok(new
        {
            name = "Xenia",
            productCode = "XENIA",
            version = "v1",
            deploymentModels = new[] { "Managed", "BringYourOwnAI" },
            providers = new[] { "OpenAI", "Anthropic", "Gemini", "AzureOpenAI", "AwsBedrock" }
        })).AllowAnonymous();

        return routes;
    }
}
