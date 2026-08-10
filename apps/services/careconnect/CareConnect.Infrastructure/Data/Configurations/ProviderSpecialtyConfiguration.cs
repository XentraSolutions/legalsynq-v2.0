using CareConnect.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareConnect.Infrastructure.Data.Configurations;

public class ProviderSpecialtyConfiguration : IEntityTypeConfiguration<ProviderSpecialty>
{
    public void Configure(EntityTypeBuilder<ProviderSpecialty> builder)
    {
        builder.ToTable("cc_ProviderSpecialties");

        builder.HasKey(ps => new { ps.ProviderId, ps.SpecialtyId });

        builder.Property(ps => ps.ProviderId).IsRequired();
        builder.Property(ps => ps.SpecialtyId).IsRequired();
        builder.Property(ps => ps.IsPrimary).IsRequired();

        builder.HasIndex(ps => ps.SpecialtyId)
            .HasDatabaseName("IX_cc_ProviderSpecialties_SpecialtyId");

        builder.HasIndex(ps => new { ps.ProviderId, ps.IsPrimary })
            .HasDatabaseName("IX_cc_ProviderSpecialties_ProviderId_IsPrimary");
    }
}
