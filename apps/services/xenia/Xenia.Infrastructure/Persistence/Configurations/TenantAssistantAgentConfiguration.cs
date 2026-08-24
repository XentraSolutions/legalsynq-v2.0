using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenia.Domain.Assistant;

namespace Xenia.Infrastructure.Persistence.Configurations;

internal sealed class TenantAssistantAgentConfiguration : IEntityTypeConfiguration<TenantAssistantAgent>
{
    public void Configure(EntityTypeBuilder<TenantAssistantAgent> builder)
    {
        builder.ToTable("xn_tenant_agents");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id").HasColumnType("char(36)").ValueGeneratedNever();
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").HasColumnType("char(36)").IsRequired();
        builder.Property(e => e.AgentKey).HasColumnName("agent_key").HasMaxLength(AssistantAgent.AgentKeyMaxLength).IsRequired();
        builder.Property(e => e.Enabled).HasColumnName("enabled").IsRequired().HasDefaultValue(true);
        builder.Property(e => e.ConfigurationJson).HasColumnName("configuration_json").HasColumnType("longtext").IsRequired();
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by").HasColumnType("char(36)");
        builder.Property(e => e.CreatedAtUtc).HasColumnName("created_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(e => e.UpdatedAtUtc).HasColumnName("updated_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(e => e.RowVersion).HasColumnName("row_version").IsRequired().IsConcurrencyToken();

        builder.HasIndex(e => new { e.TenantId, e.AgentKey }).IsUnique().HasDatabaseName("uq_xn_tenant_agents_tenant_key");
        builder.HasIndex(e => e.TenantId).HasDatabaseName("ix_xn_tenant_agents_tenant");
    }
}
