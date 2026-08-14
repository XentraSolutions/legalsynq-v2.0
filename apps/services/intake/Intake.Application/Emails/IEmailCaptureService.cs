using Intake.Contracts.Emails;

namespace Intake.Application.Emails;

public interface IEmailCaptureService
{
    Task<InboundEmailCaptureResponse> CaptureAsync(
        CaptureInboundEmailCommand command,
        string? correlationId,
        CancellationToken cancellationToken);
}