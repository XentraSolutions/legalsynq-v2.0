using Task.Domain.Entities;

namespace Task.Application.DTOs;

public record CreateTaskTemplateRequest(
    string  Code,
    string  Name,
    string  DefaultTitle,
    string? SourceProductCode  = null,
    string? Description        = null,
    string? DefaultDescription = null,
    string? DefaultPriority    = null,
    string? DefaultScope       = null,
    int?    DefaultDueInDays   = null,
    Guid?   DefaultStageId     = null);

public record UpdateTaskTemplateRequest(
    string  Name,
    string  DefaultTitle,
    int     ExpectedVersion,
    string? Description        = null,
    string? DefaultDescription = null,
    string? DefaultPriority    = null,
    string? DefaultScope       = null,
    int?    DefaultDueInDays   = null,
    Guid?   DefaultStageId     = null);

public record CreateTaskFromTemplateRequest(
    string?   TitleOverride       = null,
    string?   DescriptionOverride = null,
    Guid?     AssignedUserId      = null,
    DateTime? DueAtOverride       = null,
    string?   SourceEntityType    = null,
    Guid?     SourceEntityId      = null);

public record TaskTemplateDto(
    Guid    Id,
    Guid    TenantId,
    string? SourceProductCode,
    string  Code,
    string  Name,
    string? Description,
    string  DefaultTitle,
    string? DefaultDescription,
    string  DefaultPriority,
    string  DefaultScope,
    int?    DefaultDueInDays,
    Guid?   DefaultStageId,
    bool    IsActive,
    int     Version,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc)
{
    public static TaskTemplateDto From(TaskTemplate t) => new(
        t.Id, t.TenantId, t.SourceProductCode,
        t.Code, t.Name, t.Description,
        t.DefaultTitle, t.DefaultDescription,
        t.DefaultPriority, t.DefaultScope,
        t.DefaultDueInDays, t.DefaultStageId,
        t.IsActive, t.Version,
        t.CreatedAtUtc, t.UpdatedAtUtc);
}
