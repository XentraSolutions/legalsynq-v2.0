using System.Globalization;
using Intake.Contracts.Snapshot;
using Intake.Domain.Snapshot;
using Microsoft.Extensions.Logging;

namespace Intake.Application.Snapshot;

public sealed class SynqLienV1Adapter(
    ISynqLienClient client,
    SynqLienDestinationOptions options,
    ILogger<SynqLienV1Adapter> logger) : IIntakeDestinationAdapter
{
    public AdapterDescriptor Descriptor { get; } = new(
        IntakeAdapterCodes.SynqLienV1,
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
        if (!options.Enabled)
            return Invalid(SynqLienFailureCodes.Disabled, "SYNQLIEN_V1 is disabled.");
        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _) ||
            options.OrganizationId == Guid.Empty)
            return Invalid(SynqLienFailureCodes.ConfigurationInvalid, "SYNQLIEN_V1 destination configuration is incomplete.");
        if (snapshot.SchemaCode != ApprovedSnapshotSchemaCodes.LienIntakeApprovedSnapshotV1 ||
            snapshot.SchemaVersion != 1 ||
            snapshot.ProcessingProfileCode != "LIEN_INTAKE_V1")
            return Invalid(SynqLienFailureCodes.RoutingInvalid, "The approved snapshot profile is not supported.");

        var routing = ResolveRouting(snapshot);
        if (routing.CaseDecision is null)
            return Invalid(SynqLienFailureCodes.RoutingInvalid, "The approved snapshot has no Case decision.");
        if (!routing.CreateCase && !routing.ExistingCaseId.HasValue)
            return Invalid(SynqLienFailureCodes.RoutingInvalid, "The selected Case decision has no entity id.");
        if (routing.FacilityDecision is { Decision: not ("NO_MATCH" or "NOT_SELECTED") } &&
            !routing.FacilityId.HasValue)
            return Invalid(SynqLienFailureCodes.RoutingInvalid, "The selected Facility decision has no entity id.");
        if (Fact(snapshot, "CLIENT_FIRST_NAME") is null || Fact(snapshot, "CLIENT_LAST_NAME") is null)
            return Invalid(SynqLienFailureCodes.RoutingInvalid, "Approved client name facts are required.");
        if (Fact(snapshot, "LIEN_TYPE") is null && Fact(snapshot, "ORIGINAL_AMOUNT") is null)
            return Invalid(SynqLienFailureCodes.RoutingInvalid, "Approved lien facts are required.");
        return new(true, null, null);
    }

    public async Task<AdapterExecutionResult> ExecuteAsync(
        ApprovedIntakeSnapshotV1 snapshot,
        IntakeAdapterRequestContext context,
        CancellationToken cancellationToken)
    {
        var routing = ResolveRouting(snapshot);
        if (context.DryRun)
            return new(true, false, IntakeAdapterExecutionStatuses.Succeeded, null, null,
                ["SYNQLIEN_V1 dry-run validated routing; no Case or Lien was written."], []);

        try
        {
            SynqLienCaseResponse caseResponse;
            var caseKey = $"{context.IdempotencyKey}|CASE";
            if (routing.ExistingCaseId.HasValue)
            {
                var existing = await client.GetCaseAsync(
                    context.TenantId, routing.ExistingCaseId.Value, context.CorrelationId, cancellationToken);
                if (!existing.Success || existing.Value is null)
                    return Failure(existing, SynqLienFailureCodes.DestinationUnavailable);
                caseResponse = existing.Value;
            }
            else
            {
                var created = await client.CreateCaseAsync(
                    context.TenantId, context.RequestedByUserId, caseKey, context.CorrelationId,
                    MapCase(snapshot), cancellationToken);
                if (!created.Success || created.Value is null)
                    return Failure(created, SynqLienFailureCodes.DestinationUnavailable);
                caseResponse = created.Value;
            }

            var lienKey = $"{context.IdempotencyKey}|LIEN";
            var lien = await client.CreateLienAsync(
                context.TenantId, context.RequestedByUserId, lienKey, context.CorrelationId,
                MapLien(snapshot, caseResponse.Id, routing.FacilityId), cancellationToken);
            if (!lien.Success || lien.Value is null)
            {
                var retryable = lien.Retryable;
                return new(false, retryable, retryable
                    ? IntakeAdapterExecutionStatuses.Retryable
                    : IntakeAdapterExecutionStatuses.Failed,
                    retryable ? SynqLienFailureCodes.ReconciliationRequired : SynqLienFailureCodes.PartialSuccess,
                    "Case resolution succeeded but Lien creation did not complete.",
                    [], [new("CASE", caseResponse.Id.ToString())]);
            }

            return new(true, false, IntakeAdapterExecutionStatuses.Succeeded, null, null, [],
                [new("CASE", caseResponse.Id.ToString()), new("LIEN", lien.Value.Id.ToString())]);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SYNQLIEN_V1 destination call failed for tenant {TenantId}", context.TenantId);
            return new(false, true, IntakeAdapterExecutionStatuses.Retryable,
                SynqLienFailureCodes.DestinationUnavailable,
                "The SynqLien destination could not be reached.", [], []);
        }
    }

    private SynqLienCaseRequest MapCase(ApprovedIntakeSnapshotV1 snapshot) =>
        new(
            Fact(snapshot, "CASE_NUMBER") ?? string.Empty,
            RequiredFact(snapshot, "CLIENT_FIRST_NAME"),
            RequiredFact(snapshot, "CLIENT_LAST_NAME"),
            Fact(snapshot, "CASE_EXTERNAL_REFERENCE"),
            Fact(snapshot, "CASE_TITLE"),
            DateFact(snapshot, "CLIENT_DOB"),
            Fact(snapshot, "CLIENT_PHONE"),
            Fact(snapshot, "CLIENT_EMAIL"),
            Fact(snapshot, "CLIENT_ADDRESS"),
            DateFact(snapshot, "DATE_OF_INCIDENT"),
            Fact(snapshot, "INSURANCE_CARRIER"),
            Fact(snapshot, "POLICY_NUMBER"),
            Fact(snapshot, "CLAIM_NUMBER"),
            Fact(snapshot, "CASE_DESCRIPTION"));

    private SynqLienLienRequest MapLien(
        ApprovedIntakeSnapshotV1 snapshot, Guid caseId, Guid? facilityId) =>
        new(
            Fact(snapshot, "LIEN_NUMBER") ?? string.Empty,
            Fact(snapshot, "LIEN_EXTERNAL_REFERENCE"),
            Fact(snapshot, "LIEN_TYPE") ?? "MedicalLien",
            caseId,
            facilityId,
            DecimalFact(snapshot, "ORIGINAL_AMOUNT") ?? 0m,
            Fact(snapshot, "JURISDICTION"),
            BoolFact(snapshot, "IS_CONFIDENTIAL"),
            Fact(snapshot, "CLIENT_FIRST_NAME"),
            Fact(snapshot, "CLIENT_LAST_NAME"),
            DateFact(snapshot, "DATE_OF_INCIDENT"),
            DateFact(snapshot, "INITIAL_SERVICE_DATE"),
            DateFact(snapshot, "END_SERVICE_DATE"),
            Fact(snapshot, "LIEN_DESCRIPTION"));

    private static SynqLienRouting ResolveRouting(ApprovedIntakeSnapshotV1 snapshot)
    {
        var caseDecision = snapshot.Entities.FirstOrDefault(x =>
            x.EntityType.Equals("CASE", StringComparison.OrdinalIgnoreCase));
        var facilityDecision = snapshot.Entities.FirstOrDefault(x =>
            x.EntityType.Equals("FACILITY", StringComparison.OrdinalIgnoreCase));
        var create = caseDecision is not null &&
            caseDecision.Decision.Equals("NO_MATCH", StringComparison.OrdinalIgnoreCase);
        return new(caseDecision, facilityDecision,
            create ? null : caseDecision?.SelectedEntityId,
            facilityDecision?.SelectedEntityId, create);
    }

    private static string? Fact(ApprovedIntakeSnapshotV1 snapshot, string code) =>
        snapshot.Facts
            .Where(f => f.FactCode.Equals(code, StringComparison.OrdinalIgnoreCase) &&
                        f.ValidationStatus.Equals("VALID", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f.Ordinal)
            .Select(f => f.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string RequiredFact(ApprovedIntakeSnapshotV1 snapshot, string code) =>
        Fact(snapshot, code) ?? throw new InvalidOperationException($"Approved fact {code} is missing.");

    private static DateOnly? DateFact(ApprovedIntakeSnapshotV1 s, string c) =>
        DateOnly.TryParse(Fact(s, c), CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) ? value : null;
    private static decimal? DecimalFact(ApprovedIntakeSnapshotV1 s, string c) =>
        decimal.TryParse(Fact(s, c), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : null;
    private static bool BoolFact(ApprovedIntakeSnapshotV1 s, string c) =>
        bool.TryParse(Fact(s, c), out var value) && value;

    private static AdapterValidationResult Invalid(string code, string message) => new(false, code, message);

    private static AdapterExecutionResult Failure<T>(
        SynqLienCallResult<T> result, string fallbackCode) =>
        new(false, result.Retryable,
            result.Retryable ? IntakeAdapterExecutionStatuses.Retryable : IntakeAdapterExecutionStatuses.Failed,
            result.ErrorCode ?? fallbackCode,
            result.ErrorMessage ?? "SynqLien destination rejected the request.", [], []);
}