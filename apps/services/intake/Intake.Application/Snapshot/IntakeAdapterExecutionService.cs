using System.Text.Json;
using Intake.Application.Configuration;
using Intake.Contracts.Snapshot;
using Intake.Domain.Snapshot;
using Microsoft.Extensions.Logging;

namespace Intake.Application.Snapshot;

public sealed class IntakeAdapterExecutionService(
    IApprovedIntakeSnapshotService snapshotService,
    IAdapterExecutionRepository executionRepository,
    IIntakeDestinationAdapterRegistry adapterRegistry,
    IntakeAdapterOptions options,
    ISnapshotAuditSink auditSink,
    ILogger<IntakeAdapterExecutionService> logger) : IIntakeAdapterExecutionService
{
    public IReadOnlyList<AdapterDescriptor> ListAdapters() => adapterRegistry.List();

    public async Task<AdapterExecutionResponse> ExecuteAsync(
        Guid tenantId,
        Guid snapshotId,
        string adapterCode,
        Guid actorUserId,
        bool dryRun,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        ValidateContext(tenantId, actorUserId);
        var snapshot = await snapshotService.GetAsync(tenantId, snapshotId, cancellationToken)
            ?? throw IntakeConfigurationException.NotFound(
                IntakeAdapterFailureCodes.NotFound,
                "The approved snapshot was not found.");
        var adapter = adapterRegistry.GetRequired(adapterCode);
        EnsureSnapshotCanExecute(snapshot, adapter.Descriptor);
        if (dryRun && !adapter.Descriptor.SupportsDryRun)
            throw IntakeConfigurationException.BadRequest(
                IntakeAdapterFailureCodes.Disabled,
                $"Adapter '{adapterCode}' does not support dry-run execution.");

        var executionKey = BuildExecutionKey(
            tenantId,
            snapshotId,
            adapter.Descriptor.AdapterCode,
            adapter.Descriptor.AdapterVersion,
            dryRun);
        var claim = await executionRepository.TryClaimAsync(
            tenantId,
            snapshotId,
            adapter.Descriptor.AdapterCode,
            adapter.Descriptor.AdapterVersion,
            executionKey,
            executionKey,
            actorUserId,
            false,
            Math.Clamp(options.MaxAttempts, 1, 10),
            cancellationToken);
        if (!claim.Claimed)
            return MapResponse(claim.Execution);
        await AuditAsync(
            "adapter_requested",
            claim.Execution,
            snapshot.Payload,
            tenantId,
            null,
            cancellationToken);

        return await RunClaimedAsync(
            claim.Execution,
            adapter,
            snapshot.Payload,
            tenantId,
            snapshotId,
            actorUserId,
            dryRun,
            correlationId,
            cancellationToken);
    }

    public async Task<AdapterExecutionResponse> RetryAsync(
        Guid tenantId,
        Guid snapshotId,
        Guid executionId,
        Guid actorUserId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        ValidateContext(tenantId, actorUserId);
        var execution = await executionRepository.FindAsync(
            tenantId,
            executionId,
            cancellationToken) ?? throw IntakeConfigurationException.NotFound(
            IntakeAdapterFailureCodes.NotFound,
            "The adapter execution was not found.");
        if (execution.SnapshotId != snapshotId)
            throw IntakeConfigurationException.NotFound(
                IntakeAdapterFailureCodes.NotFound,
                "The adapter execution was not found.");
        var snapshot = await snapshotService.GetAsync(tenantId, snapshotId, cancellationToken)
            ?? throw IntakeConfigurationException.NotFound(
                IntakeAdapterFailureCodes.NotFound,
                "The approved snapshot was not found.");
        var adapter = adapterRegistry.GetRequired(execution.AdapterCode);
        EnsureSnapshotCanExecute(snapshot, adapter.Descriptor);
        if (!adapter.Descriptor.SupportsRetry)
            throw IntakeConfigurationException.BadRequest(
                IntakeAdapterFailureCodes.Disabled,
                $"Adapter '{execution.AdapterCode}' does not support retry.");

        var claim = await executionRepository.TryClaimAsync(
            tenantId,
            snapshotId,
            execution.AdapterCode,
            execution.AdapterVersion,
            execution.ExecutionKey,
            execution.IdempotencyKey,
            actorUserId,
            true,
            Math.Clamp(options.MaxAttempts, 1, 10),
            cancellationToken);
        if (!claim.Claimed)
            return MapResponse(claim.Execution);
        await AuditAsync(
            "adapter_retry_requested",
            claim.Execution,
            snapshot.Payload,
            tenantId,
            null,
            cancellationToken);
        return await RunClaimedAsync(
            claim.Execution,
            adapter,
            snapshot.Payload,
            tenantId,
            snapshotId,
            actorUserId,
            execution.IdempotencyKey.EndsWith("|DRYRUN", StringComparison.Ordinal),
            correlationId,
            cancellationToken);
    }

    public async Task<AdapterExecutionResponse?> GetAsync(
        Guid tenantId,
        Guid snapshotId,
        Guid executionId,
        CancellationToken cancellationToken)
    {
        var execution = await executionRepository.FindAsync(
            tenantId,
            executionId,
            cancellationToken);
        return execution is null || execution.SnapshotId != snapshotId
            ? null
            : MapResponse(execution);
    }

    public async Task<IReadOnlyList<AdapterExecutionResponse>> ListAsync(
        Guid tenantId,
        Guid snapshotId,
        CancellationToken cancellationToken)
    {
        var items = await executionRepository.ListBySnapshotAsync(
            tenantId,
            snapshotId,
            cancellationToken);
        return items.Select(MapResponse).ToArray();
    }

    private async Task<AdapterExecutionResponse> RunClaimedAsync(
        IntakeAdapterExecution execution,
        IIntakeDestinationAdapter adapter,
        ApprovedIntakeSnapshotV1 snapshot,
        Guid tenantId,
        Guid snapshotId,
        Guid actorUserId,
        bool dryRun,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var context = new IntakeAdapterRequestContext(
            tenantId,
            snapshotId,
            correlationId ?? string.Empty,
            execution.IdempotencyKey,
            actorUserId,
            dryRun);
        var validation = adapter.Validate(snapshot, context);
        if (!validation.IsValid)
        {
            await executionRepository.FinalizeAsync(
                tenantId,
                execution.Id,
                execution.ClaimToken,
                execution.AttemptNumber,
                IntakeAdapterExecutionStatuses.Failed,
                validation.FailureCode ?? IntakeAdapterFailureCodes.ValidationFailed,
                validation.FailureMessage,
                "{}",
                [],
                CancellationToken.None);
            var response = await GetRequiredResponseAsync(tenantId, snapshotId, execution.Id, CancellationToken.None);
            await AuditAsync("adapter_completed", execution, snapshot, tenantId, response.Status, CancellationToken.None);
            return response;
        }

        var timeoutSeconds = Math.Clamp(options.ExecutionTimeoutSeconds, 1, 300);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeout.Token);
        AdapterExecutionResult result;
        try
        {
            result = await adapter.ExecuteAsync(snapshot, context, linked.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await executionRepository.FinalizeAsync(
                tenantId,
                execution.Id,
                execution.ClaimToken,
                execution.AttemptNumber,
                IntakeAdapterExecutionStatuses.Cancelled,
                IntakeAdapterFailureCodes.ExecutionFailed,
                "Adapter execution was cancelled.",
                "{}",
                [],
                CancellationToken.None);
            var response = await GetRequiredResponseAsync(tenantId, snapshotId, execution.Id, CancellationToken.None);
            await AuditAsync("adapter_cancelled", execution, snapshot, tenantId, response.Status, CancellationToken.None);
            return response;
        }
        catch (OperationCanceledException)
        {
            result = new(
                false,
                true,
                IntakeAdapterExecutionStatuses.Retryable,
                IntakeAdapterFailureCodes.Timeout,
                "Adapter execution exceeded its bounded timeout.",
                [],
                []);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Intake adapter execution failed. TenantId={TenantId} SnapshotId={SnapshotId} AdapterCode={AdapterCode} ExecutionId={ExecutionId}",
                tenantId,
                snapshotId,
                adapter.Descriptor.AdapterCode,
                execution.Id);
            result = new(
                false,
                true,
                IntakeAdapterExecutionStatuses.Retryable,
                IntakeAdapterFailureCodes.ExecutionFailed,
                "The adapter failed before returning a result.",
                [],
                []);
        }

        var status = result.Success
            ? IntakeAdapterExecutionStatuses.Succeeded
            : result.Retryable
                ? IntakeAdapterExecutionStatuses.Retryable
                : IntakeAdapterExecutionStatuses.Failed;
        var resultJson = JsonSerializer.Serialize(new
        {
            result.Warnings,
            result.ExternalReferences,
        });
        await executionRepository.FinalizeAsync(
            tenantId,
            execution.Id,
            execution.ClaimToken,
            execution.AttemptNumber,
            status,
            result.FailureCode,
            result.FailureMessage,
            resultJson,
            result.ExternalReferences,
            CancellationToken.None);
        var completed = await GetRequiredResponseAsync(tenantId, snapshotId, execution.Id, CancellationToken.None);
        await AuditAsync("adapter_completed", execution, snapshot, tenantId, completed.Status, CancellationToken.None);
        return completed;
    }

    private Task AuditAsync(
        string action,
        IntakeAdapterExecution execution,
        ApprovedIntakeSnapshotV1 snapshot,
        Guid tenantId,
        string? status,
        CancellationToken cancellationToken) =>
        auditSink.RecordAsync(
            new SnapshotAuditEntry(
                action,
                tenantId,
                execution.SnapshotId,
                snapshot.Provenance.ArtifactId,
                snapshot.Provenance.ReviewId,
                execution.RequestedByUserId,
                status,
                execution.AdapterCode,
                execution.Id,
                null),
            cancellationToken);

    private async Task<AdapterExecutionResponse> GetRequiredResponseAsync(
        Guid tenantId,
        Guid snapshotId,
        Guid executionId,
        CancellationToken cancellationToken) =>
        await GetAsync(tenantId, snapshotId, executionId, cancellationToken)
        ?? throw IntakeConfigurationException.Conflict(
            IntakeAdapterFailureCodes.ConcurrencyConflict,
            "The adapter execution could not be reloaded after finalization.");

    private static void EnsureSnapshotCanExecute(
        ApprovedSnapshotResponse snapshot,
        AdapterDescriptor descriptor)
    {
        if (!string.Equals(snapshot.Status, ApprovedSnapshotStatuses.Ready, StringComparison.Ordinal) ||
            !snapshot.IsCurrent)
            throw IntakeConfigurationException.Conflict(
                IntakeAdapterFailureCodes.ValidationFailed,
                "Only the current ready approved snapshot can be executed.");
        if (!descriptor.SupportedSnapshotSchemas.Contains(
                $"{snapshot.SchemaCode}:{snapshot.SchemaVersion}",
                StringComparer.Ordinal) &&
            !descriptor.SupportedSnapshotSchemas.Contains(snapshot.SchemaCode, StringComparer.Ordinal))
            throw IntakeConfigurationException.BadRequest(
                IntakeAdapterFailureCodes.ValidationFailed,
                "The adapter does not support this approved snapshot schema.");
        if (descriptor.SupportedProcessingProfiles.Count > 0 &&
            !descriptor.SupportedProcessingProfiles.Contains(
                snapshot.ProcessingProfileCode,
                StringComparer.OrdinalIgnoreCase))
            throw IntakeConfigurationException.BadRequest(
                IntakeAdapterFailureCodes.ValidationFailed,
                "The adapter does not support this processing profile.");
    }

    private static string BuildExecutionKey(
        Guid tenantId,
        Guid snapshotId,
        string adapterCode,
        string adapterVersion,
        bool dryRun) =>
        $"{tenantId:N}|{snapshotId:N}|{adapterCode}|{adapterVersion}|{(dryRun ? "DRYRUN" : "EXECUTE")}";

    private static AdapterExecutionResponse MapResponse(IntakeAdapterExecution execution) =>
        new(
            execution.Id,
            execution.SnapshotId,
            execution.AdapterCode,
            execution.AdapterVersion,
            execution.ExecutionKey,
            execution.IdempotencyKey,
            execution.Status,
            execution.AttemptNumber,
            execution.RequestedByUserId,
            execution.RequestedAt,
            execution.StartedAt,
            execution.CompletedAt,
            execution.FailureCode,
            execution.FailureMessage,
            execution.ExternalReferences
                .OrderBy(reference => reference.ReferenceType, StringComparer.Ordinal)
                .ThenBy(reference => reference.ReferenceId, StringComparer.Ordinal)
                .Select(reference => new AdapterExternalReferenceResponse(
                    reference.ReferenceType,
                    reference.ReferenceId))
                .ToArray());

    private static void ValidateContext(Guid tenantId, Guid userId)
    {
        if (tenantId == Guid.Empty || userId == Guid.Empty)
            throw IntakeConfigurationException.Forbidden(
                IntakeAdapterFailureCodes.TenantContextInvalid,
                "An authenticated tenant and user are required.");
    }
}