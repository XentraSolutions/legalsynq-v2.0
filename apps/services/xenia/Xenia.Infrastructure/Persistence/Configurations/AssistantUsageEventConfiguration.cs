using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenia.Domain.Assistant;

namespace Xenia.Infrastructure.Persistence.Configurations;

internal sealed class AssistantUsageEventConfiguration : IEntityTypeConfiguration<AssistantUsageEvent>
{
    public void Configure(EntityTypeBuilder<AssistantUsageEvent> builder)
    {
        builder.ToTable("xn_usage_events");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id").HasColumnType("char(36)").ValueGeneratedNever();
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").HasColumnType("char(36)").IsRequired();
        builder.Property(e => e.ActorId).HasColumnName("actor_id").HasColumnType("char(36)").IsRequired();
        builder.Property(e => e.ConversationId).HasColumnName("conversation_id").HasColumnType("char(36)").IsRequired();
        builder.Property(e => e.MessageId).HasColumnName("message_id").HasColumnType("char(36)");
        builder.Property(e => e.AgentKey).HasColumnName("agent_key").HasMaxLength(AssistantAgent.AgentKeyMaxLength).IsRequired();
        builder.Property(e => e.Provider).HasColumnName("provider").HasMaxLength(AssistantUsageEvent.ProviderMaxLength).IsRequired();
        builder.Property(e => e.ModelKey).HasColumnName("model_key").HasMaxLength(AssistantUsageEvent.ModelKeyMaxLength).IsRequired();
        builder.Property(e => e.EventType).HasColumnName("event_type").HasMaxLength(AssistantUsageEvent.EventTypeMaxLength).IsRequired();
        builder.Property(e => e.InputTokens).HasColumnName("input_tokens").IsRequired();
        builder.Property(e => e.OutputTokens).HasColumnName("output_tokens").IsRequired();
        builder.Property(e => e.EstimatedCostUsd).HasColumnName("estimated_cost_usd").HasColumnType("decimal(18,6)").IsRequired();
        builder.Property(e => e.LatencyMs).HasColumnName("latency_ms").IsRequired();
        builder.Property(e => e.OccurredAtUtc).HasColumnName("occurred_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(e => e.CreatedAtUtc).HasColumnName("created_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(e => e.UpdatedAtUtc).HasColumnName("updated_at").HasColumnType("datetime(6)").IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.OccurredAtUtc }).HasDatabaseName("ix_xn_usage_events_tenant_occurred");
        builder.HasIndex(e => new { e.TenantId, e.ActorId, e.OccurredAtUtc }).HasDatabaseName("ix_xn_usage_events_tenant_actor_occurred");
        builder.HasIndex(e => new { e.TenantId, e.AgentKey, e.OccurredAtUtc }).HasDatabaseName("ix_xn_usage_events_tenant_agent_occurred");
    }
}
