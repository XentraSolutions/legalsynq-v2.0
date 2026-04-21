using BuildingBlocks.Exceptions;
using Task.Application.DTOs;
using Task.Application.Interfaces;
using Task.Domain.Entities;
using Task.Domain.Validation;
using Microsoft.Extensions.Logging;

namespace Task.Application.Services;

public class TaskStageService : ITaskStageService
{
    private readonly ITaskStageRepository    _stages;
    private readonly IUnitOfWork             _uow;
    private readonly ILogger<TaskStageService> _logger;

    public TaskStageService(
        ITaskStageRepository    stages,
        IUnitOfWork             uow,
        ILogger<TaskStageService> logger)
    {
        _stages = stages;
        _uow    = uow;
        _logger = logger;
    }

    public async System.Threading.Tasks.Task<TaskStageDto> CreateAsync(
        Guid tenantId, Guid userId, CreateTaskStageRequest request, CancellationToken ct = default)
    {
        // TASK-B05 (TASK-014) — validate product code against canonical registry
        var productCode = KnownProductCodes.ValidateOptional(request.SourceProductCode);

        var stage = TaskStageConfig.Create(
            tenantId, request.Code, request.Name, request.DisplayOrder,
            userId, productCode);

        await _stages.AddAsync(stage, ct);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Stage config {StageId} ({Code}) created for tenant {TenantId}", stage.Id, stage.Code, tenantId);

        return TaskStageDto.From(stage);
    }

    public async System.Threading.Tasks.Task<TaskStageDto> UpdateAsync(
        Guid tenantId, Guid id, Guid userId, UpdateTaskStageRequest request, CancellationToken ct = default)
    {
        var stage = await _stages.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Stage config {id} not found.");

        stage.Update(request.Name, request.DisplayOrder, request.IsActive, userId);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Stage config {StageId} updated by {UserId} in tenant {TenantId}", id, userId, tenantId);

        return TaskStageDto.From(stage);
    }

    public async System.Threading.Tasks.Task<IReadOnlyList<TaskStageDto>> ListAsync(
        Guid tenantId, string? sourceProductCode = null, CancellationToken ct = default)
    {
        var stages = await _stages.GetByTenantAsync(tenantId, sourceProductCode, activeOnly: true, ct);
        return stages.Select(TaskStageDto.From).ToList();
    }

    public async System.Threading.Tasks.Task<TaskStageDto?> GetByIdAsync(
        Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var stage = await _stages.GetByIdAsync(tenantId, id, ct);
        return stage is null ? null : TaskStageDto.From(stage);
    }
}
