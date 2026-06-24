namespace TenantBilling.Domain.Tests.Helpers;

/// <summary>
/// STAT-B02 — Minimal deterministic <see cref="TimeProvider"/> stub
/// for the persistence + template tests. Public to the test
/// assembly because the existing private FakeTimeProvider in
/// <c>CustomerStatementServiceTests</c> is not visible here.
/// </summary>
internal sealed class TestTimeProvider : TimeProvider
{
    private DateTimeOffset _now;
    public TestTimeProvider(DateTime utc)
        => _now = new DateTimeOffset(utc, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan delta) => _now = _now.Add(delta);
}
