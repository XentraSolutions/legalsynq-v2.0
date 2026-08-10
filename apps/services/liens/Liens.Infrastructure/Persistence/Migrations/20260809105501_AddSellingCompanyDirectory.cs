using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Liens.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSellingCompanyDirectory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "liens_CompanyTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_liens_CompanyTypes", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "liens_Companies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    OrgId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    LinkedTenantId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    CompanyTypeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NormalizedName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AddressLine1 = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    City = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    State = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PostalCode = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Phone = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Email = table.Column<string>(type: "varchar(320)", maxLength: 320, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_liens_Companies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_liens_Companies_liens_CompanyTypes_CompanyTypeId",
                        column: x => x.CompanyTypeId,
                        principalTable: "liens_CompanyTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "liens_ContactPersonTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CompanyTypeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Code = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_liens_ContactPersonTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_liens_ContactPersonTypes_liens_CompanyTypes_CompanyTypeId",
                        column: x => x.CompanyTypeId,
                        principalTable: "liens_CompanyTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "liens_CompanyContactPersons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CompanyId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ContactPersonTypeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FirstName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AddressLine1 = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    City = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    State = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PostalCode = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Phone = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Email = table.Column<string>(type: "varchar(320)", maxLength: 320, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_liens_CompanyContactPersons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_liens_CompanyContactPersons_liens_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "liens_Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_liens_CompanyContactPersons_liens_ContactPersonTypes_Contact~",
                        column: x => x.ContactPersonTypeId,
                        principalTable: "liens_ContactPersonTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "liens_CompanyTypes",
                columns: new[] { "Id", "Code", "CreatedAtUtc", "CreatedByUserId", "IsActive", "Name", "SortOrder", "UpdatedAtUtc", "UpdatedByUserId" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), "LawFirm", new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001"), true, "Law Firm", 1, new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("10000000-0000-0000-0000-000000000002"), "FundingCompany", new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001"), true, "Funding Company", 2, new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("10000000-0000-0000-0000-000000000003"), "MedicalProvider", new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001"), true, "Medical Provider", 3, new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("10000000-0000-0000-0000-000000000004"), "MedicalFacility", new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001"), true, "Medical Facility", 4, new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001") }
                });

            migrationBuilder.InsertData(
                table: "liens_ContactPersonTypes",
                columns: new[] { "Id", "Code", "CompanyTypeId", "CreatedAtUtc", "CreatedByUserId", "IsActive", "Name", "SortOrder", "UpdatedAtUtc", "UpdatedByUserId" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000001"), "Attorney", new Guid("10000000-0000-0000-0000-000000000001"), new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001"), true, "Attorney", 1, new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000002"), "Paralegal", new Guid("10000000-0000-0000-0000-000000000001"), new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001"), true, "Paralegal", 2, new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000003"), "CaseManager", new Guid("10000000-0000-0000-0000-000000000001"), new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001"), true, "Case Manager", 3, new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000004"), "IntakeSpecialist", new Guid("10000000-0000-0000-0000-000000000001"), new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001"), true, "Intake Specialist", 4, new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000005"), "LegalAssistant", new Guid("10000000-0000-0000-0000-000000000001"), new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001"), true, "Legal Assistant", 5, new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000006"), "BillingSpecialist", new Guid("10000000-0000-0000-0000-000000000001"), new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001"), true, "Billing Specialist", 6, new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000007"), "FirmAdministrator", new Guid("10000000-0000-0000-0000-000000000001"), new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001"), true, "Firm Administrator", 7, new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000008"), "Underwriter", new Guid("10000000-0000-0000-0000-000000000002"), new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001"), true, "Underwriter", 1, new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000009"), "FundingSpecialist", new Guid("10000000-0000-0000-0000-000000000002"), new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001"), true, "Funding Specialist", 2, new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000010"), "AccountManager", new Guid("10000000-0000-0000-0000-000000000002"), new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001"), true, "Account Manager", 3, new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000011"), "CollectionsSpecialist", new Guid("10000000-0000-0000-0000-000000000002"), new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001"), true, "Collections Specialist", 4, new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000012"), "ComplianceOfficer", new Guid("10000000-0000-0000-0000-000000000002"), new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001"), true, "Compliance Officer", 5, new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000013"), "FinanceManager", new Guid("10000000-0000-0000-0000-000000000002"), new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001"), true, "Finance Manager", 6, new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000014"), "CompanyAdministrator", new Guid("10000000-0000-0000-0000-000000000002"), new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001"), true, "Company Administrator", 7, new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000015"), "Physician", new Guid("10000000-0000-0000-0000-000000000003"), new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001"), true, "Physician", 1, new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000016"), "Chiropractor", new Guid("10000000-0000-0000-0000-000000000003"), new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001"), true, "Chiropractor", 2, new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000017"), "Therapist", new Guid("10000000-0000-0000-0000-000000000003"), new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001"), true, "Therapist", 3, new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000018"), "NursePractitioner", new Guid("10000000-0000-0000-0000-000000000003"), new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001"), true, "Nurse Practitioner", 4, new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000019"), "ProviderRepresentative", new Guid("10000000-0000-0000-0000-000000000003"), new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001"), true, "Provider Representative", 5, new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000020"), "BillingSpecialist", new Guid("10000000-0000-0000-0000-000000000003"), new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001"), true, "Billing Specialist", 6, new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000021"), "MedicalRecordsCoordinator", new Guid("10000000-0000-0000-0000-000000000003"), new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001"), true, "Medical Records Coordinator", 7, new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000022"), "FacilityAdministrator", new Guid("10000000-0000-0000-0000-000000000004"), new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001"), true, "Facility Administrator", 1, new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000023"), "PracticeManager", new Guid("10000000-0000-0000-0000-000000000004"), new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001"), true, "Practice Manager", 2, new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000024"), "FrontDeskIntakeStaff", new Guid("10000000-0000-0000-0000-000000000004"), new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001"), true, "Front Desk/Intake Staff", 3, new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000025"), "Scheduler", new Guid("10000000-0000-0000-0000-000000000004"), new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001"), true, "Scheduler", 4, new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000026"), "CareCoordinator", new Guid("10000000-0000-0000-0000-000000000004"), new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001"), true, "Care Coordinator", 5, new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000027"), "BillingSpecialist", new Guid("10000000-0000-0000-0000-000000000004"), new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001"), true, "Billing Specialist", 6, new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000028"), "MedicalRecordsSpecialist", new Guid("10000000-0000-0000-0000-000000000004"), new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001"), true, "Medical Records Specialist", 7, new DateTime(2026, 8, 9, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000001") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Companies_LinkedTenantId",
                table: "liens_Companies",
                column: "LinkedTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_TenantId_OrgId_CompanyTypeId_IsActive",
                table: "liens_Companies",
                columns: new[] { "TenantId", "OrgId", "CompanyTypeId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_liens_Companies_CompanyTypeId",
                table: "liens_Companies",
                column: "CompanyTypeId");

            migrationBuilder.CreateIndex(
                name: "UX_Companies_TenantId_OrgId_CompanyTypeId_NormalizedName",
                table: "liens_Companies",
                columns: new[] { "TenantId", "OrgId", "CompanyTypeId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyContactPersons_CompanyId_ContactPersonTypeId",
                table: "liens_CompanyContactPersons",
                columns: new[] { "CompanyId", "ContactPersonTypeId" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyContactPersons_TenantId_CompanyId_IsActive_Name",
                table: "liens_CompanyContactPersons",
                columns: new[] { "TenantId", "CompanyId", "IsActive", "LastName", "FirstName" });

            migrationBuilder.CreateIndex(
                name: "IX_liens_CompanyContactPersons_ContactPersonTypeId",
                table: "liens_CompanyContactPersons",
                column: "ContactPersonTypeId");

            migrationBuilder.CreateIndex(
                name: "UX_CompanyTypes_Code",
                table: "liens_CompanyTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContactPersonTypes_CompanyTypeId_IsActive_SortOrder",
                table: "liens_ContactPersonTypes",
                columns: new[] { "CompanyTypeId", "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "UX_ContactPersonTypes_CompanyTypeId_Code",
                table: "liens_ContactPersonTypes",
                columns: new[] { "CompanyTypeId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "liens_CompanyContactPersons");

            migrationBuilder.DropTable(
                name: "liens_Companies");

            migrationBuilder.DropTable(
                name: "liens_ContactPersonTypes");

            migrationBuilder.DropTable(
                name: "liens_CompanyTypes");
        }
    }
}
