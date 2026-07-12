import { requirePlatformAdmin } from '@/lib/auth-guards';
import { apiFetch, ApiError } from '@/lib/api-client';
import { CCShell } from '@/components/shell/cc-shell';
import { XeniaAdminClient } from './xenia-admin-client';

export const dynamic = 'force-dynamic';

interface OverviewResponse {
  enabledTenantCount: number;
  deploymentModelDistribution: Record<string, number>;
  providerCount: number;
  conversationCount: number;
  usage: {
    requestCount: number;
    promptTokens: number;
    completionTokens: number;
    estimatedCostUsd: number;
  };
  providerHealth: Array<{
    providerConfigurationId: string;
    providerName: string;
    status: string;
    message: string;
    checkedAtUtc: string;
  }>;
}

interface PromptTemplateResponse {
  promptTemplateId: string;
  templateCode: string;
  displayName: string;
  description: string;
  enabled: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

interface SkillResponse {
  skillId: string;
  skillCode: string;
  displayName: string;
  description: string;
  enabled: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

interface AgentResponse {
  agentId: string;
  agentCode: string;
  displayName: string;
  description: string;
  enabled: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

interface KnowledgeSourceResponse {
  knowledgeSourceId: string;
  tenantId: string | null;
  sourceCode: string;
  displayName: string;
  sourceType: string;
  status: string;
  createdAtUtc: string;
  updatedAtUtc: string;
}

interface MarketplaceAssetResponse {
  marketplaceAssetId: string;
  assetCode: string;
  assetType: string;
  displayName: string;
  description: string;
  enabled: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export default async function XeniaOverviewPage() {
  const session = await requirePlatformAdmin();

  let overview: OverviewResponse | null = null;
  let providers: unknown[] = [];
  let models: unknown[] = [];
  let usage: unknown = null;
  let audit: unknown[] = [];
  let prompts: PromptTemplateResponse[] = [];
  let skills: SkillResponse[] = [];
  let agents: AgentResponse[] = [];
  let knowledgeSources: KnowledgeSourceResponse[] = [];
  let marketplaceAssets: MarketplaceAssetResponse[] = [];
  let error: string | null = null;

  try {
    [overview, providers, models, usage, audit, prompts, skills, agents, knowledgeSources, marketplaceAssets] = await Promise.all([
      apiFetch<OverviewResponse>('/api/xenia/admin/overview'),
      apiFetch<unknown[]>('/api/xenia/admin/providers'),
      apiFetch<unknown[]>('/api/xenia/admin/models'),
      apiFetch<unknown>('/api/xenia/admin/usage'),
      apiFetch<unknown[]>('/api/xenia/admin/audit'),
      apiFetch<PromptTemplateResponse[]>('/api/xenia/admin/prompts'),
      apiFetch<SkillResponse[]>('/api/xenia/admin/skills'),
      apiFetch<AgentResponse[]>('/api/xenia/admin/agents'),
      apiFetch<KnowledgeSourceResponse[]>('/api/xenia/admin/knowledge-sources'),
      apiFetch<MarketplaceAssetResponse[]>('/api/xenia/admin/marketplace/assets'),
    ]);
  } catch (err) {
    error = err instanceof ApiError
      ? err.message
      : 'Unable to load the Xenia overview right now.';
  }

  return (
    <CCShell userEmail={session.email}>
      <div className="min-h-full bg-gray-50">
        <div className="mx-auto max-w-6xl px-6 py-8">
          {error ? (
            <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
              {error}
            </div>
          ) : null}

          {overview ? (
            <XeniaAdminClient
              initialOverview={overview}
              initialProviders={providers as never[]}
              initialModels={models as never[]}
              initialUsage={usage as never}
              initialAudit={audit as never[]}
              initialPrompts={prompts}
              initialSkills={skills}
              initialAgents={agents}
              initialKnowledgeSources={knowledgeSources}
              initialMarketplaceAssets={marketplaceAssets}
            />
          ) : null}
        </div>
      </div>
    </CCShell>
  );
}
