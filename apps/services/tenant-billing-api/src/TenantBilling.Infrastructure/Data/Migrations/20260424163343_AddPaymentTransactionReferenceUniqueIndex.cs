using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenantBilling.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentTransactionReferenceUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_payments_TenantId_TransactionReference",
                table: "payments",
                columns: new[] { "TenantId", "TransactionReference" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_payments_TenantId_TransactionReference",
                table: "payments");
        }
    }
}
