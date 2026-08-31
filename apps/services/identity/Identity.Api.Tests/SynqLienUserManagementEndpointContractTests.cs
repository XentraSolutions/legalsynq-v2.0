using Identity.Api.Endpoints;
using Identity.Application.Interfaces;
using Identity.Infrastructure.Data;
using Identity.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Identity.Api.Tests;

public sealed class SynqLienUserManagementEndpointContractTests
{
    [Fact]
    public async Task MapEndpoints_ExposesOnlyThePlannedAuthenticatedHttpSurface()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthorization();
        builder.Services.AddScoped<ISynqLienUserManagementService>(_ => null!);
        builder.Services.AddScoped<IdentityDbContext>(_ => null!);
        builder.Services.AddScoped<INotificationsEmailClient>(_ => null!);
        builder.Services.AddOptions<NotificationsServiceOptions>();
        await using var app = builder.Build();

        app.MapSynqLienUserManagementEndpoints();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => new
            {
                Route = endpoint.RoutePattern.RawText,
                Methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [],
                AllowsAnonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null,
                HasAuthorization = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0,
            })
            .ToList();

        var expected = new HashSet<(string Route, string Method)>
        {
            ("/api/v1/products/SYNQ_LIENS/user-management/users", "GET"),
            ("/api/v1/products/SYNQ_LIENS/user-management/users/{userId:guid}", "GET"),
            ("/api/v1/products/SYNQ_LIENS/user-management/roles", "GET"),
            ("/api/v1/products/SYNQ_LIENS/user-management/invitations", "GET"),
            ("/api/v1/products/SYNQ_LIENS/user-management/invitations", "POST"),
            ("/api/v1/products/SYNQ_LIENS/user-management/invitations/{invitationId:guid}/resend", "POST"),
            ("/api/v1/products/SYNQ_LIENS/user-management/invitations/{invitationId:guid}", "DELETE"),
            ("/api/v1/products/SYNQ_LIENS/user-management/users/{userId:guid}/access", "PUT"),
            ("/api/v1/products/SYNQ_LIENS/user-management/users/{userId:guid}/roles", "PUT"),
        };

        Assert.Equal(expected.Count, endpoints.Count);
        foreach (var endpoint in endpoints)
        {
            Assert.True(endpoint.HasAuthorization);
            Assert.False(endpoint.AllowsAnonymous);
            Assert.Contains((endpoint.Route!, endpoint.Methods.Single()), expected);
        }
    }
}
