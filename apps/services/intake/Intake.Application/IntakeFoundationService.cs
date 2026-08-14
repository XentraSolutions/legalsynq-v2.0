using Intake.Contracts;

namespace Intake.Application;

public sealed class IntakeFoundationService : IIntakeFoundationService
{
    public IntakeServiceInfo GetServiceInfo() => new(
        Service: "intake",
        DisplayName: "Synq Intake",
        Version: typeof(IntakeFoundationService).Assembly.GetName().Version?.ToString() ?? "1.0.0",
        Runtime: "dotnet-10.0",
        Timestamp: DateTimeOffset.UtcNow);
}