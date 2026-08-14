namespace Intake.Application.Artifacts;

public interface IEmailArtifactExtractor
{
    EmailArtifactExtractionResult Extract(
        string rawMessage,
        EmailArtifactProcessingOptions options);
}