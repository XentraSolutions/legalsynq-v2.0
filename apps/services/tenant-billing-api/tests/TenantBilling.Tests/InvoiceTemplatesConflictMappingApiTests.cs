using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TenantBilling.Api.Contracts;
using TenantBilling.Domain.Entities;
using TenantBilling.Domain.Services;
using TenantBilling.Infrastructure.Data;
using Xunit;

namespace TenantBilling.Tests;

/// <summary>
/// Asserts the controller's HTTP-status mapping for
/// <see cref="InvoiceTemplateDefaultConflictException"/> on the three
/// endpoints that can promote a default (Create, Update,
/// MakeDefault) for both Platform and Tenant scopes.
///
/// We swap in a tiny fake <see cref="IInvoiceTemplateService"/> that
/// throws on the relevant calls so we can prove the controller's
/// catch order returns 409 (Conflict) instead of being swallowed by
/// the generic <see cref="InvalidOperationException"/> catch — the
/// latter would surface as 400 and would silently break the API
/// contract advertised on the OpenAPI surface.
/// </summary>
public class InvoiceTemplatesConflictMappingApiTests : IDisposable
{
    private readonly ConflictThrowingFactory _factory = new();
    public void Dispose() => _factory.Dispose();

    private static readonly Guid TenantId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static CreateInvoiceTemplateRequest CreateBody() => new()
    {
        Name = "Conflict body",
        Status = InvoiceTemplateStatus.Active,
        IsDefault = true,
    };

    private static UpdateInvoiceTemplateRequest UpdateBody() => new()
    {
        Name = "Conflict body",
    };

    [Fact]
    public async Task CreatePlatform_DefaultConflict_Returns409()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync(
            "/api/invoice-templates/platform", CreateBody());
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task UpdatePlatform_DefaultConflict_Returns409()
    {
        var client = _factory.CreateClient();
        var resp = await client.PutAsJsonAsync(
            $"/api/invoice-templates/platform/{Guid.NewGuid()}", UpdateBody());
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task MakeDefaultPlatform_DefaultConflict_Returns409()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsync(
            $"/api/invoice-templates/platform/{Guid.NewGuid()}/make-default",
            content: null);
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task CreateTenant_DefaultConflict_Returns409()
    {
        var client = _factory.CreateClientForTenant(TenantId);
        var resp = await client.PostAsJsonAsync(
            "/api/invoice-templates/tenant", CreateBody());
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task UpdateTenant_DefaultConflict_Returns409()
    {
        var client = _factory.CreateClientForTenant(TenantId);
        var resp = await client.PutAsJsonAsync(
            $"/api/invoice-templates/tenant/{Guid.NewGuid()}", UpdateBody());
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task MakeDefaultTenant_DefaultConflict_Returns409()
    {
        var client = _factory.CreateClientForTenant(TenantId);
        var resp = await client.PostAsync(
            $"/api/invoice-templates/tenant/{Guid.NewGuid()}/make-default",
            content: null);
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    /// <summary>
    /// WebApplicationFactory variant that keeps the rest of the
    /// runtime composition (middleware, controller, tenant context)
    /// but swaps in <see cref="ConflictThrowingTemplateService"/> for
    /// the write-side service. Cloned-and-narrowed from
    /// <see cref="TenantBillingWebApplicationFactory"/> so the rest of
    /// the integration suite keeps using the real service.
    /// </summary>
    private sealed class ConflictThrowingFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"tenant-billing-conflict-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = string.Empty
                });
            });
            builder.ConfigureServices(services =>
            {
                var optionsDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<TenantBillingDbContext>));
                if (optionsDescriptor is not null) services.Remove(optionsDescriptor);
                services.AddDbContext<TenantBillingDbContext>(o => o.UseInMemoryDatabase(_databaseName));

                var svc = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IInvoiceTemplateService));
                if (svc is not null) services.Remove(svc);
                services.AddScoped<IInvoiceTemplateService, ConflictThrowingTemplateService>();
            });
        }

        public HttpClient CreateClientForTenant(Guid tenantId)
        {
            var client = CreateClient();
            client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
            return client;
        }
    }

    private sealed class ConflictThrowingTemplateService : IInvoiceTemplateService
    {
        private static InvoiceTemplateDefaultConflictException Conflict() =>
            new("forced default conflict for HTTP-mapping test");

        public Task<InvoiceTemplate> CreateAsync(Guid? tenantId, NewInvoiceTemplate input, CancellationToken ct = default)
            => throw Conflict();

        public Task<InvoiceTemplate?> UpdateAsync(Guid? tenantId, Guid id, InvoiceTemplateUpdate update, CancellationToken ct = default)
            => throw Conflict();

        public Task<InvoiceTemplate?> MakeDefaultAsync(Guid? tenantId, Guid id, CancellationToken ct = default)
            => throw Conflict();

        // GetDefaultAsync runs BEFORE MakeDefaultAsync inside the
        // controller to capture the previous default for the response
        // payload. Returning null keeps the make-default flow
        // reaching the throwing call below.
        public Task<InvoiceTemplate?> GetDefaultAsync(Guid? tenantId, CancellationToken ct = default)
            => Task.FromResult<InvoiceTemplate?>(null);

        public Task<InvoiceTemplate?> GetAsync(Guid? tenantId, Guid id, CancellationToken ct = default)
            => Task.FromResult<InvoiceTemplate?>(null);

        public Task<IReadOnlyList<InvoiceTemplate>> ListAsync(Guid? tenantId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<InvoiceTemplate>>(Array.Empty<InvoiceTemplate>());

        public Task<InvoiceTemplate?> ActivateAsync(Guid? tenantId, Guid id, CancellationToken ct = default)
            => Task.FromResult<InvoiceTemplate?>(null);

        public Task<InvoiceTemplate?> RetireAsync(Guid? tenantId, Guid id, CancellationToken ct = default)
            => Task.FromResult<InvoiceTemplate?>(null);
    }
}
