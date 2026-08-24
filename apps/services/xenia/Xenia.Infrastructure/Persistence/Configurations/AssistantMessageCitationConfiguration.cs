using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenia.Domain.Assistant;

namespace Xenia.Infrastructure.Persistence.Configurations;

internal sealed class AssistantMessageCitationConfiguration : IEntityTypeConfiguration<AssistantMessageCitation>
{
    public void Configure(EntityTypeBuilder<AssistantMessageCitation> builder)
    {
        builder.ToTable("xn_message_citations");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id").HasColumnType("char(36)").ValueGeneratedNever();
        builder.Property(e => e.MessageId).HasColumnName("message_id").HasColumnType("char(36)").IsRequired();
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").HasColumnType("char(36)").IsRequired();
        builder.Property(e => e.SourceType).HasColumnName("source_type").HasMaxLength(AssistantMessageCitation.SourceTypeMaxLength).IsRequired();
        builder.Property(e => e.SourceId).HasColumnName("source_id").HasMaxLength(AssistantMessageCitation.SourceIdMaxLength).IsRequired();
        builder.Property(e => e.Label).HasColumnName("label").HasMaxLength(AssistantMessageCitation.LabelMaxLength).IsRequired();
        builder.Property(e => e.Url).HasColumnName("url").HasMaxLength(AssistantMessageCitation.UrlMaxLength);
        builder.Property(e => e.CreatedAtUtc).HasColumnName("created_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(e => e.UpdatedAtUtc).HasColumnName("updated_at").HasColumnType("datetime(6)").IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.MessageId }).HasDatabaseName("ix_xn_citations_tenant_message");
        builder.HasIndex(e => new { e.TenantId, e.SourceType, e.SourceId }).HasDatabaseName("ix_xn_citations_tenant_source");
    }
}
