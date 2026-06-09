using Billing.Api.Security;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Billing.Api.OpenApi;

/// <summary>
/// Removes the platform-template path family
/// (<c>/api/invoice-templates/platform/*</c>) from the generated OpenAPI
/// document whenever the platform-template feature flag is OFF — which is
/// the default state for the Monk Search tenant Billing scope.
///
/// This keeps the on-disk contract aligned with the runtime surface: at
/// runtime <see cref="PlatformTemplatesGuardAttribute"/> short-circuits the
/// same actions to <c>404 Not Found</c>, so a contract consumer reading the
/// generated <c>billing-openapi.json</c> sees exactly the routes that will
/// actually serve traffic.
///
/// Flag source priority (matches <see cref="PlatformTemplatesGuardAttribute"/>):
/// <list type="number">
///   <item><c>BILLING_ENABLE_PLATFORM_TEMPLATES</c> environment variable</item>
///   <item><c>Billing:EnablePlatformTemplates</c> configuration key</item>
/// </list>
/// Both compared <c>"true"</c> case-insensitively. Anything else (or unset)
/// hides the paths.
///
/// Schemas (<see cref="OpenApiComponents.Schemas"/>) are not pruned because
/// the platform and tenant routes share the same DTOs
/// (<c>CreateInvoiceTemplateRequest</c>, <c>UpdateInvoiceTemplateRequest</c>,
/// <c>InvoiceTemplateResponse</c>); removing them would break the tenant
/// contract that callers actually use.
/// </summary>
public sealed class HideDisabledPlatformTemplateEndpointsDocumentFilter : IDocumentFilter
{
    public const string PathPrefixToHide = "/api/invoice-templates/platform";

    private readonly IConfiguration _configuration;

    public HideDisabledPlatformTemplateEndpointsDocumentFilter(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        if (IsPlatformEnabled())
        {
            return;
        }

        var pathsToRemove = swaggerDoc.Paths.Keys
            .Where(p => p.StartsWith(PathPrefixToHide, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var path in pathsToRemove)
        {
            swaggerDoc.Paths.Remove(path);
        }
    }

    private bool IsPlatformEnabled()
    {
        var fromEnv = Environment.GetEnvironmentVariable(PlatformTemplatesGuardAttribute.EnvironmentVariableName);
        if (!string.IsNullOrEmpty(fromEnv))
        {
            return string.Equals(fromEnv, "true", StringComparison.OrdinalIgnoreCase);
        }

        var fromConfig = _configuration[PlatformTemplatesGuardAttribute.ConfigurationKey];
        return string.Equals(fromConfig, "true", StringComparison.OrdinalIgnoreCase);
    }
}
