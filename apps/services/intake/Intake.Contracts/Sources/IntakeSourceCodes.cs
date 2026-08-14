namespace Intake.Contracts.Sources;

public static class IntakeSourceTypes
{
    public const string Email = "EMAIL";
    public const string Manual = "MANUAL";
}

public static class IntakeSourcePurposes
{
    public const string LienIntake = "LIEN_INTAKE";
}

public static class IntakeSourceProviders
{
    public const string Microsoft365 = "MICROSOFT_365";
    public const string GoogleWorkspace = "GOOGLE_WORKSPACE";
    public const string Generic = "GENERIC";
}

public static class IntakeSourceValidationStatuses
{
    public const string NotValidated = "NOT_VALIDATED";
    public const string Valid = "VALID";
    public const string Invalid = "INVALID";
    public const string Unavailable = "UNAVAILABLE";
}