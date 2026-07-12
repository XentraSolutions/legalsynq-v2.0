using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Xenia.Infrastructure.Data.Migrations;

[DbContext(typeof(XeniaDbContext))]
[Migration("20260709221000_InitialXeniaPersistence")]
public partial class InitialXeniaPersistence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "xen_agents",
            columns: table => new
            {
                AgentId = table.Column<Guid>(type: "char(36)", nullable: false),
                AgentCode = table.Column<string>(type: "longtext", nullable: false),
                DisplayName = table.Column<string>(type: "longtext", nullable: false),
                Description = table.Column<string>(type: "longtext", nullable: false),
                Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_xen_agents", x => x.AgentId));

        migrationBuilder.CreateTable(
            name: "xen_audit_events",
            columns: table => new
            {
                AuditEventId = table.Column<Guid>(type: "char(36)", nullable: false),
                TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                EventType = table.Column<string>(type: "longtext", nullable: false),
                ActorUserId = table.Column<string>(type: "longtext", nullable: false),
                Description = table.Column<string>(type: "longtext", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_xen_audit_events", x => x.AuditEventId));

        migrationBuilder.CreateTable(
            name: "xen_budget_policies",
            columns: table => new
            {
                BudgetPolicyId = table.Column<Guid>(type: "char(36)", nullable: false),
                TenantId = table.Column<Guid>(type: "char(36)", nullable: true),
                PolicyName = table.Column<string>(type: "longtext", nullable: false),
                SoftLimitUsd = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                HardLimitUsd = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                BlockOnHardLimit = table.Column<bool>(type: "tinyint(1)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_xen_budget_policies", x => x.BudgetPolicyId));

        migrationBuilder.CreateTable(
            name: "xen_embedding_indexes",
            columns: table => new
            {
                EmbeddingIndexId = table.Column<Guid>(type: "char(36)", nullable: false),
                IndexCode = table.Column<string>(type: "longtext", nullable: false),
                Provider = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                ModelCode = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false),
                Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_xen_embedding_indexes", x => x.EmbeddingIndexId));

        migrationBuilder.CreateTable(
            name: "xen_governance_events",
            columns: table => new
            {
                GovernanceEventId = table.Column<Guid>(type: "char(36)", nullable: false),
                TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                EventType = table.Column<string>(type: "longtext", nullable: false),
                Severity = table.Column<string>(type: "longtext", nullable: false),
                Description = table.Column<string>(type: "longtext", nullable: false),
                MetadataJson = table.Column<string>(type: "longtext", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_xen_governance_events", x => x.GovernanceEventId));

        migrationBuilder.CreateTable(
            name: "xen_knowledge_sources",
            columns: table => new
            {
                KnowledgeSourceId = table.Column<Guid>(type: "char(36)", nullable: false),
                TenantId = table.Column<Guid>(type: "char(36)", nullable: true),
                SourceCode = table.Column<string>(type: "longtext", nullable: false),
                DisplayName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                SourceType = table.Column<string>(type: "longtext", nullable: false),
                Status = table.Column<string>(type: "longtext", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_xen_knowledge_sources", x => x.KnowledgeSourceId));

        migrationBuilder.CreateTable(
            name: "xen_marketplace_assets",
            columns: table => new
            {
                MarketplaceAssetId = table.Column<Guid>(type: "char(36)", nullable: false),
                AssetCode = table.Column<string>(type: "longtext", nullable: false),
                AssetType = table.Column<string>(type: "longtext", nullable: false),
                DisplayName = table.Column<string>(type: "longtext", nullable: false),
                Description = table.Column<string>(type: "longtext", nullable: false),
                Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_xen_marketplace_assets", x => x.MarketplaceAssetId));

        migrationBuilder.CreateTable(
            name: "xen_model_catalog",
            columns: table => new
            {
                ModelCatalogEntryId = table.Column<Guid>(type: "char(36)", nullable: false),
                Provider = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                ModelCode = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false),
                DisplayName = table.Column<string>(type: "longtext", nullable: false),
                SupportsStreaming = table.Column<bool>(type: "tinyint(1)", nullable: false),
                SupportsEmbeddings = table.Column<bool>(type: "tinyint(1)", nullable: false),
                Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                ContextSize = table.Column<int>(type: "int", nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_xen_model_catalog", x => x.ModelCatalogEntryId));

        migrationBuilder.CreateTable(
            name: "xen_prompt_templates",
            columns: table => new
            {
                PromptTemplateId = table.Column<Guid>(type: "char(36)", nullable: false),
                TemplateCode = table.Column<string>(type: "longtext", nullable: false),
                DisplayName = table.Column<string>(type: "longtext", nullable: false),
                Description = table.Column<string>(type: "longtext", nullable: false),
                Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_xen_prompt_templates", x => x.PromptTemplateId));

        migrationBuilder.CreateTable(
            name: "xen_provider_configurations",
            columns: table => new
            {
                ProviderConfigurationId = table.Column<Guid>(type: "char(36)", nullable: false),
                ProviderType = table.Column<int>(type: "int", nullable: false),
                Scope = table.Column<int>(type: "int", nullable: false),
                TenantId = table.Column<Guid>(type: "char(36)", nullable: true),
                DisplayName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                Endpoint = table.Column<string>(type: "longtext", nullable: true),
                Region = table.Column<string>(type: "longtext", nullable: true),
                AzureDeploymentName = table.Column<string>(type: "longtext", nullable: true),
                DefaultModel = table.Column<string>(type: "longtext", nullable: false),
                AllowedModels = table.Column<string>(type: "longtext", nullable: false),
                TimeoutSeconds = table.Column<int>(type: "int", nullable: false),
                RetryCount = table.Column<int>(type: "int", nullable: false),
                FailoverPriority = table.Column<int>(type: "int", nullable: false),
                Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                VerificationStatus = table.Column<int>(type: "int", nullable: false),
                LastVerifiedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                CredentialStorageMode = table.Column<int>(type: "int", nullable: false),
                SecretReference = table.Column<string>(type: "longtext", nullable: true),
                CredentialFingerprint = table.Column<string>(type: "longtext", nullable: true),
                CredentialLastFour = table.Column<string>(type: "longtext", nullable: true),
                HasStoredCredential = table.Column<bool>(type: "tinyint(1)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_xen_provider_configurations", x => x.ProviderConfigurationId));

        migrationBuilder.CreateTable(
            name: "xen_provider_health_events",
            columns: table => new
            {
                ProviderHealthEventId = table.Column<Guid>(type: "char(36)", nullable: false),
                ProviderConfigurationId = table.Column<Guid>(type: "char(36)", nullable: false),
                ProviderName = table.Column<string>(type: "longtext", nullable: false),
                Status = table.Column<string>(type: "longtext", nullable: false),
                Message = table.Column<string>(type: "longtext", nullable: false),
                CheckedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_xen_provider_health_events", x => x.ProviderHealthEventId));

        migrationBuilder.CreateTable(
            name: "xen_quota_policies",
            columns: table => new
            {
                QuotaPolicyId = table.Column<Guid>(type: "char(36)", nullable: false),
                TenantId = table.Column<Guid>(type: "char(36)", nullable: true),
                PolicyName = table.Column<string>(type: "longtext", nullable: false),
                MonthlyRequestLimit = table.Column<int>(type: "int", nullable: false),
                MonthlyTokenLimit = table.Column<int>(type: "int", nullable: false),
                BlockOnLimit = table.Column<bool>(type: "tinyint(1)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_xen_quota_policies", x => x.QuotaPolicyId));

        migrationBuilder.CreateTable(
            name: "xen_skills",
            columns: table => new
            {
                SkillId = table.Column<Guid>(type: "char(36)", nullable: false),
                SkillCode = table.Column<string>(type: "longtext", nullable: false),
                DisplayName = table.Column<string>(type: "longtext", nullable: false),
                Description = table.Column<string>(type: "longtext", nullable: false),
                Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_xen_skills", x => x.SkillId));

        migrationBuilder.CreateTable(
            name: "xen_tenant_ai_configurations",
            columns: table => new
            {
                TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                DeploymentModel = table.Column<int>(type: "int", nullable: false),
                DefaultProviderConfigurationId = table.Column<Guid>(type: "char(36)", nullable: true),
                DefaultModel = table.Column<string>(type: "longtext", nullable: false),
                Temperature = table.Column<double>(type: "double", nullable: false),
                MaxTokens = table.Column<int>(type: "int", nullable: false),
                ReasoningLevel = table.Column<string>(type: "longtext", nullable: false),
                RetentionPolicy = table.Column<string>(type: "longtext", nullable: false),
                ModerationPolicy = table.Column<string>(type: "longtext", nullable: false),
                FailoverEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                BudgetPolicyId = table.Column<Guid>(type: "char(36)", nullable: true),
                QuotaPolicyId = table.Column<Guid>(type: "char(36)", nullable: true),
                AllowedSkills = table.Column<string>(type: "longtext", nullable: false),
                AllowedAgents = table.Column<string>(type: "longtext", nullable: false),
                AllowedTools = table.Column<string>(type: "longtext", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_xen_tenant_ai_configurations", x => x.TenantId));

        migrationBuilder.CreateTable(
            name: "xen_tenant_model_policies",
            columns: table => new
            {
                TenantModelPolicyId = table.Column<Guid>(type: "char(36)", nullable: false),
                TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                PolicyName = table.Column<string>(type: "longtext", nullable: false),
                AllowedProviders = table.Column<string>(type: "longtext", nullable: false),
                AllowedModels = table.Column<string>(type: "longtext", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_xen_tenant_model_policies", x => x.TenantModelPolicyId));

        migrationBuilder.CreateTable(
            name: "xen_tool_definitions",
            columns: table => new
            {
                ToolDefinitionId = table.Column<Guid>(type: "char(36)", nullable: false),
                ToolCode = table.Column<string>(type: "longtext", nullable: false),
                DisplayName = table.Column<string>(type: "longtext", nullable: false),
                Description = table.Column<string>(type: "longtext", nullable: false),
                RequiredPermission = table.Column<string>(type: "longtext", nullable: false),
                Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_xen_tool_definitions", x => x.ToolDefinitionId));

        migrationBuilder.CreateTable(
            name: "xen_usage_ledger",
            columns: table => new
            {
                UsageEventId = table.Column<Guid>(type: "char(36)", nullable: false),
                TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                UserId = table.Column<string>(type: "longtext", nullable: false),
                EventKind = table.Column<string>(type: "longtext", nullable: false),
                Provider = table.Column<string>(type: "longtext", nullable: false),
                Model = table.Column<string>(type: "longtext", nullable: false),
                PromptTokens = table.Column<int>(type: "int", nullable: false),
                CompletionTokens = table.Column<int>(type: "int", nullable: false),
                EstimatedCostUsd = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_xen_usage_ledger", x => x.UsageEventId));

        migrationBuilder.CreateTable(
            name: "xen_agent_versions",
            columns: table => new
            {
                AgentVersionId = table.Column<Guid>(type: "char(36)", nullable: false),
                AgentId = table.Column<Guid>(type: "char(36)", nullable: false),
                VersionNumber = table.Column<int>(type: "int", nullable: false),
                DefinitionJson = table.Column<string>(type: "longtext", nullable: false),
                ApprovalState = table.Column<string>(type: "longtext", nullable: false),
                IsCurrent = table.Column<bool>(type: "tinyint(1)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_xen_agent_versions", x => x.AgentVersionId);
                table.ForeignKey("FK_xen_agent_versions_xen_agents_AgentId", x => x.AgentId, "xen_agents", "AgentId", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "xen_conversations",
            columns: table => new
            {
                ConversationId = table.Column<Guid>(type: "char(36)", nullable: false),
                TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                CreatedByUserId = table.Column<string>(type: "longtext", nullable: false),
                Title = table.Column<string>(type: "longtext", nullable: false),
                ActivationSource = table.Column<string>(type: "longtext", nullable: false),
                ProductCode = table.Column<string>(type: "longtext", nullable: true),
                SourceReference = table.Column<string>(type: "longtext", nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_xen_conversations", x => x.ConversationId));

        migrationBuilder.CreateTable(
            name: "xen_embedding_records",
            columns: table => new
            {
                EmbeddingRecordId = table.Column<Guid>(type: "char(36)", nullable: false),
                EmbeddingIndexId = table.Column<Guid>(type: "char(36)", nullable: false),
                KnowledgeChunkId = table.Column<Guid>(type: "char(36)", nullable: false),
                VectorJson = table.Column<string>(type: "longtext", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_xen_embedding_records", x => x.EmbeddingRecordId);
                table.ForeignKey("FK_xen_embedding_records_xen_embedding_indexes_EmbeddingIndexId", x => x.EmbeddingIndexId, "xen_embedding_indexes", "EmbeddingIndexId", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "xen_knowledge_documents",
            columns: table => new
            {
                KnowledgeDocumentId = table.Column<Guid>(type: "char(36)", nullable: false),
                KnowledgeSourceId = table.Column<Guid>(type: "char(36)", nullable: false),
                ExternalDocumentId = table.Column<string>(type: "longtext", nullable: false),
                Title = table.Column<string>(type: "longtext", nullable: false),
                Status = table.Column<string>(type: "longtext", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_xen_knowledge_documents", x => x.KnowledgeDocumentId);
                table.ForeignKey("FK_xen_knowledge_documents_xen_knowledge_sources_KnowledgeSourceId", x => x.KnowledgeSourceId, "xen_knowledge_sources", "KnowledgeSourceId", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "xen_marketplace_installations",
            columns: table => new
            {
                MarketplaceInstallationId = table.Column<Guid>(type: "char(36)", nullable: false),
                MarketplaceAssetId = table.Column<Guid>(type: "char(36)", nullable: false),
                TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                Status = table.Column<string>(type: "longtext", nullable: false),
                InstalledAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_xen_marketplace_installations", x => x.MarketplaceInstallationId);
                table.ForeignKey("FK_xen_marketplace_installations_xen_marketplace_assets_MarketplaceAssetId", x => x.MarketplaceAssetId, "xen_marketplace_assets", "MarketplaceAssetId", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "xen_prompt_versions",
            columns: table => new
            {
                PromptVersionId = table.Column<Guid>(type: "char(36)", nullable: false),
                PromptTemplateId = table.Column<Guid>(type: "char(36)", nullable: false),
                VersionNumber = table.Column<int>(type: "int", nullable: false),
                Content = table.Column<string>(type: "longtext", nullable: false),
                ApprovalState = table.Column<string>(type: "longtext", nullable: false),
                IsCurrent = table.Column<bool>(type: "tinyint(1)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_xen_prompt_versions", x => x.PromptVersionId);
                table.ForeignKey("FK_xen_prompt_versions_xen_prompt_templates_PromptTemplateId", x => x.PromptTemplateId, "xen_prompt_templates", "PromptTemplateId", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "xen_provider_credentials",
            columns: table => new
            {
                ProviderCredentialId = table.Column<Guid>(type: "char(36)", nullable: false),
                ProviderConfigurationId = table.Column<Guid>(type: "char(36)", nullable: false),
                StorageMode = table.Column<int>(type: "int", nullable: false),
                EncryptedSecretPayload = table.Column<string>(type: "longtext", nullable: true),
                ExternalSecretReference = table.Column<string>(type: "longtext", nullable: true),
                Fingerprint = table.Column<string>(type: "longtext", nullable: false),
                LastFour = table.Column<string>(type: "longtext", nullable: true),
                IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                LastVerifiedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                VerificationStatus = table.Column<int>(type: "int", nullable: false),
                RotatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_xen_provider_credentials", x => x.ProviderCredentialId);
                table.ForeignKey("FK_xen_provider_credentials_xen_provider_configurations_ProviderConfigurationId", x => x.ProviderConfigurationId, "xen_provider_configurations", "ProviderConfigurationId", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "xen_skill_versions",
            columns: table => new
            {
                SkillVersionId = table.Column<Guid>(type: "char(36)", nullable: false),
                SkillId = table.Column<Guid>(type: "char(36)", nullable: false),
                VersionNumber = table.Column<int>(type: "int", nullable: false),
                DefinitionJson = table.Column<string>(type: "longtext", nullable: false),
                ApprovalState = table.Column<string>(type: "longtext", nullable: false),
                IsCurrent = table.Column<bool>(type: "tinyint(1)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_xen_skill_versions", x => x.SkillVersionId);
                table.ForeignKey("FK_xen_skill_versions_xen_skills_SkillId", x => x.SkillId, "xen_skills", "SkillId", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "xen_tool_execution_logs",
            columns: table => new
            {
                ToolExecutionLogId = table.Column<Guid>(type: "char(36)", nullable: false),
                ToolDefinitionId = table.Column<Guid>(type: "char(36)", nullable: false),
                TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                ProductCode = table.Column<string>(type: "longtext", nullable: false),
                ActorUserId = table.Column<string>(type: "longtext", nullable: false),
                Status = table.Column<string>(type: "longtext", nullable: false),
                RequestJson = table.Column<string>(type: "longtext", nullable: false),
                ResponseJson = table.Column<string>(type: "longtext", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_xen_tool_execution_logs", x => x.ToolExecutionLogId);
                table.ForeignKey("FK_xen_tool_execution_logs_xen_tool_definitions_ToolDefinitionId", x => x.ToolDefinitionId, "xen_tool_definitions", "ToolDefinitionId", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "xen_cost_ledger",
            columns: table => new
            {
                CostLedgerEntryId = table.Column<Guid>(type: "char(36)", nullable: false),
                TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                UsageEventId = table.Column<Guid>(type: "char(36)", nullable: false),
                ProductCode = table.Column<string>(type: "longtext", nullable: false),
                Provider = table.Column<string>(type: "longtext", nullable: false),
                Model = table.Column<string>(type: "longtext", nullable: false),
                EstimatedCostUsd = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_xen_cost_ledger", x => x.CostLedgerEntryId);
                table.ForeignKey("FK_xen_cost_ledger_xen_usage_ledger_UsageEventId", x => x.UsageEventId, "xen_usage_ledger", "UsageEventId", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "xen_agent_knowledge_links",
            columns: table => new
            {
                AgentKnowledgeLinkId = table.Column<Guid>(type: "char(36)", nullable: false),
                AgentVersionId = table.Column<Guid>(type: "char(36)", nullable: false),
                KnowledgeSourceId = table.Column<Guid>(type: "char(36)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_xen_agent_knowledge_links", x => x.AgentKnowledgeLinkId);
                table.ForeignKey("FK_xen_agent_knowledge_links_xen_agent_versions_AgentVersionId", x => x.AgentVersionId, "xen_agent_versions", "AgentVersionId", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_xen_agent_knowledge_links_xen_knowledge_sources_KnowledgeSourceId", x => x.KnowledgeSourceId, "xen_knowledge_sources", "KnowledgeSourceId", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "xen_conversation_messages",
            columns: table => new
            {
                MessageId = table.Column<Guid>(type: "char(36)", nullable: false),
                ConversationId = table.Column<Guid>(type: "char(36)", nullable: false),
                Role = table.Column<int>(type: "int", nullable: false),
                Content = table.Column<string>(type: "longtext", nullable: false),
                ActionLabel = table.Column<string>(type: "longtext", nullable: true),
                ProductCode = table.Column<string>(type: "longtext", nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_xen_conversation_messages", x => x.MessageId);
                table.ForeignKey("FK_xen_conversation_messages_xen_conversations_ConversationId", x => x.ConversationId, "xen_conversations", "ConversationId", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "xen_ai_request_logs",
            columns: table => new
            {
                AiRequestLogId = table.Column<Guid>(type: "char(36)", nullable: false),
                TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                ProductCode = table.Column<string>(type: "longtext", nullable: false),
                ActorUserId = table.Column<string>(type: "longtext", nullable: false),
                ConversationId = table.Column<Guid>(type: "char(36)", nullable: true),
                ProviderConfigurationId = table.Column<Guid>(type: "char(36)", nullable: true),
                RequestKind = table.Column<string>(type: "longtext", nullable: false),
                ActivationSource = table.Column<string>(type: "longtext", nullable: false),
                AuditClassification = table.Column<string>(type: "longtext", nullable: false),
                SourceObjectReferencesJson = table.Column<string>(type: "longtext", nullable: true),
                ApplyOutcome = table.Column<string>(type: "longtext", nullable: true),
                RequestJson = table.Column<string>(type: "longtext", nullable: false),
                ResponseJson = table.Column<string>(type: "longtext", nullable: false),
                DurationMs = table.Column<long>(type: "bigint", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_xen_ai_request_logs", x => x.AiRequestLogId);
                table.ForeignKey("FK_xen_ai_request_logs_xen_conversations_ConversationId", x => x.ConversationId, "xen_conversations", "ConversationId", onDelete: ReferentialAction.SetNull);
                table.ForeignKey("FK_xen_ai_request_logs_xen_provider_configurations_ProviderConfigurationId", x => x.ProviderConfigurationId, "xen_provider_configurations", "ProviderConfigurationId", onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "xen_knowledge_chunks",
            columns: table => new
            {
                KnowledgeChunkId = table.Column<Guid>(type: "char(36)", nullable: false),
                KnowledgeDocumentId = table.Column<Guid>(type: "char(36)", nullable: false),
                ChunkIndex = table.Column<int>(type: "int", nullable: false),
                Content = table.Column<string>(type: "longtext", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_xen_knowledge_chunks", x => x.KnowledgeChunkId);
                table.ForeignKey("FK_xen_knowledge_chunks_xen_knowledge_documents_KnowledgeDocumentId", x => x.KnowledgeDocumentId, "xen_knowledge_documents", "KnowledgeDocumentId", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "xen_citations",
            columns: table => new
            {
                CitationId = table.Column<Guid>(type: "char(36)", nullable: false),
                KnowledgeDocumentId = table.Column<Guid>(type: "char(36)", nullable: false),
                ConversationId = table.Column<Guid>(type: "char(36)", nullable: true),
                MessageId = table.Column<Guid>(type: "char(36)", nullable: true),
                ReferenceText = table.Column<string>(type: "longtext", nullable: false),
                LocationHint = table.Column<string>(type: "longtext", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_xen_citations", x => x.CitationId);
                table.ForeignKey("FK_xen_citations_xen_conversation_messages_MessageId", x => x.MessageId, "xen_conversation_messages", "MessageId", onDelete: ReferentialAction.SetNull);
                table.ForeignKey("FK_xen_citations_xen_conversations_ConversationId", x => x.ConversationId, "xen_conversations", "ConversationId", onDelete: ReferentialAction.SetNull);
                table.ForeignKey("FK_xen_citations_xen_knowledge_documents_KnowledgeDocumentId", x => x.KnowledgeDocumentId, "xen_knowledge_documents", "KnowledgeDocumentId", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "xen_agent_skill_links",
            columns: table => new
            {
                AgentSkillLinkId = table.Column<Guid>(type: "char(36)", nullable: false),
                AgentVersionId = table.Column<Guid>(type: "char(36)", nullable: false),
                SkillVersionId = table.Column<Guid>(type: "char(36)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_xen_agent_skill_links", x => x.AgentSkillLinkId);
                table.ForeignKey("FK_xen_agent_skill_links_xen_agent_versions_AgentVersionId", x => x.AgentVersionId, "xen_agent_versions", "AgentVersionId", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_xen_agent_skill_links_xen_skill_versions_SkillVersionId", x => x.SkillVersionId, "xen_skill_versions", "SkillVersionId", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "xen_agent_tool_links",
            columns: table => new
            {
                AgentToolLinkId = table.Column<Guid>(type: "char(36)", nullable: false),
                AgentVersionId = table.Column<Guid>(type: "char(36)", nullable: false),
                ToolDefinitionId = table.Column<Guid>(type: "char(36)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_xen_agent_tool_links", x => x.AgentToolLinkId);
                table.ForeignKey("FK_xen_agent_tool_links_xen_agent_versions_AgentVersionId", x => x.AgentVersionId, "xen_agent_versions", "AgentVersionId", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_xen_agent_tool_links_xen_tool_definitions_ToolDefinitionId", x => x.ToolDefinitionId, "xen_tool_definitions", "ToolDefinitionId", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("IX_xen_agent_knowledge_links_AgentVersionId", "xen_agent_knowledge_links", "AgentVersionId");
        migrationBuilder.CreateIndex("IX_xen_agent_knowledge_links_KnowledgeSourceId", "xen_agent_knowledge_links", "KnowledgeSourceId");
        migrationBuilder.CreateIndex("IX_xen_agent_skill_links_AgentVersionId", "xen_agent_skill_links", "AgentVersionId");
        migrationBuilder.CreateIndex("IX_xen_agent_skill_links_SkillVersionId", "xen_agent_skill_links", "SkillVersionId");
        migrationBuilder.CreateIndex("IX_xen_agent_tool_links_AgentVersionId", "xen_agent_tool_links", "AgentVersionId");
        migrationBuilder.CreateIndex("IX_xen_agent_tool_links_ToolDefinitionId", "xen_agent_tool_links", "ToolDefinitionId");
        migrationBuilder.CreateIndex("IX_xen_agent_versions_AgentId", "xen_agent_versions", "AgentId");
        migrationBuilder.CreateIndex("IX_xen_ai_request_logs_ConversationId", "xen_ai_request_logs", "ConversationId");
        migrationBuilder.CreateIndex("IX_xen_ai_request_logs_ProviderConfigurationId", "xen_ai_request_logs", "ProviderConfigurationId");
        migrationBuilder.CreateIndex("IX_xen_citations_ConversationId", "xen_citations", "ConversationId");
        migrationBuilder.CreateIndex("IX_xen_citations_KnowledgeDocumentId", "xen_citations", "KnowledgeDocumentId");
        migrationBuilder.CreateIndex("IX_xen_citations_MessageId", "xen_citations", "MessageId");
        migrationBuilder.CreateIndex("IX_xen_conversation_messages_ConversationId_CreatedAtUtc", "xen_conversation_messages", new[] { "ConversationId", "CreatedAtUtc" });
        migrationBuilder.CreateIndex("IX_xen_conversations_TenantId_UpdatedAtUtc", "xen_conversations", new[] { "TenantId", "UpdatedAtUtc" });
        migrationBuilder.CreateIndex("IX_xen_cost_ledger_UsageEventId", "xen_cost_ledger", "UsageEventId");
        migrationBuilder.CreateIndex("IX_xen_embedding_records_EmbeddingIndexId", "xen_embedding_records", "EmbeddingIndexId");
        migrationBuilder.CreateIndex("IX_xen_knowledge_chunks_KnowledgeDocumentId", "xen_knowledge_chunks", "KnowledgeDocumentId");
        migrationBuilder.CreateIndex("IX_xen_knowledge_documents_KnowledgeSourceId", "xen_knowledge_documents", "KnowledgeSourceId");
        migrationBuilder.CreateIndex("IX_xen_marketplace_installations_MarketplaceAssetId", "xen_marketplace_installations", "MarketplaceAssetId");
        migrationBuilder.CreateIndex("IX_xen_model_catalog_Provider_ModelCode", "xen_model_catalog", new[] { "Provider", "ModelCode" }, unique: true);
        migrationBuilder.CreateIndex("IX_xen_prompt_versions_PromptTemplateId", "xen_prompt_versions", "PromptTemplateId");
        migrationBuilder.CreateIndex("IX_xen_provider_configurations_Scope_TenantId_DisplayName", "xen_provider_configurations", new[] { "Scope", "TenantId", "DisplayName" }, unique: true);
        migrationBuilder.CreateIndex("IX_xen_provider_credentials_ProviderConfigurationId_IsActive", "xen_provider_credentials", new[] { "ProviderConfigurationId", "IsActive" });
        migrationBuilder.CreateIndex("IX_xen_skill_versions_SkillId", "xen_skill_versions", "SkillId");
        migrationBuilder.CreateIndex("IX_xen_tool_execution_logs_ToolDefinitionId", "xen_tool_execution_logs", "ToolDefinitionId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "xen_agent_knowledge_links");
        migrationBuilder.DropTable(name: "xen_agent_skill_links");
        migrationBuilder.DropTable(name: "xen_agent_tool_links");
        migrationBuilder.DropTable(name: "xen_ai_request_logs");
        migrationBuilder.DropTable(name: "xen_audit_events");
        migrationBuilder.DropTable(name: "xen_budget_policies");
        migrationBuilder.DropTable(name: "xen_citations");
        migrationBuilder.DropTable(name: "xen_cost_ledger");
        migrationBuilder.DropTable(name: "xen_embedding_records");
        migrationBuilder.DropTable(name: "xen_governance_events");
        migrationBuilder.DropTable(name: "xen_marketplace_installations");
        migrationBuilder.DropTable(name: "xen_prompt_versions");
        migrationBuilder.DropTable(name: "xen_provider_credentials");
        migrationBuilder.DropTable(name: "xen_provider_health_events");
        migrationBuilder.DropTable(name: "xen_quota_policies");
        migrationBuilder.DropTable(name: "xen_tenant_ai_configurations");
        migrationBuilder.DropTable(name: "xen_tenant_model_policies");
        migrationBuilder.DropTable(name: "xen_tool_execution_logs");
        migrationBuilder.DropTable(name: "xen_conversation_messages");
        migrationBuilder.DropTable(name: "xen_skill_versions");
        migrationBuilder.DropTable(name: "xen_usage_ledger");
        migrationBuilder.DropTable(name: "xen_embedding_indexes");
        migrationBuilder.DropTable(name: "xen_marketplace_assets");
        migrationBuilder.DropTable(name: "xen_prompt_templates");
        migrationBuilder.DropTable(name: "xen_provider_configurations");
        migrationBuilder.DropTable(name: "xen_tool_definitions");
        migrationBuilder.DropTable(name: "xen_conversations");
        migrationBuilder.DropTable(name: "xen_knowledge_chunks");
        migrationBuilder.DropTable(name: "xen_agent_versions");
        migrationBuilder.DropTable(name: "xen_knowledge_documents");
        migrationBuilder.DropTable(name: "xen_agents");
        migrationBuilder.DropTable(name: "xen_skills");
        migrationBuilder.DropTable(name: "xen_knowledge_sources");
        migrationBuilder.DropTable(name: "xen_model_catalog");
    }
}
