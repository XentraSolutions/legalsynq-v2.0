using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

public class RolePermissionAssignmentConfiguration : IEntityTypeConfiguration<RolePermissionAssignment>
{
    public void Configure(EntityTypeBuilder<RolePermissionAssignment> builder)
    {
        builder.ToTable("idt_RoleCapabilityAssignments");

        builder.HasKey(a => new { a.RoleId, a.PermissionId });

        builder.Property(a => a.PermissionId).HasColumnName("CapabilityId");

        builder.Property(a => a.AssignedAtUtc).IsRequired();
        builder.Property(a => a.AssignedByUserId).HasColumnType("char(36)");

        builder.HasOne(a => a.Role)
            .WithMany(r => r.RolePermissionAssignments)
            .HasForeignKey(a => a.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Permission)
            .WithMany(c => c.RolePermissionAssignments)
            .HasForeignKey(a => a.PermissionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.RoleId);
        builder.HasIndex(a => a.PermissionId);
    }
}
