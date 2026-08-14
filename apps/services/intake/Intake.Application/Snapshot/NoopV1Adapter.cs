using Intake.Contracts.Snapshot;
using Intake.Domain.Snapshot;

namespace Intake.Application.Snapshot;

public sealed class NoopV1Adapter : IIntakeDestinationAdapter
{
    public AdapterDescriptor Descriptor { get; } = new(
        IntakeAdapterCodes.NoopV1,
        "1",
        [ApprovedSnapshotSchemaCodes.LienIntakeApprovedSnapshotV1],
        ["LIEN_INTAKE_V1"],
        true,
        true);

    public AdapterValidationResult Validate(
        ApprovedIntakeSnapshotV1 snapshot,
        IntakeAdapterRequestContext context)
    {
        if (context.TenantId == Guid.Empty)
            return Invalid(IntakeAdapterFailureCodes.TenantContextInvalid, "Tenant context is required.");
        if (!string.Equals(
                snapshot.SchemaCode,
                ApprovedSnapshotSchemaCodes.LienIntakeApprovedSnapshotV1,
                StringComparison.Ordinal) ||
            snapshot.SchemaVersion != 1)
            return Invalid(IntakeAdapterFailureCodes.ValidationFailed, "The snapshot schema is not supported.");
        if (string.IsNullOrWhiteSpace(snapshot.ProcessingProfileCode))
            return Invalid(IntakeAdapterFailureCodes.ValidationFailed, "The processing profile is required.");
        return new(true, null, null);
    }

    public Task<AdapterExecutionResult> ExecuteAsync(
        ApprovedIntakeSnapshotV1 snapshot,
        IntakeAdapterRequestContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new AdapterExecutionResult(
            true,
            false,
            IntakeAdapterExecutionStatuses.Succeeded,
            null,
            null,
            context.DryRun
                ? ["NOOP_V1 dry-run completed; no product record was created."]
                : ["NOOP_V1 completed; no product record was created."],
            []));
    }

    private static AdapterValidationResult Invalid(string code, string message) =>
        new(false, code, message);
}