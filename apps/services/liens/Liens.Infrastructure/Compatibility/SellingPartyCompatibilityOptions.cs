namespace Liens.Infrastructure.Compatibility;

public sealed class SellingPartyCompatibilityOptions
{
    public const string SectionName = "SellingPartyCompatibility";

    public bool BackfillEnabled { get; set; }
    public bool DualWriteEnabled { get; set; }
    public bool ShadowReadEnabled { get; set; }
    public bool CanonicalReadEnabled { get; set; }
    public int BackfillBatchSize { get; set; } = 100;
    public int BackfillMaxRetries { get; set; } = 3;
    public int BackfillRetryDelayMilliseconds { get; set; } = 250;
}
