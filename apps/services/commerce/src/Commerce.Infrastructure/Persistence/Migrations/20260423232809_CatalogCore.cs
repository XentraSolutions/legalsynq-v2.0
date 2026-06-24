using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Commerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CatalogCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "catalog_bundles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Key = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_bundles", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "catalog_products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Key = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_products", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "catalog_addons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ProductId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    Key = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_addons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_catalog_addons_catalog_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "catalog_products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "catalog_features",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ProductId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Key = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FeatureType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_features", x => x.Id);
                    table.ForeignKey(
                        name: "FK_catalog_features_catalog_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "catalog_products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "catalog_plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ProductId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    Key = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    BillingInterval = table.Column<int>(type: "int", nullable: false),
                    TrialDays = table.Column<int>(type: "int", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_plans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_catalog_plans_catalog_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "catalog_products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "catalog_bundle_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    BundleId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ProductId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    PlanId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    AddonId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_bundle_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_catalog_bundle_items_catalog_addons_AddonId",
                        column: x => x.AddonId,
                        principalTable: "catalog_addons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_catalog_bundle_items_catalog_bundles_BundleId",
                        column: x => x.BundleId,
                        principalTable: "catalog_bundles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_catalog_bundle_items_catalog_plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "catalog_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_catalog_bundle_items_catalog_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "catalog_products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "catalog_plan_features",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PlanId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FeatureId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    IsEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    LimitValue = table.Column<long>(type: "bigint", nullable: true),
                    MeteredIncludedUnits = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_plan_features", x => x.Id);
                    table.ForeignKey(
                        name: "FK_catalog_plan_features_catalog_features_FeatureId",
                        column: x => x.FeatureId,
                        principalTable: "catalog_features",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_catalog_plan_features_catalog_plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "catalog_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "catalog_prices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PlanId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    AddonId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    BundleId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    Currency = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    BillingInterval = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_prices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_catalog_prices_catalog_addons_AddonId",
                        column: x => x.AddonId,
                        principalTable: "catalog_addons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_catalog_prices_catalog_bundles_BundleId",
                        column: x => x.BundleId,
                        principalTable: "catalog_bundles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_catalog_prices_catalog_plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "catalog_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_catalog_addons_product_id",
                table: "catalog_addons",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "ux_catalog_addons_key",
                table: "catalog_addons",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_catalog_bundle_items_AddonId",
                table: "catalog_bundle_items",
                column: "AddonId");

            migrationBuilder.CreateIndex(
                name: "ix_catalog_bundle_items_bundle_id",
                table: "catalog_bundle_items",
                column: "BundleId");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_bundle_items_PlanId",
                table: "catalog_bundle_items",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_bundle_items_ProductId",
                table: "catalog_bundle_items",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "ux_catalog_bundles_key",
                table: "catalog_bundles",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_catalog_features_product_id",
                table: "catalog_features",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "ux_catalog_features_product_key",
                table: "catalog_features",
                columns: new[] { "ProductId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_catalog_plan_features_FeatureId",
                table: "catalog_plan_features",
                column: "FeatureId");

            migrationBuilder.CreateIndex(
                name: "ux_catalog_plan_features_plan_feature",
                table: "catalog_plan_features",
                columns: new[] { "PlanId", "FeatureId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_catalog_plans_product_id",
                table: "catalog_plans",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "ux_catalog_plans_key",
                table: "catalog_plans",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_catalog_prices_addon_lookup",
                table: "catalog_prices",
                columns: new[] { "AddonId", "Currency", "BillingInterval", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_catalog_prices_bundle_lookup",
                table: "catalog_prices",
                columns: new[] { "BundleId", "Currency", "BillingInterval", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_catalog_prices_plan_lookup",
                table: "catalog_prices",
                columns: new[] { "PlanId", "Currency", "BillingInterval", "Status" });

            migrationBuilder.CreateIndex(
                name: "ux_catalog_products_key",
                table: "catalog_products",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "catalog_bundle_items");

            migrationBuilder.DropTable(
                name: "catalog_plan_features");

            migrationBuilder.DropTable(
                name: "catalog_prices");

            migrationBuilder.DropTable(
                name: "catalog_features");

            migrationBuilder.DropTable(
                name: "catalog_addons");

            migrationBuilder.DropTable(
                name: "catalog_bundles");

            migrationBuilder.DropTable(
                name: "catalog_plans");

            migrationBuilder.DropTable(
                name: "catalog_products");
        }
    }
}
