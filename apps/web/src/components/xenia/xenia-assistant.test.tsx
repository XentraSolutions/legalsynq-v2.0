'use client';

import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest';
import { XeniaAssistant } from './xenia-assistant';
import { xeniaClient } from '@/lib/xenia/client';
import type { XeniaBootstrap, XeniaConversation, XeniaMessage } from '@/lib/xenia/types';

vi.mock('next/navigation', () => ({
  usePathname: () => '/xenia',
  useSearchParams: () => ({
    entries: function* entries(): IterableIterator<[string, string]> {
      yield* [];
    },
  }),
}));

vi.mock('@/components/ui/select', () => ({
  Select: ({ value, onValueChange, children }: {
    value?: string;
    onValueChange?: (value: string) => void;
    children?: React.ReactNode;
  }) => (
    <select
      aria-label="Agent"
      value={value}
      onChange={(event) => onValueChange?.(event.target.value)}
    >
      {children}
    </select>
  ),
  SelectTrigger: ({ children }: { children?: React.ReactNode }) => <>{children}</>,
  SelectValue: () => null,
  SelectContent: ({ children }: { children?: React.ReactNode }) => <>{children}</>,
  SelectItem: ({ value, children }: { value: string; children?: React.ReactNode }) => (
    <option value={value}>{children}</option>
  ),
}));

vi.mock('@/lib/xenia/client', () => ({
  xeniaClient: {
    bootstrap: vi.fn(),
    agents: vi.fn(),
    conversations: vi.fn(),
    createConversation: vi.fn(),
    getConversation: vi.fn(),
    archiveConversation: vi.fn(),
    preferences: vi.fn(),
    updatePreferences: vi.fn(),
    createMessage: vi.fn(),
  },
}));

const mockedXeniaClient = vi.mocked(xeniaClient);

const bootstrap: XeniaBootstrap = {
  enabled: true,
  agents: [
    {
      agentKey: 'careconnect',
      name: 'CareConnect Agent',
      description: 'Read-only CareConnect assistant for referral and provider workflow context.',
      version: '1.0.0',
      enabled: true,
      allowedTools: [],
      requiredProductCodes: ['SYNQ_CARECONNECT'],
    },
  ],
  preferences: {
    defaultAgentKey: 'careconnect',
    contextHintsEnabled: true,
    preferencesJson: '{}',
  },
  usage: {
    requestsThisMonth: 0,
    inputTokensThisMonth: 0,
    outputTokensThisMonth: 0,
    estimatedCostUsdThisMonth: 0,
    monthlyRequestLimit: null,
    monthlyTokenLimit: null,
  },
  featureFlags: {
    streaming: 'enabled',
  },
};

const baseConversation: XeniaConversation = {
  id: 'conv-1',
  agentKey: 'careconnect',
  agentVersion: '1.0.0',
  title: 'New CareConnect Agent conversation',
  source: 'page',
  status: 'active',
  contextJson: '{}',
  lastMessageAtUtc: null,
  createdAtUtc: '2026-07-14T19:00:00.000Z',
  updatedAtUtc: '2026-07-14T19:00:00.000Z',
  messages: [],
};

describe('XeniaAssistant streaming', () => {
  let streamController: ReadableStreamDefaultController<Uint8Array> | null;
  let fetchMock: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    vi.clearAllMocks();
    streamController = null;

    HTMLElement.prototype.scrollIntoView = vi.fn();

    mockedXeniaClient.bootstrap.mockResolvedValue(bootstrap);
    mockedXeniaClient.conversations.mockResolvedValue([]);
    mockedXeniaClient.createConversation.mockResolvedValue(baseConversation);
    mockedXeniaClient.getConversation.mockResolvedValue({
      ...baseConversation,
      messages: [
        buildUserMessage('Look up RL Medical Group'),
        buildAssistantMessage('I found 88 referrals.'),
      ],
    });
    mockedXeniaClient.archiveConversation.mockResolvedValue(undefined);

    fetchMock = vi.fn().mockResolvedValue(
      new Response(new ReadableStream<Uint8Array>({
        start(controller) {
          streamController = controller;
        },
      }), {
        status: 200,
        headers: {
          'Content-Type': 'text/event-stream',
        },
      }),
    );

    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  test('renders assistant deltas before the stream completes', async () => {
    render(<XeniaAssistant />);

    const textarea = await screen.findByPlaceholderText('Message Xenia');
    fireEvent.change(textarea, { target: { value: 'Look up RL Medical Group' } });
    fireEvent.click(screen.getByTitle('Send'));

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledTimes(1);
    });

    act(() => {
      emitSseFrame({
        type: 'user_message',
        message: buildUserMessage('Look up RL Medical Group'),
      });
      emitSseFrame({
        type: 'delta',
        delta: 'I found ',
      });
    });

    await waitFor(() => {
      expect(screen.getByText(/I found/i)).toBeInTheDocument();
    });
    expect(screen.queryByText('Thinking...')).not.toBeInTheDocument();

    act(() => {
      emitSseFrame({
        type: 'delta',
        delta: '88 referrals.',
      });
    });

    await waitFor(() => {
      expect(screen.getByText(/I found 88 referrals\./i)).toBeInTheDocument();
    });

    act(() => {
      emitSseFrame({
        type: 'message',
        message: buildAssistantMessage('I found 88 referrals.'),
      });
      streamController?.close();
    });

    await waitFor(() => {
      expect(mockedXeniaClient.getConversation).toHaveBeenCalledWith('conv-1');
    });
  });

  function emitSseFrame(payload: Record<string, unknown>) {
    if (!streamController) {
      throw new Error('Stream controller is not ready.');
    }

    const frame = `event: ${payload.type ?? 'message'}\ndata: ${JSON.stringify(payload)}\n\n`;
    streamController.enqueue(new TextEncoder().encode(frame));
  }
});

function buildUserMessage(content: string): XeniaMessage {
  return {
    id: 'msg-user-1',
    conversationId: 'conv-1',
    role: 'user',
    content,
    provider: 'user',
    providerResponseId: null,
    inputTokens: null,
    outputTokens: null,
    finishReason: null,
    createdAtUtc: '2026-07-14T19:00:01.000Z',
    metadataJson: '{}',
    citations: [],
  };
}

function buildAssistantMessage(content: string): XeniaMessage {
  return {
    id: 'msg-assistant-1',
    conversationId: 'conv-1',
    role: 'assistant',
    content,
    provider: 'openai',
    providerResponseId: 'resp-1',
    inputTokens: 10,
    outputTokens: 5,
    finishReason: 'stop',
    createdAtUtc: '2026-07-14T19:00:02.000Z',
    metadataJson: '{"lookupResults":[],"followUpPrompts":[]}',
    citations: [],
  };
}
