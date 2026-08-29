using Liens.Domain.Entities;
using Liens.Infrastructure.Persistence;
using Liens.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Liens.Api.Tests.Infrastructure;

public sealed class LegacyUpdateHistoryPersistenceTests
{
    [Theory]
    [InlineData(LegacyUpdateEvent.CaseScope, true)]
    [InlineData(LegacyUpdateEvent.LienScope, false)]
    public void Create_rejects_scope_and_lien_combinations_that_cannot_be_persisted(
        string scope,
        bool includeLien)
    {
        var action = () => CreateEvent(scope, includeLien ? Guid.NewGuid() : null);

        action.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(RequiredField.TenantId)]
    [InlineData(RequiredField.OrgId)]
    [InlineData(RequiredField.CaseId)]
    [InlineData(RequiredField.ImportRunId)]
    public void Create_rejects_empty_required_identifiers(RequiredField emptyField)
    {
        var tenantId = emptyField == RequiredField.TenantId ? Guid.Empty : Guid.NewGuid();
        var orgId = emptyField == RequiredField.OrgId ? Guid.Empty : Guid.NewGuid();
        var caseId = emptyField == RequiredField.CaseId ? Guid.Empty : Guid.NewGuid();
        var importRunId = emptyField == RequiredField.ImportRunId ? Guid.Empty : Guid.NewGuid();

        var action = () => LegacyUpdateEvent.Create(
            tenantId,
            orgId,
            caseId,
            null,
            LegacyUpdateEvent.CaseScope,
            "Case Details Update",
            "raw description",
            "legacy actor",
            Utc(2024, 7, 1, 17, 22, 8),
            Utc(2026, 8, 29, 1, 0, 0),
            importRunId,
            "SL-CORE",
            "SL_CASE_UPDATE_LOG",
            "123",
            123);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_requires_utc_timestamps_and_preserves_source_evidence_verbatim()
    {
        var localTimestamp = new DateTime(2024, 7, 1, 10, 22, 8, DateTimeKind.Unspecified);

        var invalid = () => LegacyUpdateEvent.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            LegacyUpdateEvent.CaseScope,
            "Case Details Update",
            "changed A ÔåÆ B ?",
            "  legacy actor  ",
            localTimestamp,
            Utc(2026, 8, 29, 1, 0, 0),
            Guid.NewGuid(),
            "SL-CORE",
            "SL_CASE_UPDATE_LOG",
            "123",
            123);

        invalid.Should().Throw<ArgumentException>()
            .WithParameterName("occurredAtUtc");

        var persisted = CreateEvent(
            LegacyUpdateEvent.CaseScope,
            description: "changed A ÔåÆ B ?",
            actorDisplayName: "  legacy actor  ");

        persisted.Description.Should().Be("changed A ÔåÆ B ?");
        persisted.ActorDisplayName.Should().Be("  legacy actor  ");
    }

    [Theory]
    [InlineData(EntityState.Modified)]
    [InlineData(EntityState.Deleted)]
    public async Task DbContext_rejects_updates_and_deletes_for_imported_history(EntityState state)
    {
        await using var db = CreateDbContext();
        var updateEvent = CreateEvent(LegacyUpdateEvent.CaseScope);
        db.LegacyUpdateEvents.Add(updateEvent);
        await db.SaveChangesAsync();

        db.Entry(updateEvent).State = state;

        var action = () => db.SaveChangesAsync();
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*append-only*");
    }

    [Fact]
    public void Model_enforces_timeline_uniqueness_and_restricted_import_run_ownership()
    {
        using var db = CreateDbContext();
        var entity = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(LegacyUpdateEvent))
            ?? throw new InvalidOperationException("LegacyUpdateEvent is not part of the Liens model.");

        entity.GetTableName().Should().Be("liens_LegacyUpdateEvents");
        entity.FindProperty(nameof(LegacyUpdateEvent.Action))!.GetMaxLength().Should().Be(255);
        entity.FindProperty(nameof(LegacyUpdateEvent.Description))!
            .FindAnnotation(RelationalAnnotationNames.ColumnType)!.Value.Should().Be("text");
        entity.FindProperty(nameof(LegacyUpdateEvent.ActorDisplayName))!.GetMaxLength().Should().Be(255);

