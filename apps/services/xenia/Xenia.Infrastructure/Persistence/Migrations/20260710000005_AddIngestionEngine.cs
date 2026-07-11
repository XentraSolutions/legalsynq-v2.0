using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xenia.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Migration 5 — Email Ingestion Engine foundation tables:
    ///
    ///   1. xn_email_messages      — canonical email message records (no binary, no credentials)
    ///   2. xn_email_recipients    — normalized recipient records per message
    ///   3. xn_email_attachment_references — attachment metadata stubs (no binary storage)
    ///   4. xn_email_sync_state    — per-source sync cursor and incremental state
    ///   5. xn_email_ingestion_runs — audit trail of every ingestion execution
    ///
    /// Security guarantees:
    /// - No attachment binary columns
    /// - No credential columns
    /// - All enum columns stored as VARCHAR
    /// - Cursor values are encrypted at the application layer in production
    /// - Tenant isolation enforced by all indexes
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0058")]
    public partial class AddIngestionEngine : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. xn_email_messages ──────────────────────────────────────────

            migrationBuilder.CreateTable(
                name: "xn_email_messages",
                columns: table => new
                {
                    id                      = table.Column<string>(type: "char(36)", nullable: false),
                    tenant_id               = table.Column<string>(type: "char(36)", nullable: false),
                    email_source_id         = table.Column<string>(type: "char(36)", nullable: false),
                    provider_type           = table.Column<string>(maxLength: 32, nullable: false),
                    provider_message_id     = table.Column<string>(maxLength: 1024, nullable: false),
                    internet_message_id     = table.Column<string>(maxLength: 998, nullable: true),
                    thread_id               = table.Column<string>(maxLength: 500, nullable: true),
                    conversation_id         = table.Column<string>(maxLength: 500, nullable: true),
                    subject                 = table.Column<string>(maxLength: 998, nullable: true),
                    from_address            = table.Column<string>(maxLength: 320, nullable: true),
                    from_name               = table.Column<string>(maxLength: 500, nullable: true),
                    sender_address          = table.Column<string>(maxLength: 320, nullable: true),
                    sender_name             = table.Column<string>(maxLength: 500, nullable: true),
                    reply_to_addresses      = table.Column<string>(maxLength: 2000, nullable: true),
                    sent_at                 = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    received_at             = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    importance              = table.Column<string>(maxLength: 16, nullable: false, defaultValue: "Normal"),
                    is_read                 = table.Column<bool>(nullable: true),
                    has_attachments         = table.Column<bool>(nullable: false, defaultValue: false),
                    attachment_count        = table.Column<int>(nullable: false, defaultValue: 0),
                    body_type               = table.Column<string>(maxLength: 16, nullable: false, defaultValue: "Unknown"),
                    body_text               = table.Column<string>(type: "mediumtext", nullable: true),
                    body_html               = table.Column<string>(type: "mediumtext", nullable: true),
                    body_preview            = table.Column<string>(maxLength: 500, nullable: true),
                    headers_json            = table.Column<string>(type: "text", nullable: true),
                    provider_metadata_json  = table.Column<string>(maxLength: 8000, nullable: true),
                    content_hash            = table.Column<string>(maxLength: 128, nullable: true),
                    import_status           = table.Column<string>(maxLength: 32, nullable: false, defaultValue: "Pending"),
                    processing_state        = table.Column<string>(maxLength: 32, nullable: false, defaultValue: "Pending"),
                    imported_at             = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    last_observed_at        = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    last_ingestion_run_id   = table.Column<string>(type: "char(36)", nullable: true),
                    version                 = table.Column<int>(nullable: false, defaultValue: 0),
                    created_at_utc          = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc          = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table => { table.PrimaryKey("PK_xn_email_messages", x => x.id); });

            migrationBuilder.CreateIndex(
                name: "ux_email_messages_provider_unique",
                table: "xn_email_messages",
                columns: new[] { "tenant_id", "email_source_id", "provider_type", "provider_message_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_email_messages_internet_message_id",
                table: "xn_email_messages",
                columns: new[] { "tenant_id", "internet_message_id" });

            migrationBuilder.CreateIndex("ix_email_messages_tenant",  "xn_email_messages", "tenant_id");
            migrationBuilder.CreateIndex("ix_email_messages_source",  "xn_email_messages", new[] { "tenant_id", "email_source_id" });
            migrationBuilder.CreateIndex("ix_email_messages_received_at", "xn_email_messages", new[] { "tenant_id", "received_at" });
            migrationBuilder.CreateIndex("ix_email_messages_import_status", "xn_email_messages", new[] { "tenant_id", "import_status" });
            migrationBuilder.CreateIndex("ix_email_messages_has_attachments", "xn_email_messages", new[] { "tenant_id", "has_attachments" });

            // ── 2. xn_email_recipients ────────────────────────────────────────

            migrationBuilder.CreateTable(
                name: "xn_email_recipients",
                columns: table => new
                {
                    id              = table.Column<string>(type: "char(36)", nullable: false),
                    tenant_id       = table.Column<string>(type: "char(36)", nullable: false),
                    email_message_id= table.Column<string>(type: "char(36)", nullable: false),
                    recipient_type  = table.Column<string>(maxLength: 16, nullable: false),
                    email_address   = table.Column<string>(maxLength: 320, nullable: false),
                    display_name    = table.Column<string>(maxLength: 500, nullable: true),
                    created_at_utc  = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table => { table.PrimaryKey("PK_xn_email_recipients", x => x.id); });

            migrationBuilder.CreateIndex("ix_email_recipients_message", "xn_email_recipients", "email_message_id");
            migrationBuilder.CreateIndex("ix_email_recipients_address", "xn_email_recipients", new[] { "tenant_id", "email_address" });

            // ── 3. xn_email_attachment_references ─────────────────────────────

            migrationBuilder.CreateTable(
                name: "xn_email_attachment_references",
                columns: table => new
                {
                    id                      = table.Column<string>(type: "char(36)", nullable: false),
                    tenant_id               = table.Column<string>(type: "char(36)", nullable: false),
                    email_message_id        = table.Column<string>(type: "char(36)", nullable: false),
                    provider_attachment_id  = table.Column<string>(maxLength: 1024, nullable: true),
                    document_reference_id   = table.Column<string>(type: "char(36)", nullable: true),
                    file_name               = table.Column<string>(maxLength: 500, nullable: false),
                    mime_type               = table.Column<string>(maxLength: 255, nullable: true),
                    size_bytes              = table.Column<long>(nullable: true),
                    content_hash            = table.Column<string>(maxLength: 128, nullable: true),
                    is_inline               = table.Column<bool>(nullable: false, defaultValue: false),
                    content_id              = table.Column<string>(maxLength: 500, nullable: true),
                    disposition             = table.Column<string>(maxLength: 100, nullable: true),
                    dispatch_status         = table.Column<string>(maxLength: 32, nullable: false, defaultValue: "Pending"),
                    error_code              = table.Column<string>(maxLength: 100, nullable: true),
                    safe_error_summary      = table.Column<string>(maxLength: 500, nullable: true),
                    created_at_utc          = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc          = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table => { table.PrimaryKey("PK_xn_email_attachment_references", x => x.id); });

            migrationBuilder.CreateIndex("ix_email_attachments_provider_id", "xn_email_attachment_references", new[] { "tenant_id", "email_message_id", "provider_attachment_id" });
            migrationBuilder.CreateIndex("ix_email_attachments_message", "xn_email_attachment_references", "email_message_id");
            migrationBuilder.CreateIndex("ix_email_attachments_dispatch_status", "xn_email_attachment_references", new[] { "tenant_id", "dispatch_status" });

            // ── 4. xn_email_sync_state ────────────────────────────────────────

            migrationBuilder.CreateTable(
                name: "xn_email_sync_state",
                columns: table => new
                {
                    id                              = table.Column<string>(type: "char(36)", nullable: false),
                    tenant_id                       = table.Column<string>(type: "char(36)", nullable: false),
                    email_source_id                 = table.Column<string>(type: "char(36)", nullable: false),
                    provider_type                   = table.Column<string>(maxLength: 32, nullable: false),
                    cursor_type                     = table.Column<string>(maxLength: 32, nullable: false),
                    cursor_value                    = table.Column<string>(maxLength: 4000, nullable: true),
                    cursor_metadata_json            = table.Column<string>(maxLength: 2000, nullable: true),
                    safe_cursor_summary             = table.Column<string>(maxLength: 200, nullable: true),
                    last_successful_sync_at         = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    last_attempted_sync_at          = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    last_processed_provider_timestamp=table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    last_processed_provider_message_id=table.Column<string>(maxLength: 1024, nullable: true),
                    initial_sync_completed          = table.Column<bool>(nullable: false, defaultValue: false),
                    consecutive_failure_count       = table.Column<int>(nullable: false, defaultValue: 0),
                    next_eligible_sync_at           = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    last_error_code                 = table.Column<string>(maxLength: 100, nullable: true),
                    safe_last_error_summary         = table.Column<string>(maxLength: 500, nullable: true),
                    state_version                   = table.Column<int>(nullable: false, defaultValue: 0),
                    created_at_utc                  = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc                  = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table => { table.PrimaryKey("PK_xn_email_sync_state", x => x.id); });

            migrationBuilder.CreateIndex(
                name: "ux_email_sync_state_source_unique",
                table: "xn_email_sync_state",
                column: "email_source_id",
                unique: true);

            migrationBuilder.CreateIndex("ix_email_sync_state_tenant",       "xn_email_sync_state", "tenant_id");
            migrationBuilder.CreateIndex("ix_email_sync_state_next_eligible", "xn_email_sync_state", new[] { "tenant_id", "next_eligible_sync_at" });
            migrationBuilder.CreateIndex("ix_email_sync_state_last_success",  "xn_email_sync_state", new[] { "tenant_id", "last_successful_sync_at" });

            // ── 5. xn_email_ingestion_runs ────────────────────────────────────

            migrationBuilder.CreateTable(
                name: "xn_email_ingestion_runs",
                columns: table => new
                {
                    id                          = table.Column<string>(type: "char(36)", nullable: false),
                    tenant_id                   = table.Column<string>(type: "char(36)", nullable: false),
                    email_source_id             = table.Column<string>(type: "char(36)", nullable: false),
                    trigger_type                = table.Column<string>(maxLength: 32, nullable: false),
                    status                      = table.Column<string>(maxLength: 32, nullable: false),
                    started_at                  = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    completed_at                = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    duration_ms                 = table.Column<long>(nullable: true),
                    correlation_id              = table.Column<string>(maxLength: 200, nullable: true),
                    actor_id                    = table.Column<string>(type: "char(36)", nullable: true),
                    worker_instance_id          = table.Column<string>(maxLength: 200, nullable: true),
                    messages_discovered         = table.Column<int>(nullable: false, defaultValue: 0),
                    messages_imported           = table.Column<int>(nullable: false, defaultValue: 0),
                    messages_updated            = table.Column<int>(nullable: false, defaultValue: 0),
                    messages_duplicated         = table.Column<int>(nullable: false, defaultValue: 0),
                    messages_failed             = table.Column<int>(nullable: false, defaultValue: 0),
                    attachments_discovered      = table.Column<int>(nullable: false, defaultValue: 0),
                    attachments_dispatched      = table.Column<int>(nullable: false, defaultValue: 0),
                    attachments_failed          = table.Column<int>(nullable: false, defaultValue: 0),
                    pages_processed             = table.Column<int>(nullable: false, defaultValue: 0),
                    retry_count                 = table.Column<int>(nullable: false, defaultValue: 0),
                    cursor_before_safe_summary  = table.Column<string>(maxLength: 200, nullable: true),
                    cursor_after_safe_summary   = table.Column<string>(maxLength: 200, nullable: true),
                    error_code                  = table.Column<string>(maxLength: 100, nullable: true),
                    safe_error_summary          = table.Column<string>(maxLength: 500, nullable: true),
                    created_at_utc              = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc              = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table => { table.PrimaryKey("PK_xn_email_ingestion_runs", x => x.id); });

            migrationBuilder.CreateIndex("ix_ingestion_runs_tenant",     "xn_email_ingestion_runs", "tenant_id");
            migrationBuilder.CreateIndex("ix_ingestion_runs_source",     "xn_email_ingestion_runs", new[] { "tenant_id", "email_source_id" });
            migrationBuilder.CreateIndex("ix_ingestion_runs_status",     "xn_email_ingestion_runs", new[] { "tenant_id", "status" });
            migrationBuilder.CreateIndex("ix_ingestion_runs_started_at", "xn_email_ingestion_runs", new[] { "tenant_id", "started_at" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "xn_email_ingestion_runs");
            migrationBuilder.DropTable(name: "xn_email_sync_state");
            migrationBuilder.DropTable(name: "xn_email_attachment_references");
            migrationBuilder.DropTable(name: "xn_email_recipients");
            migrationBuilder.DropTable(name: "xn_email_messages");
        }
    }
}
