using Intake.Application.Snapshot;
using Intake.Contracts.Snapshot;
using Intake.Domain.Snapshot;
using Xunit;

namespace Intake.Tests;

public sealed class B13SnapshotTests
{
    [Fact]
    public void Canonical_snapshot_serialization_is_deterministic_and_hashable()
    {
        var payload = CreatePayload();
        var serializer = new CanonicalSnapshotSerializer();

        var first = serializer.Serialize(payload);
        var second = serializer.Serialize(payload);

        Assert.Equal(first, second);
        Assert.Equal(64, serializer.Hash(first).Length);
        Assert.Equal(serializer.Hash(first), serializer.Hash(second));
        Assert.Contains("\"SchemaCode\":\"LIEN_INTAKE_APPROVED_SNAPSHOT_V1\"", first);
    }

    [Fact]
    public async Task Noop_adapter_validates_and_never_returns_product_references()
    {
        var adapter = new NoopV1Adapter();
        var payload = CreatePayload();
        var context = new IntakeAdapterRequestContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "correlation",
            "idempotency",
            Guid.NewGuid(),
            false);

        var validation = adapter.Validate(payload, context);
        var result = await adapter.ExecuteAsync(payload, context, CancellationToken.None);

        Assert.True(validation.IsValid);
        Assert.True(result.Success);
        Assert.False(result.Retryable);
        Assert.Equal(IntakeAdapterExecutionStatuses.Succeeded, result.Status);
        Assert.Empty(result.ExternalReferences);
    }

    [Fact]
    public void Snapshot_schema_and_adapter_codes_are_stable()
    {
        Assert.Equal(
            "LIEN_INTAKE_APPROVED_SNAPSHOT_V1",
            ApprovedSnapshotSchemaCodes.LienIntakeApprovedSnapshotV1);
        Assert.Equal("NOOP_V1", IntakeAdapterCodes.NoopV1);
        Assert.Equal("SUPERSEDED", ApprovedSnapshotStatuses.Superseded);
    }

    private static ApprovedIntakeSnapshotV1 CreatePayload() =>
        new(
            ApprovedSnapshotSchemaCodes.LienIntakeApprovedSnapshotV1,
            1,
            1,
            "LIEN_INTAKE_V1",
            new ApprovedSnapshotClassification("B11", "B11", false),
            [
                new ApprovedSnapshotFact(
                    "LIEN_NUMBER",
                    "TEXT",
                    "L-123",
                    null,
                    "VALID",
                    "NORMALIZED_AI",
                    false,
                    false,
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    null,
                    .99,
                    ["evidence-1"],
                    0),
            ],
            [],
            [],
            [],
            new ApprovedSnapshotReviewMetadata(
                Guid.NewGuid(),
                "APPROVED",
                Guid.NewGuid(),
                DateTimeOffset.Parse("2026-08-14T00:00:00Z")),
            new ApprovedSnapshotProvenance(
                Guid.NewGuid(),
                null,
                null,
                null,
                null,
                Guid.NewGuid(),
                Guid.NewGuid()));
}