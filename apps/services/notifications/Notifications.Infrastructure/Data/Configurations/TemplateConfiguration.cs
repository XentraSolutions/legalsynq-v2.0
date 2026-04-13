using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

public class TemplateConfiguration : IEntityTypeConfiguration<Template>
{
    public void Configure(EntityTypeBuilder<Template> builder)
    {
        builder.ToTable("ntf_templates");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id");
        builder.Property(e => e.TemplateKey).HasColumnName("template_key").HasMaxLength(200);
        builder.Property(e => e.Channel).HasColumnName("channel").HasMaxLength(20);
        builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(200);
        builder.Property(e => e.Description).HasColumnName("description").HasColumnType("text");
        builder.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("active");
        builder.Property(e => e.Scope).HasColumnName("scope").HasMaxLength(20).HasDefaultValue("tenant");
        builder.Property(e => e.ProductType).HasColumnName("product_type").HasMaxLength(50);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(e => new { e.TemplateKey, e.Channel, e.TenantId })
            .HasDatabaseName("idx_templates_key_channel_tenant");
    }
}

public class TemplateVersionConfiguration : IEntityTypeConfiguration<TemplateVersion>
{
    public void Configure(EntityTypeBuilder<TemplateVersion> builder)
    {
        builder.ToTable("ntf_template_versions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TemplateId).HasColumnName("template_id");
        builder.Property(e => e.VersionNumber).HasColumnName("version_number").HasDefaultValue(1);
        builder.Property(e => e.SubjectTemplate).HasColumnName("subject_template").HasColumnType("text");
        builder.Property(e => e.BodyTemplate).HasColumnName("body_template").HasColumnType("longtext");
        builder.Property(e => e.TextTemplate).HasColumnName("text_template").HasColumnType("text");
        builder.Property(e => e.EditorType).HasColumnName("editor_type").HasMaxLength(20);
        builder.Property(e => e.IsPublished).HasColumnName("is_published").HasDefaultValue(false);
        builder.Property(e => e.PublishedBy).HasColumnName("published_by").HasMaxLength(255);
        builder.Property(e => e.PublishedAt).HasColumnName("published_at");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(e => e.TemplateId).HasDatabaseName("idx_template_versions_template_id");
    }
}