        var indexes = entity.GetIndexes().ToDictionary(index => index.GetDatabaseName()!);
        indexes["UX_LegacyUpdateEvents_Tenant_Source_Table_Key"].IsUnique.Should().BeTrue();
        indexes["UX_LegacyUpdateEvents_Tenant_Source_Table_Key"].Properties
            .Select(property => property.Name)
            .Should().Equal(
                nameof(LegacyUpdateEvent.TenantId),
                nameof(LegacyUpdateEvent.SourceSystem),
                nameof(LegacyUpdateEvent.SourceTable),
                nameof(LegacyUpdateEvent.LegacyId));

        indexes["IX_LegacyUpdateEvents_CaseTimeline"].Properties.Select(property => property.Name)
            .Should().Equal(
                nameof(LegacyUpdateEvent.TenantId),
                nameof(LegacyUpdateEvent.CaseId),
                nameof(LegacyUpdateEvent.Scope),
                nameof(LegacyUpdateEvent.OccurredAtUtc),
                nameof(LegacyUpdateEvent.LegacySequence));
        indexes["IX_LegacyUpdateEvents_CaseTimeline"].IsDescending
            .Should().Equal(false, false, false, true, true);

        indexes["IX_LegacyUpdateEvents_LienTimeline"].IsDescending
            .Should().Equal(false, false, true, true);
        indexes.Should().ContainKey("IX_LegacyUpdateEvents_ImportRunId");

        var importRunForeignKey = entity.GetForeignKeys().Single();
        importRunForeignKey.PrincipalEntityType.ClrType.Should().Be(typeof(LegacyImportRun));
        importRunForeignKey.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
        entity.GetForeignKeys()
            .Where(foreignKey =>
                foreignKey.PrincipalEntityType.ClrType == typeof(Case) ||
                foreignKey.PrincipalEntityType.ClrType == typeof(Lien))
            .Should().BeEmpty();
    }

    [Fact]
    public void Migration_creates_required_contract_and_is_intentionally_irreversible()
    {
        var migration = new TestableAddLegacyUpdateEvents();

        var operations = migration.BuildUp();
        operations.Should().OnlyContain(operation => operation is SqlOperation);
        var sql = string.Join('\n', operations.Cast<SqlOperation>().Select(operation => operation.Sql));
        sql.Should().Contain("CREATE TABLE IF NOT EXISTS `liens_LegacyUpdateEvents`");
        sql.Should().Contain("CK_LegacyUpdateEvents_Scope");
        sql.Should().Contain("CK_LegacyUpdateEvents_ScopeLien");
        sql.Should().Contain("FK_liens_LegacyUpdateEvents_liens_LegacyImportRuns_ImportRunId");
        sql.Should().NotContain("FOREIGN KEY (`CaseId`)");
        sql.Should().NotContain("FOREIGN KEY (`LienId`)");
        sql.Should().Contain("UX_LegacyUpdateEvents_Tenant_Source_Table_Key");
        sql.Should().Contain("IX_LegacyUpdateEvents_CaseTimeline");
        sql.Should().Contain("IX_LegacyUpdateEvents_LienTimeline");
        sql.Should().Contain("IX_LegacyUpdateEvents_ImportRunId");
        sql.Should().Contain("information_schema.STATISTICS");

        var action = migration.BuildDown;
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*irreversible*");
    }

    private static LiensDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<LiensDbContext>()
            .UseInMemoryDatabase($"legacy-update-history-{Guid.NewGuid()}")
            .Options;
        return new LiensDbContext(options);
    }

    private static LegacyUpdateEvent CreateEvent(
        string scope,
        Guid? lienId = null,
        string? description = "raw description",
        string? actorDisplayName = "legacy actor") =>
        LegacyUpdateEvent.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            lienId,
            scope,
            "Case Details Update",
            description,
            actorDisplayName,
            Utc(2024, 7, 1, 17, 22, 8),
            Utc(2026, 8, 29, 1, 0, 0),
            Guid.NewGuid(),
            "SL-CORE",
            "SL_CASE_UPDATE_LOG",
            "123",
            123);

    private static DateTime Utc(int year, int month, int day, int hour, int minute, int second) =>
        new(year, month, day, hour, minute, second, DateTimeKind.Utc);

    public enum RequiredField
    {
        TenantId,
        OrgId,
        CaseId,
        ImportRunId,
    }

    private sealed class TestableAddLegacyUpdateEvents : AddLegacyUpdateEvents
    {
        public IReadOnlyList<MigrationOperation> BuildUp()
        {
            var builder = new MigrationBuilder("Pomelo.EntityFrameworkCore.MySql");
            Up(builder);
            return builder.Operations;
        }

        public void BuildDown()
        {
            var builder = new MigrationBuilder("Pomelo.EntityFrameworkCore.MySql");
            Down(builder);
        }
    }
}
