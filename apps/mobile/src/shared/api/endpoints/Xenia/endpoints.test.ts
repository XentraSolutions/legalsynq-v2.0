import { apiClient } from '@/shared/api/client';

import { XeniaApi, XENIA_MESSAGE_TIMEOUT_MS } from './endpoints';

describe('XeniaApi.createMessage', () => {
  it('allows long-running assistant requests without changing the global API timeout', async () => {
    const response = {
      data: {
        id: 'message-1',
        conversationId: 'conversation-1',
        role: 'Assistant',
        content: 'There are 42 active cases.',
        createdAtUtc: '2026-07-28T00:00:00Z',
        citations: [],
      },
    };
    const post = jest.fn(() => Promise.resolve(response));
    apiClient.post = post;

    await expect(
      XeniaApi.createMessage('conversation-1', {
        clientMessageId: 'mobile-request-1',
        content: 'How many active cases are there?',
      })
    ).resolves.toEqual(response.data);

    expect(post).toHaveBeenCalledWith(
      '/xenia/assistant/conversations/conversation-1/messages',
      {
        clientMessageId: 'mobile-request-1',
        content: 'How many active cases are there?',
      },
      { timeout: 120000 }
    );
    expect(XENIA_MESSAGE_TIMEOUT_MS).toBe(120000);
  });

  it('normalizes the compact message response documented by Xenia', async () => {
    const response = {
      data: {
        role: 'assistant',
        content: 'iteration one acknowledged.',
        provider: 'openai',
        finishReason: 'stop',
        createdAtUtc: '2026-08-17T12:55:58.9873222Z',
      },
    };
    apiClient.post = jest.fn(() => Promise.resolve(response));

    await expect(
      XeniaApi.createMessage('conversation-1', {
        clientMessageId: 'lsv3-960-three-iteration-1',
        content: 'Iteration 1',
      })
    ).resolves.toEqual({
      ...response.data,
      id: 'lsv3-960-three-iteration-1',
      conversationId: 'conversation-1',
      citations: [],
    });
  });
});
