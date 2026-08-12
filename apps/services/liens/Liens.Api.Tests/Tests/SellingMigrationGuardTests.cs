using Liens.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Liens.Api.Tests.Tests;

public sealed class SellingMigrationGuardTests
{
    [Fact]
    public void Scoped_contact_person_type_migration_uses_restart_safe_mysql_guards()
    {
        var migration = new AddScopedContactPersonTypes();

        migration.UpOperations.Should().NotBeEmpty();
        migration.UpOperations.Should().OnlyContain(operation => operation is SqlOperation);

        var sqlOperations = migration.UpOperations.Cast<SqlOperation>().ToList();
        sqlOperations.Should().OnlyContain(operation => operation.SuppressTransaction);
        var sql = string.Join(Environment.NewLine, sqlOperations.Select(operation => operation.Sql));

        sql.Should().Contain("COLUMN_NAME = 'TenantId'");
        sql.Should().Contain("COLUMN_NAME = 'OrgId'");
        sql.Should().Contain("DROP INDEX `UX_ContactPersonTypes_CompanyTypeId_Code`");
        sql.Should().Contain("CREATE UNIQUE INDEX `UX_ContactPersonTypes_Scope_CompanyTypeId_Code`");
        sql.Should().Contain("CONSTRAINT_NAME = 'CK_ContactPersonTypes_Scope'");
        sql.Should().Contain("CONSTRAINT_TYPE = 'CHECK'");

        var createIndexPosition = sql.IndexOf(
            "CREATE UNIQUE INDEX `UX_ContactPersonTypes_Scope_CompanyTypeId_Code`",
            StringComparison.Ordinal);
        var dropIndexPosition = sql.IndexOf(
            "DROP INDEX `UX_ContactPersonTypes_CompanyTypeId_Code`",
            StringComparison.Ordinal);
        createIndexPosition.Should().BeLessThan(dropIndexPosition);
    }

    [Fact]
    public void Company_directory_recovery_accepts_scoped_index_as_legacy_index_replacement()
    {
        var migration = new AddSellingCompanyDirectory();

        var legacyIndexOperation = migration.UpOperations
            .OfType<SqlOperation>()
            .Single(operation => operation.Sql.Contains(
                "CREATE UNIQUE INDEX `UX_ContactPersonTypes_CompanyTypeId_Code`",
                StringComparison.Ordinal));

        legacyIndexOperation.Sql.Should().Contain(
            "INDEX_NAME IN ('UX_ContactPersonTypes_CompanyTypeId_Code', " +
            "'UX_ContactPersonTypes_Scope_CompanyTypeId_Code')");
        legacyIndexOperation.Sql.Should().Contain("COLUMN_NAME = 'TenantId'");
    }

    [Fact]
    public void Selling_schema_recovery_replays_scoped_contact_person_type_migration()
    {
        SellingSchemaRepair.CreateRecoveryMigrations()
            .Should().ContainSingle(migration => migration is AddScopedContactPersonTypes);
    }
}
