using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenia.Domain.Assistant;

namespace Xenia.Infrastructure.Persistence.Configurations;

internal sealed class AssistantConversationConfiguration : IEntityTypeConfiguration<AssistantConversation>
{
    public void Configure(EntityTypeBuilder<AssistantConversation> builder)
    {
        builder.ToTable("xn_conversations");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id").HasColumnType("char(36)").ValueGeneratedNever();
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").HasColumnType("char(36)").IsRequired();
        builder.Property(e => e.ActorId).HasColumnName("actor_id").HasColumnType("char(36)").IsRequired();
        builder.Property(e => e.AgentKey).HasColumnName("agent_key").HasMaxLength(AssistantConversation.AgentKeyMaxLength).IsRequired();
        builder.Property(e => e.AgentVersion).HasColumnName("agent_version").HasMaxLength(AssistantConversation.AgentVersionMaxLength).IsRequired();
        builder.Property(e => e.Title).HasColumnName("title").HasMaxLength(AssistantConversation.TitleMaxLength).IsRequired();
        builder.Property(e => e.Source).HasColumnName("source").HasMaxLength(AssistantConversation.SourceMaxLength).IsRequired();
        builder.Property(e => e.ContextJson).HasColumnName("context_json").HasColumnType("longtext").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").IsRequired();
        builder.Property(e => e.ArchivedAtUtc).HasColumnName("archived_at").HasColumnType("datetime(6)");
        builder.Property(e => e.LastMessageAtUtc).HasColumnName("last_message_at").HasColumnType("datetime(6)");
        builder.Property(e => e.CreatedAtUtc).HasColumnName("created_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(e => e.UpdatedAtUtc).HasColumnName("updated_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(e => e.RowVersion).HasColumnName("row_version").IsRequired().IsConcurrencyToken();

        builder.HasIndex(e => new { e.TenantId, e.ActorId, e.Status, e.UpdatedAtUtc }).HasDatabaseName("ix_xn_conversations_tenant_actor_status_updated");
        builder.HasIndex(e => new { e.TenantId, e.AgentKey }).HasDatabaseName("ix_xn_conversations_tenant_agent");
    }
}
