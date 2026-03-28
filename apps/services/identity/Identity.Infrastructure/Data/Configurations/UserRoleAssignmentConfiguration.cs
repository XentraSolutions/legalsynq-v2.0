using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

public class UserRoleAssignmentConfiguration : IEntityTypeConfiguration<UserRoleAssignment>
{
    public void Configure(EntityTypeBuilder<UserRoleAssignment> builder)
    {
        builder.ToTable("UserRoleAssignments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.UserId).IsRequired();
        builder.Property(a => a.RoleId).IsRequired();
        builder.Property(a => a.OrganizationId);
        builder.Property(a => a.AssignedAtUtc).IsRequired();
        builder.Property(a => a.AssignedByUserId);

        builder.HasIndex(a => new { a.UserId, a.RoleId, a.OrganizationId }).IsUnique();
        builder.HasIndex(a => a.RoleId);
        builder.HasIndex(a => a.OrganizationId);

        builder.HasOne(a => a.User)
            .WithMany(u => u.RoleAssignments)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Role)
            .WithMany(r => r.RoleAssignments)
            .HasForeignKey(a => a.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Organization)
            .WithMany(o => o.RoleAssignments)
            .HasForeignKey(a => a.OrganizationId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
