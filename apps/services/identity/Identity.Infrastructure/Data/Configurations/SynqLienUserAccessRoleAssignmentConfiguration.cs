using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

public sealed class SynqLienUserAccessRoleAssignmentConfiguration : IEntityTypeConfiguration<SynqLienUserAccessRoleAssignment>
{
    public void Configure(EntityTypeBuilder<SynqLienUserAccessRoleAssignment> builder)
    {
        builder.ToTable("idt_SynqLienUserAccessRoleAssignments");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.TenantId, x.OrganizationId, x.UserId, x.RoleId });
        builder.HasIndex(x => new { x.TenantId, x.OrganizationId, x.UserId, x.ActiveSlot }).IsUnique();
        builder.HasOne(x => x.Role).WithMany(x => x.Assignments).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
