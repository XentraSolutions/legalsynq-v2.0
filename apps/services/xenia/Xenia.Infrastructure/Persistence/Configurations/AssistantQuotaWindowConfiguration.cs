using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenia.Domain.Assistant;

namespace Xenia.Infrastructure.Persistence.Configurations;

internal sealed class AssistantQuotaWindowConfiguration : IEntityTypeConfiguration<AssistantQuotaWindow>
{
    public void Configure(EntityTypeBuilder<AssistantQuotaWindow> builder)
    {
        builder.ToTable("xn_quota_windows");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id").HasColumnType("char(36)").ValueGeneratedNever();
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").HasColumnType("char(36)").IsRequired();
        builder.Property(e => e.ActorId).HasColumnName("actor_id").HasColumnType("char(36)");
        builder.Property(e => e.WindowKey).HasColumnName("window_key").HasMaxLength(AssistantQuotaWindow.WindowKeyMaxLength).IsRequired();
        builder.Property(e => e.StartsAtUtc).HasColumnName("starts_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(e => e.EndsAtUtc).HasColumnName("ends_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(e => e.RequestCount).HasColumnName("request_count").IsRequired();
        builder.Property(e => e.InputTokens).HasColumnName("input_tokens").IsRequired();
        builder.Property(e => e.OutputTokens).HasColumnName("output_tokens").IsRequired();
        builder.Property(e => e.EstimatedCostUsd).HasColumnName("estimated_cost_usd").HasColumnType("decimal(18,6)").IsRequired();
        builder.Property(e => e.CreatedAtUtc).HasColumnName("created_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(e => e.UpdatedAtUtc).HasColumnName("updated_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(e => e.RowVersion).HasColumnName("row_version").IsRequired().IsConcurrencyToken();

        builder.HasIndex(e => new { e.TenantId, e.ActorId, e.WindowKey, e.StartsAtUtc }).IsUnique().HasDatabaseName("uq_xn_quota_windows_scope");
        builder.HasIndex(e => new { e.TenantId, e.EndsAtUtc }).HasDatabaseName("ix_xn_quota_windows_tenant_ends");
    }
}
