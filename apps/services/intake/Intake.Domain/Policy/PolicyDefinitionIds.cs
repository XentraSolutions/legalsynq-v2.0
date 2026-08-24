namespace Intake.Domain.Policy;

public static class PolicyDefinitionIds
{
    public static readonly Guid LienIntakePolicyV1 =
        new("d9dcf7c5-6b13-4f87-a9b9-793af934b101");

    public static readonly DateTimeOffset SeedTimestamp =
        new(2026, 8, 14, 0, 0, 0, TimeSpan.Zero);
}