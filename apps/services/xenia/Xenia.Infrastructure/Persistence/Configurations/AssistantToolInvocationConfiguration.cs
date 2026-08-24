using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenia.Domain.Assistant;

namespace Xenia.Infrastructure.Persistence.Configurations;

internal sealed class AssistantToolInvocationConfiguration : IEntityTypeConfiguration<AssistantToolInvocation>
{
    public void Configure(EntityTypeBuilder<AssistantToolInvocation> builder)
    {
        builder.ToTable("xn_tool_invocations");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id").HasColumnType("char(36)").ValueGeneratedNever();
        builder.Property(e => e.ConversationId).HasColumnName("conversation_id").HasColumnType("char(36)").IsRequired();
        builder.Property(e => e.MessageId).HasColumnName("message_id").HasColumnType("char(36)");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").HasColumnType("char(36)").IsRequired();
        builder.Property(e => e.ActorId).HasColumnName("actor_id").HasColumnType("char(36)").IsRequired();
        builder.Property(e => e.AgentKey).HasColumnName("agent_key").HasMaxLength(AssistantAgent.AgentKeyMaxLength).IsRequired();
        builder.Property(e => e.ToolKey).HasColumnName("tool_key").HasMaxLength(AssistantToolInvocation.ToolKeyMaxLength).IsRequired();
        builder.Property(e => e.InputJson).HasColumnName("input_json").HasColumnType("longtext").IsRequired();
        builder.Property(e => e.OutputJson).HasColumnName("output_json").HasColumnType("longtext");
        builder.Property(e => e.Status).HasColumnName("status").HasMaxLength(AssistantToolInvocation.StatusMaxLength).IsRequired();
        builder.Property(e => e.ConfirmationRequired).HasColumnName("confirmation_required").IsRequired();
        builder.Property(e => e.StartedAtUtc).HasColumnName("started_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(e => e.CompletedAtUtc).HasColumnName("completed_at").HasColumnType("datetime(6)");
        builder.Property(e => e.SafeError).HasColumnName("safe_error").HasMaxLength(AssistantToolInvocation.SafeErrorMaxLength);
        builder.Property(e => e.CreatedAtUtc).HasColumnName("created_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(e => e.UpdatedAtUtc).HasColumnName("updated_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(e => e.RowVersion).HasColumnName("row_version").IsRequired().IsConcurrencyToken();

        builder.HasIndex(e => new { e.TenantId, e.ConversationId, e.StartedAtUtc }).HasDatabaseName("ix_xn_tool_invocations_tenant_conversation_started");
        builder.HasIndex(e => new { e.TenantId, e.ToolKey, e.Status }).HasDatabaseName("ix_xn_tool_invocations_tenant_tool_status");
    }
}
