using Flow.Application.Interfaces;
using Flow.Domain.Common;
using Flow.Domain.Entities;
using Flow.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Flow.Infrastructure.Persistence;

public class FlowDbContext : DbContext, IFlowDbContext
{
    private readonly ITenantProvider? _tenantProvider;

    public FlowDbContext(DbContextOptions<FlowDbContext> options) : base(options)
    {
    }

    public FlowDbContext(DbContextOptions<FlowDbContext> options, ITenantProvider tenantProvider) : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    public DbSet<FlowDefinition> FlowDefinitions => Set<FlowDefinition>();
    public DbSet<WorkflowStage> WorkflowStages => Set<WorkflowStage>();
    public DbSet<WorkflowTransition> WorkflowTransitions => Set<WorkflowTransition>();
    public DbSet<TaskItem> TaskItems => Set<TaskItem>();
    public DbSet<WorkflowAutomationHook> AutomationHooks => Set<WorkflowAutomationHook>();
    public DbSet<AutomationAction> AutomationActions => Set<AutomationAction>();
    public DbSet<AutomationExecutionLog> AutomationExecutionLogs => Set<AutomationExecutionLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ProductWorkflowMapping> ProductWorkflowMappings => Set<ProductWorkflowMapping>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<FlowDefinition>(entity =>
        {
            entity.ToTable("flow_definitions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(128);
            entity.HasIndex(e => e.TenantId);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Description).HasMaxLength(2048);
            entity.Property(e => e.Version).IsRequired().HasMaxLength(32);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.ProductKey).IsRequired().HasMaxLength(64).HasDefaultValue(Flow.Domain.Common.ProductKeys.FlowGeneric);
            entity.HasIndex(e => new { e.TenantId, e.ProductKey });
            entity.Property(e => e.CreatedBy).HasMaxLength(256);
            entity.Property(e => e.UpdatedBy).HasMaxLength(256);
            entity.HasQueryFilter(e => _tenantProvider == null || e.TenantId == _tenantProvider.GetTenantId());
        });

        modelBuilder.Entity<WorkflowStage>(entity =>
        {
            entity.ToTable("flow_workflow_stages");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(128);
            entity.HasIndex(e => e.TenantId);
            entity.Property(e => e.Key).IsRequired().HasMaxLength(64);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(256);
            entity.Property(e => e.MappedStatus).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.Order).IsRequired();
            entity.HasIndex(e => new { e.WorkflowDefinitionId, e.Key }).IsUnique();
            entity.HasOne(e => e.WorkflowDefinition)
                .WithMany(w => w.Stages)
                .HasForeignKey(e => e.WorkflowDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(e => _tenantProvider == null || e.TenantId == _tenantProvider.GetTenantId());
        });

        modelBuilder.Entity<WorkflowTransition>(entity =>
        {
            entity.ToTable("flow_workflow_transitions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(128);
            entity.HasIndex(e => e.TenantId);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(256);
            entity.Property(e => e.RulesJson).HasMaxLength(2048);
            entity.HasIndex(e => new { e.WorkflowDefinitionId, e.FromStageId, e.ToStageId }).IsUnique();
            entity.HasOne(e => e.WorkflowDefinition)
                .WithMany(w => w.Transitions)
                .HasForeignKey(e => e.WorkflowDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.FromStage)
                .WithMany(s => s.TransitionsFrom)
                .HasForeignKey(e => e.FromStageId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ToStage)
                .WithMany(s => s.TransitionsTo)
                .HasForeignKey(e => e.ToStageId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(e => _tenantProvider == null || e.TenantId == _tenantProvider.GetTenantId());
        });

        modelBuilder.Entity<WorkflowAutomationHook>(entity =>
        {
            entity.ToTable("flow_automation_hooks");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(128);
            entity.HasIndex(e => e.TenantId);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(256);
            entity.Property(e => e.TriggerEventType).IsRequired().HasMaxLength(64);
            entity.Property(e => e.ActionType).IsRequired().HasMaxLength(64);
            entity.Property(e => e.ConfigJson).HasMaxLength(2048);
            entity.Property(e => e.ProductKey).IsRequired().HasMaxLength(64).HasDefaultValue(Flow.Domain.Common.ProductKeys.FlowGeneric);
            entity.HasIndex(e => new { e.TenantId, e.ProductKey });
            entity.Property(e => e.CreatedBy).HasMaxLength(256);
            entity.Property(e => e.UpdatedBy).HasMaxLength(256);
            entity.HasIndex(e => new { e.WorkflowDefinitionId, e.WorkflowTransitionId });
            entity.HasOne(e => e.WorkflowDefinition)
                .WithMany(w => w.AutomationHooks)
                .HasForeignKey(e => e.WorkflowDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.WorkflowTransition)
                .WithMany()
                .HasForeignKey(e => e.WorkflowTransitionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(e => _tenantProvider == null || e.TenantId == _tenantProvider.GetTenantId());
        });

        modelBuilder.Entity<AutomationAction>(entity =>
        {
            entity.ToTable("flow_automation_actions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(128);
            entity.HasIndex(e => e.TenantId);
            entity.Property(e => e.ActionType).IsRequired().HasMaxLength(64);
            entity.Property(e => e.ConfigJson).HasMaxLength(2048);
            entity.Property(e => e.ConditionJson).HasMaxLength(2048);
            entity.Property(e => e.Order).IsRequired();
            entity.Property(e => e.RetryCount).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.RetryDelaySeconds);
            entity.Property(e => e.StopOnFailure).IsRequired().HasDefaultValue(false);
            entity.HasIndex(e => new { e.HookId, e.Order }).IsUnique();
            entity.HasOne(e => e.Hook)
                .WithMany(h => h.Actions)
                .HasForeignKey(e => e.HookId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(e => _tenantProvider == null || e.TenantId == _tenantProvider.GetTenantId());
        });

        modelBuilder.Entity<AutomationExecutionLog>(entity =>
        {
            entity.ToTable("flow_automation_execution_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(128);
            entity.HasIndex(e => e.TenantId);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(32);
            entity.Property(e => e.Message).HasMaxLength(2048);
            entity.Property(e => e.ActionType).IsRequired().HasMaxLength(64);
            entity.Property(e => e.ActionOrder);
            // Default 1 matches pre-019-C semantics: every legacy log row
            // represented exactly one execution attempt. The executor always
            // populates Attempts explicitly for new rows.
            entity.Property(e => e.Attempts).IsRequired().HasDefaultValue(1);
            entity.HasIndex(e => e.TaskId);
            entity.HasIndex(e => e.WorkflowAutomationHookId);
            entity.HasIndex(e => e.ActionId);
            entity.HasOne(e => e.Task)
                .WithMany()
                .HasForeignKey(e => e.TaskId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.AutomationHook)
                .WithMany()
                .HasForeignKey(e => e.WorkflowAutomationHookId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(e => _tenantProvider == null || e.TenantId == _tenantProvider.GetTenantId());
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("flow_notifications");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(128);
            entity.HasIndex(e => e.TenantId);
            entity.Property(e => e.Type).IsRequired().HasMaxLength(64);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(512);
            entity.Property(e => e.Message).IsRequired().HasMaxLength(2048);
            entity.Property(e => e.TargetUserId).HasMaxLength(256);
            entity.Property(e => e.TargetRoleKey).HasMaxLength(128);
            entity.Property(e => e.TargetOrgId).HasMaxLength(256);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(16);
            entity.Property(e => e.SourceType).IsRequired().HasMaxLength(64);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.TargetUserId);
            entity.HasIndex(e => e.TargetRoleKey);
            entity.HasIndex(e => e.TargetOrgId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.TaskId);
            entity.HasOne(e => e.Task)
                .WithMany()
                .HasForeignKey(e => e.TaskId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.WorkflowDefinition)
                .WithMany()
                .HasForeignKey(e => e.WorkflowDefinitionId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasQueryFilter(e => _tenantProvider == null || e.TenantId == _tenantProvider.GetTenantId());
        });

        modelBuilder.Entity<ProductWorkflowMapping>(entity =>
        {
            entity.ToTable("flow_product_workflow_mappings");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.ProductKey).IsRequired().HasMaxLength(64);
            entity.Property(e => e.SourceEntityType).IsRequired().HasMaxLength(128);
            entity.Property(e => e.SourceEntityId).IsRequired().HasMaxLength(256);
            entity.Property(e => e.CorrelationKey).HasMaxLength(256);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(32).HasDefaultValue("Active");
            entity.Property(e => e.CreatedBy).HasMaxLength(256);
            entity.Property(e => e.UpdatedBy).HasMaxLength(256);
            entity.HasIndex(e => new { e.TenantId, e.ProductKey });
            entity.HasIndex(e => new { e.TenantId, e.ProductKey, e.SourceEntityType, e.SourceEntityId })
                .HasDatabaseName("ix_pwm_product_entity");
            entity.HasIndex(e => e.WorkflowDefinitionId);
            entity.HasIndex(e => e.WorkflowInstanceTaskId);
            entity.HasOne(e => e.WorkflowDefinition)
                .WithMany()
                .HasForeignKey(e => e.WorkflowDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.WorkflowInstanceTask)
                .WithMany()
                .HasForeignKey(e => e.WorkflowInstanceTaskId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasQueryFilter(e => _tenantProvider == null || e.TenantId == _tenantProvider.GetTenantId());
        });

        WorkflowSeedData.Seed(modelBuilder);

        modelBuilder.Entity<TaskItem>(entity =>
        {
            entity.ToTable("flow_task_items");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(128);
            entity.HasIndex(e => e.TenantId);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(512);
            entity.Property(e => e.Description).HasMaxLength(4096);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.ProductKey).IsRequired().HasMaxLength(64).HasDefaultValue(Flow.Domain.Common.ProductKeys.FlowGeneric);
            entity.HasIndex(e => new { e.TenantId, e.ProductKey });
            entity.Property(e => e.AssignedToUserId).HasMaxLength(256);
            entity.Property(e => e.AssignedToRoleKey).HasMaxLength(128);
            entity.Property(e => e.AssignedToOrgId).HasMaxLength(256);
            entity.Property(e => e.CreatedBy).HasMaxLength(256);
            entity.Property(e => e.UpdatedBy).HasMaxLength(256);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.AssignedToUserId);
            entity.HasIndex(e => e.AssignedToRoleKey);
            entity.HasIndex(e => e.AssignedToOrgId);
            entity.HasIndex(e => e.FlowDefinitionId);
            entity.HasIndex(e => e.WorkflowStageId);
            entity.HasOne(e => e.WorkflowDefinition)
                .WithMany()
                .HasForeignKey(e => e.FlowDefinitionId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.WorkflowStage)
                .WithMany()
                .HasForeignKey(e => e.WorkflowStageId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.OwnsOne(e => e.Context, ctx =>
            {
                ctx.Property(c => c.ContextType).HasMaxLength(128).HasColumnName("context_type");
                ctx.Property(c => c.ContextId).HasMaxLength(256).HasColumnName("context_id");
                ctx.Property(c => c.Label).HasMaxLength(512).HasColumnName("context_label");
                ctx.HasIndex(c => new { c.ContextType, c.ContextId });
            });
            entity.HasQueryFilter(e => _tenantProvider == null || e.TenantId == _tenantProvider.GetTenantId());
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider?.GetTenantId();

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added && string.IsNullOrEmpty(entry.Entity.TenantId))
            {
                if (string.IsNullOrEmpty(tenantId))
                {
                    throw new InvalidOperationException(
                        "Cannot persist new Flow entity: no tenant context available. " +
                        "Authenticated requests must carry a tenant_id claim.");
                }
                entry.Entity.TenantId = tenantId;
            }
        }

        // LS-FLOW-020-A — Defensively normalise ProductKey on Added entities
        // so direct entity construction (e.g. seed data, tests, migrations)
        // never persists an empty value. Service-layer validation runs first
        // for normal request paths.
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Added) continue;
            switch (entry.Entity)
            {
                case Flow.Domain.Entities.FlowDefinition fd when string.IsNullOrWhiteSpace(fd.ProductKey):
                    fd.ProductKey = Flow.Domain.Common.ProductKeys.FlowGeneric;
                    break;
                case Flow.Domain.Entities.TaskItem ti when string.IsNullOrWhiteSpace(ti.ProductKey):
                    ti.ProductKey = Flow.Domain.Common.ProductKeys.FlowGeneric;
                    break;
                case Flow.Domain.Entities.WorkflowAutomationHook hk when string.IsNullOrWhiteSpace(hk.ProductKey):
                    hk.ProductKey = Flow.Domain.Common.ProductKeys.FlowGeneric;
                    break;
            }
        }

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> IFlowDbContext.BeginTransactionAsync(CancellationToken cancellationToken)
    {
        return Database.BeginTransactionAsync(cancellationToken);
    }

    Microsoft.EntityFrameworkCore.Storage.IExecutionStrategy IFlowDbContext.CreateExecutionStrategy()
    {
        return Database.CreateExecutionStrategy();
    }
}
