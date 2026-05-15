using TenantBilling.Domain.Statements;
using TenantBilling.Domain.Tests.Fakes;
using Xunit;

namespace TenantBilling.Domain.Tests;

public class StatementNumberGeneratorTests
{
    [Fact]
    public async Task NextAsync_FirstForYear_ReturnsOne()
    {
        var repo = new InMemoryCustomerStatementRepository();
        var gen = new StatementNumberGenerator(repo);
        Assert.Equal("STMT-2026-000001", await gen.NextAsync(Guid.NewGuid(), 2026));
    }

    [Fact]
    public async Task NextAsync_IncrementsPerTenant()
    {
        var repo = new InMemoryCustomerStatementRepository();
        var gen = new StatementNumberGenerator(repo);
        var tenant = Guid.NewGuid();

        // Seed an existing statement.
        await repo.AddAsync(new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenant,
            CustomerId = Guid.NewGuid(),
            StatementNumber = "STMT-2026-000007",
            PeriodStart = new DateTime(2026, 1, 1),
            PeriodEnd = new DateTime(2026, 1, 31),
            GeneratedAtUtc = DateTime.UtcNow,
            Status = "Generated",
            Currency = "USD",
            StatementSnapshotJson = "{}",
        });

        Assert.Equal("STMT-2026-000008", await gen.NextAsync(tenant, 2026));
        Assert.Equal("STMT-2026-000001", await gen.NextAsync(Guid.NewGuid(), 2026));
        Assert.Equal("STMT-2027-000001", await gen.NextAsync(tenant, 2027));
    }

    [Fact]
    public async Task NextAsync_RejectsEmptyTenant()
    {
        var gen = new StatementNumberGenerator(new InMemoryCustomerStatementRepository());
        await Assert.ThrowsAsync<ArgumentException>(() => gen.NextAsync(Guid.Empty, 2026));
    }
}
