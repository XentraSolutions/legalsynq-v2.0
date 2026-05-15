using System.Text.Json;
using TenantBilling.Domain.Entities;
using TenantBilling.Domain.Statements;
using TenantBilling.Domain.StatementTemplates;
using TenantBilling.Domain.Tests.Fakes;
using TenantBilling.Domain.Tests.Helpers;
using Xunit;

namespace TenantBilling.Domain.Tests;

/// <summary>
/// STAT-B02 — Tests for the persistence service. Composes the
/// real STAT-B01 builder (over the existing in-memory repos), the
/// real number generator, and the new in-memory persistence repo.
/// </summary>
public class CustomerStatementPersistenceServiceTests
{
    private const string Currency = "USD";

    private sealed class Fixture
    {
        public Guid Tenant { get; init; } = Guid.NewGuid();
        public Guid CustomerId { get; init; } = Guid.NewGuid();
        public InMemoryCustomerRepository Customers { get; init; } = new();
        public InMemoryInvoiceRepository Invoices { get; init; } = new();
        public InMemoryPaymentRepository Payments { get; init; } = new();
        public InMemoryStatementTemplateRepository Templates { get; init; } = new();
        public InMemoryCustomerStatementRepository Statements { get; init; } = new();
        public TestTimeProvider Time { get; init; } = new(new DateTime(2026, 4, 29, 12, 0, 0, DateTimeKind.Utc));

        public CustomerStatementPersistenceService Build(out StatementTemplateService templateSvc)
        {
            templateSvc = new StatementTemplateService(Templates, new InMemoryUnitOfWork(), Time);
            var renderer = new CustomerStatementHtmlRenderer();
            var builder = new CustomerStatementService(Customers, Invoices, Payments, renderer, Time);
            var gen = new StatementNumberGenerator(Statements);
            return new CustomerStatementPersistenceService(
                builder, renderer, templateSvc, gen, Statements, Time);
        }
    }

