using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xenia.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(Xenia.Infrastructure.Persistence.XeniaDbContext))]
    [Migration("20260713000001_AddAssistantCore")]
    public partial class AddAssistantCore : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "xn_assistant_agents",
                columns: table => new
                {
                    id = table.Column<string>(type: "char(36)", nullable: false),
                    agent_key = table.Column<string>(maxLength: 100, nullable: false),
                    name = table.Column<string>(maxLength: 200, nullable: false),
                    description = table.Column<string>(maxLength: 1000, nullable: false),
                    version = table.Column<string>(maxLength: 50, nullable: false),
                    system_prompt = table.Column<string>(type: "text", nullable: false),
                    allowed_tools_json = table.Column<string>(type: "text", nullable: false),
                    required_product_codes_json = table.Column<string>(type: "text", nullable: false),
                    is_enabled = table.Column<bool>(nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<uint>(nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_xn_assistant_agents", x => x.id));

            migrationBuilder.CreateIndex("uq_xn_assistant_agents_key", "xn_assistant_agents", "agent_key", unique: true);
            migrationBuilder.CreateIndex("ix_xn_assistant_agents_enabled", "xn_assistant_agents", "is_enabled");

            migrationBuilder.CreateTable(
                name: "xn_tenant_agents",
                columns: table => new
                {
                    id = table.Column<string>(type: "char(36)", nullable: false),
                    tenant_id = table.Column<string>(type: "char(36)", nullable: false),
                    agent_key = table.Column<string>(maxLength: 100, nullable: false),
                    enabled = table.Column<bool>(nullable: false, defaultValue: true),
                    configuration_json = table.Column<string>(type: "longtext", nullable: false),
                    updated_by = table.Column<string>(type: "char(36)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<uint>(nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_xn_tenant_agents", x => x.id));

            migrationBuilder.CreateIndex("uq_xn_tenant_agents_tenant_key", "xn_tenant_agents", new[] { "tenant_id", "agent_key" }, unique: true);
            migrationBuilder.CreateIndex("ix_xn_tenant_agents_tenant", "xn_tenant_agents", "tenant_id");

            migrationBuilder.CreateTable(
                name: "xn_conversations",
                columns: table => new
                {
                    id = table.Column<string>(type: "char(36)", nullable: false),
                    tenant_id = table.Column<string>(type: "char(36)", nullable: false),
                    actor_id = table.Column<string>(type: "char(36)", nullable: false),
                    agent_key = table.Column<string>(maxLength: 100, nullable: false),
                    agent_version = table.Column<string>(maxLength: 50, nullable: false),
                    title = table.Column<string>(maxLength: 200, nullable: false),
                    source = table.Column<string>(maxLength: 50, nullable: false),
                    context_json = table.Column<string>(type: "longtext", nullable: false),
                    status = table.Column<int>(nullable: false),
                    archived_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    last_message_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<uint>(nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_xn_conversations", x => x.id));

            migrationBuilder.CreateIndex("ix_xn_conversations_tenant_actor_status_updated", "xn_conversations", new[] { "tenant_id", "actor_id", "status", "updated_at" });
            migrationBuilder.CreateIndex("ix_xn_conversations_tenant_agent", "xn_conversations", new[] { "tenant_id", "agent_key" });

            migrationBuilder.CreateTable(
                name: "xn_conversation_messages",
                columns: table => new
                {
                    id = table.Column<string>(type: "char(36)", nullable: false),
                    conversation_id = table.Column<string>(type: "char(36)", nullable: false),
                    tenant_id = table.Column<string>(type: "char(36)", nullable: false),
                    actor_id = table.Column<string>(type: "char(36)", nullable: false),
                    role = table.Column<int>(nullable: false),
                    content = table.Column<string>(type: "longtext", nullable: false),
                    provider = table.Column<string>(maxLength: 50, nullable: false),
                    provider_response_id = table.Column<string>(maxLength: 200, nullable: true),
                    input_tokens = table.Column<int>(nullable: true),
                    output_tokens = table.Column<int>(nullable: true),
                    finish_reason = table.Column<string>(maxLength: 100, nullable: true),
                    metadata_json = table.Column<string>(type: "longtext", nullable: false),
                    message_created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<uint>(nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_xn_conversation_messages", x => x.id));

            migrationBuilder.CreateIndex("ix_xn_messages_tenant_conversation_created", "xn_conversation_messages", new[] { "tenant_id", "conversation_id", "message_created_at" });
            migrationBuilder.CreateIndex("ix_xn_messages_provider_response", "xn_conversation_messages", "provider_response_id");

            migrationBuilder.CreateTable(
                name: "xn_message_citations",
                columns: table => new
                {
                    id = table.Column<string>(type: "char(36)", nullable: false),
                    message_id = table.Column<string>(type: "char(36)", nullable: false),
                    tenant_id = table.Column<string>(type: "char(36)", nullable: false),
                    source_type = table.Column<string>(maxLength: 50, nullable: false),
                    source_id = table.Column<string>(maxLength: 200, nullable: false),
                    label = table.Column<string>(maxLength: 300, nullable: false),
                    url = table.Column<string>(maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_xn_message_citations", x => x.id));

            migrationBuilder.CreateIndex("ix_xn_citations_tenant_message", "xn_message_citations", new[] { "tenant_id", "message_id" });
            migrationBuilder.CreateIndex("ix_xn_citations_tenant_source", "xn_message_citations", new[] { "tenant_id", "source_type", "source_id" });

            migrationBuilder.CreateTable(
                name: "xn_tool_invocations",
                columns: table => new
                {
                    id = table.Column<string>(type: "char(36)", nullable: false),
                    conversation_id = table.Column<string>(type: "char(36)", nullable: false),
                    message_id = table.Column<string>(type: "char(36)", nullable: true),
                    tenant_id = table.Column<string>(type: "char(36)", nullable: false),
                    actor_id = table.Column<string>(type: "char(36)", nullable: false),
                    agent_key = table.Column<string>(maxLength: 100, nullable: false),
                    tool_key = table.Column<string>(maxLength: 100, nullable: false),
                    input_json = table.Column<string>(type: "longtext", nullable: false),
                    output_json = table.Column<string>(type: "longtext", nullable: true),
                    status = table.Column<string>(maxLength: 50, nullable: false),
                    confirmation_required = table.Column<bool>(nullable: false),
                    started_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    completed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    safe_error = table.Column<string>(maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<uint>(nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_xn_tool_invocations", x => x.id));

            migrationBuilder.CreateIndex("ix_xn_tool_invocations_tenant_conversation_started", "xn_tool_invocations", new[] { "tenant_id", "conversation_id", "started_at" });
            migrationBuilder.CreateIndex("ix_xn_tool_invocations_tenant_tool_status", "xn_tool_invocations", new[] { "tenant_id", "tool_key", "status" });

            migrationBuilder.CreateTable(
                name: "xn_usage_events",
                columns: table => new
                {
                    id = table.Column<string>(type: "char(36)", nullable: false),
                    tenant_id = table.Column<string>(type: "char(36)", nullable: false),
                    actor_id = table.Column<string>(type: "char(36)", nullable: false),
                    conversation_id = table.Column<string>(type: "char(36)", nullable: false),
                    message_id = table.Column<string>(type: "char(36)", nullable: true),
                    agent_key = table.Column<string>(maxLength: 100, nullable: false),
                    provider = table.Column<string>(maxLength: 50, nullable: false),
                    model_key = table.Column<string>(maxLength: 100, nullable: false),
                    event_type = table.Column<string>(maxLength: 50, nullable: false),
                    input_tokens = table.Column<int>(nullable: false),
                    output_tokens = table.Column<int>(nullable: false),
                    estimated_cost_usd = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    latency_ms = table.Column<int>(nullable: false),
                    occurred_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_xn_usage_events", x => x.id));

            migrationBuilder.CreateIndex("ix_xn_usage_events_tenant_occurred", "xn_usage_events", new[] { "tenant_id", "occurred_at" });
            migrationBuilder.CreateIndex("ix_xn_usage_events_tenant_actor_occurred", "xn_usage_events", new[] { "tenant_id", "actor_id", "occurred_at" });
            migrationBuilder.CreateIndex("ix_xn_usage_events_tenant_agent_occurred", "xn_usage_events", new[] { "tenant_id", "agent_key", "occurred_at" });

            migrationBuilder.CreateTable(
                name: "xn_quota_windows",
                columns: table => new
                {
                    id = table.Column<string>(type: "char(36)", nullable: false),
                    tenant_id = table.Column<string>(type: "char(36)", nullable: false),
                    actor_id = table.Column<string>(type: "char(36)", nullable: true),
                    window_key = table.Column<string>(maxLength: 100, nullable: false),
                    starts_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ends_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    request_count = table.Column<int>(nullable: false),
                    input_tokens = table.Column<int>(nullable: false),
                    output_tokens = table.Column<int>(nullable: false),
                    estimated_cost_usd = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<uint>(nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_xn_quota_windows", x => x.id));

            migrationBuilder.CreateIndex("uq_xn_quota_windows_scope", "xn_quota_windows", new[] { "tenant_id", "actor_id", "window_key", "starts_at" }, unique: true);
            migrationBuilder.CreateIndex("ix_xn_quota_windows_tenant_ends", "xn_quota_windows", new[] { "tenant_id", "ends_at" });

            migrationBuilder.CreateTable(
                name: "xn_user_preferences",
                columns: table => new
                {
                    id = table.Column<string>(type: "char(36)", nullable: false),
                    tenant_id = table.Column<string>(type: "char(36)", nullable: false),
                    actor_id = table.Column<string>(type: "char(36)", nullable: false),
                    default_agent_key = table.Column<string>(maxLength: 100, nullable: false),
                    context_hints_enabled = table.Column<bool>(nullable: false, defaultValue: true),
                    preferences_json = table.Column<string>(type: "longtext", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<uint>(nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_xn_user_preferences", x => x.id));

            migrationBuilder.CreateIndex("uq_xn_user_preferences_tenant_actor", "xn_user_preferences", new[] { "tenant_id", "actor_id" }, unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("xn_user_preferences");
            migrationBuilder.DropTable("xn_quota_windows");
            migrationBuilder.DropTable("xn_usage_events");
            migrationBuilder.DropTable("xn_tool_invocations");
            migrationBuilder.DropTable("xn_message_citations");
            migrationBuilder.DropTable("xn_conversation_messages");
            migrationBuilder.DropTable("xn_conversations");
            migrationBuilder.DropTable("xn_tenant_agents");
            migrationBuilder.DropTable("xn_assistant_agents");
        }
    }
}
