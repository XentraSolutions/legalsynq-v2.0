using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for <see cref="RoleCapabilityAssignment"/>.
///
/// Composite PK on (RoleId, CapabilityId) ensures a capability can only be
/// assigned to a role once. On-delete is Restrict on both sides so that
/// dropping a role or capability doesn't silently cascade.
/// </summary>
public class RoleCapabilityAssignmentConfiguration : IEntityTypeConfiguration<RoleCapabilityAssignment>
{
    public void Configure(EntityTypeBuilder<RoleCapabilityAssignment> builder)
    {
        builder.ToTable("RoleCapabilityAssignments");

        builder.HasKey(a => new { a.RoleId, a.CapabilityId });

        builder.Property(a => a.AssignedAtUtc).IsRequired();
        builder.Property(a => a.AssignedByUserId).HasColumnType("char(36)");

        builder.HasOne(a => a.Role)
            .WithMany(r => r.RoleCapabilityAssignments)
            .HasForeignKey(a => a.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Capability)
            .WithMany(c => c.RoleCapabilityAssignments)
            .HasForeignKey(a => a.CapabilityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.RoleId);
        builder.HasIndex(a => a.CapabilityId);
    }
}
