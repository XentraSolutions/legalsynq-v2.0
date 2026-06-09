namespace Commerce.Api.Configuration;

public sealed class CommerceOptions
{
    public const string SectionName = "Commerce";

    public string ServiceName { get; set; } = "Commerce";
    public string Version { get; set; } = "1.0.0";
}
