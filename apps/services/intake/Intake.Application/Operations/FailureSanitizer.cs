using Intake.Domain.Operations;

namespace Intake.Application.Operations;

public static class FailureSanitizer
{
    public static RecoveryFailure FromException(
        Exception exception,
        string code = "RECOVERY_FAILED",
        bool retryable = true) =>
        exception switch
        {
            OperationCanceledException => new(
                "RECOVERY_CANCELLED",
                IntakeFailureCategories.Timeout,
                "The recovery operation was cancelled.",
                false),
            TimeoutException => new(
                "DEPENDENCY_TIMEOUT",
                IntakeFailureCategories.Timeout,
                "A downstream dependency exceeded its bounded timeout.",
                retryable),
            UnauthorizedAccessException => new(
                "RECOVERY_UNAUTHORIZED",
                IntakeFailureCategories.Authorization,
                "The recovery operation was not authorized.",
                false),
            _ => new(
                code,
                IntakeFailureCategories.Unknown,
                "The recovery operation failed before a safe result was available.",
                retryable),
        };

    public static string Message(string? message, string fallback) =>
        string.IsNullOrWhiteSpace(message) || message.Length > 500
            ? fallback
            : message;
}