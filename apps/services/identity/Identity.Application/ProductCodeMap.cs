namespace Identity.Application;

/// <summary>
/// Single source of truth for DB-product-code ↔ frontend-product-code translations.
/// Both directions are maintained here so no code sites diverge over time.
/// </summary>
public static class ProductCodeMap
{
    /// <summary>DB Code (e.g. "SYNQ_FUND") → Frontend code (e.g. "SynqFund").</summary>
    public static readonly IReadOnlyDictionary<string, string> DbToFrontend
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SYNQ_FUND"]        = "SynqFund",
            ["SYNQ_LIENS"]       = "SynqLien",
            ["SYNQ_CARECONNECT"] = "CareConnect",
            ["SYNQ_AI"]          = "SynqAI",
            ["SYNQ_INSIGHTS"]    = "SynqInsights",
            ["SYNQ_BILL"]        = "SynqBill",
            ["SYNQ_RX"]          = "SynqRx",
            ["SYNQ_PAYOUT"]      = "SynqPayout",
        };

    /// <summary>Frontend code (e.g. "SynqFund") → DB Code (e.g. "SYNQ_FUND").</summary>
    public static readonly IReadOnlyDictionary<string, string> FrontendToDb
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SynqFund"]    = "SYNQ_FUND",
            ["SynqLien"]    = "SYNQ_LIENS",
            ["CareConnect"] = "SYNQ_CARECONNECT",
            ["SynqAI"]      = "SYNQ_AI",
            ["SynqInsights"] = "SYNQ_INSIGHTS",
            ["SynqBill"]    = "SYNQ_BILL",
            ["SynqRx"]      = "SYNQ_RX",
            ["SynqPayout"]  = "SYNQ_PAYOUT",
        };
}
