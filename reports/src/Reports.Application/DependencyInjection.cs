using Microsoft.Extensions.DependencyInjection;
using Reports.Application.Guardrails;
using Reports.Application.Templates;
using Reports.Contracts.Guardrails;

namespace Reports.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddReportsApplication(this IServiceCollection services)
    {
        services.AddSingleton<IGuardrailValidator, GuardrailValidator>();
        services.AddScoped<ITemplateManagementService, TemplateManagementService>();

        return services;
    }
}
