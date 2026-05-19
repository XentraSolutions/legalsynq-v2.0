using Commerce.Domain.Infrastructure;
using Commerce.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Commerce.Tests;

public class DbContextTests
{
    [Fact]
    public void CommerceDbContext_can_be_constructed_with_inmemory_options()
    {
        var options = new DbContextOptionsBuilder<CommerceDbContext>()
            .UseInMemoryDatabase($"commerce-test-{Guid.CreateVersion7():N}")
            .Options;

        using var ctx = new CommerceDbContext(options);

        ctx.Should().NotBeNull();
        ctx.SchemaMarkers.Should().NotBeNull();
    }

    [Fact]
    public void CommerceDbContext_model_includes_schema_marker_entity()
    {
        var options = new DbContextOptionsBuilder<CommerceDbContext>()
            .UseInMemoryDatabase($"commerce-test-{Guid.CreateVersion7():N}")
            .Options;

        using var ctx = new CommerceDbContext(options);

        var entity = ctx.Model.FindEntityType(typeof(CommerceSchemaMarker));
        entity.Should().NotBeNull();
    }
}
