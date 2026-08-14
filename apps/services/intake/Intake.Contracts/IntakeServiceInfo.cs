namespace Intake.Contracts;

public sealed record IntakeServiceInfo(
    string Service,
    string DisplayName,
    string Version,
    string Runtime,
    DateTimeOffset Timestamp);