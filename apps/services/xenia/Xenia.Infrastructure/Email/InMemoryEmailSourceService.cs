using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Xenia.Application.Email;
using Xenia.Domain.Email;

namespace Xenia.Infrastructure.Email;

/// <summary>
/// Pure in-memory implementation of IEmailSourceService.
///
/// Used when no ConnectionStrings__XeniaDb is configured.
/// All CRUD operations work correctly; data is lost on restart.
/// Registered as Singleton so the dictionary survives the request scope.
/// </summary>
internal sealed class InMemoryEmailSourceService : IEmailSourceService
{
    private readonly ConcurrentDictionary<Guid, EmailSource> _sources = new();
    private readonly ILogger<InMemoryEmailSourceService> _logger;

    public InMemoryEmailSourceService(ILogger<InMemoryEmailSourceService> logger)
    {
        _logger = logger;
        _logger.LogWarning(
            "[Xenia] No ConnectionStrings__XeniaDb configured — using volatile in-memory store. " +
            "Email sources will be lost on restart.");
    }

    public Task<IReadOnlyList<EmailSourceDto>> GetSourcesAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        var result = _sources.Values
            .Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .OrderBy(s => s.DisplayName)
            .Select(EmailSourceDto.FromEntity)
            .ToList();
        return Task.FromResult<IReadOnlyList<EmailSourceDto>>(result);
    }

    public Task<EmailSourceDto?> GetSourceAsync(
        Guid tenantId, Guid sourceId, CancellationToken ct = default)
    {
        _sources.TryGetValue(sourceId, out var source);
        if (source is null || source.TenantId != tenantId || source.IsDeleted)
            return Task.FromResult<EmailSourceDto?>(null);
        return Task.FromResult<EmailSourceDto?>(EmailSourceDto.FromEntity(source));
    }

    public Task<EmailSourceDto> CreateSourceAsync(
        Guid tenantId,
        Guid? actorId,
        CreateEmailSourceRequest request,
        CancellationToken ct = default)
    {
        if (!Enum.TryParse<EmailProviderType>(request.ProviderType, ignoreCase: true, out var providerType))
            throw new ArgumentException($"Unknown provider type: '{request.ProviderType}'.");

        if (!Enum.TryParse<EmailAuthType>(request.AuthType, ignoreCase: true, out var authType))
            throw new ArgumentException($"Unknown auth type: '{request.AuthType}'.");

        if (!EmailProviderDefinitions.IsAuthTypeSupported(providerType, authType))
            throw new ArgumentException(
                $"Auth type '{authType}' is not supported by provider '{providerType}'.");

        var source = new EmailSource(
            Guid.CreateVersion7(),
            tenantId,
            request.DisplayName,
            request.EmailAddress,
            providerType,
            authType,
            actorId);

        source.SetDescription(request.Description);
        source.SetConnectionDetails(
            request.IncomingHost,
            request.IncomingPort,
            request.UseTls,
            request.MailboxFolder,
            request.Username);
        source.SetSecretReference(request.SecretReferenceId);
        source.SetOAuthConnectionRef(request.OAuthConnectionRef);

        if (!request.Enabled)
            source.Disable(actorId);

        _sources[source.Id] = source;

        _logger.LogInformation(
            "[InMemory] EmailSource created. TenantId={TenantId} SourceId={SourceId} Provider={Provider}",
            tenantId, source.Id, providerType);

        return Task.FromResult(EmailSourceDto.FromEntity(source));
    }

    public Task<EmailSourceDto?> UpdateSourceAsync(
        Guid tenantId,
        Guid sourceId,
        Guid? actorId,
        UpdateEmailSourceRequest request,
        CancellationToken ct = default)
    {
        _sources.TryGetValue(sourceId, out var source);
        if (source is null || source.TenantId != tenantId || source.IsDeleted)
            return Task.FromResult<EmailSourceDto?>(null);

        if (request.ExpectedRowVersion.HasValue && source.RowVersion != request.ExpectedRowVersion.Value)
            throw new InvalidOperationException(
                $"Concurrency conflict: expected row version {request.ExpectedRowVersion} but found {source.RowVersion}. Reload and retry.");

        if (request.DisplayName is not null)
            source.UpdateDisplayName(request.DisplayName);

        if (request.Description is not null || request.DisplayName is not null)
            source.SetDescription(request.Description);

        source.SetConnectionDetails(
            request.IncomingHost ?? source.IncomingHost,
            request.IncomingPort ?? source.IncomingPort,
            request.UseTls ?? source.UseTls,
            request.MailboxFolder ?? source.MailboxFolder,
            request.Username ?? source.Username);

        if (request.SecretReferenceId is not null)
            source.SetSecretReference(request.SecretReferenceId);

        if (request.OAuthConnectionRef is not null)
            source.SetOAuthConnectionRef(request.OAuthConnectionRef);

        source.SetUpdatedBy(actorId);

        return Task.FromResult<EmailSourceDto?>(EmailSourceDto.FromEntity(source));
    }

    public Task<bool> DeleteSourceAsync(
        Guid tenantId, Guid sourceId, Guid? actorId, CancellationToken ct = default)
    {
        _sources.TryGetValue(sourceId, out var source);
        if (source is null || source.TenantId != tenantId || source.IsDeleted)
            return Task.FromResult(false);

        source.SoftDelete(actorId);
        return Task.FromResult(true);
    }

    public Task<bool> EnableSourceAsync(
        Guid tenantId, Guid sourceId, Guid? actorId, CancellationToken ct = default)
    {
        _sources.TryGetValue(sourceId, out var source);
        if (source is null || source.TenantId != tenantId || source.IsDeleted)
            return Task.FromResult(false);

        source.Enable(actorId);
        return Task.FromResult(true);
    }

    public Task<bool> DisableSourceAsync(
        Guid tenantId, Guid sourceId, Guid? actorId, CancellationToken ct = default)
    {
        _sources.TryGetValue(sourceId, out var source);
        if (source is null || source.TenantId != tenantId || source.IsDeleted)
            return Task.FromResult(false);

        source.Disable(actorId);
        return Task.FromResult(true);
    }

    public Task<EmailValidationResultDto> ValidateSourceAsync(
        Guid tenantId,
        Guid sourceId,
        Guid? actorId,
        string? correlationId,
        CancellationToken ct = default)
    {
        _sources.TryGetValue(sourceId, out var source);
        if (source is null || source.TenantId != tenantId || source.IsDeleted)
        {
            return Task.FromResult(new EmailValidationResultDto
            {
                SourceId = sourceId,
                Success = false,
                Result = EmailValidationResult.ConfigurationInvalid.ToString(),
                DurationMs = 0,
                ErrorCode = "SOURCE_NOT_FOUND",
                SafeErrorSummary = "Email source not found.",
                ValidatedAt = DateTime.UtcNow,
            });
        }

        source.RecordValidationFailure(
            "NO_DATABASE",
            "Live connectivity validation requires a database. Configure ConnectionStrings__XeniaDb to enable.",
            0, actorId);

        return Task.FromResult(new EmailValidationResultDto
        {
            SourceId = sourceId,
            Success = false,
            Result = EmailValidationResult.ValidatorUnavailable.ToString(),
            DurationMs = 0,
            ErrorCode = "NO_DATABASE",
            SafeErrorSummary = "Validation unavailable — no database configured.",
            ValidatedAt = DateTime.UtcNow,
        });
    }

    public Task<IReadOnlyList<ValidationHistoryDto>> GetValidationHistoryAsync(
        Guid tenantId,
        Guid sourceId,
        int limit,
        CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<ValidationHistoryDto>>(
            Array.Empty<ValidationHistoryDto>());
    }
}
