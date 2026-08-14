using Intake.Contracts;

namespace Intake.Application;

public interface IIntakeFoundationService
{
    IntakeServiceInfo GetServiceInfo();
}