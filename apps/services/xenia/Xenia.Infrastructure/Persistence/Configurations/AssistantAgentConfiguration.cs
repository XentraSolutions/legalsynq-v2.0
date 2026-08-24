using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenia.Domain.Assistant;

namespace Xenia.Infrastructure.Persistence.Configurations;

internal sealed class AssistantAgentConfiguration : IEntityTypeConfiguration<AssistantAgent>
{
    public void Configure(EntityTypeBuilder<AssistantAgent> builder)
    {
        builder.ToTable("xn_assistant_agents");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id").HasColumnType("char(36)").ValueGeneratedNever();
        builder.Property(e => e.AgentKey).HasColumnName("agent_key").HasMaxLength(AssistantAgent.AgentKeyMaxLength).IsRequired();
        builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(AssistantAgent.NameMaxLength).IsRequired();
        builder.Property(e => e.Description).HasColumnName("description").HasMaxLength(AssistantAgent.DescriptionMaxLength).IsRequired();
        builder.Property(e => e.Version).HasColumnName("version").HasMaxLength(AssistantAgent.VersionMaxLength).IsRequired();
        builder.Property(e => e.SystemPrompt).HasColumnName("system_prompt").HasColumnType("text").IsRequired();
        builder.Property(e => e.AllowedToolsJson).HasColumnName("allowed_tools_json").HasColumnType("text").IsRequired();
        builder.Property(e => e.RequiredProductCodesJson).HasColumnName("required_product_codes_json").HasColumnType("text").IsRequired();
        builder.Property(e => e.IsEnabled).HasColumnName("is_enabled").IsRequired().HasDefaultValue(true);
        builder.Property(e => e.CreatedAtUtc).HasColumnName("created_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(e => e.UpdatedAtUtc).HasColumnName("updated_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(e => e.RowVersion).HasColumnName("row_version").IsRequired().IsConcurrencyToken();

        builder.HasIndex(e => e.AgentKey).IsUnique().HasDatabaseName("uq_xn_assistant_agents_key");
        builder.HasIndex(e => e.IsEnabled).HasDatabaseName("ix_xn_assistant_agents_enabled");
    }
}
