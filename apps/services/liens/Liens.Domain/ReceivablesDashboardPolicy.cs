namespace Liens.Domain;

public static class ReceivablesDashboardPolicy
{
    public const decimal MediumRiskPastDuePercent = 10m;
    public const decimal HighRiskPastDuePercent = 25m;

    public static string ResolveRiskLevel(decimal pastDuePercent)
    {
        if (pastDuePercent >= HighRiskPastDuePercent)
            return "High";
        if (pastDuePercent >= MediumRiskPastDuePercent)
            return "Medium";
        return "Low";
    }

    public static string ResolveOperationalStatus(
        string lienStatus,
        bool hasSettlement,
        decimal settlementAmount,
        decimal paymentAmount,
        bool hasReduction)
    {
        if (settlementAmount > 0m && paymentAmount >= settlementAmount)
            return "paid";
        if (hasSettlement || string.Equals(lienStatus, Enums.LienStatus.Settled, StringComparison.Ordinal))
            return "settled";
        if (hasReduction)
            return "inReduction";
        if (!Enums.LienStatus.Terminal.Contains(lienStatus))
            return "active";
        return "otherClosed";
    }
}
