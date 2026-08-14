using System.Text.Json;
using Intake.Application.Artifacts;
using Intake.Contracts.Snapshot;
using Intake.Domain.Snapshot;
using Microsoft.Extensions.Logging;

namespace Intake.Application.Snapshot;

public sealed class DocumentAssociationExecutionService(
    IApprovedSnapshotRepository snapshots,
    IAdapterExecutionRepository adapterExecutions,
    IDocumentAssociationExecutionRepository executions,
    IDocumentAssociationPolicy policy,
    IIntakeDocumentsClient documents,
    IDocumentAssociationDestinationClient destination,
    ILogger<DocumentAssociationExecutionService> logger)
    : IDocumentAssociationExecutionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<DocumentAssociationExecutionResponse> ExecuteAsync(
        Guid tenantId, Guid snapshotId, Guid actorUserId, string? correlationId, CancellationToken ct)
    {
        var snapshot = await snapshots.FindAsync(tenantId, snapshotId, ct)
            ?? throw new KeyNotFoundException($"Approved snapshot '{snapshotId}' was not found.");
        if (snapshot.Status != ApprovedSnapshotStatuses.Ready)
            throw new InvalidOperationException("Only a READY approved snapshot can start document association.");
        var executionKey = $"snapshot:{snapshotId}:document-association:{DocumentAssociationPolicyCodes.V1}:v{policy.Version}";
        var existing = await executions.FindByExecutionKeyAsync(tenantId, executionKey, ct);
        if (existing is not null)
            return Map(existing);

        var payload = JsonSerializer.Deserialize<ApprovedIntakeSnapshotV1>(snapshot.PayloadJson, JsonOptions)
            ?? throw new InvalidOperationException("The approved snapshot payload is invalid.");
        var b14 = (await adapterExecutions.ListBySnapshotAsync(tenantId, snapshotId, ct))
            .Where(x => x.AdapterCode == IntakeAdapterCodes.SynqLienV1
                     && x.Status == IntakeAdapterExecutionStatuses.Succeeded)
            .OrderByDescending(x => x.CompletedAt)
            .FirstOrDefault();
        var refs = b14?.ExternalReferences.ToDictionary(x => x.ReferenceType, x => x.ReferenceId,
            StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var caseId = ParseReference(refs, "CASE");
        var lienId = ParseReference(refs, "LIEN");
        var plan = policy.BuildPlan(payload)
            .Select(item => item.Target.TargetType switch
            {
                "CASE" when caseId.HasValue => item with
                {
                    Target = item.Target with { TargetId = caseId.Value },
                },
                "LIEN" when lienId.HasValue => item with
                {
                    Target = item.Target with { TargetId = lienId.Value },
                },
                _ => item,
            })
            .ToList();
        if (!caseId.HasValue && plan.Any(x => x.Target.TargetType == "CASE"))
            throw new InvalidOperationException("B14 did not produce a CASE reference for this snapshot.");
        if (!lienId.HasValue && plan.Any(x => x.Target.TargetType == "LIEN"))
            throw new InvalidOperationException("B14 did not produce a LIEN reference for this snapshot.");

        var execution = new DocumentAssociationExecution
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            SnapshotId = snapshotId,
            AdapterExecutionId = b14?.Id,
            PolicyCode = DocumentAssociationPolicyCodes.V1,
            PolicyVersion = policy.Version,
            ExecutionKey = executionKey,
            IdempotencyKey = $"b15:{snapshotId}:v{policy.Version}",
            Status = DocumentAssociationExecutionStatuses.Processing,
            AttemptNumber = 1,
            RequestedByUserId = actorUserId,
            RequestedAt = DateTimeOffset.UtcNow,
            StartedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        foreach (var item in plan)
            execution.Items.Add(new DocumentAssociationItem
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenantId,
                ExecutionId = execution.Id,
                ArtifactId = snapshot.ArtifactId,
                DocumentId = item.Document.DocumentId,
                DocumentReference = item.Document.Reference ?? $"documents:{item.Document.DocumentId}",
                DocumentRole = item.Target.Role,
                TargetType = item.Target.TargetType,
                TargetId = item.Target.TargetId,
                RelatedCaseId = item.Target.RelatedCaseId,
                ItemKey = $"{snapshotId}:{item.Document.DocumentId}:{item.Target.TargetType}:{item.Target.TargetId}:{item.Target.Role}",
                Required = item.Required,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });

        await executions.SaveAsync(execution, ct);
        await ProcessAsync(execution, tenantId, actorUserId, correlationId ?? string.Empty, ct);
        await executions.SaveAsync(execution, ct);
        return Map(execution);
    }

    public async Task<DocumentAssociationExecutionResponse> RetryAsync(
        Guid tenantId, Guid snapshotId, Guid executionId, Guid actorUserId, string? correlationId, CancellationToken ct)
    {
        var execution = await executions.FindAsync(tenantId, executionId, ct)
            ?? throw new KeyNotFoundException($"Document association execution '{executionId}' was not found.");
        if (execution.SnapshotId != snapshotId)
            throw new UnauthorizedAccessException("The execution does not belong to the snapshot.");
        var staleProcessing = execution.Status == DocumentAssociationExecutionStatuses.Processing &&
            execution.StartedAt < DateTimeOffset.UtcNow.AddMinutes(-10);
        if (execution.Status is not (DocumentAssociationExecutionStatuses.Pending
            or DocumentAssociationExecutionStatuses.Retryable
            or DocumentAssociationExecutionStatuses.PartiallySucceeded
            or DocumentAssociationExecutionStatuses.Failed) && !staleProcessing)
            return Map(execution);

        execution.Status = DocumentAssociationExecutionStatuses.Processing;
        execution.AttemptNumber++;
        execution.StartedAt = DateTimeOffset.UtcNow;
        execution.CompletedAt = null;
        execution.FailureCode = null;
        execution.FailureMessage = null;
        execution.UpdatedAt = DateTimeOffset.UtcNow;
        await ProcessAsync(execution, tenantId, actorUserId, correlationId ?? string.Empty, ct);
        await executions.SaveAsync(execution, ct);
        return Map(execution);
    }

    public async Task<DocumentAssociationExecutionResponse?> GetAsync(
        Guid tenantId, Guid snapshotId, Guid executionId, CancellationToken ct)
    {
        var value = await executions.FindAsync(tenantId, executionId, ct);
        return value is null || value.SnapshotId != snapshotId ? null : Map(value);
    }

    public async Task<IReadOnlyList<DocumentAssociationExecutionResponse>> ListAsync(
        Guid tenantId, Guid snapshotId, CancellationToken ct) =>
        (await executions.ListAsync(tenantId, snapshotId, ct)).Select(Map).ToList();

    private async Task ProcessAsync(
        DocumentAssociationExecution execution,
        Guid tenantId,
        Guid actorUserId,
        string correlationId,
        CancellationToken ct)
    {
        foreach (var item in execution.Items.Where(x =>
            x.Status is DocumentAssociationItemStatuses.Pending
                or DocumentAssociationItemStatuses.Retryable
                or DocumentAssociationItemStatuses.Failed))
        {
            item.Status = DocumentAssociationItemStatuses.Processing;
            item.AttemptNumber++;
            item.FailureCode = null;
            item.FailureMessage = null;
            item.UpdatedAt = DateTimeOffset.UtcNow;

            if (item.TargetType == "SKIP")
            {
                item.Status = DocumentAssociationItemStatuses.Skipped;
                item.CompletedAt = DateTimeOffset.UtcNow;
                continue;
            }

            if (!item.DocumentId.HasValue)
            {
                Fail(item, "DOCUMENT_REFERENCE_MISSING", "The approved document has no Documents-service id.", false);
                continue;
            }

            var metadata = await documents.GetMetadataAsync(tenantId, item.DocumentId.Value, ct);
            if (!metadata.Found || metadata.TenantId != tenantId || metadata.IsDeleted
                || string.Equals(metadata.Status, "Deleted", StringComparison.OrdinalIgnoreCase))
            {
                Fail(item, "DOCUMENT_NOT_ACCESSIBLE", "The approved document is missing, deleted, or belongs to another tenant.", false);
                continue;
            }

            var expected = (await snapshots.FindAsync(tenantId, execution.SnapshotId, ct)) is { } current
                ? JsonSerializer.Deserialize<ApprovedIntakeSnapshotV1>(current.PayloadJson, JsonOptions)
                : null;
            var approved = expected?.Documents.FirstOrDefault(x => x.DocumentId == item.DocumentId);
            if (approved?.Sha256 is not null && metadata.Sha256 is not null
                && !string.Equals(approved.Sha256, metadata.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                Fail(item, "DOCUMENT_CHECKSUM_MISMATCH", "Documents metadata checksum differs from the approved snapshot.", false);
                continue;
            }

            var result = await destination.AssociateAsync(
                tenantId, actorUserId, item.ItemKey, correlationId,
                item.TargetType, item.TargetId, item.RelatedCaseId, item.DocumentId.Value,
                item.DocumentRole, item.DocumentReference, ct);
            if (result.Success)
            {
                item.Status = DocumentAssociationItemStatuses.Succeeded;
                item.DestinationReference = result.DestinationReference;
                item.CompletedAt = DateTimeOffset.UtcNow;
            }
            else
                Fail(item, result.FailureCode ?? "DESTINATION_REJECTED",
                    result.FailureMessage ?? "Destination rejected the association.", result.Retryable);
        }

        var failed = execution.Items.Count(x => x.Status is DocumentAssociationItemStatuses.Failed
            or DocumentAssociationItemStatuses.Retryable);
        var succeeded = execution.Items.Count(x => x.Status == DocumentAssociationItemStatuses.Succeeded);
        execution.Status = failed == 0
            ? DocumentAssociationExecutionStatuses.Succeeded
            : succeeded > 0
                ? DocumentAssociationExecutionStatuses.PartiallySucceeded
                : execution.Items.Any(x => x.Status == DocumentAssociationItemStatuses.Retryable)
                    ? DocumentAssociationExecutionStatuses.Retryable
                    : DocumentAssociationExecutionStatuses.Failed;
        execution.FailureCode = failed == 0 ? null : "ITEMS_FAILED";
        execution.FailureMessage = failed == 0 ? null : $"{failed} association item(s) did not complete.";
        execution.CompletedAt = DateTimeOffset.UtcNow;
        execution.UpdatedAt = DateTimeOffset.UtcNow;
        execution.Version++;
    }

    private static void Fail(DocumentAssociationItem item, string code, string message, bool retryable)
    {
        item.Status = retryable
            ? DocumentAssociationItemStatuses.Retryable
            : DocumentAssociationItemStatuses.Failed;
        item.FailureCode = code;
        item.FailureMessage = message;
        item.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static Guid? ParseReference(
        IReadOnlyDictionary<string, string> references,
        string type) =>
        references.TryGetValue(type, out var value) && Guid.TryParse(value, out var id)
            ? id
            : null;

    private static DocumentAssociationExecutionResponse Map(DocumentAssociationExecution x) =>
        new(x.Id, x.SnapshotId, x.PolicyCode, x.PolicyVersion, x.ExecutionKey, x.IdempotencyKey,
            x.Status, x.AttemptNumber, x.RequestedByUserId, x.RequestedAt, x.StartedAt,
            x.CompletedAt, x.FailureCode, x.FailureMessage,
            x.Items.OrderBy(i => i.ItemKey).Select(i => new DocumentAssociationItemResponse(
                i.Id, i.ArtifactId, i.DocumentId, i.DocumentReference, i.DocumentRole,
                i.TargetType, i.TargetId, i.ItemKey, i.Required, i.Status, i.AttemptNumber,
                i.FailureCode, i.FailureMessage, i.DestinationReference)).ToList());
}