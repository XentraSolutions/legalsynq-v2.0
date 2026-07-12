namespace Identity.Domain;

public static class ProductCodeNormalizer
{
    public static string Normalize(string productCode)
    {
        if (string.IsNullOrWhiteSpace(productCode))
            return string.Empty;

        return productCode.Trim().ToUpperInvariant() switch
        {
            "CARECONNECT" or "SYNQCARECONNECT" or ProductCodes.SynqCareConnect => ProductCodes.SynqCareConnect,
            "SYNQFUND" or ProductCodes.SynqFund => ProductCodes.SynqFund,
            "SYNQLIEN" or "SYNQLIENS" or "SYNQ_LIEN" or ProductCodes.SynqLiens => ProductCodes.SynqLiens,
            "SYNQPAY" or ProductCodes.SynqPay => ProductCodes.SynqPay,
            "XENIA" or "SYNQAI" or "SYNQ_AI" => ProductCodes.Xenia,
            _ => productCode.Trim().ToUpperInvariant(),
        };
    }

    public static string? NormalizeOptional(string? productCode) =>
        string.IsNullOrWhiteSpace(productCode) ? null : Normalize(productCode);
}
