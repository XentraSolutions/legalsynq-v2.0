namespace BuildingBlocks.Authorization;

public static class ProductCodes
{
    public const string SynqCareConnect = "SYNQ_CARECONNECT";
    public const string SynqFund        = "SYNQ_FUND";
    public const string SynqLiens       = "SYNQ_LIENS";
    public const string SynqPay         = "SYNQ_PAY";
    public const string Xenia           = "XENIA";
    /// <summary>LS-ID-TNT-010: Synq Insights analytics product.</summary>
    public const string SynqInsights    = "SYNQ_INSIGHTS";
    /// <summary>LS-ID-TNT-010: Synq Comms messaging product.</summary>
    public const string SynqComms       = "SYNQ_COMMS";
    /// <summary>
    /// LS-ID-TNT-011: Virtual pseudo-product code used as the catalog anchor for
    /// tenant-level permission codes (TENANT.*).  Never enabled in TenantProducts;
    /// not a subscribable product for tenants.
    /// </summary>
    public const string SynqPlatform    = "SYNQ_PLATFORM";

    public static string Normalize(string productCode)
    {
        if (string.IsNullOrWhiteSpace(productCode))
            return string.Empty;

        return productCode.Trim().ToUpperInvariant() switch
        {
            "CARECONNECT" or "SYNQCARECONNECT" or SynqCareConnect => SynqCareConnect,
            "SYNQFUND" or SynqFund => SynqFund,
            "SYNQLIEN" or "SYNQLIENS" or "SYNQ_LIEN" or SynqLiens => SynqLiens,
            "SYNQPAY" or SynqPay => SynqPay,
            "SYNQINSIGHTS" or SynqInsights => SynqInsights,
            "SYNQCOMMS" or SynqComms => SynqComms,
            "SYNQAI" or "SYNQ_AI" or "XENIA" => Xenia,
            "SYNQPLATFORM" or SynqPlatform => SynqPlatform,
            _ => productCode.Trim().ToUpperInvariant(),
        };
    }
}
