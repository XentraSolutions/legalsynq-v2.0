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

    [Fact]
    public void Receivable_due_date_migration_uses_restart_safe_mysql_guards()
    {
        var migration = new AddReceivableDueDate();

        migration.UpOperations.Should().HaveCount(5);
        migration.UpOperations.Should().OnlyContain(operation => operation is SqlOperation);
        var sqlOperations = migration.UpOperations.Cast<SqlOperation>().ToList();
        sqlOperations.Should().OnlyContain(operation => operation.SuppressTransaction);
        var sql = string.Join(Environment.NewLine, sqlOperations.Select(operation => operation.Sql));

        sql.Should().Contain("COLUMN_NAME = 'ReceivableDueDate'");
        sql.Should().Contain("ALTER TABLE `liens_Liens` ADD COLUMN `ReceivableDueDate` date NULL");
        sql.Should().Contain("INDEX_NAME = 'IX_SettlementPayments_Tenant_Date_Deleted'");
        sql.Should().Contain("INDEX_NAME = 'IX_SettlementPayments_Tenant_Lien_Deleted'");
        sql.Should().Contain("INDEX_NAME = 'IX_Liens_Tenant_Seller_FundingCompanyCompanyId'");
        sql.Should().Contain("INDEX_NAME = 'IX_Liens_Tenant_Seller_ReceivableDueDate'");
    }

    [Fact]
    public void Selling_schema_recovery_replays_receivable_due_date_migration()
    {
        SellingSchemaRepair.CreateRecoveryMigrations()
            .Should().ContainSingle(migration => migration is AddReceivableDueDate);
    }

    [Fact]
    public void Selling_schema_recovery_replays_case_payment_and_legacy_parity_migrations()
    {
        SellingSchemaRepair.CreateRecoveryMigrations()
            .Select(migration => migration.GetType())
            .Should()
            .ContainInOrder(
                typeof(AddCasePaymentLedgerFields),
                typeof(AddWeeklyAgingReportIndex),
                typeof(AddLegacyReportParityFields),
                typeof(AddLienImportedCreatedByName));
    }
}
