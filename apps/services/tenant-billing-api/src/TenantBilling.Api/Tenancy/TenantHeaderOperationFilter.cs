using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace TenantBilling.Api.Tenancy;

/// <summary>
/// Adds the X-Tenant-Id header requirement to Swagger only for operations
/// served from <see cref="TenantResolutionMiddleware.ProtectedPathPrefix"/>.
/// Without this filter the security requirement applies to every operation
/// (including <c>/health</c>), which misleads consumers.
/// </summary>
public sealed class TenantHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var route = "/" + (context.ApiDescription.RelativePath ?? string.Empty);
        if (!route.StartsWith(TenantResolutionMiddleware.ProtectedPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        operation.Security ??= new List<OpenApiSecurityRequirement>();
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "TenantHeader",
                    },
                },
                Array.Empty<string>()
            },
        });
    }
}
