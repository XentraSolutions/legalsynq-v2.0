using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

public class GroupMembershipConfiguration : IEntityTypeConfiguration<GroupMembership>
{
    public void Configure(EntityTypeBuilder<GroupMembership> builder)
    {
        builder.ToTable("GroupMemberships");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.GroupId).IsRequired();
        builder.Property(m => m.UserId).IsRequired();
        builder.Property(m => m.TenantId).IsRequired();
        builder.Property(m => m.JoinedAtUtc).IsRequired();
        builder.Property(m => m.AddedByUserId);

        builder.HasIndex(m => new { m.GroupId, m.UserId }).IsUnique();
        builder.HasIndex(m => m.UserId);
        builder.HasIndex(m => m.TenantId);

        builder.HasOne(m => m.Group)
            .WithMany(g => g.Members)
            .HasForeignKey(m => m.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
