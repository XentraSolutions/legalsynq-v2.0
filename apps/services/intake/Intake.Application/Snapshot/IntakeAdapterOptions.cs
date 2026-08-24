namespace Intake.Application.Snapshot;

public sealed class IntakeAdapterOptions
{
    public const string SectionName = "Intake:Adapters";
    public int ExecutionTimeoutSeconds { get; set; } = 30;
    public int MaxAttempts { get; set; } = 3;
}