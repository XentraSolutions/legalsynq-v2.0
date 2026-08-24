using Intake.Contracts.Snapshot;

namespace Intake.Application.Snapshot;

public sealed class SynqLienDocumentAssociationPolicy : IDocumentAssociationPolicy
{
    public const string CaseTarget = "CASE";
    public const string LienTarget = "LIEN";
    public const string BothTarget = "BOTH";
    public int Version => 1;

    public IReadOnlyList<DocumentAssociationPlanItem> BuildPlan(
        ApprovedIntakeSnapshotV1 snapshot)
    {
        var caseId = Selected(snapshot, "CASE");
        var lienId = Selected(snapshot, "LIEN");
        var result = new List<DocumentAssociationPlanItem>();

        foreach (var document in snapshot.Documents
                     .OrderBy(x => x.ArtifactId)
                     .ThenBy(x => x.FileName, StringComparer.Ordinal))
        {
            var classification = snapshot.Classification.EffectiveClassification?.Trim().ToUpperInvariant();
            var role = NormalizeRole(document.DocumentRole, classification);
            var target = classification switch
            {
                "MEDICAL_RECORD" or "MEDICAL_BILL" or "MEDICAL_STATEMENT" => lienId.HasValue
                    ? new DocumentAssociationTarget(LienTarget, lienId.Value, role, caseId)
                    : default,
                "LIEN_DOCUMENT" or "LETTER_OF_PROTECTION" => lienId.HasValue
                    ? new DocumentAssociationTarget(LienTarget, lienId.Value, role, caseId)
                    : default,
                "SETTLEMENT_DOCUMENT" => caseId.HasValue
                    ? new DocumentAssociationTarget(CaseTarget, caseId.Value, role)
                    : default,
                "ATTORNEY_DOCUMENT" or "CORRESPONDENCE" or "INSURANCE_DOCUMENT" =>
                    caseId.HasValue && lienId.HasValue
                        ? new DocumentAssociationTarget(BothTarget, caseId.Value, role, caseId)
                        : caseId.HasValue
                            ? new DocumentAssociationTarget(CaseTarget, caseId.Value, role)
                            : default,
                _ => default,
            };

            if (target is null || target.TargetId == Guid.Empty)
            {
                result.Add(new(
                    document,
                    new DocumentAssociationTarget("SKIP", Guid.Empty, "SKIPPED"),
                    false));
                continue;
            }

            if (target.TargetType == BothTarget)
            {
                result.Add(new(document, target with { TargetType = CaseTarget }, true));
                result.Add(new(document, target with
                {
                    TargetType = LienTarget,
                    TargetId = lienId!.Value,
                    RelatedCaseId = caseId,
                }, true));
            }
            else
                result.Add(new(document, target, true));
        }

        return result;
    }

    private static Guid? Selected(ApprovedIntakeSnapshotV1 snapshot, string type) =>
        snapshot.Entities
            .Where(x => string.Equals(x.EntityType, type, StringComparison.OrdinalIgnoreCase)
                     && string.Equals(x.Decision, "SELECTED", StringComparison.OrdinalIgnoreCase)
                     && x.SelectedEntityId.HasValue)
            .Select(x => x.SelectedEntityId)
            .FirstOrDefault();

    private static string NormalizeRole(string? role, string? classification) =>
        !string.IsNullOrWhiteSpace(role)
            ? role.Trim().ToUpperInvariant()
            : classification switch
            {
                "MEDICAL_RECORD" => "MEDICAL_RECORD",
                "MEDICAL_BILL" or "MEDICAL_STATEMENT" => "MEDICAL_BILL",
                "LIEN_DOCUMENT" => "LIEN",
                "LETTER_OF_PROTECTION" => "LETTER_OF_PROTECTION",
                _ => "SUPPORTING_DOCUMENT",
            };
}