using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;

namespace Billing.Api.Security;

/// <summary>
/// Disables a controller action when the Billing platform-template feature
/// flag is off. Monk Search's tenant Billing scope deliberately excludes
/// platform-template endpoints (`/api/invoice-templates/platform/*`); the
/// donor surface is preserved in code for reuse but the routes return
/// <see cref="StatusCodes.Status404NotFound"/> by default so they are
/// indistinguishable from non-existent routes for callers.
///
/// The flag is sourced from:
/// <list type="number">
///   <item>Environment variable <see cref="EnvironmentVariableName"/></item>
///   <item>Configuration key <see cref="ConfigurationKey"/> (fallback)</item>
/// </list>
/// Both values must equal <c>"true"</c> (case-insensitive) to enable the
/// platform routes. The default is disabled.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class PlatformTemplatesGuardAttribute : Attribute, IAsyncActionFilter
{
    public const string ConfigurationKey = "Billing:EnablePlatformTemplates";
    public const string EnvironmentVariableName = "BILLING_ENABLE_PLATFORM_TEMPLATES";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!IsEnabled(context))
        {
            context.Result = new NotFoundResult();
            return;
        }

        await next();
    }

    private static bool IsEnabled(ActionExecutingContext context)
    {
        var fromEnv = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (!string.IsNullOrEmpty(fromEnv))
        {
            return string.Equals(fromEnv, "true", StringComparison.OrdinalIgnoreCase);
        }

        var configuration = context.HttpContext.RequestServices.GetService(typeof(IConfiguration)) as IConfiguration;
        if (configuration is null) return false;

        var fromConfig = configuration[ConfigurationKey];
        return string.Equals(fromConfig, "true", StringComparison.OrdinalIgnoreCase);
    }
}
