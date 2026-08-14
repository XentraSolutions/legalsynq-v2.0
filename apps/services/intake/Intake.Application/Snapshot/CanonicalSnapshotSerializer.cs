using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Intake.Application.Review;
using Intake.Contracts.Snapshot;

namespace Intake.Application.Snapshot;

public interface ICanonicalSnapshotSerializer
{
    string Serialize(ApprovedIntakeSnapshotV1 snapshot);
    string Hash(string canonicalJson);
}

public sealed class CanonicalSnapshotSerializer : ICanonicalSnapshotSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = null,
        DictionaryKeyPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false,
    };

    public string Serialize(ApprovedIntakeSnapshotV1 snapshot) =>
        JsonSerializer.Serialize(snapshot, Options);

    public string Hash(string canonicalJson) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson)));
}

public sealed class B12ReviewedIntakeProjectionService(
    IIntakeReviewService reviewService) : IReviewedIntakeProjectionService
{
    public async Task<ReviewedIntakeSnapshotSource> GetAsync(
        Guid tenantId,
        Guid reviewId,
        CancellationToken cancellationToken)
    {
        var workspace = await reviewService.GetAsync(tenantId, reviewId, cancellationToken)
            ?? throw Configuration.IntakeConfigurationException.NotFound(
                Domain.Snapshot.ApprovedSnapshotFailureCodes.ReviewRequired,
                "The B12 review was not found.");
        var projection = await reviewService.GetEffectiveAsync(
            tenantId,
            reviewId,
            cancellationToken);
        return new(workspace, projection);
    }
}