using Commerce.Application.Common.Time;
using Commerce.Infrastructure.Billing.Services;
using Commerce.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Tests.Billing;

internal sealed class BillingFixedClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
}

internal sealed class BillingTestHost : IDisposable
{
    public CommerceDbContext Db { get; }
    public BillingFixedClock Clock { get; } = new();

    public BillingAccountService AccountService { get; }
    public BillingAccountExternalRefService ExternalRefService { get; }
    public BillingContactService ContactService { get; }
    public BillingProfileService ProfileService { get; }
    public BillingAccountAuditService AuditService { get; }

    public BillingTestHost()
    {
        var opts = new DbContextOptionsBuilder<CommerceDbContext>()
            .UseInMemoryDatabase($"billing-tests-{Guid.NewGuid()}")
            .Options;
        Db = new CommerceDbContext(opts);

        var appAsm = typeof(Commerce.Application.DependencyInjection).Assembly;
        IValidator<T> Resolve<T>()
        {
            var validatorType = appAsm.GetTypes()
                .First(t => !t.IsAbstract && typeof(IValidator<T>).IsAssignableFrom(t));
            return (IValidator<T>)Activator.CreateInstance(validatorType)!;
        }

        var audit = new BillingAuditWriter(Db, Clock);
        var numbers = new AccountNumberGenerator(Db);

        AccountService = new BillingAccountService(Db, Clock,
            Resolve<Contracts.Billing.CreateBillingAccountRequest>(),
            Resolve<Contracts.Billing.UpdateBillingAccountRequest>(),
            numbers, audit);

        ExternalRefService = new BillingAccountExternalRefService(Db, Clock,
            Resolve<Contracts.Billing.CreateExternalRefRequest>(),
            Resolve<Contracts.Billing.UpdateExternalRefRequest>(),
            audit);

        ContactService = new BillingContactService(Db, Clock,
            Resolve<Contracts.Billing.CreateBillingContactRequest>(),
            Resolve<Contracts.Billing.UpdateBillingContactRequest>(),
            audit);

        ProfileService = new BillingProfileService(Db, Clock,
            Resolve<Contracts.Billing.UpdateBillingProfileRequest>(),
            audit);

        AuditService = new BillingAccountAuditService(Db);
    }

    public void Dispose() => Db.Dispose();
}
