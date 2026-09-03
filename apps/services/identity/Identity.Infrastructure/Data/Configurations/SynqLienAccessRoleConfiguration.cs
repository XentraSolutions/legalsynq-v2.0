using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

public sealed class SynqLienAccessRoleConfiguration : IEntityTypeConfiguration<SynqLienAccessRole>
{
    public void Configure(EntityTypeBuilder<SynqLienAccessRole> builder)
    {
        builder.ToTable("idt_SynqLienAccessRoles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property<string>("ActiveName")
            .HasMaxLength(100)
            .HasComputedColumnSql("CASE WHEN `IsActive` = 1 THEN LOWER(`Name`) ELSE NULL END", stored: true);
        builder.HasIndex("TenantId", "OrganizationId", "ActiveName").IsUnique();
    }
}
