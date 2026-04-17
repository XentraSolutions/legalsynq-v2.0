using Flow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Flow.Application.Interfaces;

public interface IFlowDbContext
{
    DbSet<FlowDefinition> FlowDefinitions { get; }
    DbSet<WorkflowStage> WorkflowStages { get; }
    DbSet<WorkflowTransition> WorkflowTransitions { get; }
    DbSet<TaskItem> TaskItems { get; }
    DbSet<WorkflowAutomationHook> AutomationHooks { get; }
    DbSet<AutomationAction> AutomationActions { get; }
    DbSet<AutomationExecutionLog> AutomationExecutionLogs { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<ProductWorkflowMapping> ProductWorkflowMappings { get; }
    // LS-FLOW-MERGE-P4 — dedicated workflow-instance grain.
    DbSet<WorkflowInstance> WorkflowInstances { get; }
    // LS-FLOW-E10.2 — transactional outbox for durable side effects.
    DbSet<OutboxMessage> OutboxMessages { get; }
    // LS-FLOW-E11.1 — first-class human-work item produced by workflow
    // execution. Activated for automatic creation in E11.2.
    DbSet<WorkflowTask> WorkflowTasks { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    Microsoft.EntityFrameworkCore.Storage.IExecutionStrategy CreateExecutionStrategy();
}
