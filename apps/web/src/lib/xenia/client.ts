import type {
  XeniaAgent,
  XeniaBootstrap,
  XeniaConversation,
  XeniaConversationSummary,
  XeniaMessage,
  XeniaPreferences,
} from './types';

async function jsonRequest<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`/api/xenia${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...(init?.headers ?? {}),
    },
    cache: 'no-store',
  });

  if (!res.ok) {
    let message = `Xenia request failed (${res.status})`;
    try {
      const body = await res.json();
      message = body.detail ?? body.message ?? body.title ?? message;
    } catch {
      // keep default
    }
    throw new Error(message);
  }

  if (res.status === 204) return undefined as T;
  return res.json() as Promise<T>;
}

export const xeniaClient = {
  bootstrap: () => jsonRequest<XeniaBootstrap>('/assistant/bootstrap'),
  agents: async () => {
    const res = await jsonRequest<{ agents: XeniaAgent[] }>('/assistant/agents');
    return res.agents;
  },
  conversations: async () => {
    const res = await jsonRequest<{ conversations: XeniaConversationSummary[] }>('/assistant/conversations');
    return res.conversations;
  },
  createConversation: (agentKey: string, source: string, contextJson: string) =>
    jsonRequest<XeniaConversation>('/assistant/conversations', {
      method: 'POST',
      body: JSON.stringify({ agentKey, source, contextJson }),
    }),
  getConversation: (id: string) => jsonRequest<XeniaConversation>(`/assistant/conversations/${id}`),
  archiveConversation: (id: string) =>
    jsonRequest<void>(`/assistant/conversations/${id}`, { method: 'DELETE' }),
  preferences: () => jsonRequest<XeniaPreferences>('/assistant/preferences'),
  updatePreferences: (body: Partial<XeniaPreferences>) =>
    jsonRequest<XeniaPreferences>('/assistant/preferences', {
      method: 'PATCH',
      body: JSON.stringify(body),
    }),
  createMessage: (conversationId: string, content: string, contextJson: string) =>
    jsonRequest<XeniaMessage>(`/assistant/conversations/${conversationId}/messages`, {
      method: 'POST',
      body: JSON.stringify({ content, contextJson }),
    }),
};
