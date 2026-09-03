namespace Liens.Domain.Entities;

public sealed class CaseNumberReservation
{
    public Guid TenantId { get; private set; }
    public string CaseNumber { get; private set; } = string.Empty;
    public DateTime ReservedAtUtc { get; private set; }

    private CaseNumberReservation() { }

    public static CaseNumberReservation Create(Guid tenantId, string caseNumber)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(caseNumber))
            throw new ArgumentException("Case number is required.", nameof(caseNumber));

        return new CaseNumberReservation
        {
            TenantId = tenantId,
            CaseNumber = caseNumber.Trim(),
            ReservedAtUtc = DateTime.UtcNow,
        };
    }
}
