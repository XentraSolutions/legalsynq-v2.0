namespace Intake.Domain.Matching;

public static class MatchingDefinitionIds
{
    public static readonly Guid LienIntakeMatchingProfileV1 =
        Guid.Parse("5a54cc3e-748d-4f3d-b10b-000000000001");

    public static readonly DateTimeOffset SeedTimestamp =
        new(2026, 8, 14, 0, 0, 0, TimeSpan.Zero);
}