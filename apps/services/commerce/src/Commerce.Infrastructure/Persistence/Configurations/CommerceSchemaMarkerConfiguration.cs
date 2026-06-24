using Commerce.Domain.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Commerce.Infrastructure.Persistence.Configurations;

internal sealed class CommerceSchemaMarkerConfiguration : IEntityTypeConfiguration<CommerceSchemaMarker>
{
    public void Configure(EntityTypeBuilder<CommerceSchemaMarker> builder)
    {
        builder.ToTable("commerce_schema_marker");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.SchemaName).HasMaxLength(64).IsRequired();
        builder.Property(x => x.SchemaVersion).HasMaxLength(32).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();

        builder.HasData(new CommerceSchemaMarker(
            id: 1,
            schemaName: "commerce",
            schemaVersion: "1.0.0",
            createdAtUtc: new DateTime(2026, 4, 23, 0, 0, 0, DateTimeKind.Utc)));
    }
}
