using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

public class UserInvitationRoleGrantConfiguration : IEntityTypeConfiguration<UserInvitationRoleGrant>
{
    public void Configure(EntityTypeBuilder<UserInvitationRoleGrant> builder)
    {
        builder.ToTable("idt_UserInvitationRoleGrants");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.InvitationId).IsRequired();
        builder.Property(g => g.TenantId).IsRequired();
        builder.Property(g => g.ProductCode).IsRequired().HasMaxLength(50);
        builder.Property(g => g.RoleCode).IsRequired().HasMaxLength(100);
        builder.Property(g => g.CreatedAtUtc).IsRequired();
        builder.Property(g => g.AppliedAtUtc);

        builder.HasIndex(g => new { g.InvitationId, g.ProductCode, g.RoleCode }).IsUnique();
        builder.HasIndex(g => new { g.TenantId, g.ProductCode, g.RoleCode });
    }
}
