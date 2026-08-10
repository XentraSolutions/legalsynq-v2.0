namespace Liens.Domain.Entities;

/// <summary>
/// A non-sensitive migration exception. The source row is identified by a
/// legacy key and hash; raw source data must remain in protected staging.
/// </summary>
public sealed class LegacyImportException
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ImportRunId { get; private set; }
    public string SourceTable { get; private set; } = string.Empty;
    public string LegacyId { get; private set; } = string.Empty;
    public string Severity { get; private set; } = string.Empty;
    public string ErrorCode { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public string? SourceHash { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private LegacyImportException() { }
}
