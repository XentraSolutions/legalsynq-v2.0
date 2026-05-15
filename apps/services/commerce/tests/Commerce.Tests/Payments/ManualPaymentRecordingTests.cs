using Commerce.Application.Common.Exceptions;
using Commerce.Application.Common.Time;
using Commerce.Application.Invoicing.Validators;
using Commerce.Application.Payments.Validators;
using Commerce.Contracts.Invoicing;
using Commerce.Contracts.Payments;
using Commerce.Domain.Billing;
using Commerce.Domain.Invoicing.Enums;
using Commerce.Domain.Payments.Enums;
using Commerce.Infrastructure.Invoicing.Services;
using Commerce.Infrastructure.Payments.Services;
using Commerce.Infrastructure.Persistence;
using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Commerce.Tests.Payments;

/// <summary>
/// Direct service tests for <c>ManualPaymentRecordingService</c>. The
/// API-level test in <see cref="ManualPaymentApiTests"/> covers wire
/// shape; this file pins down the business rules.
/// </summary>
public class ManualPaymentRecordingTests
{
    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    }

    private sealed class TestHarness : IDisposable
    {
        public CommerceDbContext Db { get; }
        public FixedClock Clock { get; } = new();
        public ManualPaymentRecordingService Service { get; }
        public BillingAccount Account { get; }
        public Domain.Invoicing.Invoice Invoice { get; }

        public TestHarness(string currency = "USD", long lineAmountMinor = 5000, int qty = 2)
        {
            var opts = new DbContextOptionsBuilder<CommerceDbContext>()
                .UseInMemoryDatabase($"manualpay-{Guid.NewGuid()}")
                .Options;
            Db = new CommerceDbContext(opts);

            Account = BillingAccount.Create("COM-ACC-MAN-" + Guid.NewGuid().ToString("N")[..6],
                "ManualCo", null, currency, Clock.UtcNow);
            Account.Activate(Clock.UtcNow);
            Db.BillingAccounts.Add(Account);
            Db.SaveChanges();

            var numberGen = new InvoiceNumberGenerator(Db);
            var invValidator = new CreateInvoiceRequestValidator();
            var invoiceService = new InvoiceService(Db, Clock, numberGen, invValidator);

            var inv = invoiceService.CreateAsync(
                new CreateInvoiceRequest(Account.Id, currency,
                    new[] { new CreateInvoiceLineRequest("Seats", qty, lineAmountMinor) }),
                CancellationToken.None).GetAwaiter().GetResult();
            Invoice = Db.Invoices.First(i => i.Id == inv.Id);

            var validator = new RecordManualPaymentRequestValidator();
            Service = new ManualPaymentRecordingService(Db, Clock, validator);
        }

        public void Dispose() => Db.Dispose();
    }

    [Fact]
    public async Task Records_partial_payment_and_keeps_invoice_open()
    {
        using var h = new TestHarness();
        var paidAt = h.Clock.UtcNow.AddHours(-1);

        var resp = await h.Service.RecordAsync(h.Invoice.Id,
            new RecordManualPaymentRequest(4000, paidAt, "cash", null, "Op-A", "partial 1"),
            CancellationToken.None);

        resp.AmountMinor.Should().Be(4000);
        resp.Provider.Should().Be(PaymentProviderType.Manual);
        resp.Status.Should().Be(PaymentStatus.Succeeded);
        resp.PaidAtUtc.Should().Be(paidAt);
        resp.Method.Should().Be("cash");
        resp.RecordedByLabel.Should().Be("Op-A");
        resp.Notes.Should().Be("partial 1");
        resp.InvoiceId.Should().Be(h.Invoice.Id);
        resp.BillingAccountId.Should().Be(h.Invoice.BillingAccountId);

        var inv = await h.Db.Invoices.AsNoTracking().FirstAsync(i => i.Id == h.Invoice.Id);
        inv.AmountPaidMinor.Should().Be(4000);
        inv.AmountDueMinor.Should().Be(6000);
        inv.Status.Should().Be(InvoiceStatus.Open);
        inv.PaidAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task Records_full_payment_and_marks_invoice_paid()
    {
        using var h = new TestHarness();
        await h.Service.RecordAsync(h.Invoice.Id,
            new RecordManualPaymentRequest(10_000, h.Clock.UtcNow, "wire"),
            CancellationToken.None);

        var inv = await h.Db.Invoices.AsNoTracking().FirstAsync(i => i.Id == h.Invoice.Id);
        inv.AmountPaidMinor.Should().Be(10_000);
        inv.AmountDueMinor.Should().Be(0);
        inv.Status.Should().Be(InvoiceStatus.Paid);
        inv.PaidAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Two_partial_payments_settle_invoice()
    {
        using var h = new TestHarness();
        await h.Service.RecordAsync(h.Invoice.Id,
            new RecordManualPaymentRequest(3000, h.Clock.UtcNow, "cash"),
            CancellationToken.None);
        await h.Service.RecordAsync(h.Invoice.Id,
            new RecordManualPaymentRequest(7000, h.Clock.UtcNow.AddMinutes(5), "check", "chk-42"),
            CancellationToken.None);

        var inv = await h.Db.Invoices.AsNoTracking().FirstAsync(i => i.Id == h.Invoice.Id);
        inv.Status.Should().Be(InvoiceStatus.Paid);
        inv.AmountPaidMinor.Should().Be(10_000);

        var payments = await h.Db.Payments.AsNoTracking()
            .Where(p => p.InvoiceId == h.Invoice.Id).ToListAsync();
        payments.Count.Should().Be(2);
        payments.All(p => p.Provider == PaymentProviderType.Manual).Should().BeTrue();
        payments.Should().Contain(p => p.TransactionReference == "chk-42");
        payments.All(p => p.ProviderPaymentId == null).Should().BeTrue();
    }

    [Fact]
    public async Task Two_payments_with_same_transaction_reference_are_allowed()
    {
        // Architect-flagged regression: when manual references were
        // mapped to ProviderPaymentId they collided with the
        // (Provider, ProviderPaymentId) unique index. Now they're stored
        // on the dedicated TransactionReference column and re-using the
        // same check / wire reference (correction, refile, partial) is
        // accepted by the persistence layer.
        using var h = new TestHarness();
        await h.Service.RecordAsync(h.Invoice.Id,
            new RecordManualPaymentRequest(2000, h.Clock.UtcNow, "check", "chk-DUPE"),
            CancellationToken.None);
        await h.Service.RecordAsync(h.Invoice.Id,
            new RecordManualPaymentRequest(3000, h.Clock.UtcNow.AddMinutes(1), "check", "chk-DUPE"),
            CancellationToken.None);

        var rows = await h.Db.Payments.AsNoTracking()
            .Where(p => p.InvoiceId == h.Invoice.Id && p.TransactionReference == "chk-DUPE")
            .ToListAsync();
        rows.Count.Should().Be(2);
    }

    [Fact]
    public async Task Overpayment_is_rejected()
    {
        using var h = new TestHarness();
        var act = () => h.Service.RecordAsync(h.Invoice.Id,
            new RecordManualPaymentRequest(99_999, h.Clock.UtcNow, "cash"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidRelationshipException>()
            .WithMessage("*exceeds the invoice balance due*");

        // Invoice should be unchanged.
        var inv = await h.Db.Invoices.AsNoTracking().FirstAsync(i => i.Id == h.Invoice.Id);
        inv.AmountPaidMinor.Should().Be(0);
        (await h.Db.Payments.AsNoTracking().AnyAsync(p => p.InvoiceId == h.Invoice.Id))
            .Should().BeFalse();
    }

    [Fact]
    public async Task Already_paid_invoice_is_rejected()
    {
        using var h = new TestHarness();
        await h.Service.RecordAsync(h.Invoice.Id,
            new RecordManualPaymentRequest(10_000, h.Clock.UtcNow, "wire"),
            CancellationToken.None);

        var act = () => h.Service.RecordAsync(h.Invoice.Id,
            new RecordManualPaymentRequest(1, h.Clock.UtcNow, "cash"),
            CancellationToken.None);
        await act.Should().ThrowAsync<InvalidStateTransitionException>()
            .WithMessage("*already fully paid*");
    }

    [Fact]
    public async Task Void_invoice_is_rejected()
    {
        using var h = new TestHarness();
        h.Invoice.Void(h.Clock.UtcNow);
        await h.Db.SaveChangesAsync();

        var act = () => h.Service.RecordAsync(h.Invoice.Id,
            new RecordManualPaymentRequest(100, h.Clock.UtcNow, "cash"),
            CancellationToken.None);
        await act.Should().ThrowAsync<InvalidStateTransitionException>()
            .WithMessage("*void invoice*");
    }

    [Fact]
    public async Task Unknown_invoice_returns_not_found()
    {
        using var h = new TestHarness();
        var act = () => h.Service.RecordAsync(Guid.NewGuid(),
            new RecordManualPaymentRequest(100, h.Clock.UtcNow, "cash"),
            CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Validation_fails_on_zero_amount_or_default_paidAt_or_bad_method()
    {
        using var h = new TestHarness();

        var bad1 = () => h.Service.RecordAsync(h.Invoice.Id,
            new RecordManualPaymentRequest(0, h.Clock.UtcNow, "cash"), CancellationToken.None);
        await bad1.Should().ThrowAsync<ValidationException>();

        var bad2 = () => h.Service.RecordAsync(h.Invoice.Id,
            new RecordManualPaymentRequest(100, default, "cash"), CancellationToken.None);
        await bad2.Should().ThrowAsync<ValidationException>();

        var bad3 = () => h.Service.RecordAsync(h.Invoice.Id,
            new RecordManualPaymentRequest(100, h.Clock.UtcNow, "bitcoin"), CancellationToken.None);
        await bad3.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Persists_payment_with_invoice_currency_regardless_of_caller()
    {
        // Currency comes off the invoice itself, not the request body — so
        // even without a currency field the recorded payment is consistent.
        using var h = new TestHarness(currency: "EUR", lineAmountMinor: 2500, qty: 1);
        var resp = await h.Service.RecordAsync(h.Invoice.Id,
            new RecordManualPaymentRequest(2500, h.Clock.UtcNow, "wire"),
            CancellationToken.None);
        resp.Currency.Should().Be("EUR");
    }
}
