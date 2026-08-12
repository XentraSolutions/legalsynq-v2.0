import type { XeniaCitation, XeniaMessage } from './types';

export interface XeniaStreamEvent {
  type: string;
  delta?: string | null;
  message?: XeniaMessage | null;
  error?: string | null;
}

export interface XeniaSseDrainResult {
  events: XeniaStreamEvent[];
  rest: string;
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
  const metadataJson = pick<string>(record, 'metadataJson', 'MetadataJson') ?? '{}';

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
    metadataJson,
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

function parseSseFrame(frame: string): XeniaStreamEvent | null {
  const data = frame
    .split(/\r?\n/)
    .filter(line => line.startsWith('data:'))
    .map(line => line.slice('data:'.length).trimStart())
    .join('\n')
    .trim();

  if (!data) return null;

  try {
    return normalizeStreamEvent(JSON.parse(data));
  } catch {
    return null;
  }
}

export function drainSseBuffer(buffer: string): XeniaSseDrainResult {
  const frames = buffer.split(/\r?\n\r?\n/);
  if (frames.length === 1) {
    return { events: [], rest: buffer };
  }

  const rest = frames.pop() ?? '';
  return {
    events: frames
      .map(parseSseFrame)
      .filter((event): event is XeniaStreamEvent => event !== null),
    rest,
  };
}

export function flushSseBuffer(buffer: string): XeniaStreamEvent[] {
  const trimmed = buffer.trim();
  if (!trimmed) return [];

  const event = parseSseFrame(trimmed);
  return event ? [event] : [];
}
