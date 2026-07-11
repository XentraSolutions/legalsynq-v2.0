using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xenia.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Migration 6 — Durable database-backed source synchronization lock.
    ///
    /// Adds the xn_email_source_sync_locks table.
    ///
    /// This replaces production reliance on the in-process SemaphoreSlim lock
    /// (InProcessEmailSourceSyncLock) which is not safe for multi-instance deployments
    /// or process restarts.
    ///
    /// Key constraints:
    /// - Unique on (tenant_id, email_source_id) — one lock row per source.
    /// - Lease expiry allows stale-lock recovery without deadlocks.
    /// - Version field provides optimistic concurrency for stale-lock takeover.
    ///
    /// Security guarantees:
    /// - tenant_id always part of the unique key — cross-tenant lock interference impossible.
    /// - No credential columns.
    /// - No attachment binary columns.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0058")]
    public partial class AddDurableSyncLock : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "xn_email_source_sync_locks",
                columns: table => new
                {
                    id              = table.Column<string>(type: "char(36)", nullable: false),
                    tenant_id       = table.Column<string>(type: "char(36)", nullable: false),
                    email_source_id = table.Column<string>(type: "char(36)", nullable: false),
                    lease_owner_id  = table.Column<string>(maxLength: 200, nullable: false),
                    acquired_at     = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    renewed_at      = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    expires_at      = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    version         = table.Column<int>(nullable: false, defaultValue: 1),
                    created_at      = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at      = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_xn_email_source_sync_locks", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_email_source_sync_locks_source",
                table: "xn_email_source_sync_locks",
                columns: new[] { "tenant_id", "email_source_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_email_source_sync_locks_expires_at",
                table: "xn_email_source_sync_locks",
                column: "expires_at");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "xn_email_source_sync_locks");
        }
    }
}
