namespace Liens.Application.DTOs;

public sealed class CreateWorkflowConfigRequest
{
    public string WorkflowName  { get; init; } = string.Empty;
    public string UpdateSource  { get; init; } = string.Empty;
    public string? UpdatedByName { get; init; }
}

public sealed class UpdateWorkflowConfigRequest
{
    public string WorkflowName  { get; init; } = string.Empty;
    public bool   IsActive      { get; init; } = true;
    public string UpdateSource  { get; init; } = string.Empty;
    public string? UpdatedByName { get; init; }
    public int    Version       { get; init; }
}

public sealed class AddWorkflowStageRequest
{
    public string StageName         { get; init; } = string.Empty;
    public int    StageOrder        { get; init; }
    public string? Description      { get; init; }
    public string? DefaultOwnerRole { get; init; }
    public string? SlaMetadata      { get; init; }
}

public sealed class UpdateWorkflowStageRequest
{
    public string StageName         { get; init; } = string.Empty;
    public int    StageOrder        { get; init; }
    public bool   IsActive          { get; init; } = true;
    public string? Description      { get; init; }
    public string? DefaultOwnerRole { get; init; }
    public string? SlaMetadata      { get; init; }
}

public sealed class ReorderStagesRequest
{
    public List<StageOrderEntry> Stages { get; init; } = [];
}

public sealed class StageOrderEntry
{
    public Guid StageId    { get; init; }
    public int  StageOrder { get; init; }
}
