import { z } from 'zod';

export const xeniaCitationSchema = z.object({
  id: z.string(),
  sourceType: z.string(),
  sourceId: z.string(),
  label: z.string(),
  url: z.string().nullable().optional(),
});

export const xeniaMessageSchema = z.object({
  id: z.string(),
  conversationId: z.string(),
  role: z.string(),
  content: z.string(),
  provider: z.string().nullable().optional(),
  providerResponseId: z.string().nullable().optional(),
  inputTokens: z.number().nullable().optional(),
  outputTokens: z.number().nullable().optional(),
  finishReason: z.string().nullable().optional(),
  createdAtUtc: z.string(),
  metadataJson: z.string().nullable().optional(),
  citations: z.array(xeniaCitationSchema).default([]),
});

export const xeniaConversationSummarySchema = z.object({
  id: z.string(),
  agentKey: z.string(),
  agentVersion: z.string().nullable().optional(),
  title: z.string().nullable().optional(),
  source: z.string().nullable().optional(),
  status: z.string(),
  lastMessageAtUtc: z.string().nullable().optional(),
  createdAtUtc: z.string(),
  updatedAtUtc: z.string(),
});

export const xeniaConversationListSchema = z.object({
  conversations: z.array(xeniaConversationSummarySchema),
});

export const xeniaConversationSchema = xeniaConversationSummarySchema.extend({
  contextJson: z.string().nullable().optional(),
  messages: z.array(xeniaMessageSchema).default([]),
});

export const xeniaAgentSchema = z.object({
  agentKey: z.string(),
  name: z.string(),
  description: z.string(),
  version: z.string(),
  enabled: z.boolean(),
  allowedTools: z.array(z.string()).default([]),
  requiredProductCodes: z.array(z.string()).default([]),
});

export const xeniaPreferencesSchema = z.object({
  defaultAgentKey: z.string().nullable().optional(),
  contextHintsEnabled: z.boolean(),
  preferencesJson: z.string(),
});

export const xeniaUsageSchema = z.object({
  requestsThisMonth: z.number(),
  inputTokensThisMonth: z.number(),
  outputTokensThisMonth: z.number(),
  estimatedCostUsdThisMonth: z.number(),
  monthlyRequestLimit: z.number().nullable(),
  monthlyTokenLimit: z.number().nullable(),
});

export const xeniaBootstrapSchema = z.object({
  enabled: z.boolean(),
  agents: z.array(xeniaAgentSchema),
  preferences: xeniaPreferencesSchema,
  usage: xeniaUsageSchema,
  featureFlags: z.record(z.string()),
});

export const xeniaStreamEventSchema = z.object({
  type: z.string(),
  delta: z.string().nullable().optional(),
  message: xeniaMessageSchema.nullable().optional(),
  error: z.string().nullable().optional(),
});
