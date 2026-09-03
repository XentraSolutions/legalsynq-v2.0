using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

public sealed class SynqLienAccessRolePermissionConfiguration : IEntityTypeConfiguration<SynqLienAccessRolePermission>
{
    public void Configure(EntityTypeBuilder<SynqLienAccessRolePermission> builder)
    {
        builder.ToTable("idt_SynqLienAccessRolePermissions");
        builder.HasKey(x => new { x.RoleId, x.PermissionId });
        builder.HasOne(x => x.Role).WithMany(x => x.Permissions).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Permission).WithMany().HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Restrict);
    }
}
