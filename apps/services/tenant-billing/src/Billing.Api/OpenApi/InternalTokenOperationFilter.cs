using Billing.Api.Security;
using Billing.Api.Tenancy;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Billing.Api.OpenApi;

/// <summary>
/// Adds the <c>X-Internal-Token</c> security requirement to Swagger only for
/// operations served from
/// <see cref="TenantResolutionMiddleware.ProtectedPathPrefix"/> — i.e.
/// every <c>/api/*</c> route. Mirrors the pattern in
/// <see cref="TenantHeaderOperationFilter"/>.
///
/// The security scheme itself is registered in <c>Program.cs</c> under the
/// id <c>InternalToken</c> with an <see cref="OpenApiSecurityScheme.In"/> of
/// <see cref="ParameterLocation.Header"/> and
/// <see cref="OpenApiSecurityScheme.Name"/> equal to
/// <see cref="RequireInternalTokenMiddleware.HeaderName"/>.
///
/// Health endpoints are mapped via minimal-API (<c>app.MapGet("/health")</c>
/// and <c>app.MapGet("/healthz")</c>) and do not register with
/// <c>ApiExplorer</c>, so they never appear in the generated document and
/// are correctly excluded from this filter by construction.
/// </summary>
public sealed class InternalTokenOperationFilter : IOperationFilter
{
    public const string SchemeId = "InternalToken";

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
                        Id = SchemeId,
                    },
                },
                Array.Empty<string>()
            },
        });
    }
}
