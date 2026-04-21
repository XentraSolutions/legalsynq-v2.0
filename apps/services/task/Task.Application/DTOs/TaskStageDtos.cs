using Task.Domain.Entities;

namespace Task.Application.DTOs;

public record CreateTaskStageRequest(
    string  Code,
    string  Name,
    int     DisplayOrder,
    string? SourceProductCode = null);

public record UpdateTaskStageRequest(
    string  Name,
    int     DisplayOrder,
    bool    IsActive);

public record TaskStageDto(
    Guid    Id,
    Guid    TenantId,
    string? SourceProductCode,
    string  Code,
    string  Name,
    int     DisplayOrder,
    bool    IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc)
{
    public static TaskStageDto From(TaskStageConfig s) => new(
        s.Id, s.TenantId, s.SourceProductCode,
        s.Code, s.Name, s.DisplayOrder, s.IsActive,
        s.CreatedAtUtc, s.UpdatedAtUtc);
}