    private static async Task SeedAsync(Fixture fx)
    {
        await fx.Customers.AddAsync(new Customer
        {
            Id = fx.CustomerId,
            TenantId = fx.Tenant,
            Name = "Acme Co",
            Email = "billing@acme.test",
            CreatedAt = fx.Time.GetUtcNow().UtcDateTime,
            UpdatedAt = fx.Time.GetUtcNow().UtcDateTime,
        });
        await fx.Invoices.AddAsync(new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = fx.Tenant,
            CustomerId = fx.CustomerId,
            InvoiceNumber = "INV-1",
            Status = InvoiceStatus.Issued,
            Currency = Currency,
            IssueDate = new DateTime(2026, 4, 10),
            DueDate = new DateTime(2026, 5, 10),
            Subtotal = 200m,
            TotalAmount = 200m,
            CreatedAt = fx.Time.GetUtcNow().UtcDateTime,
            UpdatedAt = fx.Time.GetUtcNow().UtcDateTime,
        });
    }

    [Fact]
    public async Task GenerateMonthlyAsync_PersistsSnapshotAndAssignsNumber()
    {
        var fx = new Fixture();
        await SeedAsync(fx);
        var svc = fx.Build(out _);

        var s = await svc.GenerateMonthlyAsync(fx.Tenant, fx.CustomerId, 2026, 4, null, renderHtml: false);

        Assert.NotNull(s);
        Assert.Equal("STMT-2026-000001", s!.StatementNumber);
        Assert.Equal(CustomerStatementStatus.Generated, s.Status);
        Assert.Null(s.HtmlSnapshot);
        Assert.False(string.IsNullOrEmpty(s.StatementSnapshotJson));
        Assert.Equal(200m, s.TotalInvoiced);
        Assert.Equal(0m, s.TotalPaid);
        Assert.Equal(200m, s.OutstandingBalance);

        // Snapshot is real, parseable JSON.
        var doc = JsonSerializer.Deserialize<CustomerStatementDocument>(
            s.StatementSnapshotJson, CustomerStatementPersistenceService.SnapshotJsonOptions);
        Assert.NotNull(doc);
        Assert.Equal(fx.CustomerId, doc!.CustomerId);
    }

    [Fact]
    public async Task GenerateAsync_RetriesOnTransientNumberConflict()
    {
        var fx = new Fixture();
        await SeedAsync(fx);
        var svc = fx.Build(out _);

        fx.Statements.SimulateNumberConflictOnce = true;
        var s = await svc.GenerateMonthlyAsync(fx.Tenant, fx.CustomerId, 2026, 4, null, renderHtml: false);

        Assert.NotNull(s);
        // First (failed) attempt would have been STMT-2026-000001;
        // the in-memory repo discards it on conflict so the next
        // GetLatestNumberForYearAsync still returns null and we
        // again get -000001 on the second attempt.
        Assert.Equal("STMT-2026-000001", s!.StatementNumber);
    }

    [Fact]
    public async Task GenerateAsync_StampsDefaultTemplate()
    {
        var fx = new Fixture();
        await SeedAsync(fx);
        var svc = fx.Build(out var templates);

        var template = await templates.CreateAsync(fx.Tenant,
            new NewStatementTemplate(Name: "Default", Status: StatementTemplateStatus.Active));

        var s = await svc.GenerateMonthlyAsync(fx.Tenant, fx.CustomerId, 2026, 4, null, renderHtml: false);
        Assert.NotNull(s);
        Assert.Equal(template.Id, s!.TemplateId);
        Assert.False(string.IsNullOrEmpty(s.TemplateSnapshotJson));
    }

    [Fact]
    public async Task GenerateAsync_RejectsDraftExplicitTemplate()
    {
        var fx = new Fixture();
        await SeedAsync(fx);
        var svc = fx.Build(out var templates);
        var draft = await templates.CreateAsync(fx.Tenant, new NewStatementTemplate(Name: "Draft"));

        await Assert.ThrowsAsync<StatementTemplateNotSelectableException>(() =>
            svc.GenerateMonthlyAsync(fx.Tenant, fx.CustomerId, 2026, 4, draft.Id, renderHtml: false));
    }

    [Fact]
    public async Task GenerateAsync_CapturesHtmlWhenRequested()
    {
        var fx = new Fixture();
        await SeedAsync(fx);
        var svc = fx.Build(out _);
        var s = await svc.GenerateMonthlyAsync(fx.Tenant, fx.CustomerId, 2026, 4, null, renderHtml: true);
        Assert.NotNull(s!.HtmlSnapshot);
        Assert.Contains("Acme Co", s.HtmlSnapshot);
    }

    [Fact]
    public async Task GenerateAsync_ReturnsNullForCrossTenantCustomer()
    {
        var fx = new Fixture();
        await SeedAsync(fx);
        var svc = fx.Build(out _);
        var s = await svc.GenerateMonthlyAsync(Guid.NewGuid(), fx.CustomerId, 2026, 4, null, false);
        Assert.Null(s);
    }

    [Fact]
    public async Task RenderHtmlAsync_PrefersCachedSnapshot()
    {
        var fx = new Fixture();
        await SeedAsync(fx);
        var svc = fx.Build(out _);
        var s = await svc.GenerateMonthlyAsync(fx.Tenant, fx.CustomerId, 2026, 4, null, renderHtml: true);
        Assert.NotNull(s);
        var html = await svc.RenderHtmlAsync(fx.Tenant, s!.Id);
        Assert.Equal(s.HtmlSnapshot, html);
    }

    [Fact]
    public async Task RenderHtmlAsync_RehydratesFromJsonWhenNoCachedHtml()
    {
        var fx = new Fixture();
        await SeedAsync(fx);
        var svc = fx.Build(out _);
        var s = await svc.GenerateMonthlyAsync(fx.Tenant, fx.CustomerId, 2026, 4, null, renderHtml: false);
        Assert.NotNull(s);
        Assert.Null(s!.HtmlSnapshot);

        var html = await svc.RenderHtmlAsync(fx.Tenant, s.Id);
        Assert.NotNull(html);
        Assert.Contains("Acme Co", html);
    }

    [Fact]
    public async Task VoidAsync_IsIdempotent()
    {
        var fx = new Fixture();
        await SeedAsync(fx);
        var svc = fx.Build(out _);
        var s = await svc.GenerateMonthlyAsync(fx.Tenant, fx.CustomerId, 2026, 4, null, false);
        Assert.NotNull(s);
        var v1 = await svc.VoidAsync(fx.Tenant, s!.Id, "duplicate");
        Assert.NotNull(v1);
        Assert.Equal(CustomerStatementStatus.Voided, v1!.Status);
        Assert.Equal("duplicate", v1.VoidReason);
        var v2 = await svc.VoidAsync(fx.Tenant, s.Id, "ignored");
        Assert.Equal(v1.VoidedAtUtc, v2!.VoidedAtUtc);
        Assert.Equal("duplicate", v2.VoidReason);
    }

    [Fact]
    public async Task VoidAsync_ReturnsNullForCrossTenant()
    {
        var fx = new Fixture();
        await SeedAsync(fx);
        var svc = fx.Build(out _);
        var s = await svc.GenerateMonthlyAsync(fx.Tenant, fx.CustomerId, 2026, 4, null, false);
        var voided = await svc.VoidAsync(Guid.NewGuid(), s!.Id, null);
        Assert.Null(voided);
    }
}
