using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public sealed class CaseNumberReservationConfiguration : IEntityTypeConfiguration<CaseNumberReservation>
{
    public void Configure(EntityTypeBuilder<CaseNumberReservation> builder)
    {
        builder.ToTable("liens_CaseNumberReservations");
        builder.HasKey(reservation => new { reservation.TenantId, reservation.CaseNumber });

        builder.Property(reservation => reservation.TenantId).IsRequired();
        builder.Property(reservation => reservation.CaseNumber).IsRequired().HasMaxLength(50);
        builder.Property(reservation => reservation.ReservedAtUtc).IsRequired();
    }
}
