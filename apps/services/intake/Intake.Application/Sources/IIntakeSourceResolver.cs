using Intake.Contracts.Sources;

namespace Intake.Application.Sources;

public interface IIntakeSourceResolver
{
    Task<ResolvedIntakeSource> ResolveByEmailAddressAsync(
        string emailAddress,
        CancellationToken cancellationToken);
}