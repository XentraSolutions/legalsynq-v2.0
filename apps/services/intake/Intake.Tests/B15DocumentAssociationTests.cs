using Intake.Application.Snapshot;
using Intake.Contracts.Snapshot;
using Intake.Domain.Snapshot;
using Xunit;

namespace Intake.Tests;

public sealed class B15DocumentAssociationTests
{
    [Fact]
    public void Human_overridden_classification_routes_each_document_to_the_lien()
    {
        var policy = new SynqLienDocumentAssociationPolicy();
        var snapshot = Snapshot(
            effectiveClassification: "MEDICAL_BILL",
            originalClassification: "UNKNOWN",
            documents:
            [
                Document(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "bill-1.pdf"),
                Document(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "bill-2.pdf"),
            ]);

        var plan = policy.BuildPlan(snapshot);

        Assert.Equal(2, plan.Count);
        Assert.All(plan, item =>
        {
            Assert.Equal("LIEN", item.Target.TargetType);
            Assert.Equal("MEDICAL_BILL", item.Target.Role);
            Assert.Equal(Guid.Parse("22222222-2222-2222-2222-222222222222"), item.Target.TargetId);
        });
    }

    [Fact]
    public void Attorney_document_expands_BOTH_into_case_and_lien_items()
    {
        var policy = new SynqLienDocumentAssociationPolicy();
        var plan = policy.BuildPlan(Snapshot(
            effectiveClassification: "ATTORNEY_DOCUMENT",
            originalClassification: "ATTORNEY_DOCUMENT",
            documents: [Document(Guid.NewGuid(), "notice.pdf")]));

        Assert.Equal(["CASE", "LIEN"], plan.Select(item => item.Target.TargetType));
        Assert.All(plan, item => Assert.Equal("SUPPORTING_DOCUMENT", item.Target.Role));
    }

    [Fact]
    public void Unsupported_classification_is_persistable_as_a_skip()
    {
        var plan = new SynqLienDocumentAssociationPolicy().BuildPlan(Snapshot(
            effectiveClassification: "UNKNOWN",
            originalClassification: "UNKNOWN",
            documents: [Document(null, "inline-signature.png")]));

        var item = Assert.Single(plan);
        Assert.Equal("SKIP", item.Target.TargetType);
        Assert.False(item.Required);
    }

    private static ApprovedIntakeSnapshotV1 Snapshot(
        string effectiveClassification,
        string originalClassification,
        IReadOnlyList<ApprovedSnapshotDocument> documents) =>
        new(
            ApprovedSnapshotSchemaCodes.LienIntakeApprovedSnapshotV1,
            1,
            1,
            "LIEN_INTAKE_V1",
            new(originalClassification, effectiveClassification,
                !string.Equals(originalClassification, effectiveClassification, StringComparison.Ordinal)),
            [],
            [
                new("CASE", "SELECTED",
                    Guid.Parse("11111111-1111-1111-1111-111111111111"), null, true, "TEST"),
                new("LIEN", "SELECTED",
                    Guid.Parse("22222222-2222-2222-2222-222222222222"), null, true, "TEST"),
            ],
            documents,
            [],
            new(Guid.NewGuid(), "APPROVED", Guid.NewGuid(), DateTimeOffset.UtcNow),
            new(Guid.NewGuid(), null, null, null, null, Guid.NewGuid(), Guid.NewGuid()));

    private static ApprovedSnapshotDocument Document(Guid? id, string fileName) =>
        new(id, Guid.NewGuid(), string.Empty, fileName, "application/pdf", null,
            id.HasValue ? $"documents:{id}" : null);
}