import type { XeniaCitation, XeniaMessage } from './types';

export interface XeniaStreamEvent {
  type: string;
  delta?: string | null;
  message?: XeniaMessage | null;
  error?: string | null;
}

type UnknownRecord = Record<string, unknown>;

function asRecord(value: unknown): UnknownRecord | null {
  return value !== null && typeof value === 'object'
    ? value as UnknownRecord
    : null;
}

function pick<T>(record: UnknownRecord, camelKey: string, pascalKey: string): T | undefined {
  return (record[camelKey] ?? record[pascalKey]) as T | undefined;
}

function normalizeCitation(raw: unknown): XeniaCitation | null {
  const record = asRecord(raw);
  if (!record) return null;

  const id = pick<string>(record, 'id', 'Id');
  const sourceType = pick<string>(record, 'sourceType', 'SourceType');
  const sourceId = pick<string>(record, 'sourceId', 'SourceId');
  const label = pick<string>(record, 'label', 'Label');

  if (!id || !sourceType || !sourceId || !label) return null;

  return {
    id,
    sourceType,
    sourceId,
    label,
    url: pick<string | null>(record, 'url', 'Url') ?? null,
  };
}

function normalizeMessage(raw: unknown): XeniaMessage | null {
  const record = asRecord(raw);
  if (!record) return null;

  const id = pick<string>(record, 'id', 'Id');
  const conversationId = pick<string>(record, 'conversationId', 'ConversationId');
  const role = pick<string>(record, 'role', 'Role');
  const content = pick<string>(record, 'content', 'Content');
  const provider = pick<string>(record, 'provider', 'Provider');
  const createdAtUtc = pick<string>(record, 'createdAtUtc', 'CreatedAtUtc');

  if (!id || !conversationId || !role || !content || !provider || !createdAtUtc) {
    return null;
  }

  const citationsRaw = pick<unknown[]>(record, 'citations', 'Citations') ?? [];

  return {
    id,
    conversationId,
    role: role.toLowerCase() as XeniaMessage['role'],
    content,
    provider,
    providerResponseId: pick<string | null>(record, 'providerResponseId', 'ProviderResponseId') ?? null,
    inputTokens: pick<number | null>(record, 'inputTokens', 'InputTokens') ?? null,
    outputTokens: pick<number | null>(record, 'outputTokens', 'OutputTokens') ?? null,
    finishReason: pick<string | null>(record, 'finishReason', 'FinishReason') ?? null,
    createdAtUtc,
    citations: citationsRaw
      .map(normalizeCitation)
      .filter((citation): citation is XeniaCitation => citation !== null),
  };
}

export function normalizeStreamEvent(raw: unknown): XeniaStreamEvent | null {
  const record = asRecord(raw);
  if (!record) return null;

  const type = pick<string>(record, 'type', 'Type');
  if (!type) return null;

  return {
    type,
    delta: pick<string | null>(record, 'delta', 'Delta') ?? null,
    message: normalizeMessage(pick(record, 'message', 'Message')) ?? null,
    error: pick<string | null>(record, 'error', 'Error') ?? null,
  };
}
