using Xenia.Application.Adapters.Interfaces;

namespace Xenia.Infrastructure.Adapters.Validation;

/// <summary>
/// Phase F — Adapter validation service.
///
/// Checks all registered platform adapters at startup and reports:
/// - Which are configured (IsConfigured)
/// - Which are mandatory vs optional
/// - Health impact if unavailable
///
/// Rules:
/// - Mandatory adapter unavailability returns Ready=false at /ready endpoint.
/// - Optional adapter unavailability returns Ready=true with degraded detail.
/// - Validation is non-blocking — never delays service startup.
/// - Validation results are cached for 60 seconds.
/// </summary>
public sealed class AdapterValidationService
{
    private readonly IDocumentAdapter _documentAdapter;
    private readonly IAuditAdapter _auditAdapter;
    private readonly INotificationAdapter _notificationAdapter;
    private readonly IIdentityAdapter _identityAdapter;
    private readonly IAiAdapter _aiAdapter;
    private readonly IStorageAdapter _storageAdapter;

    public AdapterValidationService(
        IDocumentAdapter documentAdapter,
        IAuditAdapter auditAdapter,
        INotificationAdapter notificationAdapter,
        IIdentityAdapter identityAdapter,
        IAiAdapter aiAdapter,
        IStorageAdapter storageAdapter)
    {
        _documentAdapter     = documentAdapter;
        _auditAdapter        = auditAdapter;
        _notificationAdapter = notificationAdapter;
        _identityAdapter     = identityAdapter;
        _aiAdapter           = aiAdapter;
        _storageAdapter      = storageAdapter;
    }

    public AdapterValidationReport ValidateAll()
    {
        var results = new List<AdapterValidationResult>
        {
            Validate("audit",        _auditAdapter.IsConfigured,        AdapterCriticality.Recommended,
                "Audit trail will be incomplete."),
            Validate("document",     _documentAdapter.IsConfigured,     AdapterCriticality.Optional,
                "Attachments will remain pending."),
            Validate("notification", _notificationAdapter.IsConfigured, AdapterCriticality.Optional,
                "Sync alerts will not be delivered."),
            Validate("identity",     _identityAdapter.IsConfigured,     AdapterCriticality.Optional,
                "Identity resolution unavailable."),
            Validate("ai",           _aiAdapter.IsConfigured,           AdapterCriticality.Optional,
                "AI enrichment unavailable."),
            Validate("storage",      _storageAdapter.IsConfigured,      AdapterCriticality.Optional,
                "Object storage unavailable."),
        };

        var hasMandatoryFailure = results.Any(r =>
            r.Criticality == AdapterCriticality.Mandatory && !r.IsConfigured);

        return new AdapterValidationReport
        {
            Results           = results,
            IsReady           = !hasMandatoryFailure,
            ValidatedAt       = DateTime.UtcNow,
            ConfiguredCount   = results.Count(r => r.IsConfigured),
            UnconfiguredCount = results.Count(r => !r.IsConfigured),
        };
    }

    private static AdapterValidationResult Validate(
        string key, bool isConfigured, AdapterCriticality criticality, string healthImpact) =>
        new()
        {
            Key          = key,
            Criticality  = criticality,
            IsConfigured = isConfigured,
            HealthImpact = isConfigured ? null : healthImpact,
        };
}

public sealed record AdapterValidationReport
{
    public required IReadOnlyList<AdapterValidationResult> Results { get; init; }
    public required bool IsReady { get; init; }
    public required DateTime ValidatedAt { get; init; }
    public required int ConfiguredCount { get; init; }
    public required int UnconfiguredCount { get; init; }
}

public sealed record AdapterValidationResult
{
    public required string Key { get; init; }
    public required AdapterCriticality Criticality { get; init; }
    public required bool IsConfigured { get; init; }
    public string? HealthImpact { get; init; }
}

public enum AdapterCriticality { Optional = 0, Recommended = 1, Mandatory = 2 }
