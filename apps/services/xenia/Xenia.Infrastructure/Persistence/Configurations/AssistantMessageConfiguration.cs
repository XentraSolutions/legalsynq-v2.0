using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenia.Domain.Assistant;

namespace Xenia.Infrastructure.Persistence.Configurations;

internal sealed class AssistantMessageConfiguration : IEntityTypeConfiguration<AssistantMessage>
{
    public void Configure(EntityTypeBuilder<AssistantMessage> builder)
    {
        builder.ToTable("xn_conversation_messages");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id").HasColumnType("char(36)").ValueGeneratedNever();
        builder.Property(e => e.ConversationId).HasColumnName("conversation_id").HasColumnType("char(36)").IsRequired();
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").HasColumnType("char(36)").IsRequired();
        builder.Property(e => e.ActorId).HasColumnName("actor_id").HasColumnType("char(36)").IsRequired();
        builder.Property(e => e.Role).HasColumnName("role").IsRequired();
        builder.Property(e => e.Content).HasColumnName("content").HasColumnType("longtext").IsRequired();
        builder.Property(e => e.Provider).HasColumnName("provider").HasMaxLength(AssistantMessage.ProviderMaxLength).IsRequired();
        builder.Property(e => e.ProviderResponseId).HasColumnName("provider_response_id").HasMaxLength(AssistantMessage.ProviderResponseIdMaxLength);
        builder.Property(e => e.InputTokens).HasColumnName("input_tokens");
        builder.Property(e => e.OutputTokens).HasColumnName("output_tokens");
        builder.Property(e => e.FinishReason).HasColumnName("finish_reason").HasMaxLength(AssistantMessage.FinishReasonMaxLength);
        builder.Property(e => e.MetadataJson).HasColumnName("metadata_json").HasColumnType("longtext").IsRequired();
        builder.Property(e => e.CreatedAtMessageUtc).HasColumnName("message_created_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(e => e.CreatedAtUtc).HasColumnName("created_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(e => e.UpdatedAtUtc).HasColumnName("updated_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(e => e.RowVersion).HasColumnName("row_version").IsRequired().IsConcurrencyToken();

        builder.HasIndex(e => new { e.TenantId, e.ConversationId, e.CreatedAtMessageUtc }).HasDatabaseName("ix_xn_messages_tenant_conversation_created");
        builder.HasIndex(e => e.ProviderResponseId).HasDatabaseName("ix_xn_messages_provider_response");
    }
}
