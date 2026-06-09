using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Data.Configurations;

public class UserTenantConfiguration : IEntityTypeConfiguration<UserTenant>
{
    public void Configure(EntityTypeBuilder<UserTenant> builder)
    {
        builder.ToTable("idt_UserTenants");

        builder.HasKey(ut => ut.Id);

        builder.Property(ut => ut.UserId)
            .IsRequired();

        builder.Property(ut => ut.TenantId)
            .IsRequired();

        builder.Property(ut => ut.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(ut => ut.JoinedAtUtc)
            .IsRequired();

        builder.HasIndex(ut => new { ut.UserId, ut.TenantId })
            .IsUnique();

        builder.HasOne(ut => ut.User)
            .WithMany(u => u.TenantMemberships)
            .HasForeignKey(ut => ut.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ut => ut.Tenant)
            .WithMany()
            .HasForeignKey(ut => ut.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
