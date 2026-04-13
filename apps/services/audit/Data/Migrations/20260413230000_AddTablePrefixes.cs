using Microsoft.EntityFrameworkCore.Migrations;

  #nullable disable

  namespace PlatformAuditEventService.Data;

  public partial class AddTablePrefixes : Migration
  {
      protected override void Up(MigrationBuilder migrationBuilder)
      {
              migrationBuilder.RenameTable(name: "AuditEventRecords", newName: "aud_AuditEventRecords");
            migrationBuilder.RenameTable(name: "AuditExportJobs", newName: "aud_AuditExportJobs");
            migrationBuilder.RenameTable(name: "IngestSourceRegistrations", newName: "aud_IngestSourceRegistrations");
            migrationBuilder.RenameTable(name: "IntegrityCheckpoints", newName: "aud_IntegrityCheckpoints");
            migrationBuilder.RenameTable(name: "LegalHolds", newName: "aud_LegalHolds");
            migrationBuilder.RenameTable(name: "AuditEvents", newName: "aud_AuditEvents");
            migrationBuilder.RenameTable(name: "OutboxMessages", newName: "aud_OutboxMessages");
      }

      protected override void Down(MigrationBuilder migrationBuilder)
      {
              migrationBuilder.RenameTable(name: "aud_AuditEventRecords", newName: "AuditEventRecords");
            migrationBuilder.RenameTable(name: "aud_AuditExportJobs", newName: "AuditExportJobs");
            migrationBuilder.RenameTable(name: "aud_IngestSourceRegistrations", newName: "IngestSourceRegistrations");
            migrationBuilder.RenameTable(name: "aud_IntegrityCheckpoints", newName: "IntegrityCheckpoints");
            migrationBuilder.RenameTable(name: "aud_LegalHolds", newName: "LegalHolds");
            migrationBuilder.RenameTable(name: "aud_AuditEvents", newName: "AuditEvents");
            migrationBuilder.RenameTable(name: "aud_OutboxMessages", newName: "OutboxMessages");
      }
  }
  