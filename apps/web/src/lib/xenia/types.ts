export interface XeniaAgent {
  agentKey: string;
  name: string;
  description: string;
  version: string;
  enabled: boolean;
  allowedTools: string[];
  requiredProductCodes: string[];
}

export interface XeniaUsageSummary {
  requestsThisMonth: number;
  inputTokensThisMonth: number;
  outputTokensThisMonth: number;
  estimatedCostUsdThisMonth: number;
  monthlyRequestLimit: number | null;
  monthlyTokenLimit: number | null;
}

export interface XeniaPreferences {
  defaultAgentKey: string;
  contextHintsEnabled: boolean;
  preferencesJson: string;
}

export interface XeniaBootstrap {
  enabled: boolean;
  agents: XeniaAgent[];
  preferences: XeniaPreferences;
  usage: XeniaUsageSummary;
  featureFlags: Record<string, string>;
}

export interface XeniaConversationSummary {
  id: string;
  agentKey: string;
  agentVersion: string;
  title: string;
  source: string;
  status: string;
  lastMessageAtUtc: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface XeniaMessage {
  id: string;
  conversationId: string;
  role: 'user' | 'assistant' | 'tool' | 'system';
  content: string;
  provider: string;
  providerResponseId: string | null;
  inputTokens: number | null;
  outputTokens: number | null;
  finishReason: string | null;
  createdAtUtc: string;
  metadataJson: string;
  citations: XeniaCitation[];
}

export interface XeniaMessageMetadata {
  lookupResults: XeniaLookupResult[];
  followUpPrompts: string[];
}

export interface XeniaLookupResult {
  kind: string;
  id: string;
  title: string;
  subtitle: string | null;
  description: string | null;
  status: string | null;
  url: string | null;
  badges: string[];
}

export interface XeniaCitation {
  id: string;
  sourceType: string;
  sourceId: string;
  label: string;
  url: string | null;
}

export interface XeniaConversation {
  id: string;
  agentKey: string;
  agentVersion: string;
  title: string;
  source: string;
  status: string;
  contextJson: string;
  lastMessageAtUtc: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  messages: XeniaMessage[];
}
