using Intake.Application.Snapshot;
using Intake.Contracts.Snapshot;
using Intake.Domain.Snapshot;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Intake.Tests;

public sealed class B14SynqLienTests
{
    [Fact]
    public async Task Dry_run_never_calls_destination_and_returns_no_references()
    {
        var client = new FakeSynqLienClient();
        var adapter = CreateAdapter(client);
        var snapshot = Snapshot();
        var context = Context(dryRun: true);

        var result = await adapter.ExecuteAsync(snapshot, context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(result.ExternalReferences);
        Assert.Equal(0, client.Calls);
    }

    [Fact]
    public async Task No_match_creates_case_then_lien_with_stable_child_keys()
    {
        var client = new FakeSynqLienClient();
        var adapter = CreateAdapter(client);
        var context = Context();

        var result = await adapter.ExecuteAsync(Snapshot(), context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, client.Calls);
        Assert.Equal($"{context.IdempotencyKey}|CASE", client.CaseKey);
        Assert.Equal($"{context.IdempotencyKey}|LIEN", client.LienKey);
        Assert.Equal(["CASE", "LIEN"], result.ExternalReferences.Select(x => x.ReferenceType));
        Assert.Equal("11111111-1111-1111-1111-111111111111",
            result.ExternalReferences.Single(x => x.ReferenceType == "CASE").ReferenceId);
    }

    [Fact]
    public void Disabled_configuration_fails_validation_without_destination_access()
    {
        var adapter = new SynqLienV1Adapter(
            new FakeSynqLienClient(),
            new SynqLienDestinationOptions
            {
                Enabled = false,
                BaseUrl = "https://liens.example.test",
                OrganizationId = Guid.NewGuid(),
            },
            NullLogger<SynqLienV1Adapter>.Instance);

        var validation = adapter.Validate(Snapshot(), Context());

        Assert.False(validation.IsValid);
        Assert.Equal(SynqLienFailureCodes.Disabled, validation.FailureCode);
    }

    private static SynqLienV1Adapter CreateAdapter(FakeSynqLienClient client) =>
        new(client, new SynqLienDestinationOptions
        {
            Enabled = true,
            BaseUrl = "https://liens.example.test",
            OrganizationId = Guid.NewGuid(),
        }, NullLogger<SynqLienV1Adapter>.Instance);

    private static IntakeAdapterRequestContext Context(bool dryRun = false) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "corr-b14", "exec-b14", Guid.NewGuid(), dryRun);

    private static ApprovedIntakeSnapshotV1 Snapshot() =>
        new(
            ApprovedSnapshotSchemaCodes.LienIntakeApprovedSnapshotV1,
            1, 1, "LIEN_INTAKE_V1",
            new("B11", "B11", false),
            [
                Fact("CLIENT_FIRST_NAME", "Ada"),
                Fact("CLIENT_LAST_NAME", "Lovelace"),
                Fact("ORIGINAL_AMOUNT", "125.50"),
                Fact("LIEN_TYPE", "MedicalLien"),
                Fact("DATE_OF_INCIDENT", "2026-08-01"),
                new("CLIENT_EMAIL", "TEXT", "rejected@example.test", null, "REJECTED",
                    "HUMAN_CORRECTED", true, false, null, null, Guid.NewGuid(), .99, [], 5),
                Fact("CLIENT_EMAIL", "ada@example.test"),
            ],
            [
                new("CASE", "NO_MATCH", null, null, true, "NO_CASE_MATCH"),
                new("FACILITY", "NO_MATCH", null, null, false, "NO_FACILITY_MATCH"),
            ],
            [], [],
            new(Guid.NewGuid(), "APPROVED", Guid.NewGuid(), DateTimeOffset.UtcNow),
            new(Guid.NewGuid(), null, null, null, null, Guid.NewGuid(), Guid.NewGuid()));

    private static ApprovedSnapshotFact Fact(string code, string value) =>
        new(code, "TEXT", value, null, "VALID", "HUMAN_CORRECTED", true, true,
            null, null, Guid.NewGuid(), .99, [], 0);

    private sealed class FakeSynqLienClient : ISynqLienClient
    {
        public int Calls { get; private set; }
        public string? CaseKey { get; private set; }
        public string? LienKey { get; private set; }

        public Task<SynqLienCallResult<SynqLienCaseResponse>> GetCaseAsync(
            Guid tenantId, Guid caseId, string correlationId, CancellationToken cancellationToken) =>
            Task.FromResult(new SynqLienCallResult<SynqLienCaseResponse>(
                true, false, 200, new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "26-000001"), null, null));

        public Task<SynqLienCallResult<SynqLienCaseResponse>> CreateCaseAsync(
            Guid tenantId, Guid actingUserId, string idempotencyKey, string correlationId,
            SynqLienCaseRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            CaseKey = idempotencyKey;
            return Task.FromResult(new SynqLienCallResult<SynqLienCaseResponse>(
                true, false, 201, new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "26-000001"), null, null));
        }

        public Task<SynqLienCallResult<SynqLienLienResponse>> CreateLienAsync(
            Guid tenantId, Guid actingUserId, string idempotencyKey, string correlationId,
            SynqLienLienRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            LienKey = idempotencyKey;
            return Task.FromResult(new SynqLienCallResult<SynqLienLienResponse>(
                true, false, 201, new(Guid.NewGuid(), "26-000001-01", request.CaseId), null, null));
        }
    }
}