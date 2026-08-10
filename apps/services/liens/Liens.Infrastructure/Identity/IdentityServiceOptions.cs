namespace Liens.Infrastructure.Identity;

public sealed class IdentityServiceOptions
{
    public const string SectionName = "IdentityService";

    public string? BaseUrl { get; set; }
    public int TimeoutSeconds { get; set; } = 5;
    public string? ProvisioningToken { get; set; }
    public string? AuthHeaderName { get; set; }
    public string? AuthHeaderValue { get; set; }
}
