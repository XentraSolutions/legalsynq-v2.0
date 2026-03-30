using FluentValidation;
using PlatformAuditEventService.DTOs;
using PlatformAuditEventService.Models;

namespace PlatformAuditEventService.Validators;

public sealed class IngestAuditEventRequestValidator : AbstractValidator<IngestAuditEventRequest>
{
    public IngestAuditEventRequestValidator()
    {
        RuleFor(x => x.Source)
            .NotEmpty().WithMessage("Source is required.")
            .MaximumLength(200).WithMessage("Source must not exceed 200 characters.");

        RuleFor(x => x.EventType)
            .NotEmpty().WithMessage("EventType is required.")
            .MaximumLength(200).WithMessage("EventType must not exceed 200 characters.")
            .Matches(@"^[a-z0-9_.-]+$").WithMessage("EventType must be lowercase alphanumeric with dots, dashes, or underscores (e.g. user.login).");

        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("Category is required.")
            .MaximumLength(100).WithMessage("Category must not exceed 100 characters.");

        RuleFor(x => x.Severity)
            .NotEmpty().WithMessage("Severity is required.")
            .Must(EventSeverity.IsValid)
            .WithMessage($"Severity must be one of: DEBUG, INFO, WARN, ERROR, CRITICAL.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.")
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters.");

        RuleFor(x => x.Outcome)
            .NotEmpty().WithMessage("Outcome is required.")
            .Must(EventOutcome.IsValid)
            .WithMessage("Outcome must be one of: SUCCESS, FAILURE, PARTIAL, UNKNOWN.");

        RuleFor(x => x.TenantId)
            .MaximumLength(100).When(x => x.TenantId is not null)
            .WithMessage("TenantId must not exceed 100 characters.");

        RuleFor(x => x.ActorId)
            .MaximumLength(200).When(x => x.ActorId is not null)
            .WithMessage("ActorId must not exceed 200 characters.");

        RuleFor(x => x.TargetId)
            .MaximumLength(200).When(x => x.TargetId is not null)
            .WithMessage("TargetId must not exceed 200 characters.");

        RuleFor(x => x.IpAddress)
            .MaximumLength(45).When(x => x.IpAddress is not null)
            .WithMessage("IpAddress must not exceed 45 characters (IPv6 max).");

        RuleFor(x => x.OccurredAtUtc)
            .Must(ts => ts is null || ts <= DateTimeOffset.UtcNow.AddMinutes(5))
            .WithMessage("OccurredAtUtc must not be in the future (tolerance: 5 min).");
    }
}
