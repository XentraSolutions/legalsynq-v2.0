using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenia.Domain.Assistant;

namespace Xenia.Infrastructure.Persistence.Configurations;

internal sealed class AssistantUserPreferenceConfiguration : IEntityTypeConfiguration<AssistantUserPreference>
{
    public void Configure(EntityTypeBuilder<AssistantUserPreference> builder)
    {
        builder.ToTable("xn_user_preferences");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id").HasColumnType("char(36)").ValueGeneratedNever();
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").HasColumnType("char(36)").IsRequired();
        builder.Property(e => e.ActorId).HasColumnName("actor_id").HasColumnType("char(36)").IsRequired();
        builder.Property(e => e.DefaultAgentKey).HasColumnName("default_agent_key").HasMaxLength(AssistantAgent.AgentKeyMaxLength).IsRequired();
        builder.Property(e => e.ContextHintsEnabled).HasColumnName("context_hints_enabled").IsRequired().HasDefaultValue(true);
        builder.Property(e => e.PreferencesJson).HasColumnName("preferences_json").HasColumnType("longtext").IsRequired();
        builder.Property(e => e.CreatedAtUtc).HasColumnName("created_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(e => e.UpdatedAtUtc).HasColumnName("updated_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(e => e.RowVersion).HasColumnName("row_version").IsRequired().IsConcurrencyToken();

        builder.HasIndex(e => new { e.TenantId, e.ActorId }).IsUnique().HasDatabaseName("uq_xn_user_preferences_tenant_actor");
    }
}
